using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private async Task VerifyLiveDragAsync()
    {
        var results=new List<string>();
        AppWindow.Show();Activate();await Task.Delay(300);RootGrid.UpdateLayout();
        var original=AppWindow.Position;
        GetCursorPosTest(out var saved);
        try
        {
            var origin=WindowDragArea.TransformToVisual(RootGrid).TransformPoint(new global::Windows.Foundation.Point());
            var scale=RootGrid.XamlRoot.RasterizationScale;
            var x=original.X+(int)((origin.X+WindowDragArea.ActualWidth/2)*scale);
            var y=original.Y+(int)((origin.Y+WindowDragArea.ActualHeight/2)*scale);
            SetCursorPosTest(x,y);await Task.Delay(100);
            MouseButtonTest(2);await Task.Delay(100);
            SetCursorPosTest(x+90,y+60);await Task.Delay(180);
            var moved=AppWindow.Position;
            if(Math.Abs(moved.X-original.X-90)>4 || Math.Abs(moved.Y-original.Y-60)>4)
                throw new InvalidOperationException($"Window did not move while held: {moved.X-original.X},{moved.Y-original.Y}");
            results.Add("PASS: window moves 90,60 pixels while the left button is still held, before release");
            MouseButtonTest(4);await Task.Delay(80);
            SetCursorPosTest(x+130,y+90);await Task.Delay(80);
            if(AppWindow.Position!=moved) throw new InvalidOperationException("Window continued moving after release");
            results.Add("PASS: release immediately stops window movement");
        }
        catch(Exception exception) {results.Add("FAIL: "+exception.Message);}
        finally {MouseButtonTest(4);AppWindow.Move(original);SetCursorPosTest(saved.X,saved.Y);}
        await File.WriteAllLinesAsync(Path.Combine(AppContext.BaseDirectory,"drag-test.txt"),results);
    }
    private static void MouseButtonTest(uint flags)
    {
        var input=new MouseInputTest {Type=0,Data=new MouseUnionTest {Mouse=new MouseDataTest {Flags=flags}}};
        if(SendInputTest(1,[input],Marshal.SizeOf<MouseInputTest>())!=1) throw new InvalidOperationException("Could not inject test mouse input");
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInputTest {public uint Type;public MouseUnionTest Data;}
    [StructLayout(LayoutKind.Explicit,Size=32)] private struct MouseUnionTest {[FieldOffset(0)] public MouseDataTest Mouse;}
    [StructLayout(LayoutKind.Sequential)] private struct MouseDataTest {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",EntryPoint="SendInput",SetLastError=true)] private static extern uint SendInputTest(uint count,MouseInputTest[] input,int size);
}
