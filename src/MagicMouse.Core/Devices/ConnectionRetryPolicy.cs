namespace MagicMouse.Core.Devices;

/// <summary>Bounded backoff avoids continuously reopening a busy HID collection.</summary>
public sealed class ConnectionRetryPolicy
{
    private int _attempt;
    public TimeSpan NextDelay()
    {
        var seconds=Math.Min(30,3*Math.Pow(2,Math.Min(_attempt,4)));
        _attempt=Math.Min(_attempt+1,4);
        return TimeSpan.FromSeconds(seconds);
    }
    public void Reset()=>_attempt=0;
}
