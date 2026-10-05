using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private async Task VerifyVisibleAppCursorAsync(UIElement target,int expectedWidth)
    {
        if(!GetCursorPosTest(out var saved)) throw new InvalidOperationException("Cannot read pointer position");
        var handle=WinRT.Interop.WindowNative.GetWindowHandle(this); GetWindowRectTest(handle,out var window);
        var element=(FrameworkElement)target;
        var origin=element.TransformToVisual(RootGrid).TransformPoint(new global::Windows.Foundation.Point());
        var scale=RootGrid.XamlRoot.RasterizationScale;
        try
        {
            SetCursorPosTest(window.Left+(int)((origin.X+element.ActualWidth/2)*scale),window.Top+(int)((origin.Y+element.ActualHeight/2)*scale));
            await Task.Delay(150);
            if(_chrome?.PointerIsInsideApp!=true) throw new InvalidOperationException("Visible cursor test point is covered by another window");
            var cursor=new CursorInfoTest { Size=(uint)Marshal.SizeOf<CursorInfoTest>() };
            if(!GetCursorInfoTest(ref cursor) || cursor.Handle==0 || !GetIconInfoTest(cursor.Handle,out var image)) throw new InvalidOperationException("No visible app cursor");
            try
            {
                if(GetObjectTest(image.Color,Marshal.SizeOf<BitmapTest>(),out var bitmap)==0 || bitmap.Width!=expectedWidth)
                    {
                    GetIconInfoTest(_chrome!.AppCursorHandle,out var nativeImage);
                    GetObjectTest(nativeImage.Color,Marshal.SizeOf<BitmapTest>(),out var nativeBitmap);
                    DeleteObjectTest(nativeImage.Color); DeleteObjectTest(nativeImage.Mask);
                    throw new InvalidOperationException($"Actual cursor width {bitmap.Width}, native asset {nativeBitmap.Width}, expected {expectedWidth} over {element.GetType().Name}");
                }
            }
            finally { if(image.Color!=0) DeleteObjectTest(image.Color); if(image.Mask!=0) DeleteObjectTest(image.Mask); }
        }
        finally { SetCursorPosTest(saved.X,saved.Y); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PointTest { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectTest { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfoTest { public uint Size,Flags; public nint Handle; public PointTest Position; }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfoTest { public int Icon; public uint X,Y; public nint Mask,Color; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapTest { public int Type,Width,Height,WidthBytes; public ushort Planes,BitsPixel; public nint Bits; }
    [DllImport("user32.dll",EntryPoint="GetCursorPos")] private static extern bool GetCursorPosTest(out PointTest point);
    [DllImport("user32.dll",EntryPoint="SetCursorPos")] private static extern bool SetCursorPosTest(int x,int y);
    [DllImport("user32.dll",EntryPoint="GetWindowRect")] private static extern bool GetWindowRectTest(nint handle,out RectTest rect);
    [DllImport("user32.dll",EntryPoint="GetCursorInfo")] private static extern bool GetCursorInfoTest(ref CursorInfoTest info);
    [DllImport("user32.dll",EntryPoint="GetIconInfo")] private static extern bool GetIconInfoTest(nint handle,out IconInfoTest info);
    [DllImport("gdi32.dll",EntryPoint="GetObjectW")] private static extern int GetObjectTest(nint handle,int size,out BitmapTest bitmap);
    [DllImport("gdi32.dll",EntryPoint="DeleteObject")] private static extern bool DeleteObjectTest(nint handle);
}
