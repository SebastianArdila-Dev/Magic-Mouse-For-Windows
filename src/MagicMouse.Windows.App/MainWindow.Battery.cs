using MagicMouse.Core.Devices;
using MagicMouse.Windows.App.Devices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private BatteryReading _battery = BatteryReading.Unknown;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _batteryTimer;
    private bool _batteryRefreshing;
    private void InitializeBattery()
    {
        UpdateBatteryIcon(_battery);
        _batteryTimer=DispatcherQueue.CreateTimer(); _batteryTimer.Interval=TimeSpan.FromSeconds(30);
        _batteryTimer.Tick+=async (_,_)=>await RefreshBatteryAsync(); _batteryTimer.Start();
        BatteryButton.Click+=async (_,_)=>
        {
            await RefreshBatteryAsync();
            await ShowSheetAsync("Batería del Magic Mouse",_battery.Percent is int level
                ? $"Nivel: {level}%. " + (_battery.IsCharging is true ? "Cargando." : _battery.IsCharging is false ? "Sin cargar." : "Estado de carga no disponible.") + "\nSe consulta el dato que Windows expone para este mouse cada 30 segundos."
                : "El mouse no está conectado o Windows no proporciona su nivel de batería. El icono muestra un estado desconocido; no representa la batería del PC.");
        };
    }
    private async Task RefreshBatteryAsync()
    {
        if(_closed) return;
        var device=_currentMouseDevice;
        if(device is null) { UpdateBatteryIcon(BatteryReading.Unknown); return; }
        if(_batteryRefreshing) return; _batteryRefreshing=true;
        try
        {
            var reading=await MouseBatteryService.ReadAsync(device);
            if(!_closed && _currentMouseDevice?.Id==device.Id) UpdateBatteryIcon(reading);
        }
        catch(Exception exception)
        {
            if(!_closed && _currentMouseDevice?.Id==device.Id) UpdateBatteryIcon(BatteryReading.Unknown);
            await LogAsync($"Battery property unavailable: {exception.Message}");
        }
        finally { _batteryRefreshing=false; }
    }
    private void UpdateBatteryIcon(BatteryReading reading)
    {
        _battery=reading;
        BatteryFill.Width=36*reading.FillFraction;
        BatteryFill.Visibility=reading.Percent is null ? Visibility.Collapsed : Visibility.Visible;
        BatteryBolt.Visibility=reading.IsCharging is true ? Visibility.Visible : Visibility.Collapsed;
        BatteryUnknown.Visibility=reading.Percent is null ? Visibility.Visible : Visibility.Collapsed;
        BatteryPercentage.Text=reading.Percent is int level ? $"{level}%" : "—";
        BatteryPercentage.Visibility=reading.Percent is null ? Visibility.Collapsed : Visibility.Visible;
        var label=reading.Percent is int percent ? $"Batería del Magic Mouse: {percent}%" + (reading.IsCharging is true ? ", cargando" : reading.IsCharging is false ? ", sin cargar" : ", estado de carga desconocido") : "Batería del Magic Mouse no disponible";
        AutomationProperties.SetName(BatteryButton,label); ToolTipService.SetToolTip(BatteryButton,label);
    }
}
