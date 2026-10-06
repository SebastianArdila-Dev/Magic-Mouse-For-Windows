namespace MagicMouse.Core.Drivers;

/// <summary>One capture session; prevents replayed reports from reaching gesture recognition.</summary>
public sealed class DriverBridgeSequence
{
    private ulong? _previous;
    public bool Accept(ulong sequence, bool flagged, out bool discontinuity)
    {
        discontinuity = false;
        if (sequence == 0 || (_previous is { } old && sequence <= old)) return false;
        discontinuity = flagged || _previous is null || sequence != _previous.Value + 1;
        _previous = sequence;
        return true;
    }
}
