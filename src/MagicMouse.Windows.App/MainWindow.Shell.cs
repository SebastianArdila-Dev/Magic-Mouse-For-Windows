using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private WindowChrome? _chrome;
    private readonly List<string> _navigationHistory = [];
    private int _historyIndex = -1;
    private TaskCompletionSource<bool>? _dialogCompletion;
    private Control? _dialogPreviousFocus;

    private void InitializeShell()
    {
        _chrome = new WindowChrome(this);
        RootGrid.CursorRefreshRequested=()=>_chrome.RefreshVisibleCursor();
        RootGrid.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,args)=>
        {
            if(_closed || RootGrid.XamlRoot is null || _chrome is null) return;
            var point=args.GetCurrentPoint(RootGrid);
            if(point.Properties.IsLeftButtonPressed && _chrome.IsCaptionPoint(point.Position.X,point.Position.Y,RootGrid.XamlRoot.RasterizationScale))
            {
                args.Handled=true;_chrome.BeginNativeMove();
            }
        }),true);
        BrandButton.Click += async (_,_) => await RunUiActionAsync("Acerca de Magic Mouse",ShowCreatorAsync);
        CloseWindowButton.Click += async (_, _) => await RunUiActionAsync("Cerrar",RequestCloseAsync);
        MinimizeWindowButton.Click += (_, _) => _chrome.Minimize();
        MaximizeWindowButton.Click += (_, _) => _chrome.ToggleMaximize();
        void UpdateCaption()
        {
            if (RootGrid.XamlRoot is null || _introduction is not null) return;
            var origin = WindowDragArea.TransformToVisual(RootGrid).TransformPoint(new global::Windows.Foundation.Point());
            _chrome.SetCaption(origin.X, origin.Y, WindowDragArea.ActualWidth, WindowDragArea.ActualHeight, RootGrid.XamlRoot.RasterizationScale);
        }
        WindowDragArea.SizeChanged += (_, _) => UpdateCaption();
        WindowDragArea.Loaded += (_, _) => UpdateCaption();
        BackNavigationButton.Click += (_, _) => NavigateHistory(-1);
        ForwardNavigationButton.Click += (_, _) => NavigateHistory(1);
        NavigationSearch.TextChanged += (_, _) =>
        {
            foreach (var button in NavigationPanel.Children.OfType<Button>())
                button.Visibility = button.Tag?.ToString()?.Contains(NavigationSearch.Text, StringComparison.CurrentCultureIgnoreCase) == true ? Visibility.Visible : Visibility.Collapsed;
        };
        PageInfoButton.Click += async (_, _) => await RunUiActionAsync("Información",async()=>await ShowSheetAsync(PageTitle.Text, PageInformation(PageTitle.Text), hero: true));
        ModalAcceptButton.Click += (_, _) => CompleteSheet(true);
        ModalCancelButton.Click += (_, _) => CompleteSheet(false);
        RootGrid.KeyDown += (_, e) =>
        {
            if (e.Key == global::Windows.System.VirtualKey.Escape && ModalScrim.Visibility == Visibility.Visible) { CompleteSheet(false); e.Handled = true; }
            if (e.Key == global::Windows.System.VirtualKey.Tab && ModalScrim.Visibility == Visibility.Visible)
            {
                if(_creatorOpen) { _creatorPrimaryButton?.Focus(FocusState.Keyboard); e.Handled=true;return; }
                var focused = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
                if (ModalCancelButton.Visibility == Visibility.Visible)
                    (ReferenceEquals(focused, ModalAcceptButton) ? ModalCancelButton : ModalAcceptButton).Focus(FocusState.Keyboard);
                else ModalAcceptButton.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };
        foreach (var button in new[] { CloseWindowButton, MinimizeWindowButton, MaximizeWindowButton })
        {
            var symbol=TrafficSymbol(button==CloseWindowButton ? 0:button==MinimizeWindowButton ? 1:2);symbol.Opacity=0;button.Content=symbol;
            button.PointerEntered += (_, _) => symbol.Opacity=.7;
            button.PointerExited += (_, _) => symbol.Opacity=0;
        }
    }

    private static FrameworkElement TrafficSymbol(int kind)
    {
        var data=kind switch {0=>"M3.5,3.5 L9.5,9.5 M3.5,9.5 L9.5,3.5",1=>"M3.5,6.5 L9.5,6.5",_=>"M3.5,7.5 L3.5,3.5 L7.5,3.5 M5.5,9.5 L9.5,9.5 L9.5,5.5"};
        var path=(Microsoft.UI.Xaml.Shapes.Path)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{data}' Stroke='#352D25' StrokeThickness='1.1' StrokeStartLineCap='Round' StrokeEndLineCap='Round'/>");
        var canvas=new Canvas {Width=13,Height=13,IsHitTestVisible=false};canvas.Children.Add(path);return canvas;
    }
    private void RememberNavigation(string page)
    {
        if (_historyIndex < 0 || _navigationHistory[_historyIndex] != page)
        {
            if (_historyIndex + 1 < _navigationHistory.Count) _navigationHistory.RemoveRange(_historyIndex + 1, _navigationHistory.Count - _historyIndex - 1);
            _navigationHistory.Add(page); _historyIndex = _navigationHistory.Count - 1;
        }
        BackNavigationButton.IsEnabled = _historyIndex > 0;
        ForwardNavigationButton.IsEnabled = _historyIndex + 1 < _navigationHistory.Count;
        ContentScrollViewer.ChangeView(null, 0, null, true);
    }

    private void NavigateHistory(int direction)
    {
        var index = _historyIndex + direction;
        if (index < 0 || index >= _navigationHistory.Count) return;
        _historyIndex = index;
        ShowPage(_navigationHistory[index]);
    }

    private Task<bool> ShowSheetAsync(string title, string message, bool confirm = false, bool hero = false, string accept = "Entendido")
    {
        if(_closed) return Task.FromResult(false);
        RestoreCreatorContent();
        _dialogCompletion?.TrySetResult(false);
        _dialogPreviousFocus = FocusManager.GetFocusedElement(RootGrid.XamlRoot) as Control;
        _dialogCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ModalTitle.Text = title; ModalMessage.Text = message; ModalAcceptButton.Content = accept;
        ModalHero.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        ModalCancelButton.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        ModalScrim.Visibility = Visibility.Visible;
        ModalAcceptButton.Focus(FocusState.Programmatic);
        return _dialogCompletion.Task;
    }

    private void CompleteSheet(bool accepted)
    {
        RestoreCreatorContent();
        ModalScrim.Visibility = Visibility.Collapsed;
        _dialogPreviousFocus?.Focus(FocusState.Programmatic);
        _dialogCompletion?.TrySetResult(accepted); _dialogCompletion = null;
    }

    private static string PageInformation(string page) => page switch
    {
        "Gestos" => "Configura movimientos y taps de uno, dos o tres dedos. Las asignaciones se conservan localmente. El banco DEBUG reproduce contactos normalizados y reconoce gestos. La entrada táctil física todavía requiere verificar los reportes del Magic Mouse conectado.",
        "Puntero y desplazamiento" => "La velocidad del puntero puede aplicarse a Windows mediante una acción explícita. Los ajustes de desplazamiento preparan el motor táctil; requieren reportes físicos verificados para controlar el Magic Mouse.",
        "Botones" => "Configura las zonas de clic y taps. El intercambio de botones de Windows se aplica mediante una acción explícita. Los taps y clic central táctil necesitan acceso verificado a la superficie del dispositivo.",
        "Aplicaciones" => "Añade ejecutables y guarda sus ajustes. La app detecta cambios de ventana activa y selecciona el perfil correspondiente. No muestra aplicaciones inventadas.",
        "Aspecto" => "Elige tema claro, oscuro o del sistema. El estilo y tamaño del puntero se aplican a toda la interfaz. Aplicar a Windows cambia el estilo y tamaño del puntero de selección normal. Restaurar vuelve a tu esquema de Windows. El indicador de clic se prueba dentro de la aplicación.",
        _ => "Configuración local, diagnóstico HID y simulador de desarrollo. Los datos de batería, firmware y serial solo se muestran si el hardware los proporciona. Ningún dato DEBUG se presenta como conexión real."
    };
}
