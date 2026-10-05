using System.ComponentModel;
using System.Runtime.InteropServices;
using MagicMouse.Core.Gestures;

namespace MagicMouse.Windows.App.Devices;

internal sealed class WindowsActionDispatcher
{
    private readonly HashSet<IntPtr> _zoomed = [];
    public void Dispatch(string action)
    {
        var window = GetForegroundWindow();
        if (action == "Sin acción") return;
        if (action == "Minimizar") { ShowWindow(window, 6); return; }
        if (action == "Maximizar/restaurar") { ShowWindow(window, IsZoomed(window) ? 9 : 3); return; }
        if (action is "Clic principal" or "Clic secundario" or "Clic central")
        { var flags = action switch { "Clic principal" => (2u,4u), "Clic secundario" => (8u,16u), _ => (32u,64u) }; Send([Mouse(flags.Item1), Mouse(flags.Item2)]); return; }
        if (action == "Zoom inteligente")
        {
            if (_zoomed.Contains(window)) { Chord(WindowsActionPlan.Keys("Restablecer zoom")); _zoomed.Remove(window); }
            else { for (var i = 0; i < 3; i++) Chord(WindowsActionPlan.Keys(action)); _zoomed.Add(window); }
            return;
        }
        Chord(WindowsActionPlan.Keys(action));
    }
    public void Scroll(double horizontal, double vertical)
    {
        var inputs = new List<Input>();
        if (vertical != 0) inputs.Add(Mouse(0x0800, unchecked((uint)(int)Math.Round(-vertical * 1200))));
        if (horizontal != 0) inputs.Add(Mouse(0x1000, unchecked((uint)(int)Math.Round(horizontal * 1200))));
        if (inputs.Count > 0) Send(inputs.ToArray());
    }
    private static void Chord(ushort[] keys)
    {
        var owned = keys.Where(k => (GetAsyncKeyState(k) & 0x8000) == 0).ToArray();
        var inputs = owned.Select(k => Key(k, false)).Concat(owned.Reverse().Select(k => Key(k, true))).ToArray();
        if (inputs.Length > 0) Send(inputs);
    }
    private static Input Key(ushort key, bool up) => new() { Type = 1, Data = new() { Keyboard = new() { Key = key, Flags = (up ? 2u : 0) | (key is 0x25 or 0x27 or 0x5B or >= 0xA6 and <= 0xB3 ? 1u : 0) } } };
    private static Input Mouse(uint flags, uint data = 0) => new() { Type = 0, Data = new() { Mouse = new() { Flags = flags, MouseData = data } } };
    private static void Send(Input[] inputs)
    { if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows bloqueó la acción. Las ventanas elevadas o el escritorio seguro pueden impedir la entrada."); }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsZoomed(IntPtr window);
}
