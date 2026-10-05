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
        if (!_capture.IsCapturing) await StartHidCaptureAsync();
        if (!_capture.IsCapturing) throw new InvalidOperationException("Windows no permitió abrir la colección HID. Consulta el diagnóstico en Avanzado.");
        if(_closed || _currentMouseDevice is null || !_settings.Toggles.GetValueOrDefault("input.enabled")) return;
        _capture.RequestTouchReports(); ResetInputEngine(); _inputConnected = true;
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
        _previousTouch = null;
    }
    private void ProcessInputReport(HidReportSnapshot report)
    {
        if (_closed || !_inputConnected || !_settings.Toggles.GetValueOrDefault("input.enabled")) return;
        try
        {
            if (!AppleMouseReportParser.TryParse(Convert.FromHexString(report.BytesHex), report.Timestamp, out var decoded)) return;
            _decodedReports++; var frame = decoded!.Frame;
            if (decoded.Buttons != 0) { ResetInputEngine(); return; } // physical clicks remain owned by the Windows driver
            foreach (var gesture in _inputRecognizer.Process(frame)) DispatchGesture(gesture);
            if (_inputScroll is not null && _previousTouch is { Contacts.Count: > 0 } previous && frame.Contacts.Count > 0 && frame.Contacts.Select(c => c.Id).SequenceEqual(previous.Contacts.Select(c => c.Id)))
            {
                var x = frame.Contacts.Average(c=>c.X)-previous.Contacts.Average(c=>c.X); var y = frame.Contacts.Average(c=>c.Y)-previous.Contacts.Average(c=>c.Y);
                // Horizontal gestures with an assigned action own that axis; avoid both a swipe and wheel action.
                var ownsHorizontal = frame.Contacts.Count == 1 ? GestureActionMapper.Map(new(GestureKind.OneFingerSwipeLeft,frame.Timestamp,TimeSpan.Zero,0),InputPreferences) != "Sin acción" : frame.Contacts.Count == 2;
                var fingers = InputPreferences.Choices.GetValueOrDefault("scroll.fingers", "1 o 2 dedos");
                var allowed = fingers == "1 o 2 dedos" ? frame.Contacts.Count <= 2 : fingers == "2 dedos" ? frame.Contacts.Count == 2 : frame.Contacts.Count == 1;
                var elapsed = frame.Timestamp - previous.Timestamp;
                if (elapsed <= TimeSpan.Zero) return;
                var output = _inputScroll.Process(ownsHorizontal || !allowed ? 0 : x, !allowed ? 0 : y, elapsed, true, mouseMoving: decoded.DeltaX != 0 || decoded.DeltaY != 0);
                if (WindowsMouseSettings.ControlPressed)
                {
                    if (InputPreferences.Toggles.GetValueOrDefault("zoom.modifier")) _actionDispatcher.Scroll(0,output.Vertical);
                    _inputScroll.Reset(); // do not keep zoom inertia after releasing Control
                }
                else _actionDispatcher.Scroll(output.Horizontal,output.Vertical);
            }
            _previousTouch = frame;
        }
        catch (Exception exception) { StopInputOnFailure(exception); }
    }
    private void OnInputTick(Microsoft.UI.Dispatching.DispatcherQueueTimer timer, object args)
    {
        if (_closed || !_inputConnected || !_settings.Toggles.GetValueOrDefault("input.enabled")) return;
        try
        {
            foreach(var gesture in _inputRecognizer.Flush(DateTimeOffset.UtcNow)) DispatchGesture(gesture);
            if (_inputScroll is not null && _previousTouch is { Contacts.Count: 0 }) { var delta = _inputScroll.Process(0,0,TimeSpan.FromMilliseconds(16),false); _actionDispatcher.Scroll(delta.Horizontal,delta.Vertical); }
        }
        catch(Exception exception) { StopInputOnFailure(exception); }
    }
    private void DispatchGesture(GestureEvent gesture)
    {
        var action = GestureActionMapper.Map(gesture, InputPreferences);
        if (action == "Sin acción" && InputPreferences.Toggles.GetValueOrDefault("buttons.tapClick")) action = gesture.Kind switch
        {
            GestureKind.OneFingerTap when InputPreferences.Choices.GetValueOrDefault("buttons.tap1", "").Contains("clic principal") => "Clic principal",
            GestureKind.TwoFingerTap when InputPreferences.Choices.GetValueOrDefault("buttons.tap2", "").Contains("clic secundario") => "Clic secundario",
            GestureKind.TwoFingerTap when InputPreferences.Choices.GetValueOrDefault("buttons.tap2", "").Contains("clic central") => "Clic central",
            GestureKind.ThreeFingerTap when InputPreferences.Choices.GetValueOrDefault("buttons.tap3", "").Contains("clic central") => "Clic central", _ => action
        };
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
