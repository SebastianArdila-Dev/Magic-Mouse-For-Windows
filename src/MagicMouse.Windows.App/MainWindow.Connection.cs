using MagicMouse.Core.Devices;
using MagicMouse.Windows.App.Devices;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private readonly ConnectionRetryPolicy _connectionRetry=new();
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _reconnectTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _discoveryTimer;
    private bool _connectingInput;
    private bool _refreshingDiscovery;
    private void InitializeConnectionWatchdog()
    {
        _discoveryTimer=DispatcherQueue.CreateTimer();_discoveryTimer.Interval=TimeSpan.FromSeconds(30);
        _discoveryTimer.Tick+=async (_,_)=>
        {
            if(_closed || _refreshingDiscovery) return;_refreshingDiscovery=true;
            try {await _discovery.RefreshAsync();}
            catch(Exception exception) {await LogAsync($"Discovery retry failed: {exception.Message}");}
            finally {_refreshingDiscovery=false;}
        };
        _discoveryTimer.Start();
    }
    private void ScheduleInputReconnect()
    {
        if(_closed || !_settings.Toggles.GetValueOrDefault("input.enabled") || _currentMouseDevice is null) return;
        _reconnectTimer ??=DispatcherQueue.CreateTimer();_reconnectTimer.IsRepeating=false;
        _reconnectTimer.Tick-=RetryInputConnection;_reconnectTimer.Tick+=RetryInputConnection;
        if(_reconnectTimer.IsRunning) return;
        _reconnectTimer.Interval=_connectionRetry.NextDelay();_reconnectTimer.Start();
    }
    private async void RetryInputConnection(Microsoft.UI.Dispatching.DispatcherQueueTimer timer,object args)
    {
        if(_closed || !_settings.Toggles.GetValueOrDefault("input.enabled") || _currentMouseDevice is null || (_inputConnected && _capture.IsCapturing)) return;
        try {await ConnectInputCoreAsync(false);}
        catch(Exception exception) {StopInputOnFailure(exception);}
    }
    private void AttachCaptureEvents(HidReportCaptureService source)
    {
        source.ReportReceived+=report=>OnReportCaptured(source,report);
        source.CaptureFailed+=exception=>OnCaptureFailed(source,exception);
        source.CaptureStateChanged+=active=>DispatcherQueue.TryEnqueue(()=> {if(!_closed && ReferenceEquals(_capture,source)) UpdateCaptureButtons();});
    }
}
