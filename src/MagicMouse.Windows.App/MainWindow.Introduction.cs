using MagicMouse.Core.Gestures;
using MagicMouse.Windows.App.Devices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private Border? _introduction;
    private int _introductionStep;
    private bool _introductionBusy;
    private void ShowIntroduction(int step)
    {
        if(_closed) return;
        _introductionStep=step;
        if(_introduction is null)
        {
            _introduction=new Border { Background=new SolidColorBrush(ColorFromHex(DesignTokens.IsDark ? "#242429" : "#F7F7F9")),Padding=new Thickness(30,66,30,30) };
            foreach(var shell in RootGrid.Children.Take(2)) shell.Visibility=Visibility.Collapsed;
            Grid.SetColumnSpan(_introduction,2); RootGrid.Children.Add(_introduction);
            _introduction.SizeChanged+=(_,_)=> {if(_introduction is not null && RootGrid.XamlRoot is not null) _chrome?.SetSetupCaption(RootGrid.ActualWidth,RootGrid.XamlRoot.RasterizationScale);};
            // Keep functional traffic lights above the introduction.
            var traffic=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8,Margin=new Thickness(20,19,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top };
            var trafficIndex=0;
            foreach(var (color,label,action) in new (string,string,Action)[] {
                ("#FF5F57","Cerrar",()=>Close()),("#FEBC2E","Minimizar",()=>_chrome?.Minimize()),("#28C840","Ampliar o restaurar",()=>_chrome?.ToggleMaximize()) })
            {
                var button=new Button { Style=(Style)Application.Current.Resources["TrafficLightButton"],Background=new SolidColorBrush(ColorFromHex(color)) };
                var symbol=TrafficSymbol(trafficIndex++);symbol.Opacity=0;button.Content=symbol;
                button.PointerEntered+=(_,_)=>symbol.Opacity=.7;button.PointerExited+=(_,_)=>symbol.Opacity=0;
                AutomationProperties.SetName(button,label); button.Click+=(_,_)=>action(); traffic.Children.Add(button);
            }
            Grid.SetColumnSpan(traffic,2); traffic.Tag="introductionTraffic"; RootGrid.Children.Add(traffic);
        }
        _chrome?.ShowSetupWindow(step);
        var content=new StackPanel { Spacing=20,Width=480,MaxWidth=480,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Transitions={new EntranceThemeTransition()} };
        var centerHost=new Grid {Width=Math.Max(480,AppWindow.Size.Width/(RootGrid.XamlRoot?.RasterizationScale??1)-60)};
        centerHost.Children.Add(content);
        centerHost.Loaded+=(_,_)=>centerHost.Width=Math.Max(480,_introduction?.ActualWidth-60??480);
        centerHost.SizeChanged+=(_,_)=> {if(_introduction is not null) centerHost.Width=Math.Max(480,_introduction.ActualWidth-60);};
        _introduction.Child=new ScrollViewer { Content=centerHost,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        var heading=Text(step switch {0=>"Tu Magic Mouse. En Windows.",1=>"Bienvenido a Magic Mouse",2=>"Conecta tu Magic Mouse",_=>"A tu manera."},30,DesignTokens.PrimaryText,true); heading.TextAlignment=TextAlignment.Center; content.Children.Add(heading);
        if(step==0)
        {
            content.Children.Add(CenteredIntroText("Una instalación sencilla. Una experiencia cuidada.",14));
            var art=new StackPanel { Orientation=Orientation.Horizontal,Spacing=24,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,20,0,14) };
            var logo=IntroLogo();logo.CanDrag=true;
            logo.DragStarting+=(_,args)=> {args.Data.SetText("magicmouse-install");args.AllowedOperations=global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;};
            art.Children.Add(logo);
            art.Children.Add(new TextBlock {Text="● ● ●  ›",FontSize=20,Foreground=new SolidColorBrush(ColorFromHex(DesignTokens.Accent)),VerticalAlignment=VerticalAlignment.Center});
            var folder=new Border {Width=132,Height=108,CornerRadius=new CornerRadius(22),Background=new SolidColorBrush(ColorFromHex("#337AB9")),AllowDrop=true};
            var folderText=new StackPanel {Spacing=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            folderText.Children.Add(new TextBlock {Text="Aplicaciones",FontSize=14,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Foreground=new SolidColorBrush(Microsoft.UI.Colors.White)});
            folderText.Children.Add(new TextBlock {Text="Arrastra aquí",FontSize=10,Foreground=new SolidColorBrush(Microsoft.UI.Colors.White),Opacity=.85,HorizontalAlignment=HorizontalAlignment.Center});folder.Child=folderText;
            async Task Install()
            {
                if(_introductionBusy || _closed) return;_introductionBusy=true;
                try {await InstallationService.InstallAsync();if(InstallationService.IsInstalled) ShowIntroduction(1);else {_forceExit=true;Close();}}
                catch(Exception exception) {content.Children.Add(CenteredIntroText("No se pudo instalar: "+exception.Message,12));}
                finally {_introductionBusy=false;}
            }
            folder.DragOver+=(_,args)=> {if(args.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) args.AcceptedOperation=global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;};
            folder.Drop+=async (_,args)=>await RunUiActionAsync("Instalar",async()=> {if(args.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text) && await args.DataView.GetTextAsync()=="magicmouse-install") await Install();});
            art.Children.Add(folder);content.Children.Add(art);
            content.Children.Add(CenteredIntroText("Arrastra el logo a Aplicaciones o pulsa Instalar. Se instalará solo para tu usuario y aparecerá en el menú Inicio, sin permisos de administrador.",13));
            content.Children.Add(IntroButton("Instalar y continuar",Install));
            content.Children.Add(IntroButton("Usar sin instalar",()=>{ShowIntroduction(1);return Task.CompletedTask;},false));
        }
        else if(step==1)
        {
            content.Children.Add(IntroLogo()); content.Children.Add(CenteredIntroText("Elige cómo empezar. Siempre podrás cambiarlo en la app.",14));
            content.Children.Add(IntroButton("Configuración recomendada",()=>{MacGestureDefaults.Apply(_settings);ShowIntroduction(2);return Task.CompletedTask;}));
            content.Children.Add(IntroButton("Conservar mis preferencias",()=>{ShowIntroduction(2);return Task.CompletedTask;},false));
            content.Children.Add(CenteredIntroText("Scroll natural, navegación con un dedo y escritorios con dos. Los gestos físicos requieren una colección táctil accesible en Windows.",12));
        }
        else if(step==2)
        {
            content.Children.Add(IntroLogo());
            content.Children.Add(CenteredIntroText("Enciende tu Magic Mouse y empareja el dispositivo en Bluetooth. Si aún no lo tienes, puedes preparar tus ajustes ahora.",14));
            var status=CenteredIntroText(_currentMouseDevice is null ? "Sin Magic Mouse conectado":"Detectado: "+_currentMouseDevice.Name,13); content.Children.Add(status);
            content.Children.Add(IntroButton("Abrir ajustes de Bluetooth",async()=>await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"))));
            content.Children.Add(IntroButton("Comprobar conexión",async()=>{await RefreshDevicesAsync();status.Text=_currentMouseDevice is null ? "Todavía no se detecta un Magic Mouse":"Detectado: "+_currentMouseDevice.Name;},false));
            content.Children.Add(IntroButton("Continuar",()=>{ShowIntroduction(3);return Task.CompletedTask;},false));
        }
        else
        {
            content.Children.Add(IntroLogo());
            content.Children.Add(CenteredIntroText("La app guarda tus ajustes localmente y no envía telemetría. Tú decides cómo se ejecuta.",14));
            var startup=new CheckBox { Content="Iniciar con Windows",IsChecked=StartupService.IsEnabled() };
            var background=new CheckBox { Content="Continuar en la bandeja al cerrar",IsChecked=_settings.Toggles.GetValueOrDefault("app.background") };
            var permissions=new StackPanel {Spacing=12,Width=380,HorizontalAlignment=HorizontalAlignment.Center};
            permissions.Children.Add(startup);permissions.Children.Add(background);content.Children.Add(permissions);
            content.Children.Add(CenteredIntroText("El puntero de Windows se cambia solo al pulsar Aplicar a Windows. Los atajos no controlan ventanas ejecutadas como administrador. Esta versión no instala un controlador táctil propio.",12));
            content.Children.Add(IntroButton("Finalizar",async()=>
            {
                try
                {
                    StartupService.SetEnabled(startup.IsChecked==true); _settings.Toggles["app.startup"]=startup.IsChecked==true;
                    _settings.Toggles["app.background"]=background.IsChecked==true;
                    if(background.IsChecked==true) { _settings.Toggles["app.tray"]=true;EnsureTray(true); }
                    _settings.Toggles["app.onboardingComplete"]=true;await SaveNowAsync();RefreshAppCursor();ShowPage("Gestos");CloseIntroduction(true);
                }
                catch(Exception exception) { content.Children.Add(CenteredIntroText("No se pudo guardar: "+exception.Message,12)); }
            }));
        }
        if(step>1) content.Children.Add(IntroButton("Volver",()=>{ShowIntroduction(step-1);return Task.CompletedTask;},false));
        RootGrid.RefreshCursorTree();
    }
    private UIElement IntroLogo()=>new Border { Width=108,Height=108,HorizontalAlignment=HorizontalAlignment.Center,CornerRadius=new CornerRadius(27),Background=new SolidColorBrush(ColorFromHex(DesignTokens.CardFill)),BorderBrush=new SolidColorBrush(ColorFromHex(DesignTokens.CardBorder)),BorderThickness=new Thickness(1),Child=DeviceImage("MagicMouse.png",100,100,"Magic Mouse for Windows") };
    private TextBlock CenteredIntroText(string value,double size)
    {
        var text=Text(value,size,DesignTokens.SecondaryText);text.TextAlignment=TextAlignment.Center;text.LineHeight=size*1.55;return text;
    }
    private Button IntroButton(string label,Func<Task> action,bool primary=true)
    {
        var button=ActionButton(label,action);button.HorizontalAlignment=HorizontalAlignment.Center;button.Width=380;button.MaxWidth=380;button.HorizontalContentAlignment=HorizontalAlignment.Center;
        button.MinHeight=48;button.CornerRadius=new CornerRadius(24);button.FontSize=15;button.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;
        if(primary) {button.Background=new SolidColorBrush(ColorFromHex(DesignTokens.Accent));button.Foreground=new SolidColorBrush(Microsoft.UI.Colors.White);}
        return button;
    }
    private Storyboard? _shellEntry;
    private void CloseIntroduction(bool animate=false)
    {
        if(_introduction is null) return;
        RootGrid.Children.Remove(_introduction);_introduction=null;
        foreach(var element in RootGrid.Children.OfType<FrameworkElement>().Where(item=>Equals(item.Tag,"introductionTraffic")).ToArray()) RootGrid.Children.Remove(element);
        _shellEntry?.Stop();_shellEntry=null;
        var shells=RootGrid.Children.Take(2).ToArray();
        foreach(var shell in shells) {shell.Visibility=Visibility.Visible;shell.Opacity=animate && _systemUiSettings.AnimationsEnabled ? 0:1;}
        _chrome?.RestoreRegularWindow();RootGrid.UpdateLayout();
        if(RootGrid.XamlRoot is not null)
        {
            var origin=WindowDragArea.TransformToVisual(RootGrid).TransformPoint(new global::Windows.Foundation.Point());
            _chrome?.SetCaption(origin.X,origin.Y,WindowDragArea.ActualWidth,WindowDragArea.ActualHeight,RootGrid.XamlRoot.RasterizationScale);
        }
        if(animate && _systemUiSettings.AnimationsEnabled)
        {
            var entry=new Storyboard();_shellEntry=entry;
            foreach(var shell in shells)
            {
                var fade=new DoubleAnimation {From=0,To=1,Duration=TimeSpan.FromMilliseconds(480),EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut }};
                Storyboard.SetTarget(fade,shell);Storyboard.SetTargetProperty(fade,"Opacity");entry.Children.Add(fade);
            }
            entry.Completed+=(_,_)=> {foreach(var shell in shells) shell.Opacity=1;if(ReferenceEquals(_shellEntry,entry)) _shellEntry=null;};entry.Begin();
        }
        NavigationPanel.Children.OfType<Button>().FirstOrDefault(item=>Equals(item.Tag,PageTitle.Text))?.Focus(FocusState.Programmatic);
    }
}
