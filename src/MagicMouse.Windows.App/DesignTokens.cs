namespace MagicMouse.Windows.App;

internal static class DesignTokens
{
    public static bool IsDark { get; set; }
    public static string ThemeColor(string color) => !IsDark ? color : color.ToUpperInvariant() switch
    {
        "#202024" or "#303036" or "#172235" or "#26354C" or "#27354A" or "#101827" => "#F1F1F4",
        "#718097" or "#748198" or "#8793A5" or "#69778D" or "#64728A" or "#65728A" or "#536884" or "#3F6EAA" => "#B3BCD0",
        "#FCFCFD" or "#FBFBFC" or "#55FFFFFF" => "#29292E",
        "#E3E3E8" or "#DDE3ED" or "#EEEEF1" or "#DCDDE4" => "#42424A",
        "#E8EDF5" => "#202026", "#E8F2FF" or "#EAF3FF" => "#18345B", "#B9D9FF" => "#365D91",
        _ => color
    };
    public const string Accent = "#007AFF";
    public const string PrimaryText = "#202024";
    public const string BodyText = "#303036";
    public const string SecondaryText = "#718097";
    public const string MutedText = "#748198";
    public const string CardFill = "#FCFCFD";
    public const string CardBorder = "#E3E3E8";
    public const string PreviewSurface = "#E8EDF5";
    public const double CardRadius = 12;
    public const double ControlRadius = 7;
    public static readonly TimeSpan MotionNormal = TimeSpan.FromMilliseconds(520);
}
