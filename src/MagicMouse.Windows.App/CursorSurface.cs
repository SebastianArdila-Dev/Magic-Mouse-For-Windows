using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MagicMouse.Windows.App;

public sealed class CursorSurface : Grid
{
    private static readonly PropertyInfo CursorProperty = typeof(UIElement).GetProperty("ProtectedCursor",BindingFlags.Instance|BindingFlags.NonPublic)!;
    private ConditionalWeakTable<UIElement, AppliedCursor> _applied = new();
    private sealed record AppliedCursor(InputCursor Cursor);
    public Action? CursorRefreshRequested { get; set; }
    private bool _refreshPending;
    private readonly ConditionalWeakTable<UIElement,object> _popupHandlers = new();
    public InputCursor? AppCursor { get; private set; }
    public int AppliedElementCount { get; private set; }
    public CursorSurface()
    {
        Loaded += (_,_)=>RefreshCursorTree();
        AddHandler(PointerMovedEvent,new Microsoft.UI.Xaml.Input.PointerEventHandler((_,args)=>
        {
            if(AppCursor is null) return;
            var element=args.OriginalSource as UIElement;
            while(element is not null)
            {
                if(!UsesSelectedCursor(element)) CursorProperty.SetValue(element,AppCursor);
                element=VisualTreeHelper.GetParent(element) as UIElement;
            }
            ScheduleNativeRefresh();
        }),true);
    }
    private void ScheduleNativeRefresh()
    {
        if(_refreshPending) return; _refreshPending=true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High,()=>{ _refreshPending=false; CursorRefreshRequested?.Invoke(); });
    }
    public void ChangeCursor(InputCursor cursor)
    {
        AppCursor=cursor; ProtectedCursor=cursor; _applied=new(); AppliedElementCount=0; RefreshCursorTree();
    }
    public void RefreshCursorTree()
    {
        if(AppCursor is null) return;
        Apply(this);
        if(XamlRoot is not null) foreach(var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot)) if(popup.Child is UIElement child)
        {
            Apply(child);
            if(!_popupHandlers.TryGetValue(child,out _))
            { child.AddHandler(PointerMovedEvent,new Microsoft.UI.Xaml.Input.PointerEventHandler((_,_)=>ScheduleNativeRefresh()),true); _popupHandlers.Add(child,new object()); }
        }
    }
    private void Apply(UIElement element)
    {
        if (!_applied.TryGetValue(element,out _) || !UsesSelectedCursor(element))
        {
            CursorProperty.SetValue(element,AppCursor);
            _applied.Remove(element); _applied.Add(element,new(AppCursor!)); AppliedElementCount++;
        }
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(element);index++)
            if(VisualTreeHelper.GetChild(element,index) is UIElement child) Apply(child);
    }
    public bool UsesSelectedCursor(UIElement element) => ReferenceEquals(CursorProperty.GetValue(element),AppCursor);
}
