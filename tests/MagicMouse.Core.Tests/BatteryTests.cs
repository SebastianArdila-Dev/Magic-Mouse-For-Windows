using MagicMouse.Core.Devices;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class BatteryTests
{
    [Theory] [InlineData(0,0.0)] [InlineData(10,.1)] [InlineData(50,.5)] [InlineData(100,1.0)]
    public void Fill_is_proportional_and_preserves_empty_zero(int percentage,double fraction)
    { var reading=BatteryReading.FromWindowsProperties((byte)percentage,null); Assert.Equal(percentage,reading.Percent); Assert.Equal(fraction,reading.FillFraction); Assert.Null(reading.IsCharging); }
    [Theory] [InlineData(101,1)] [InlineData(150,50)] [InlineData(200,100)]
    public void Charging_encoding_is_decoded(int combined,int level)
    { var reading=BatteryReading.FromWindowsProperties(null,(byte)combined); Assert.Equal(level,reading.Percent); Assert.True(reading.IsCharging); }
    [Fact] public void Discharging_property_never_shows_bolt()
    { var reading=BatteryReading.FromWindowsProperties((byte)78,(byte)78); Assert.Equal(78,reading.Percent); Assert.False(reading.IsCharging); }
    [Fact] public void Unknown_sentinels_are_not_clamped_to_full_battery()
    { var reading=BatteryReading.FromWindowsProperties((byte)101,(byte)201); Assert.Null(reading.Percent); Assert.Null(reading.IsCharging); Assert.Equal(0,reading.FillFraction); }
    [Fact] public void Missing_and_wrong_typed_properties_stay_unknown()
    { Assert.Equal(BatteryReading.Unknown,BatteryReading.FromWindowsProperties(null,null)); Assert.Equal(BatteryReading.Unknown,BatteryReading.FromWindowsProperties("74",true)); }
}
