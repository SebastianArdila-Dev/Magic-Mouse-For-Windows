using MagicMouse.Core.Touch;

namespace MagicMouse.Core.Gestures;

public enum GestureKind
{
    OneFingerSwipeLeft, OneFingerSwipeRight,
    TwoFingerSwipeLeft, TwoFingerSwipeRight, TwoFingerSwipeUp, TwoFingerSwipeDown,
    OneFingerTap, TwoFingerTap, ThreeFingerTap, OneFingerDoubleTap, TwoFingerDoubleTap
}

public sealed record GestureEvent(GestureKind Kind, DateTimeOffset StartedAt, TimeSpan Duration, double Distance);

public sealed record GestureRecognizerOptions(
    double MinimumSwipeDistance = 0.12,
    double MaximumTapMovement = 0.035,
    TimeSpan? MaximumSwipeDuration = null,
    TimeSpan? MaximumTapDuration = null)
{
    public TimeSpan SwipeDurationLimit => MaximumSwipeDuration ?? TimeSpan.FromMilliseconds(900);
    public TimeSpan TapDurationLimit => MaximumTapDuration ?? TimeSpan.FromMilliseconds(320);
}

/// <summary>Recognizes normalized single- and multi-contact swipes and taps from touch frames.</summary>
public sealed class GestureRecognizer
{
    private readonly GestureRecognizerOptions _options;
    private int _contactCount;
    private DateTimeOffset _startedAt;
    private TouchFrame? _lastFrame;
    private double _maximumMovement;

    public GestureRecognizer(GestureRecognizerOptions? options = null) => _options = options ?? new GestureRecognizerOptions();

    public GestureEvent? Process(TouchFrame frame)
    {
        frame.Validate();
        if (frame.Contacts.Count > 0)
        {
            if (_lastFrame is null)
            {
                _contactCount = frame.Contacts.Count;
                _startedAt = frame.Timestamp;
                _lastFrame = frame;
                _startContacts = frame.Contacts.ToArray();
                return null;
            }
            if (frame.Timestamp < _lastFrame.Timestamp || frame.Contacts.Count != _contactCount)
            {
                Reset();
                return null;
            }
            var activeIds = frame.Contacts.Select(contact => contact.Id).Order().ToArray();
            var initialIds = _startContacts!.Select(contact => contact.Id).Order().ToArray();
            if (!activeIds.SequenceEqual(initialIds))
            {
                Reset();
                return null;
            }
            _lastFrame = frame;
            var origin = Center(_startContacts!);
            var current = Center(frame.Contacts);
            _maximumMovement = Math.Max(_maximumMovement, Math.Sqrt(Math.Pow(current.X - origin.X, 2) + Math.Pow(current.Y - origin.Y, 2)));
            return null;
        }

        if (_lastFrame is null) return null;
        var first = Center(_startContacts ?? _lastFrame.Contacts);
        var end = Center(_lastFrame.Contacts);
        var duration = frame.Timestamp < _lastFrame.Timestamp ? TimeSpan.MinValue : frame.Timestamp - _startedAt;
        var dx = end.X - first.X;
        var dy = end.Y - first.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var count = _contactCount;
        var startedAt = _startedAt;
        var maximumMovement = _maximumMovement;
        Reset();

        if (duration < TimeSpan.Zero) return null;
        if (duration <= _options.TapDurationLimit && maximumMovement <= _options.MaximumTapMovement)
            return count switch
            {
                1 => new GestureEvent(GestureKind.OneFingerTap, startedAt, duration, distance),
                2 => new GestureEvent(GestureKind.TwoFingerTap, startedAt, duration, distance),
                3 => new GestureEvent(GestureKind.ThreeFingerTap, startedAt, duration, distance),
                _ => null
            };

        if (count is < 1 or > 2 || duration > _options.SwipeDurationLimit || distance < _options.MinimumSwipeDistance) return null;
        var kind = Math.Abs(dx) >= Math.Abs(dy)
            ? (count, dx < 0) switch
            {
                (1, true) => GestureKind.OneFingerSwipeLeft,
                (1, false) => GestureKind.OneFingerSwipeRight,
                (2, true) => GestureKind.TwoFingerSwipeLeft,
                _ => GestureKind.TwoFingerSwipeRight
            }
            : (count, dy < 0) switch
            {
                (2, true) => GestureKind.TwoFingerSwipeUp,
                (2, false) => GestureKind.TwoFingerSwipeDown,
                _ => (GestureKind?)null
            };
        return kind is null ? null : new GestureEvent(kind.Value, startedAt, duration, distance);
    }

    private IReadOnlyList<TouchContact>? _startContacts;

    private static (double X, double Y) Center(IReadOnlyList<TouchContact> contacts) =>
        (contacts.Average(contact => contact.X), contacts.Average(contact => contact.Y));

    private void Reset()
    {
        _contactCount = 0;
        _startedAt = default;
        _lastFrame = null;
        _startContacts = null;
        _maximumMovement = 0;
    }
}
