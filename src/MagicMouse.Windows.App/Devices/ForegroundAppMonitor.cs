using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace MagicMouse.Windows.App.Devices;

/// <summary>Observes foreground-window changes without polling and reports the owning executable path.</summary>
public sealed class ForegroundAppMonitor : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutOfContext = 0x0000;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private readonly WinEventProc _callback;
    private IntPtr _hook;

    public event Action<string?>? ForegroundExecutableChanged;

    public ForegroundAppMonitor() => _callback = OnWinEvent;

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, WineventOutOfContext);
        if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not monitor foreground applications.");
        OnWinEvent(_hook, EventSystemForeground, GetForegroundWindow(), 0, 0, 0, 0);
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (window == IntPtr.Zero) return;
        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return;
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero) { ForegroundExecutableChanged?.Invoke(null); return; }
        try
        {
            var path = new StringBuilder(32768);
            uint length = (uint)path.Capacity;
            var result = QueryFullProcessImageName(process, 0, path, ref length);
            ForegroundExecutableChanged?.Invoke(result ? path.ToString() : null);
        }
        finally { CloseHandle(process); }
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr eventHookModule, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder executableName, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
