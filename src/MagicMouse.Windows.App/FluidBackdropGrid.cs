using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
namespace MagicMouse.Windows.App;
public sealed class FluidBackdropGrid : Grid
{
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(0,0);
    }
}
