using MagicMouse.Core.Appearance;
using MagicMouse.Core.Gestures;
using MagicMouse.Core.Settings;
using MagicMouse.Core.Touch;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class MacParityTests
{
    [Fact] public void Touch_contact_rejects_nonfinite_coordinates() => Assert.Throws<ArgumentOutOfRangeException>(()=>new TouchContact(1,double.NaN,.5).Validate());
    [Theory] [InlineData(70)] [InlineData(100)] [InlineData(180)]
    public void Cursor_file_has_valid_directory_hotspot_and_alpha(int percent)
    {
        var bytes = CursorFile.Create("macOS",percent); var size = bytes[6];
        Assert.Equal(2,BitConverter.ToUInt16(bytes,2)); Assert.Equal(1,BitConverter.ToUInt16(bytes,4));
        Assert.Equal((int)Math.Round(32*percent/100.0),size); Assert.Equal(22u,BitConverter.ToUInt32(bytes,18));
        Assert.Equal(bytes.Length-22,(int)BitConverter.ToUInt32(bytes,14));
        Assert.InRange(BitConverter.ToUInt16(bytes,10),0,size-1);
        Assert.Contains(Enumerable.Range(0,size*size).Select(i=>bytes[62+i*4+3]),alpha=>alpha==255);
        Assert.Contains(Enumerable.Range(0,size*size).Select(i=>bytes[62+i*4+3]),alpha=>alpha==0);
    }
    [Fact] public void Cursor_rejects_nonfinite_size() => Assert.Throws<ArgumentOutOfRangeException>(()=>CursorFile.Create("macOS",double.NaN));
    [Theory] [InlineData(0x29,6)] [InlineData(0x12,14)]
    public void Apple_packet_decodes_contact_identifier_and_center(int id,int header)
    {
        var packet = new byte[header+8]; packet[0]=(byte)id; packet[header+5]=64; packet[header+7]=0x40;
        Assert.True(AppleMouseReportParser.TryParse(packet,DateTimeOffset.UtcNow,out var report));
        var contact=Assert.Single(report!.Frame.Contacts); Assert.Equal(1,contact.Id);
        Assert.Equal(1100/2358.0,contact.X,6); Assert.Equal(1589/3636.0,contact.Y,6);
    }
    [Fact] public void Apple_packet_rejects_truncated_unknown_and_duplicate_contacts()
    {
        Assert.False(AppleMouseReportParser.TryParse([0x12,0],DateTimeOffset.UtcNow,out _));
        Assert.False(AppleMouseReportParser.TryParse([0x28,0,0,0],DateTimeOffset.UtcNow,out _));
        var duplicate=new byte[22]; duplicate[0]=0x29; duplicate[13]=duplicate[21]=0x40;
        Assert.False(AppleMouseReportParser.TryParse(duplicate,DateTimeOffset.UtcNow,out _));
    }
    [Fact] public void Mouse2_motion_only_packet_has_no_contacts_and_signed_motion()
    {
        Assert.True(AppleMouseReportParser.TryParse([0x12,2,255,255,3,0,0,0],DateTimeOffset.UtcNow,out var report));
        Assert.Empty(report!.Frame.Contacts); Assert.Equal(-1,report.DeltaX); Assert.Equal(3,report.DeltaY); Assert.Equal(2,report.Buttons);
    }
    [Theory] [InlineData(1,GestureKind.OneFingerDoubleTap)] [InlineData(2,GestureKind.TwoFingerDoubleTap)]
    public void Double_tap_fires_once_and_suppresses_single_taps(int count,GestureKind kind)
    {
        var recognizer=new GestureSequenceRecognizer(); var time=DateTimeOffset.UtcNow;
        TouchContact[] contacts=Enumerable.Range(0,count).Select(i=>new TouchContact(i,.4+i*.1,.5)).ToArray();
        Assert.Empty(recognizer.Process(new(time,contacts))); Assert.Empty(recognizer.Process(new(time.AddMilliseconds(100),[])));
        Assert.Empty(recognizer.Process(new(time.AddMilliseconds(250),contacts)));
        Assert.Empty(recognizer.Flush(time.AddMilliseconds(520))); // second tap still touching
        var events=recognizer.Process(new(time.AddMilliseconds(540),[])); Assert.Equal(kind,Assert.Single(events).Kind);
        Assert.Empty(recognizer.Flush(time.AddMilliseconds(1000)));
    }
    [Fact] public void Single_tap_waits_for_double_tap_window()
    {
        var recognizer=new GestureSequenceRecognizer(); var time=DateTimeOffset.UtcNow;
        recognizer.Process(new(time,[new(1,.5,.5)])); recognizer.Process(new(time.AddMilliseconds(100),[]));
        Assert.Empty(recognizer.Flush(time.AddMilliseconds(400)));
        Assert.Equal(GestureKind.OneFingerTap,Assert.Single(recognizer.Flush(time.AddMilliseconds(501))).Kind);
    }
    [Fact] public void Mac_defaults_map_double_taps_and_do_not_enable_hardware()
    {
        var settings=new UserSettings(); MacGestureDefaults.Apply(settings);
        Assert.Equal("Zoom inteligente",GestureActionMapper.Map(new(GestureKind.OneFingerDoubleTap,DateTimeOffset.UtcNow,TimeSpan.Zero,0),settings));
        Assert.Equal("Task View",GestureActionMapper.Map(new(GestureKind.TwoFingerDoubleTap,DateTimeOffset.UtcNow,TimeSpan.Zero,0),settings));
        Assert.False(settings.Toggles.GetValueOrDefault("input.enabled")); Assert.Equal(new ushort[]{0x5B,0x09},WindowsActionPlan.Keys("Task View"));
    }
}
