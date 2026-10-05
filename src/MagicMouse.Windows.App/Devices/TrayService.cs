using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace MagicMouse.Windows.App.Devices;

internal sealed class TrayService : IDisposable
{
    private readonly Window _window;
    private readonly nint _handle;
    private readonly SubclassProc _callback;
    private NotifyIconData _icon;
    private bool _disposed;
    public event Action? ExitRequested;

    public TrayService(Window window)
    {
        _window = window; _handle = WindowNative.GetWindowHandle(window); _callback = WindowProcedure;
        _icon = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = _handle, Id = 1,
            Flags = 1 | 2 | 4, CallbackMessage = 0x8000 + 42,
            Icon = LoadIcon(0, new nint(32512)), Tip = "Magic Mouse for Windows"
        };
        if (!SetWindowSubclass(_handle, _callback, 2, 0)) throw new InvalidOperationException("No se pudo preparar la bandeja del sistema.");
        if (!ShellNotifyIcon(0, ref _icon)) { RemoveWindowSubclass(_handle, _callback, 2); throw new InvalidOperationException("Windows no permitió crear el icono de bandeja."); }
    }
    internal void Show()
    {
        _window.AppWindow.Show();
        if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) presenter.Restore();
        _window.Activate();
    }
    private nint WindowProcedure(nint handle, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == _icon.CallbackMessage)
        {
            if (lParam == 0x0202 || lParam == 0x0203) Show();
            if (lParam == 0x0205)
            {
                var menu = CreatePopupMenu();
                try
                {
                    AppendMenu(menu, 0, 1, "Abrir Magic Mouse"); AppendMenu(menu, 0, 2, "Salir");
                    GetCursorPos(out var position); SetForegroundWindow(handle);
                    var selected = TrackPopupMenu(menu, 0x0100 | 0x0002, position.X, position.Y, 0, handle, 0);
                    if (selected == 1) Show();
                    if (selected == 2) ExitRequested?.Invoke();
                }
                finally { DestroyMenu(menu); }
            }
            return 0;
        }
        return DefSubclassProc(handle, message, wParam, lParam);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        ShellNotifyIcon(2, ref _icon); RemoveWindowSubclass(_handle, _callback, 2);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public nint Window; public uint Id, Flags, CallbackMessage; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate nint SubclassProc(nint handle, uint message, nuint wParam, nint lParam, nuint id, nuint reference);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)] private static extern bool ShellNotifyIcon(uint operation, ref NotifyIconData icon);
    [DllImport("user32.dll", EntryPoint = "LoadIconW")] private static extern nint LoadIcon(nint instance, nint name);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint handle, nint rectangle);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint handle);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint handle, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint handle, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint handle, uint message, nuint wParam, nint lParam);
}
