using MagicMouse.Core.Gestures;
using MagicMouse.Core.Settings;
using MagicMouse.Core.Touch;
using MagicMouse.Windows.App.Devices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private readonly WindowsActionDispatcher _actionDispatcher = new();
    private GestureSequenceRecognizer _inputRecognizer = new();
    private ScrollEngine? _inputScroll;
    private TouchFrame? _previousTouch;
    private DateTimeOffset _inputStartedAt, _lastInputTick, _lastTouchAt;
    private bool _pointerMovedSinceTouch;
    private bool _inputConnected;
    private long _decodedReports;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _inputTimer;
    private UserSettings InputPreferences => _activePreferences ?? _settings;

    private UIElement InputConnectionCard() => SectionCard("Gestos del Magic Mouse en Windows", "Lee la superficie táctil y ejecuta las acciones elegidas. La conexión física está pendiente de comprobar en este equipo.",
        ToggleSetting("input.enabled", "Activar acciones del dispositivo", true, enabled => _ = ChangeInputConnectionAsync(enabled)),
        ActionButton("Conectar superficie táctil", ConnectInputAsync),
        ActionButton("Usar gestos de macOS", async () => { MacGestureDefaults.Apply(Preferences); await SaveNowAsync(); ShowPage("Gestos"); await ShowSheetAsync("Gestos configurados", "Un dedo: atrás/adelante. Dos dedos: escritorios virtuales. Doble tap de un dedo: zoom del navegador/PDF. Doble tap de dos dedos: Vista de tareas. Scroll natural. El zoom depende de los atajos de la aplicación activa."); }),
        Notice(_inputConnected ? $"Motor conectado · {_decodedReports} reportes interpretados" : "Sin superficie táctil activa. Puedes guardar los ajustes y probar los gestos en el simulador."));

    private async Task ChangeInputConnectionAsync(bool enabled)
    {
        if(!enabled) { _inputConnected=false; _inputTimer?.Stop(); _reconnectTimer?.Stop();_capture.Stop();ResetInputEngine(); return; }
        if(_currentMouseDevice is null) return;
        try { await ConnectInputCoreAsync(false); } catch(Exception exception) { StopInputOnFailure(exception); }
    }
    private Task ConnectInputAsync() => ConnectInputCoreAsync(true);
    private async Task ConnectInputCoreAsync(bool showDialog)
    {
        if(_closed || _connectingInput || (_inputConnected && _capture.IsCapturing)) return;
        _connectingInput=true;
        try {await ConnectInputAttemptAsync(showDialog);}
        finally {_connectingInput=false;}
    }
    private async Task ConnectInputAttemptAsync(bool showDialog)
    {
        _inputConnected = false;
        if (_currentMouseDevice is null) { if(showDialog) await ShowSheetAsync("Magic Mouse no conectado", "Puedes preparar los ajustes ahora. Empareja tu Magic Mouse por Bluetooth y vuelve a conectar la superficie cuando lo tengas."); return; }
        if (!_capture.IsCapturing) await StartHidCaptureCoreAsync(true);
        else _capture.RequestTouchReports();
        if (!_capture.IsCapturing) throw new InvalidOperationException("Windows no permitió abrir la colección HID. Consulta el diagnóstico en Avanzado.");
        if(_closed || _currentMouseDevice is null || !_settings.Toggles.GetValueOrDefault("input.enabled")) return;
        ResetInputEngine(); _inputStartedAt = _lastInputTick = DateTimeOffset.UtcNow; _inputConnected = true;
        _connectionRetry.Reset();_reconnectTimer?.Stop();
        _settings.Toggles["input.enabled"] = true; await SaveNowAsync();
        _inputTimer ??= DispatcherQueue.CreateTimer(); _inputTimer.Interval = TimeSpan.FromMilliseconds(16);
        _inputTimer.Tick -= OnInputTick; _inputTimer.Tick += OnInputTick; _inputTimer.Start();
        if (showDialog) await ShowSheetAsync("Lectura táctil iniciada", "Se solicitó el modo táctil. Solo se ejecutarán acciones para reportes reconocidos. Si Windows reserva esta colección para su controlador, el diagnóstico mostrará el impedimento.");
    }
    private void ResetInputEngine()
    {
        var settings = InputPreferences;
        _inputRecognizer = new(new(MinimumSwipeDistance: settings.Numbers.GetValueOrDefault("gesture.distance",12) / 100,
            MaximumTapMovement: settings.Numbers.GetValueOrDefault("tap.movement",3.5) / 100,
            MaximumSwipeDuration: TimeSpan.FromMilliseconds(settings.Numbers.GetValueOrDefault("gesture.timeout",900)),
            MaximumTapDuration: TimeSpan.FromMilliseconds(settings.Numbers.GetValueOrDefault("tap.timeout",320))));
        _inputScroll = new(new(Enabled: settings.Toggles.GetValueOrDefault("scroll.enabled",true),
            Vertical: settings.Toggles.GetValueOrDefault("scroll.vertical",true), Horizontal: settings.Toggles.GetValueOrDefault("scroll.horizontal",true),
            Natural: settings.Toggles.GetValueOrDefault("scroll.natural"), InvertVertical: settings.Toggles.GetValueOrDefault("scroll.invertVertical"), InvertHorizontal: settings.Toggles.GetValueOrDefault("scroll.invertHorizontal"),
            AxisLock: settings.Toggles.GetValueOrDefault("scroll.axisLock",true), Smooth: settings.Toggles.GetValueOrDefault("scroll.smooth",true),
            Speed: settings.Numbers.GetValueOrDefault("scroll.speed",8)/8, Sensitivity: settings.Numbers.GetValueOrDefault("scroll.sensitivity",10)/10,
            Friction: 24 - Math.Clamp(settings.Numbers.GetValueOrDefault("scroll.inertia",35),0,100) * .2,
            IgnoreMouseMovement: settings.Toggles.GetValueOrDefault("scroll.ignoreMouseMovement"), IgnoreLifted: settings.Toggles.GetValueOrDefault("buttons.ignoreLifted",true)));
        _previousTouch = null; _pointerMovedSinceTouch = false; _lastInputTick = DateTimeOffset.UtcNow;
        _actionDispatcher.ResetScroll();
    }
    private void ProcessInputReport(HidReportSnapshot report)
    {
        if (_closed || !_inputConnected || !_settings.Toggles.GetValueOrDefault("input.enabled") || report.Timestamp < _inputStartedAt) return;
        try
        {
            if (!AppleMouseReportParser.TryParse(Convert.FromHexString(report.BytesHex), report.Timestamp, out var decoded)) return;
            if (report.Discontinuity) ResetInputEngine();
            _decodedReports++; var frame = decoded!.Frame;
            if (decoded.Buttons != 0) { ResetInputEngine(); return; } // physical clicks remain owned by the Windows driver
            _pointerMovedSinceTouch |= decoded.DeltaX != 0 || decoded.DeltaY != 0;
            if (!decoded.HasTouchData)
            {
                if (_pointerMovedSinceTouch && InputPreferences.Toggles.GetValueOrDefault("scroll.ignoreMouseMovement"))
                {
                    _inputScroll?.Reset(); _actionDispatcher.ResetScroll();
                }
                return; // A short mouse-motion packet carries no touch coordinates.
            }
            _lastTouchAt = frame.Timestamp;
            if (_previousTouch is null || !frame.Contacts.Select(c => c.Id).Order().SequenceEqual(_previousTouch.Contacts.Select(c => c.Id).Order()))
            {
                if (frame.Contacts.Count > 0) { _inputScroll?.Reset(); _actionDispatcher.ResetScroll(); }
            }
            foreach (var gesture in _inputRecognizer.Process(frame)) DispatchGesture(gesture);
            if (_inputScroll is not null && _previousTouch is { Contacts.Count: > 0 } previous && frame.Contacts.Count > 0 && frame.Contacts.Select(c => c.Id).Order().SequenceEqual(previous.Contacts.Select(c => c.Id).Order()))
            {
                var x = frame.Contacts.Average(c=>c.X)-previous.Contacts.Average(c=>c.X); var y = frame.Contacts.Average(c=>c.Y)-previous.Contacts.Average(c=>c.Y);
                var reserved = InputRouting.ReservedAxes(frame.Contacts.Count, InputPreferences);
                var allowed = InputRouting.AllowsScroll(frame.Contacts.Count, InputPreferences.Choices.GetValueOrDefault("scroll.fingers", "1 o 2 dedos"));
                var elapsed = frame.Timestamp - previous.Timestamp;
                if (elapsed <= TimeSpan.Zero) return;
                var output = _inputScroll.Process(reserved.Horizontal || !allowed ? 0 : x, reserved.Vertical || !allowed ? 0 : y, elapsed, true, mouseMoving: _pointerMovedSinceTouch);
                if (WindowsMouseSettings.ControlPressed)
                {
                    if (InputPreferences.Toggles.GetValueOrDefault("zoom.modifier")) _actionDispatcher.Scroll(0,output.Vertical);
                    _inputScroll.Reset(); // do not keep zoom inertia after releasing Control
                }
                else _actionDispatcher.Scroll(output.Horizontal,output.Vertical);
            }
            _previousTouch = frame; _pointerMovedSinceTouch = false;
        }
        catch (Exception exception) { StopInputOnFailure(exception); }
    }
    private void OnInputTick(Microsoft.UI.Dispatching.DispatcherQueueTimer timer, object args)
    {
        if (_closed || !_inputConnected || !_settings.Toggles.GetValueOrDefault("input.enabled")) return;
        try
        {
            var now = DateTimeOffset.UtcNow; var elapsed = now - _lastInputTick; _lastInputTick = now;
            // Silence or motion-only packets must not leave a stale touch active indefinitely.
            if (_previousTouch is { Contacts.Count: > 0 } && now - _lastTouchAt > TimeSpan.FromSeconds(2))
            {
                ResetInputEngine(); return;
            }
            foreach(var gesture in _inputRecognizer.Flush(now)) DispatchGesture(gesture);
            if (_inputScroll is not null && _previousTouch is { Contacts.Count: 0 } && elapsed > TimeSpan.Zero) { var delta = _inputScroll.Process(0,0,elapsed,false); _actionDispatcher.Scroll(delta.Horizontal,delta.Vertical); }
        }
        catch(Exception exception) { StopInputOnFailure(exception); }
    }
    private void DispatchGesture(GestureEvent gesture)
    {
        var action = GestureActionMapper.Map(gesture, InputPreferences);
        _actionDispatcher.Dispatch(action);
    }
    private void StopInputOnFailure(Exception exception)
    {
        if(_closed) return;
        _inputConnected = false; _inputTimer?.Stop(); _capture.Stop();ResetInputEngine();ScheduleInputReconnect();
        _ = LogAsync($"Input paused: {exception}");
        DeviceDetailText.Text = "Motor pausado: " + exception.Message;
    }
}
