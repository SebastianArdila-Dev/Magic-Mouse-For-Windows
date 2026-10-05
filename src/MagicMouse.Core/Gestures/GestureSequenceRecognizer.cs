using MagicMouse.Core.Touch;

namespace MagicMouse.Core.Gestures;

/// <summary>Defers single taps until the double-tap interval expires, so both actions never fire.</summary>
public sealed class GestureSequenceRecognizer
{
    private readonly GestureRecognizer _recognizer;
    private readonly TimeSpan _interval;
    private GestureEvent? _pending;
    private (double X, double Y)? _pendingPosition, _currentPosition;
    private bool _contactActive;
    public GestureSequenceRecognizer(GestureRecognizerOptions? options = null, TimeSpan? interval = null)
    { _recognizer = new(options); _interval = interval ?? TimeSpan.FromMilliseconds(400); }
    public IReadOnlyList<GestureEvent> Process(TouchFrame frame)
    {
        var events = new List<GestureEvent>();
        _contactActive = frame.Contacts.Count > 0;
        if (frame.Contacts.Count > 0) _currentPosition = (frame.Contacts.Average(c => c.X), frame.Contacts.Average(c => c.Y));
        var gesture = _recognizer.Process(frame);
        if (gesture is null) return events;
        if (gesture.Kind is not (GestureKind.OneFingerTap or GestureKind.TwoFingerTap))
        { events.AddRange(Flush(frame.Timestamp, true)); events.Add(gesture); return events; }
        if (_pending is not null && _pending.Kind == gesture.Kind && gesture.StartedAt >= _pending.StartedAt + _pending.Duration && gesture.StartedAt - (_pending.StartedAt + _pending.Duration) <= _interval && Near(_pendingPosition, _currentPosition))
        {
            events.Add(new(gesture.Kind == GestureKind.OneFingerTap ? GestureKind.OneFingerDoubleTap : GestureKind.TwoFingerDoubleTap, _pending.StartedAt, frame.Timestamp - _pending.StartedAt, gesture.Distance));
            _pending = null; _pendingPosition = null;
        }
        else { events.AddRange(Flush(frame.Timestamp, true)); _pending = gesture; _pendingPosition = _currentPosition; }
        return events;
    }
    public IReadOnlyList<GestureEvent> Flush(DateTimeOffset now, bool force = false)
    {
        if (_pending is null || (!force && (_contactActive || now - (_pending.StartedAt + _pending.Duration) <= _interval))) return [];
        var result = _pending; _pending = null; _pendingPosition = null; return [result];
    }
    private static bool Near((double X, double Y)? a, (double X, double Y)? b) => a is { } first && b is { } second && Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2) < .01;
}
