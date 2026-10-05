using MagicMouse.Core.Devices;
using MagicMouse.Core.Touch;
using Xunit;

namespace MagicMouse.Core.Tests;

public class DeviceModelTests
{
    [Fact]
    public void Unknown_hardware_values_remain_absent()
    {
        var descriptor = new DeviceDescriptor("hid#1", "Apple Mouse", 0x05AC, 0, ConnectionTransport.Bluetooth,
            ConnectionState.Connected, new DeviceCapabilities()).Validate();

        Assert.Null(descriptor.BatteryPercent);
        Assert.Null(descriptor.SerialNumber);
        Assert.Equal(Availability.Unknown, descriptor.Capabilities.Battery);
    }

    [Fact]
    public void Battery_value_requires_confirmed_capability()
    {
        var descriptor = new DeviceDescriptor("hid#1", "Apple Mouse", 0x05AC, 0, ConnectionTransport.Bluetooth,
            ConnectionState.Connected, new DeviceCapabilities(), BatteryPercent: 74);

        Assert.Throws<ArgumentException>(() => descriptor.Validate());
    }

    [Fact]
    public void Touch_coordinates_must_be_normalized()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchContact(0, 1.2, 0.4).Validate());
    }
}
