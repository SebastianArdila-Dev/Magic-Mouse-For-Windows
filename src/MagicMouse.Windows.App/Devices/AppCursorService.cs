using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MagicMouse.Core.Appearance;
using Microsoft.UI.Input;

namespace MagicMouse.Windows.App.Devices;

internal static class AppCursorService
{
    private static readonly Dictionary<string, InputCursor> Cache = [];
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateFromHCursor(nint factory,nint cursor,out nint result);
    public static InputCursor Create(string style,double size,uint accent)
    {
        if(style=="Automático" && Math.Abs(size-100)<.1) return InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        var bytes=CursorFile.Create(style,size,accent);
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        if(Cache.TryGetValue(hash,out var cached)) return cached;
        var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MagicMouseForWindows","Cursors");
        Directory.CreateDirectory(folder); var path=Path.Combine(folder,$"app-{hash}.cur");
        if(!File.Exists(path)) File.WriteAllBytes(path,bytes);
        var native=SystemCursorService.LoadFileExactSize(path);
        if(native==0) throw new Win32Exception(Marshal.GetLastWin32Error(),"No se pudo cargar el puntero de la app.");
        nint className=0,factory=0,result=0;
        try
        {
            const string cursorClass="Microsoft.UI.Input.InputCursor";
            Marshal.ThrowExceptionForHR(WindowsCreateString(cursorClass,cursorClass.Length,out className));
            // IInputCursorStaticsInterop from the Windows App SDK's InputCursor.Interop.h.
            var iid=new Guid("ac6f5065-90c4-46ce-beb7-05e138e54117");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(className,ref iid,out factory));
            var vtable=Marshal.ReadIntPtr(factory);
            var create=Marshal.GetDelegateForFunctionPointer<CreateFromHCursor>(Marshal.ReadIntPtr(vtable,6*IntPtr.Size));
            Marshal.ThrowExceptionForHR(create(factory,native,out result));
            // WinUI copies the full HCURSOR, retaining its pixel size and hotspot.
            var cursor=WinRT.MarshalInspectable<InputCursor>.FromAbi(result);
            Cache[hash]=cursor; return cursor;
        }
        finally
        {
            if(result!=0) Marshal.Release(result);
            if(factory!=0) Marshal.Release(factory);
            if(className!=0) WindowsDeleteString(className);
            DestroyCursor(native);
        }
    }

    [DllImport("user32.dll")] private static extern bool DestroyCursor(nint cursor);
    [DllImport("combase.dll",CharSet=CharSet.Unicode)] private static extern int WindowsCreateString(string value,int length,out nint result);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name,ref Guid iid,out nint factory);
}
