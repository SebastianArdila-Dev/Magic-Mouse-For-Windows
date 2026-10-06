using MagicMouse.Core.Settings;

namespace MagicMouse.Core.Gestures;

/// <summary>Only configured and enabled swipe actions reserve a scrolling axis.</summary>
public static class InputRouting
{
    public static bool AllowsScroll(int contacts, string fingers) => fingers switch
    {
        "1 o 2 dedos" => contacts is 1 or 2,
        "2 dedos" => contacts == 2,
        _ => contacts == 1
    };

    public static (bool Horizontal, bool Vertical) ReservedAxes(int contacts, UserSettings settings)
    {
        bool Assigned(GestureKind kind) => GestureActionMapper.Map(new(kind, default, TimeSpan.Zero, 0), settings) != "Sin acción";
        return contacts switch
        {
            1 => (Assigned(GestureKind.OneFingerSwipeLeft) || Assigned(GestureKind.OneFingerSwipeRight), false),
            2 => (Assigned(GestureKind.TwoFingerSwipeLeft) || Assigned(GestureKind.TwoFingerSwipeRight),
                Assigned(GestureKind.TwoFingerSwipeUp) || Assigned(GestureKind.TwoFingerSwipeDown)),
            _ => (false, false)
        };
    }
}

/// <summary>Preserves sub-unit wheel movement instead of rounding every device sample away.</summary>
public sealed class ScrollWheelAccumulator
{
    private double _horizontal, _vertical;
    public (int Horizontal, int Vertical) Add(double horizontal, double vertical)
    {
        if (!double.IsFinite(horizontal) || !double.IsFinite(vertical)) throw new ArgumentOutOfRangeException(nameof(horizontal));
        _horizontal += horizontal * 1200;
        _vertical -= vertical * 1200;
        var x = (int)Math.Clamp(Math.Truncate(_horizontal), int.MinValue, int.MaxValue);
        var y = (int)Math.Clamp(Math.Truncate(_vertical), int.MinValue, int.MaxValue);
        _horizontal -= x;
        _vertical -= y;
        return (x, y);
    }
    public void Reset() => _horizontal = _vertical = 0;
}
