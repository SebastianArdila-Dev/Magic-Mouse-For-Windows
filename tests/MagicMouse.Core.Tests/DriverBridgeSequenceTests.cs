using MagicMouse.Core.Drivers;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class DriverBridgeSequenceTests
{
    [Fact]
    public void First_report_and_gaps_reset_the_gesture_but_contiguous_reports_do_not()
    {
        var session = new DriverBridgeSequence();
        Assert.True(session.Accept(40, false, out var reset)); Assert.True(reset);
        Assert.True(session.Accept(41, false, out reset)); Assert.False(reset);
        Assert.True(session.Accept(45, false, out reset)); Assert.True(reset);
        Assert.True(session.Accept(46, true, out reset)); Assert.True(reset);
    }
    [Fact]
    public void Replay_and_zero_are_rejected_without_advancing_the_session()
    {
        var session = new DriverBridgeSequence();
        Assert.False(session.Accept(0, false, out _));
        Assert.True(session.Accept(12, false, out _));
        Assert.False(session.Accept(12, true, out _));
        Assert.False(session.Accept(11, false, out _));
        Assert.True(session.Accept(13, false, out var reset)); Assert.False(reset);
    }
    [Fact]
    public void Sequence_overflow_requires_a_new_session_instead_of_replaying_old_packets()
    {
        var session = new DriverBridgeSequence();
        Assert.True(session.Accept(ulong.MaxValue, false, out _));
        Assert.False(session.Accept(1, true, out _));
        Assert.True(new DriverBridgeSequence().Accept(1, true, out var reset)); Assert.True(reset);
    }
}
