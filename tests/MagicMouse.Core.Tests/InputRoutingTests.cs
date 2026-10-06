using MagicMouse.Core.Gestures;
using MagicMouse.Core.Settings;
using MagicMouse.Core.Touch;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class InputRoutingTests
{
    [Fact]
    public void Disabled_two_finger_gestures_leave_both_scroll_axes_available()
    {
        var settings = new UserSettings(); MacGestureDefaults.Apply(settings);
        settings.Toggles["gesture.twoEnabled"] = false;
        Assert.Equal((false, false), InputRouting.ReservedAxes(2, settings));
    }

    [Fact]
    public void Unassigned_two_finger_gestures_do_not_swallow_horizontal_scroll()
    {
        Assert.Equal((false, false), InputRouting.ReservedAxes(2, new UserSettings()));
    }

    [Fact]
    public void Either_direction_can_reserve_an_axis_and_vertical_actions_do_not_also_scroll()
    {
        var settings = new UserSettings();
        settings.Choices["gesture:Deslizar 1 dedo a la derecha"] = "Adelante";
        Assert.Equal((true, false), InputRouting.ReservedAxes(1, settings));
        settings.Choices["gesture:Deslizar 2 dedos arriba"] = "Task View";
        Assert.Equal((false, true), InputRouting.ReservedAxes(2, settings));
    }

    [Theory]
    [InlineData("1 o 2 dedos",1,true)] [InlineData("1 o 2 dedos",2,true)]
    [InlineData("1 o 2 dedos",3,false)] [InlineData("2 dedos",1,false)]
    [InlineData("2 dedos",2,true)] [InlineData("1 dedo",2,false)]
    public void Scroll_respects_finger_selection(string mode,int count,bool allowed)
        => Assert.Equal(allowed, InputRouting.AllowsScroll(count,mode));

    [Fact]
    public void Fractional_scroll_samples_accumulate_and_reset_discards_the_old_remainder()
    {
        var wheel = new ScrollWheelAccumulator();
        Assert.Equal((0,0),wheel.Add(.00025,.00025));
        Assert.Equal((0,0),wheel.Add(.00025,.00025));
        Assert.Equal((0,0),wheel.Add(.00025,.00025));
        Assert.Equal((1,-1),wheel.Add(.00025,.00025));
        wheel.Reset(); Assert.Equal((0,0),wheel.Add(.00025,.00025));
    }

    [Fact]
    public void Direction_reversal_cancels_fractional_wheel_movement()
    {
        var wheel = new ScrollWheelAccumulator(); wheel.Add(.0005,.0005);
        Assert.Equal((0,0),wheel.Add(-.0005,-.0005));
        Assert.Throws<ArgumentOutOfRangeException>(()=>wheel.Add(double.NaN,0));
    }

    [Theory]
    [InlineData(GestureKind.OneFingerTap,"buttons.tap1","1 dedo: clic principal","Clic principal")]
    [InlineData(GestureKind.TwoFingerTap,"buttons.tap2","2 dedos: clic secundario","Clic secundario")]
    [InlineData(GestureKind.ThreeFingerTap,"buttons.tap3","3 dedos: clic central","Clic central")]
    public void Tap_settings_are_used_by_the_same_mapper_as_gestures(GestureKind kind,string key,string choice,string expected)
    {
        var settings = new UserSettings(); settings.Toggles["buttons.tapClick"] = true;
        settings.Choices[key] = choice;
        var gesture = new GestureEvent(kind, default, TimeSpan.Zero,0);
        Assert.Equal(expected,GestureActionMapper.Map(gesture,settings));
        settings.Toggles["buttons.tapClick"] = false;
        Assert.Equal("Sin acción",GestureActionMapper.Map(gesture,settings));
    }

    [Fact]
    public void Motion_only_report_does_not_synthesise_a_touch_release()
    {
        Assert.True(AppleMouseReportParser.TryParse([0x12,0,1,0,0,0,0,0],default,out var motion));
        Assert.False(motion!.HasTouchData);
        var packet = new byte[14]; packet[0]=0x12;
        Assert.True(AppleMouseReportParser.TryParse(packet,default,out var release));
        Assert.True(release!.HasTouchData); Assert.Empty(release.Frame.Contacts);
    }

    [Theory]
    [InlineData(0x29,6)] [InlineData(0x12,14)]
    public void Reports_to_swipe_to_action_work_in_both_mouse_layouts(int reportId,int header)
    {
        var settings = new UserSettings(); MacGestureDefaults.Apply(settings);
        var recognizer = new GestureSequenceRecognizer();
        var start = DateTimeOffset.UtcNow;
        byte[] Packet(int x,bool touching)
        {
            var bytes = new byte[header+(touching?8:0)]; bytes[0]=(byte)reportId;
            if(touching) {var bits=x&0xfff; bytes[header]=(byte)bits; bytes[header+1]=(byte)(bits>>8); bytes[header+5]=64; bytes[header+7]=0x40;}
            return bytes;
        }
        Assert.True(AppleMouseReportParser.TryParse(Packet(0,true),start,out var first));
        Assert.Empty(recognizer.Process(first!.Frame));
        Assert.True(AppleMouseReportParser.TryParse(Packet(700,true),start.AddMilliseconds(100),out var moved));
        Assert.Empty(recognizer.Process(moved!.Frame));
        Assert.True(AppleMouseReportParser.TryParse(Packet(0,false),start.AddMilliseconds(150),out var last));
        var action=Assert.Single(recognizer.Process(last!.Frame));
        Assert.Equal("Adelante",GestureActionMapper.Map(action,settings));
        Assert.Equal(new ushort[]{0xA7},WindowsActionPlan.Keys("Adelante"));
    }
}
