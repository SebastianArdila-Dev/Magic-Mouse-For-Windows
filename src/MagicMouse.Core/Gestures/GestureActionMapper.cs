using MagicMouse.Core.Settings;

namespace MagicMouse.Core.Gestures;

public static class GestureActionMapper
{
    public static string Map(GestureEvent gesture, UserSettings settings)
    {
        var kind = gesture.Kind;
        if (kind is GestureKind.OneFingerSwipeLeft or GestureKind.OneFingerSwipeRight)
        {
            if (!settings.Toggles.GetValueOrDefault("gesture.oneEnabled", true)) return "Sin acción";
            if (settings.Toggles.GetValueOrDefault("gesture.swapOne")) kind = kind == GestureKind.OneFingerSwipeLeft ? GestureKind.OneFingerSwipeRight : GestureKind.OneFingerSwipeLeft;
        }
        if (kind is GestureKind.TwoFingerSwipeLeft or GestureKind.TwoFingerSwipeRight or GestureKind.TwoFingerSwipeUp or GestureKind.TwoFingerSwipeDown)
        {
            if (!settings.Toggles.GetValueOrDefault("gesture.twoEnabled", true)) return "Sin acción";
            if (settings.Toggles.GetValueOrDefault("gesture.swapTwo")) kind = kind switch { GestureKind.TwoFingerSwipeLeft => GestureKind.TwoFingerSwipeRight, GestureKind.TwoFingerSwipeRight => GestureKind.TwoFingerSwipeLeft, _ => kind };
        }
        var name = kind switch
        {
            GestureKind.OneFingerSwipeLeft => "Deslizar 1 dedo a la izquierda",
            GestureKind.OneFingerSwipeRight => "Deslizar 1 dedo a la derecha",
            GestureKind.TwoFingerSwipeLeft => "Deslizar 2 dedos a la izquierda",
            GestureKind.TwoFingerSwipeRight => "Deslizar 2 dedos a la derecha",
            GestureKind.TwoFingerSwipeUp => "Deslizar 2 dedos arriba",
            GestureKind.TwoFingerSwipeDown => "Deslizar 2 dedos abajo",
            GestureKind.OneFingerTap => "Tap con 1 dedo",
            GestureKind.TwoFingerTap => "Tap con 2 dedos",
            GestureKind.ThreeFingerTap => "Tap con 3 dedos",
            GestureKind.OneFingerDoubleTap => "Doble tap con 1 dedo",
            GestureKind.TwoFingerDoubleTap => "Doble tap con 2 dedos",
            _ => throw new ArgumentOutOfRangeException(nameof(gesture))
        };
        return settings.Choices.GetValueOrDefault("gesture:" + name, "Sin acción");
    }
}
