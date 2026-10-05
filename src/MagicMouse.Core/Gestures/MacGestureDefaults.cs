using MagicMouse.Core.Settings;

namespace MagicMouse.Core.Gestures;

public static class MacGestureDefaults
{
    public static string Action(string name) => name switch
    {
        "Deslizar 1 dedo a la izquierda" => "Atrás", "Deslizar 1 dedo a la derecha" => "Adelante",
        "Deslizar 2 dedos a la izquierda" => "Escritorio siguiente", "Deslizar 2 dedos a la derecha" => "Escritorio anterior",
        "Doble tap con 1 dedo" => "Zoom inteligente", "Doble tap con 2 dedos" => "Task View", _ => "Sin acción"
    };
    public static void Apply(UserSettings settings)
    {
        foreach (var name in new[] { "Deslizar 1 dedo a la izquierda", "Deslizar 1 dedo a la derecha", "Deslizar 2 dedos a la izquierda", "Deslizar 2 dedos a la derecha", "Doble tap con 1 dedo", "Doble tap con 2 dedos" }) settings.Choices["gesture:" + name] = Action(name);
        settings.Toggles["gesture.oneEnabled"] = settings.Toggles["gesture.twoEnabled"] = true;
        settings.Toggles["gesture.swapOne"] = settings.Toggles["gesture.swapTwo"] = false;
        settings.Toggles["scroll.enabled"] = settings.Toggles["scroll.natural"] = settings.Toggles["scroll.vertical"] = settings.Toggles["scroll.horizontal"] = true;
        settings.Toggles["scroll.invertHorizontal"] = settings.Toggles["scroll.invertVertical"] = false;
    }
}
