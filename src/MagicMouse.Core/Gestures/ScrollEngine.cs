namespace MagicMouse.Core.Gestures;

public sealed record ScrollOptions(bool Enabled = true, bool Vertical = true, bool Horizontal = true,
    bool Natural = false, bool InvertVertical = false, bool InvertHorizontal = false,
    bool AxisLock = true, bool Smooth = true, double Speed = 1, double Sensitivity = 1,
    double Friction = 12, bool IgnoreMouseMovement = false, bool IgnoreLifted = true);

public readonly record struct ScrollDelta(double Horizontal, double Vertical);

/// <summary>Pure continuous-delta engine; callers provide verified device deltas and elapsed time.</summary>
public sealed class ScrollEngine
{
    private ScrollOptions _options;
    private double _horizontalVelocity;
    private double _verticalVelocity;
    private int _lockedAxis;
    public ScrollEngine(ScrollOptions? options = null) => _options = Validate(options ?? new());
    public void Configure(ScrollOptions options) { _options = Validate(options); Reset(); }
    private static ScrollOptions Validate(ScrollOptions options)
    {
        if (!double.IsFinite(options.Speed) || options.Speed <= 0 || !double.IsFinite(options.Sensitivity) || options.Sensitivity <= 0 || !double.IsFinite(options.Friction) || options.Friction <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        return options;
    }
    public ScrollDelta Process(double dx, double dy, TimeSpan elapsed, bool touching, bool mouseMoving = false, bool lifted = false)
    {
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || elapsed <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var seconds = Math.Min(elapsed.TotalSeconds, .1);
        if (!_options.Enabled || (_options.IgnoreMouseMovement && mouseMoving) || (_options.IgnoreLifted && lifted)) { Reset(); return default; }
        if (touching)
        {
            var scale = _options.Speed * _options.Sensitivity;
            dx *= scale * (_options.Natural ^ _options.InvertHorizontal ? -1 : 1);
            dy *= scale * (_options.Natural ^ _options.InvertVertical ? -1 : 1);
            if (!_options.Horizontal) dx = 0;
            if (!_options.Vertical) dy = 0;
            if (_options.AxisLock)
            {
                if (_lockedAxis == 0 && Math.Abs(dx) + Math.Abs(dy) > 0) _lockedAxis = Math.Abs(dx) > Math.Abs(dy) ? 1 : 2;
                if (_lockedAxis == 1) dy = 0;
                if (_lockedAxis == 2) dx = 0;
            }
            _horizontalVelocity = dx / seconds;
            _verticalVelocity = dy / seconds;
            return new(dx, dy); // direct input never waits for an inertia timer
        }
        _lockedAxis = 0;
        if (!_options.Smooth) { Reset(); return default; }
        var decay = Math.Exp(-_options.Friction * seconds);
        var integral = (1 - decay) / _options.Friction;
        var result = new ScrollDelta(_horizontalVelocity * integral, _verticalVelocity * integral);
        _horizontalVelocity *= decay; _verticalVelocity *= decay;
        if (Math.Abs(_horizontalVelocity) + Math.Abs(_verticalVelocity) < .001) Reset();
        return result;
    }
    public void Reset() { _horizontalVelocity = 0; _verticalVelocity = 0; _lockedAxis = 0; }
}
