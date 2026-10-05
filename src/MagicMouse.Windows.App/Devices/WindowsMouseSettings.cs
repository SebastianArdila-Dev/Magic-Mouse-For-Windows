using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MagicMouse.Windows.App.Devices;

internal static class WindowsMouseSettings
{
    public static bool ControlPressed => (GetAsyncKeyState(0x11) & 0x8000) != 0;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    public static int PointerSpeed
    {
        get
        {
            if (!SystemParametersInfoGet(0x0070, 0, out var speed, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return speed;
        }
    }
    public static bool ButtonsSwapped => GetSystemMetrics(23) != 0;
    public static void SetPointerSpeed(int speed)
    {
        if (speed is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!SystemParametersInfoSet(0x0071, 0, speed, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static void SetButtonsSwapped(bool swapped) => SwapMouseButton(swapped);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] private static extern bool SystemParametersInfoGet(uint action, uint parameter, out int value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] private static extern bool SystemParametersInfoSet(uint action, uint parameter, nint value, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool SwapMouseButton(bool swapped);
}
