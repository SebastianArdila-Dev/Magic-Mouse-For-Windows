using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace MagicMouse.Windows.App;

internal static class IconFactory
{
    private static Path Line(string data, Brush brush, double thickness = 1.5) => (Path)XamlReader.Load($"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{data}' Stroke='#303036' StrokeThickness='{thickness.ToString(System.Globalization.CultureInfo.InvariantCulture)}' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round'/>") is Path path ? WithBrush(path, brush) : throw new InvalidOperationException();
    private static Path WithBrush(Path path, Brush brush) { path.Stroke = brush; return path; }

    public static FrameworkElement Navigation(string name, Brush brush)
    {
        var grid = new Grid { Width = 24, Height = 24 };
        var data = name switch
        {
            "Gestos" => "M6,18 L4,12 Q3,9 5,9 L8,12 L8,5 Q8,3 10,3 Q12,3 12,5 L12,11 L15,9 Q18,8 19,11 L20,15 Q21,20 16,21 L10,21 Z M15,4 L19,4 M17,2 L19,4 L17,6",
            "Puntero y desplazamiento" => "M5,2 L5,19 L10,14 L14,22 L17,20 L13,13 L20,13 Z",
            "Botones" => "M7,3 Q12,0 17,3 Q20,5 20,10 L20,16 Q20,23 12,23 Q4,23 4,16 L4,10 Q4,5 7,3 Z M12,2 L12,11 M4,11 L20,11",
            "Aplicaciones" => "M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z",
            "Aspecto" => "M3,4 L21,4 L21,17 L3,17 Z M8,21 L16,21 M12,17 L12,21",
            _ => "M10,2 L14,2 L15,5 L18,6 L21,6 L23,10 L20,12 L20,15 L22,18 L19,21 L16,19 L13,20 L11,23 L7,21 L7,18 L4,16 L1,15 L2,10 L5,9 L7,6 L7,3 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12"
        };
        grid.Children.Add(Line(data, brush));
        return new Viewbox { Width = 18, Height = 18, Child = grid };
    }

    public static void Recolor(FrameworkElement icon, Brush brush)
    {
        if (icon is Viewbox { Child: Panel panel }) foreach (var path in panel.Children.OfType<Path>()) path.Stroke = brush;
    }

    public static FrameworkElement Pointer(string style, double size = 32, uint accent = 0x007AFF)
    {
        var path = (Path)XamlReader.Load("<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='M4,2 L4,29 L11,22 L17,35 L23,32 L17,20 L29,20 Z' Fill='#141416' Stroke='White' StrokeThickness='1.5' StrokeLineJoin='Round'/>");
        if (style == "Minimal") { path.Fill = new SolidColorBrush(Microsoft.UI.Colors.White); path.Stroke = new SolidColorBrush(Microsoft.UI.Colors.Black); }
        if (style == "Personalizado") path.Fill = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255,(byte)(accent>>16),(byte)(accent>>8),(byte)accent));
        var grid = new Grid { Width = 34, Height = 38 }; grid.Children.Add(path);
        return new Viewbox { Width = style == "Grande" ? size * 1.25 : size, Height = style == "Grande" ? size * 1.4 : size * 1.12, Child = grid };
    }

    public static FrameworkElement ClickEffect(string style)
    {
        var grid = new Grid { Width = 40, Height = 40 };
        var blue = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 0, 122, 255));
        if (style == "Ninguno") return grid;
        if (style is "Ráfaga" or "Ondas")
        {
            foreach (var size in style == "Ondas" ? new[] { 36, 26, 16 } : new[] { 32, 22 })
                grid.Children.Add(new Ellipse { Width = size, Height = size, Stroke = blue, StrokeThickness = 1, Opacity = .6 });
        }
        if (style == "Destello") grid.Children.Add(Line("M20,2 L20,12 M20,28 L20,38 M2,20 L12,20 M28,20 L38,20 M7,7 L13,13 M27,27 L33,33 M7,33 L13,27 M27,13 L33,7", blue));
        grid.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = blue });
        return grid;
    }
}
