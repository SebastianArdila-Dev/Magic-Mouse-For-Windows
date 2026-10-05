using MagicMouse.Core.Gestures;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class ScrollEngineTests
{
    [Fact] public void Natural_and_axis_inversion_cancel_each_other()
    {
        var engine = new ScrollEngine(new(Natural: true, InvertVertical: true, AxisLock: false));
        Assert.Equal(new ScrollDelta(-2, 4), engine.Process(2, 4, TimeSpan.FromMilliseconds(16), true));
    }
    [Fact] public void Axis_lock_preserves_axis_until_contacts_end()
    {
        var engine = new ScrollEngine();
        Assert.Equal(0, engine.Process(1, 4, TimeSpan.FromMilliseconds(16), true).Horizontal);
        Assert.Equal(0, engine.Process(8, 1, TimeSpan.FromMilliseconds(16), true).Horizontal);
        engine.Process(0, 0, TimeSpan.FromMilliseconds(16), false);
        Assert.Equal(0, engine.Process(8, 1, TimeSpan.FromMilliseconds(16), true).Vertical);
    }
    [Fact] public void Lift_and_disabled_scroll_stop_inertia_immediately()
    {
        var engine = new ScrollEngine();
        engine.Process(0, 4, TimeSpan.FromMilliseconds(16), true);
        Assert.Equal(default, engine.Process(0, 0, TimeSpan.FromMilliseconds(16), false, lifted: true));
        Assert.Equal(default, engine.Process(0, 0, TimeSpan.FromMilliseconds(16), false));
    }
    [Fact] public void Inertia_decays_and_smooth_off_has_no_tail()
    {
        var engine = new ScrollEngine(); engine.Process(0, 4, TimeSpan.FromMilliseconds(16), true);
        var first = engine.Process(0, 0, TimeSpan.FromMilliseconds(16), false).Vertical;
        var second = engine.Process(0, 0, TimeSpan.FromMilliseconds(16), false).Vertical;
        Assert.True(first > second && second > 0);
        engine.Configure(new(Smooth: false)); engine.Process(0, 4, TimeSpan.FromMilliseconds(16), true);
        Assert.Equal(default, engine.Process(0, 0, TimeSpan.FromMilliseconds(16), false));
    }
}
