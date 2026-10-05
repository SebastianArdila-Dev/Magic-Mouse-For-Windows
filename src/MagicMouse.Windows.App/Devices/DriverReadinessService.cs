using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace MagicMouse.Windows.App.Devices;

internal sealed record DriverReadiness(bool BluetoothAvailable,bool WindowsHidAvailable,string? DeviceName,string? Provider,string? Version,string? InfName);
internal static class DriverReadinessService
{
    public static async Task<DriverReadiness> ReadAsync(DeviceInformation? mouse)
    {
        bool bluetooth=false;
        try { bluetooth=await BluetoothAdapter.GetDefaultAsync() is not null; } catch { }
        var drivers=Path.Combine(Environment.SystemDirectory,"drivers");
        var hid=new[]{"hidclass.sys","hidbth.sys","mouhid.sys"}.All(name=>File.Exists(Path.Combine(drivers,name)));
        string? provider=null,version=null,inf=null;
        if(mouse is not null)
        {
            try
            {
                var props=new[]{"System.Devices.DeviceInstanceId","System.Devices.DriverProvider","System.Devices.DriverVersion","System.Devices.DriverInfPath"};
                var info=await DeviceInformation.CreateFromIdAsync(mouse.Id,props,DeviceInformationKind.DeviceInterface);
                if(info?.Properties.GetValueOrDefault("System.Devices.DeviceInstanceId") is string instance)
                {
                    var dev=await DeviceInformation.CreateFromIdAsync(instance,props,DeviceInformationKind.Device);
                    if(dev is not null) info=dev;
                }
                provider=info?.Properties.GetValueOrDefault("System.Devices.DriverProvider") as string;
                version=info?.Properties.GetValueOrDefault("System.Devices.DriverVersion") as string;
                inf=info?.Properties.GetValueOrDefault("System.Devices.DriverInfPath") as string;
            }
            catch { /* Missing driver metadata must remain unknown, not a fabricated success. */ }
        }
        return new(bluetooth,hid,mouse?.Name,provider,version,inf);
    }
}
