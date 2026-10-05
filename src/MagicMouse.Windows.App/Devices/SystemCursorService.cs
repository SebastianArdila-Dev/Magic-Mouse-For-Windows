using System.ComponentModel;
using System.Runtime.InteropServices;
using MagicMouse.Core.Appearance;

namespace MagicMouse.Windows.App.Devices;

internal static class SystemCursorService
{
    public static void Apply(string style, double percent, uint accent)
    {
        if (style == "Automático" && Math.Abs(percent - 100) < .1) { Restore(); return; }
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagicMouseForWindows", "Cursors");
        Directory.CreateDirectory(folder); var file = Path.Combine(folder, "pointer.cur");
        File.WriteAllBytes(file, CursorFile.Create(style, percent, accent));
        var cursor = LoadFileExactSize(file);
        if (cursor == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo cargar el puntero.");
        if (!SetSystemCursor(cursor, 32512)) { DestroyCursor(cursor); throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no pudo aplicar el puntero."); }
    }
    public static void Restore()
    {
        if (!SystemParametersInfo(0x0057, 0, IntPtr.Zero, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo restaurar el esquema de punteros de Windows.");
    }
    public static bool ValidateFile(string path)
    {
        var cursor = LoadFileExactSize(path); if (cursor == IntPtr.Zero) return false; DestroyCursor(cursor); return true;
    }
    public static nint LoadFileExactSize(string path)
    {
        var header=File.ReadAllBytes(path);
        if(header.Length<22) throw new InvalidDataException("Archivo de puntero incompleto.");
        var width=header[6]==0 ? 256 : header[6]; var height=header[7]==0 ? 256 : header[7];
        return LoadImage(0,path,2,width,height,0x0010);
    }
    [DllImport("user32.dll", EntryPoint="LoadImageW",CharSet=CharSet.Unicode,SetLastError=true)] private static extern nint LoadImage(nint instance,string name,uint type,int width,int height,uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetSystemCursor(IntPtr cursor, uint id);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
}
