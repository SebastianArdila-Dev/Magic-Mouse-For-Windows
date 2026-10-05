namespace MagicMouse.Core.Devices;

public enum ConnectionState { Disconnected, Connecting, Connected, Sleeping, Unavailable, Error }
public enum ConnectionTransport { Unknown, Bluetooth, Usb }
public enum Availability { Available, Unavailable, Unknown }

public sealed record DeviceCapabilities(
    Availability TouchSurface = Availability.Unknown,
    Availability MultiFingerTouch = Availability.Unknown,
    Availability Battery = Availability.Unknown,
    Availability ChargingState = Availability.Unknown,
    Availability SerialNumber = Availability.Unknown,
    Availability FirmwareVersion = Availability.Unknown,
    Availability HorizontalScroll = Availability.Unknown,
    Availability TapToClick = Availability.Unknown);

public sealed record DeviceDescriptor(
    string DeviceId,
    string FriendlyName,
    ushort VendorId,
    ushort ProductId,
    ConnectionTransport Transport,
    ConnectionState State,
    DeviceCapabilities Capabilities,
    int? BatteryPercent = null,
    string? ChargingState = null,
    string? SerialNumber = null,
    string? FirmwareVersion = null)
{
    public DeviceDescriptor Validate()
    {
        if (string.IsNullOrWhiteSpace(DeviceId)) throw new ArgumentException("A device identifier is required.", nameof(DeviceId));
        if (BatteryPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(BatteryPercent));
        if (BatteryPercent is not null && Capabilities.Battery != Availability.Available)
            throw new ArgumentException("Battery data requires an explicitly available battery capability.", nameof(BatteryPercent));
        if (ChargingState is not null && Capabilities.ChargingState != Availability.Available)
            throw new ArgumentException("Charging data requires an explicitly available charging capability.", nameof(ChargingState));
        if (SerialNumber is not null && Capabilities.SerialNumber != Availability.Available)
            throw new ArgumentException("Serial data requires an explicitly available serial capability.", nameof(SerialNumber));
        if (FirmwareVersion is not null && Capabilities.FirmwareVersion != Availability.Available)
            throw new ArgumentException("Firmware data requires an explicitly available firmware capability.", nameof(FirmwareVersion));
        return this;
    }
}
