using MagicMouse.Core.Devices;
using Windows.Devices.Enumeration;

namespace MagicMouse.Windows.App.Devices;

internal static class MouseBatteryService
{
    private static readonly string[] Properties = ["System.Devices.BatteryLife","System.Devices.BatteryPlusCharging","System.Devices.ContainerId"];
    private static BatteryReading Read(DeviceInformation information) => BatteryReading.FromWindowsProperties(
        information.Properties.GetValueOrDefault(Properties[0]),information.Properties.GetValueOrDefault(Properties[1]));
    public static async Task<BatteryReading> ReadAsync(DeviceInformation device)
    {
        var current = await DeviceInformation.CreateFromIdAsync(device.Id,Properties,DeviceInformationKind.DeviceInterface);
        if (current is null) return BatteryReading.Unknown;
        var reading=Read(current); if (reading.Percent is not null && reading.IsCharging is not null) return reading;
        if (!current.Properties.TryGetValue(Properties[2],out var containerValue) || !Guid.TryParse(containerValue?.ToString(),out var container) || container==Guid.Empty) return reading;
        // Query only the same hardware container; never use the PC battery or a nearby Bluetooth device.
        var query=$"System.Devices.ContainerId:=\"{container:B}\"";
        foreach (var kind in new[] { DeviceInformationKind.Device, DeviceInformationKind.DeviceContainer })
        {
            var peers=await DeviceInformation.FindAllAsync(query,Properties,kind);
            foreach(var peer in peers)
            {
                var candidate=Read(peer);
                if(candidate.Percent is not null && (reading.Percent is null || candidate.IsCharging is not null)) reading=candidate;
                if(reading.Percent is not null && reading.IsCharging is not null) return reading;
            }
        }
        return reading;
    }
}
