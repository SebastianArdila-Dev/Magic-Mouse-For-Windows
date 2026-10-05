namespace MagicMouse.Core.Gestures;

/// <summary>Platform-independent virtual-key plans for Windows equivalents.</summary>
public static class WindowsActionPlan
{
    public static ushort[] Keys(string action) => action switch
    {
        "Sin acción" => [], "Atrás" => [0xA6], "Adelante" => [0xA7],
        "Escritorio anterior" => [0x5B, 0x11, 0x25], "Escritorio siguiente" => [0x5B, 0x11, 0x27],
        "Task View" => [0x5B, 0x09], "Mostrar escritorio" => [0x5B, 0x44], "Alt + Tab" => [0x12, 0x09],
        "Cerrar ventana" => [0x12, 0x73], "Búsqueda" => [0x5B, 0x53],
        "Play/Pause" => [0xB3], "Siguiente pista" => [0xB0], "Pista anterior" => [0xB1],
        "Subir volumen" => [0xAF], "Bajar volumen" => [0xAE], "Silenciar" => [0xAD],
        "Zoom inteligente" => [0x11, 0xBB], "Restablecer zoom" => [0x11, 0x30],
        "Acercar pantalla" => [0x5B, 0xBB], "Alejar pantalla" => [0x5B, 0xBD],
        "Minimizar" or "Maximizar/restaurar" or "Clic principal" or "Clic secundario" or "Clic central" => [],
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Acción desconocida")
    };
}
