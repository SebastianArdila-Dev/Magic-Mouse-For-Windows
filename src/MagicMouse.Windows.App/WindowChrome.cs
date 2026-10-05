using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace MagicMouse.Windows.App;

/// <summary>Borderless native window with custom caption buttons and DPI-aware resizing.</summary>
internal sealed class WindowChrome : IDisposable
{
    private readonly nint _handle;
    private readonly SubclassProc _callback;
    private readonly SubclassProc _cursorCallback;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint,nint> _cursorChildren=new();
    private readonly NativeWindowProc _nativeCursorCallback;
    private readonly AppWindow _window;
    private readonly OverlappedPresenter _presenter;

    public WindowChrome(Window window)
    {
        _handle = WindowNative.GetWindowHandle(window);
        _window = window.AppWindow;
        _presenter = (OverlappedPresenter)_window.Presenter;
        _presenter.SetBorderAndTitleBar(false, false);
        _callback = WindowProcedure; _cursorCallback=CursorChildProcedure; _nativeCursorCallback=NativeCursorProcedure;
        if (!SetWindowSubclass(_handle, _callback, 1, 0)) throw new InvalidOperationException("Could not initialize window chrome.");
        // Remove the residual non-client resize frame: it otherwise leaves an 8px strip on Windows 10.
        var policy = 2; DwmSetWindowAttribute(_handle, 2, ref policy, sizeof(int));
        SetWindowPos(_handle, 0, 0, 0, 0, 0, 0x0037);
        var area = DisplayArea.GetFromWindowId(_window.Id, DisplayAreaFallback.Primary).WorkArea;
        var scale = GetDpiForWindow(_handle) / 96.0;
        var width = Math.Min((int)(1140 * scale), area.Width - 40);
        var height = Math.Min((int)(760 * scale), area.Height - 40);
        _window.MoveAndResize(new global::Windows.Graphics.RectInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
        ApplyCorners();
    }

    private global::Windows.Graphics.RectInt32? _regularBounds;
    private bool _regularMaximized;
    public void ShowSetupWindow(int step)
    {
        if(_regularBounds is null)
        {
            _regularMaximized=IsMaximized;
            if(_regularMaximized) _presenter.Restore();
            var point=_window.Position;var size=_window.Size;
            _regularBounds=new(point.X,point.Y,size.Width,size.Height);
        }
        var area=DisplayArea.GetFromWindowId(_window.Id,DisplayAreaFallback.Primary).WorkArea;
        var scale=GetDpiForWindow(_handle)/96.0;
        var width=Math.Min((int)((step==0 ? 640:600)*scale),area.Width-40);
        var height=Math.Min((int)((step==0 ? 500:step==1 ? 560:660)*scale),area.Height-40);
        _window.MoveAndResize(new(area.X+(area.Width-width)/2,area.Y+(area.Height-height)/2,width,height));
        SetSetupCaption(width/scale,scale);ApplyCorners();
    }
    public void SetSetupCaption(double width,double scale)=>SetCaption(76,0,Math.Max(0,width-94),52,scale);
    public void RestoreRegularWindow()
    {
        if(_regularBounds is not { } bounds) return;
        _regularBounds=null;_window.MoveAndResize(bounds);
        if(_regularMaximized) _presenter.Maximize();
        ApplyCorners();
    }
    public void Minimize() => _presenter.Minimize();
    public void ToggleMaximize()
    {
        if (_presenter.State == OverlappedPresenterState.Maximized) _presenter.Restore();
        else _presenter.Maximize();
        ApplyCorners();
    }
    public bool IsMaximized => _presenter.State == OverlappedPresenterState.Maximized;
    public bool ClientFillsWindow
    {
        get { GetWindowRect(_handle,out var outer); GetClientRect(_handle,out var inner); return outer.Right-outer.Left == inner.Right && outer.Bottom-outer.Top == inner.Bottom; }
    }
    private Rect _caption;
    public bool IsCaptionPoint(double x,double y,double scale)
    {
        var px=(int)(x*scale);var py=(int)(y*scale);
        return (px>=_caption.Left && px<_caption.Right && py>=_caption.Top && py<_caption.Bottom) ||
            (_regularBounds is null && px>=(int)(80*scale) && px<(int)(224*scale) && py>=0 && py<(int)(52*scale));
    }
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _dragTimer;
    private NativePoint _dragOrigin;
    private global::Windows.Graphics.PointInt32 _dragWindowOrigin;
    public void BeginNativeMove()
    {
        if((GetAsyncKeyState(1)&0x8000)==0) return;
        if(IsMaximized) _presenter.Restore();
        GetCursorPosNative(out _dragOrigin);_dragWindowOrigin=_window.Position;
        if(_dragTimer is null)
        {
            _dragTimer=Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
            _dragTimer.Interval=TimeSpan.FromMilliseconds(8);
            _dragTimer.Tick+=(_,_)=>
            {
                if((GetAsyncKeyState(1)&0x8000)==0) {_dragTimer.Stop();return;}
                if(!GetCursorPosNative(out var point)) return;
                var position=new global::Windows.Graphics.PointInt32(_dragWindowOrigin.X+point.X-_dragOrigin.X,_dragWindowOrigin.Y+point.Y-_dragOrigin.Y);
                if(_window.Position!=position) _window.Move(position);
            };
        }
        _dragTimer.Start();
    }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private (int Width, int Height, uint Dpi, bool Maximized)? _cornerState;
    public int CornerUpdates { get; private set; }
    public void SetCaption(double x, double y, double width, double height, double scale) => _caption = new Rect { Left=(int)(x*scale), Top=(int)(y*scale), Right=(int)((x+width)*scale), Bottom=(int)((y+height)*scale) };
    public bool CaptionAcceptsPointerInput
    {
        get
        {
            GetWindowRect(_handle, out var rect);
            var x=rect.Left+(_caption.Left+_caption.Right)/2; var y=rect.Top+(_caption.Top+_caption.Bottom)/2;
            var point = new nint((y << 16) | (x & 0xffff));
              return _caption.Right > _caption.Left && SendMessage(_handle,0x0084,0,point)==1;
        }
    }

    private nint _appCursor;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _cursorTimer;
    public void SetAppCursor(string style,double size,uint accent)
    {
        nint cursor=0;
        if(style != "Automático" || Math.Abs(size-100)>.1)
        {
            var file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MagicMouseForWindows","Cursors","app-native.cur");
            File.WriteAllBytes(file,MagicMouse.Core.Appearance.CursorFile.Create(style,size,accent));
            cursor=Devices.SystemCursorService.LoadFileExactSize(file);
            if(cursor==0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        var old=_appCursor; _appCursor=cursor; if(old!=0) DestroyCursor(old);
        AttachCursorChildren();
        _cursorTimer ??= Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _cursorTimer.Interval=TimeSpan.FromMilliseconds(16);
        _cursorTimer.Tick-=OnCursorTick; _cursorTimer.Tick+=OnCursorTick;
        if(_appCursor!=0) _cursorTimer.Start(); else _cursorTimer.Stop();
    }
    private void OnCursorTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender,object args)
    {
        // WinUI's input island can update the cursor after the XAML UI thread.
        // Keep a single native cursor; never paint a second pointer on top.
        if(_appCursor!=0 && !IsIconic(_handle) && PointerIsInsideApp) SetCursor(_appCursor);
    }
    private void AttachCursorChildren()
    {
        EnumChildWindows(_handle,(child,_)=>
        {
            if(!_cursorChildren.ContainsKey(child))
            {
                var previous=SetWindowLongPtr(child,-4,Marshal.GetFunctionPointerForDelegate(_nativeCursorCallback));
                if(previous!=0) _cursorChildren.TryAdd(child,previous);
            }
            return true;
        },0);
    }
    private nint CursorChildProcedure(nint handle,uint message,nuint wParam,nint lParam,nuint id,nuint data) => DefSubclassProc(handle,message,wParam,lParam);
    private nint NativeCursorProcedure(nint handle,uint message,nuint wParam,nint lParam)
    {
        if(!_cursorChildren.TryGetValue(handle,out var previous)) return DefWindowProc(handle,message,wParam,lParam);
        if(message==0x0020 && _appCursor!=0) { SetCursor(_appCursor); return 1; }
        var result=CallWindowProc(previous,handle,message,wParam,lParam);
        if(message==0x0082) _cursorChildren.TryRemove(handle,out _);
        else if(message is 0x0200 or 0x0245 && _appCursor!=0) SetCursor(_appCursor);
        return result;
    }
    private delegate nint NativeWindowProc(nint handle,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] private static extern nint SetWindowLongPtr(nint handle,int index,nint value);
    [DllImport("user32.dll",EntryPoint="CallWindowProcW")] private static extern nint CallWindowProc(nint previous,nint handle,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll",EntryPoint="DefWindowProcW")] private static extern nint DefWindowProc(nint handle,uint message,nuint wParam,nint lParam);
    public nint AppCursorHandle=>_appCursor;
    private delegate bool EnumChildProc(nint handle,nint data);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent,EnumChildProc callback,nint data);
    public bool PointerIsInsideApp
    {
        get { GetCursorPosNative(out var point); var hit=WindowFromPointNative(point); return hit==_handle || GetAncestorNative(hit,3)==_handle; }
    }
    public void RefreshVisibleCursor()
    {
        AttachCursorChildren();
        if(_appCursor!=0 && PointerIsInsideApp) SetCursor(_appCursor);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [DllImport("user32.dll",EntryPoint="GetCursorPos")] private static extern bool GetCursorPosNative(out NativePoint point);
    [DllImport("user32.dll",EntryPoint="WindowFromPoint")] private static extern nint WindowFromPointNative(NativePoint point);
    [DllImport("user32.dll",EntryPoint="GetAncestor")] private static extern nint GetAncestorNative(nint handle,uint flags);
    private void ApplyCorners()
    {
        if (IsIconic(_handle)) return;
        GetWindowRect(_handle, out var rect);
        var state = (rect.Right-rect.Left, rect.Bottom-rect.Top, GetDpiForWindow(_handle), IsZoomed(_handle));
        if (_cornerState == state) return;
        _cornerState = state; CornerUpdates++;
        if (state.Item4) { SetWindowRgn(_handle, 0, true); return; }
        var radius = (int)Math.Round(22 * GetDpiForWindow(_handle) / 96.0);
        var region = CreateRoundRectRgn(0, 0, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1, radius * 2, radius * 2);
        if (SetWindowRgn(_handle, region, true) == 0) DeleteObject(region);
    }

    private nint WindowProcedure(nint handle, uint message, nuint wParam, nint lParam, nuint id, nuint reference)
    {
        if (message == 0x0020 && _appCursor!=0 && (short)(lParam.ToInt64() & 0xffff) is 1 or 2) { SetCursor(_appCursor); return 1; }
        if (message == 0x0083) // WM_NCCALCSIZE: no residual Windows frame above the custom caption.
        {
            if (wParam != 0 && IsZoomed(handle))
            {
                var area = DisplayArea.GetFromWindowId(_window.Id, DisplayAreaFallback.Primary).WorkArea;
                Marshal.StructureToPtr(new Rect { Left=area.X, Top=area.Y, Right=area.X+area.Width, Bottom=area.Y+area.Height },lParam,false);
            }
            return 0;
        }
        if (message is 0x0005 or 0x02E0) ApplyCorners(); // size / DPI changed
        if (message == 0x0084) // WM_NCHITTEST
        {
            GetWindowRect(handle, out var rect);
            var x = (short)(lParam.ToInt64() & 0xffff);
            var y = (short)((lParam.ToInt64() >> 16) & 0xffff);
            var clientX = x-rect.Left; var clientY = y-rect.Top;
            if (clientX >= _caption.Left && clientX < _caption.Right && clientY >= _caption.Top && clientY < _caption.Bottom) return 1; // XAML starts live movement on pointer-down; never queue a native move loop.
            if (IsZoomed(handle)) return DefSubclassProc(handle, message, wParam, lParam);
            var edge = (int)(7 * GetDpiForWindow(handle) / 96.0);
            var left = x < rect.Left + edge; var right = x >= rect.Right - edge;
            var top = y < rect.Top + edge; var bottom = y >= rect.Bottom - edge;
            if (top) return left ? 13 : right ? 14 : 12;
            if (bottom) return left ? 16 : right ? 17 : 15;
            if (left) return 10;
            if (right) return 11;
        }
        return DefSubclassProc(handle, message, wParam, lParam);
    }

    public void Dispose() { _dragTimer?.Stop(); _cursorTimer?.Stop(); foreach(var child in _cursorChildren.ToArray()) SetWindowLongPtr(child.Key,-4,child.Value); _cursorChildren.Clear(); RemoveWindowSubclass(_handle,_callback,1); if(_appCursor!=0) { DestroyCursor(_appCursor); _appCursor=0; } }

    [DllImport("user32.dll")] private static extern nint SetCursor(nint cursor);
    [DllImport("user32.dll")] private static extern bool DestroyCursor(nint cursor);
    private delegate nint SubclassProc(nint handle, uint message, nuint wParam, nint lParam, nuint id, nuint reference);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint handle, SubclassProc callback, nuint id, nuint reference);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint handle, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint handle, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint handle,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint handle,int attribute,ref int value,int length);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint handle, uint message, nuint wParam, nint lParam);
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint handle, nint region, bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
}
