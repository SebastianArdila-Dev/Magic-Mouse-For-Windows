using System.Diagnostics;
using MagicMouse.Core.Devices;
using MagicMouse.Core.Settings;
using MagicMouse.Windows.App.Devices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Windowing;
using Windows.Devices.Enumeration;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow : Window
{
    private static readonly string[] Pages = ["Gestos", "Puntero y desplazamiento", "Botones", "Aplicaciones", "Aspecto", "Avanzado"];
    private static readonly string[] GestureNames = ["Deslizar 1 dedo a la izquierda", "Deslizar 1 dedo a la derecha", "Deslizar 2 dedos a la izquierda", "Deslizar 2 dedos a la derecha", "Deslizar 2 dedos arriba", "Deslizar 2 dedos abajo", "Tap con 1 dedo", "Tap con 2 dedos", "Tap con 3 dedos", "Doble tap con 1 dedo", "Doble tap con 2 dedos"];
    private static readonly string[] Actions = ["Sin acción", "Atrás", "Adelante", "Escritorio anterior", "Escritorio siguiente", "Task View", "Mostrar escritorio", "Alt + Tab", "Cerrar ventana", "Minimizar", "Maximizar/restaurar", "Búsqueda", "Play/Pause", "Siguiente pista", "Pista anterior", "Subir volumen", "Bajar volumen", "Silenciar", "Zoom inteligente", "Acercar pantalla", "Alejar pantalla", "Clic principal", "Clic secundario", "Clic central"];
    private readonly SettingsStore _store = new();
    private readonly string _logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagicMouseForWindows", "Logs");
    private UserSettings _settings = new();
    private CancellationTokenSource? _saveDebounce;
    private readonly MagicMouseDiscoveryService _discovery = new();
    private readonly ForegroundAppMonitor _foregroundMonitor = new();
    private DeviceInformation? _currentMouseDevice;
    private HidReportCaptureService _capture = new();
    private TextBlock? _captureSummary;
    private Button? _startCaptureButton;
    private Button? _stopCaptureButton;
    private Button? _exportCaptureButton;
    private TextBlock? _foregroundSummary;
    private TextBlock? _simulationStatus;
    private string? _foregroundExecutablePath;
    private AppProfile? _foregroundProfile;
    private long _lastCaptureUiUpdateTicks;
    private bool _initialized;
    private bool _closed;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","MagicMouse.ico"));
        SystemBackdrop = new DesktopAcrylicBackdrop();
        InitializeShell();
        InitializeBattery();
        RetryDeviceButton.Click += async (_, _) => await RunUiActionAsync("Buscar dispositivos",RefreshDevicesAsync);
        BluetoothSettingsButton.Click += async (_, _) => await RunUiActionAsync("Bluetooth",async()=>await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth")));
        AttachCaptureEvents(_capture);
        RootGrid.Loaded += async (_, _) => await RunUiActionAsync("Iniciar aplicación",InitializeAsync);
        _foregroundMonitor.ForegroundExecutableChanged += path => DispatcherQueue.TryEnqueue(() => UpdateForegroundProfile(path));
        Closed += (_, _) => { _closed=true; _creatorOpen=false; StopCreatorMotion(); _dialogCompletion?.TrySetResult(false); _dialogCompletion=null; _batteryTimer?.Stop(); _inputTimer?.Stop(); _reconnectTimer?.Stop();_discoveryTimer?.Stop();_shellEntry?.Stop(); _tray?.Dispose(); _chrome?.Dispose(); _discovery.Dispose(); _foregroundMonitor.Dispose(); _capture.Dispose(); _saveDebounce?.Cancel(); };
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        Directory.CreateDirectory(_logsPath);
        try { _settings = await _store.LoadAsync(); }
        catch (Exception ex) { await LogAsync($"Settings load failed: {ex.Message}"); _settings = new UserSettings(); }
        ApplyTheme(_settings.Choices.GetValueOrDefault("app.theme", "Claro"));
        _settings.Toggles.TryAdd("input.enabled",true);
        if(!_settings.Toggles.GetValueOrDefault("app.closeToTrayDefault.v1"))
        {
            _settings.Toggles["app.background"]=true;_settings.Toggles["app.tray"]=true;
            _settings.Toggles["app.closeToTrayDefault.v1"]=true;
        }
        RefreshAppCursor();
        try { RestoreSavedSystemCursor(); } catch(Exception exception) { await LogAsync($"Cursor restore failed: {exception.Message}"); }
        await LogAsync("Settings loaded");
        foreach (var page in Pages) AddNavigation(page);
        ShowPage("Aspecto");
        _discovery.DeviceChanged += OnDeviceChanged;
        try { await _discovery.StartAsync(); }
        catch (Exception ex) { await LogAsync($"Device discovery startup failed: {ex.Message}"); }
        InitializeConnectionWatchdog();
        try { _foregroundMonitor.Start(); }
        catch (Exception ex) { await LogAsync($"Foreground application monitoring failed: {ex.Message}"); }
        try { InitializeLifecycle(); } catch (Exception exception) { await LogAsync($"Lifecycle startup failed: {exception}"); }
        var arguments=Environment.GetCommandLineArgs();
        if(!arguments.Contains("--background") &&
           (arguments.Contains("--install") || !_settings.Toggles.GetValueOrDefault("app.onboardingComplete")))
            ShowIntroduction(arguments.Contains("--install") || !InstallationService.IsInstalled ? 0 : 1);
    }

    private void AddNavigation(string page)
    {
        var button = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = {
                IconFactory.Navigation(page, new SolidColorBrush(ColorFromHex(DesignTokens.BodyText))),
                new TextBlock { Text = page, VerticalAlignment = VerticalAlignment.Center, FontSize = page == "Puntero y desplazamiento" ? 12 : 14 }
            } },
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8, 8, 8),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Foreground = new SolidColorBrush(ColorFromHex("#27354A")),
            FontSize = 14,
            Tag = page
        };
        button.Click += (_, _) => ShowPage(page);
        AutomationProperties.SetName(button, page);
        NavigationPanel.Children.Add(button);
    }

    private void ShowPage(string page)
    {
        RememberNavigation(page);
        foreach (var item in NavigationPanel.Children.OfType<Button>())
        {
            var selected = Equals(item.Tag, page);
            item.Background = new SolidColorBrush(selected ? ColorFromHex(DesignTokens.Accent) : Microsoft.UI.Colors.Transparent);
            item.Foreground = new SolidColorBrush(selected ? Microsoft.UI.Colors.White : ColorFromHex(DesignTokens.BodyText));
            if (item.Content is StackPanel panel && panel.Children[0] is FrameworkElement icon) IconFactory.Recolor(icon, item.Foreground);
            item.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }
        PageTitle.Text = page;
        PageSubtitle.Text = page switch
        {
            "Gestos" => "Asigna acciones a los movimientos de la superficie táctil.",
            "Puntero y desplazamiento" => "Ajusta la velocidad y el desplazamiento del mouse.",
            "Botones" => "Configura clics y toques según las capacidades detectadas.",
            "Aplicaciones" => "Usa ajustes distintos cuando una aplicación esté activa.",
            "Aspecto" => "Personaliza el puntero y prueba el indicador de clic.",
            _ => "Preferencias de la aplicación y herramientas de diagnóstico."
        };
        PageContent.Children.Clear();
        AddProfileEditingBanner();
        switch (page)
        {
            case "Gestos": BuildGestures(); break;
            case "Puntero y desplazamiento": BuildPointer(); break;
            case "Botones": BuildButtons(); break;
            case "Aplicaciones": BuildApplications(); break;
            case "Aspecto": BuildAppearance(); break;
            default: BuildAdvanced(); break;
        }
        RootGrid.RefreshCursorTree();
    }

    private void BuildGestures()
    {
        var intro = Card();
        var layout = new Grid { ColumnSpacing = 24 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        var visual = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 14 };
        visual.Children.Add(GestureMouseMap());
        visual.Children.Add(Text("La superficie se procesa al conectar la lectura HID. Los formatos no reconocidos se ignoran.", 12, "#748198"));
        Grid.SetColumn(visual, 0);
        var list = new StackPanel { Spacing = 12 };
        list.Children.Add(Text("Acción para cada gesto", 16, "#172235", true));
        foreach (var gesture in GestureNames)
        {
            var row = new StackPanel { Spacing = 7 };
            row.Children.Add(Text(gesture, 12, "#64728A"));
            row.Children.Add(Choice("gesture:" + gesture, Actions, MagicMouse.Core.Gestures.MacGestureDefaults.Action(gesture)));
            list.Children.Add(row);
        }
        Grid.SetColumn(list, 1);
        layout.Children.Add(visual); layout.Children.Add(list);
        intro.Child = layout; PageContent.Children.Add(intro);
        PageContent.Children.Add(InputConnectionCard());
        PageContent.Children.Add(SectionCard("Gestos rápidos", "Sensibilidad y dirección de los movimientos reconocidos.",
            ToggleSetting("gesture.oneEnabled", "Gestos con un dedo", true), ToggleSetting("gesture.swapOne", "Invertir swipe de un dedo", false),
            ToggleSetting("gesture.twoEnabled", "Gestos con dos dedos", true), ToggleSetting("gesture.swapTwo", "Invertir swipe de dos dedos", false),
            NumberSetting("gesture.distance", "Distancia mínima de swipe", 5, 40, 12, "%"),
            NumberSetting("gesture.timeout", "Duración máxima de swipe", 100, 1200, 900, " ms")));
        PageContent.Children.Add(Notice("Los swipes y dobles taps tienen equivalentes en Windows. Los gestos adicionales de tres dedos son personalizaciones; no forman parte de los gestos estándar del Magic Mouse en macOS."));
    }

    private void BuildPointer()
    {
        PageContent.Children.Add(SectionCard("Puntero", "La velocidad se aplica a todos los mouse del sistema al pulsar Aplicar.",
            NumberSetting("pointer.speed", "Velocidad del puntero", 1, 20, WindowsMouseSettings.PointerSpeed, ""),
            ActionButton("Aplicar velocidad a Windows", async () => { WindowsMouseSettings.SetPointerSpeed((int)Preferences.Numbers.GetValueOrDefault("pointer.speed", 10)); await ShowSheetAsync("Velocidad aplicada", "Windows utiliza ahora la velocidad seleccionada para el puntero."); })));
        PageContent.Children.Add(SectionCard("Desplazamiento", "", ToggleSetting("scroll.enabled", "Activar desplazamiento", true),
            ToggleSetting("scroll.vertical", "Desplazamiento vertical", true), ToggleSetting("scroll.horizontal", "Desplazamiento horizontal", true),
            ToggleSetting("scroll.natural", "Desplazamiento natural", false), ToggleSetting("scroll.invertVertical", "Invertir dirección vertical", false),
            ToggleSetting("scroll.invertHorizontal", "Invertir dirección horizontal", false), ToggleSetting("scroll.axisLock", "Bloquear a un eje", true),
            ToggleSetting("scroll.ignoreMouseMovement", "Pausar scroll al mover el mouse", false),
            ToggleSetting("scroll.smooth", "Desplazamiento suave", true), NumberSetting("scroll.speed", "Velocidad", 1, 20, 8, "%"),
            NumberSetting("scroll.sensitivity", "Sensibilidad", 1, 20, 10, "%"), NumberSetting("scroll.inertia", "Suavidad e inercia", 0, 100, 35, "%"),
            Choice("scroll.fingers", ["1 dedo", "2 dedos", "1 o 2 dedos"])));
        PageContent.Children.Add(Notice("La configuración de scroll se guarda. Aplicarla a la superficie del Magic Mouse requiere reportes táctiles verificados."));
        PageContent.Children.Add(SectionCard("Zoom con modificador", "Control + scroll ajusta el zoom en navegadores y lectores PDF compatibles. Para ampliar toda la pantalla puedes asignar Acercar pantalla o Alejar pantalla a un gesto.", ToggleSetting("zoom.modifier", "Permitir zoom con Control + scroll", false)));
    }

    private void BuildButtons()
    {
        var card = Card();
        var grid = new Grid { ColumnSpacing = 26 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        grid.Children.Add(MouseIllustration(160, 220));
        var settings = new StackPanel { Spacing = 10 };
        settings.Children.Add(Text("Zonas de clic", 16, "#172235", true));
        settings.Children.Add(Unavailable(Choice("buttons.primary", ["Zona izquierda · Clic principal", "Zona central · Clic principal", "Zona derecha · Clic principal"])));
        settings.Children.Add(Unavailable(Choice("buttons.secondary", ["Zona derecha · Clic secundario", "Zona central · Clic secundario", "Zona izquierda · Clic secundario"])));
        settings.Children.Add(Unavailable(ToggleSetting("buttons.middle", "Clic físico central · pendiente", false)));
        settings.Children.Add(Unavailable(ToggleSetting("buttons.simultaneous", "Clic físico simultáneo · pendiente", false)));
        settings.Children.Add(ToggleSetting("buttons.swap", "Intercambiar clic principal/secundario", false));
        settings.Children.Add(ToggleSetting("buttons.leftHanded", "Modo para zurdos", false));
        settings.Children.Add(ToggleSetting("buttons.tapClick", "Tap para hacer clic", false));
        settings.Children.Add(Unavailable(ToggleSetting("buttons.ignoreLifted", "Detectar levantamiento · pendiente", true)));
        settings.Children.Add(Choice("buttons.tap1", ["1 dedo: sin acción", "1 dedo: clic principal"]));
        settings.Children.Add(Choice("buttons.tap2", ["2 dedos: sin acción", "2 dedos: clic secundario", "2 dedos: clic central"]));
        settings.Children.Add(Choice("buttons.tap3", ["3 dedos: sin acción", "3 dedos: clic central"]));
        Grid.SetColumn(settings, 1); grid.Children.Add(settings); card.Child = grid;
        PageContent.Children.Add(card);
        PageContent.Children.Add(SectionCard("Botones de Windows", "El cambio afecta a todos los mouse conectados.",
            ActionButton("Aplicar intercambio de botones", async () => { WindowsMouseSettings.SetButtonsSwapped(_settings.Toggles.GetValueOrDefault("buttons.swap") || _settings.Toggles.GetValueOrDefault("buttons.leftHanded")); await ShowSheetAsync("Botones aplicados", "Se ha aplicado la selección de botones principales de Windows."); })));
        PageContent.Children.Add(Notice("Los taps y el clic central táctil requieren reportes del Magic Mouse. El clic físico normal continúa gestionado por Windows."));
        PageContent.Children.Add(Notice("La reasignación de zonas físicas, el clic simultáneo y detectar levantamiento requieren soporte de un controlador de filtro. Están desactivados porque esta versión no incluye ese controlador. Puedes asignar clic central a un tap."));
        PageContent.Children.Add(SectionCard("Filtros de tap", "Se utilizan en el reconocedor de contactos del simulador.",
            NumberSetting("tap.timeout", "Duración máxima", 50, 500, 320, " ms"),
            NumberSetting("tap.movement", "Movimiento permitido", 1, 10, 3.5, "%")));
    }

    private void BuildApplications()
    {
        var header = Card();
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(Text("Perfiles por aplicación", 17, "#172235", true));
        stack.Children.Add(Text("Añade ejecutables para asociarlos con un perfil local.", 13, "#718097"));
        _foregroundSummary = Text(ForegroundSummary(), 12, "#536884");
        stack.Children.Add(_foregroundSummary);
        var add = new Button { Content = "＋  Añadir aplicación .exe", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(15, 9, 15, 9), CornerRadius = new CornerRadius(DesignTokens.ControlRadius), Background = new SolidColorBrush(ColorFromHex(DesignTokens.Accent)), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
        add.Click += async (_, _) => await RunUiActionAsync("Añadir aplicación",AddProfileAsync); stack.Children.Add(add);
        header.Child = stack; PageContent.Children.Add(header);
        if (_settings.Profiles.Count == 0)
        {
            var empty = Card(); empty.Child = new StackPanel { Spacing = 6, Children = { Text("No hay aplicaciones configuradas", 15, "#26354C", true), Text("Los perfiles que añadas aparecerán aquí.", 12, "#748198") } }; PageContent.Children.Add(empty);
        }
        foreach (var profile in _settings.Profiles.ToArray()) PageContent.Children.Add(ProfileRow(profile));
        PageContent.Children.Add(Notice("El perfil de la aplicación activa se aplica al motor táctil cuando la lectura HID está conectada."));
    }

    private void BuildAppearance()
    {
        PageContent.Children.Add(AppearanceThemeCard());
        var grid = new Grid { ColumnSpacing = 18 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
        var controls = new StackPanel { Spacing = 12 };
        Border? previewPulse = null;
        Grid? pointerPreview = null;
        controls.Children.Add(Text("Puntero del mouse", 17, "#172235", true));
        controls.Children.Add(VisualChoices("appearance.style", ["Automático", "macOS", "Minimal", "Grande", "Personalizado"], "Automático", style =>
        {
            RefreshAppCursor();
            if (pointerPreview is not null) { pointerPreview.Children.Clear(); pointerPreview.Children.Add(IconFactory.Pointer(style, pointerPreview.Width, AccentColor(Preferences.Choices.GetValueOrDefault("appearance.color","Azul")))); }
        }));
        controls.Children.Add(NumberSetting("appearance.size", "Tamaño del puntero", 70, 180, 120, "%", size =>
        {
            RefreshAppCursor();
            if (pointerPreview is not null) { pointerPreview.Width = 28 * size / 120; pointerPreview.Height = 34 * size / 120; pointerPreview.Children.Clear(); pointerPreview.Children.Add(IconFactory.Pointer(Preferences.Choices.GetValueOrDefault("appearance.style", "Automático"), pointerPreview.Width, AccentColor(Preferences.Choices.GetValueOrDefault("appearance.color","Azul")))); }
        }));
        controls.Children.Add(Text("Color de acento", 12, "#65728A"));
        var savedColor = Preferences.Choices.GetValueOrDefault("appearance.color", "Azul");
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var colorRings=new Dictionary<string,Microsoft.UI.Xaml.Shapes.Ellipse>();
        foreach (var (name, hex) in new[] { ("Azul", "#1683FF"), ("Violeta", "#8050E8"), ("Rosa", "#EF5276"), ("Naranja", "#FF8B33"), ("Amarillo", "#F4BF20"), ("Verde", "#36B875"), ("Gris", "#9AA5B4") })
        {
            var face=new Grid {Width=32,Height=32};
            var ring=new Microsoft.UI.Xaml.Shapes.Ellipse { Width=30,Height=30,Stroke=new SolidColorBrush(ColorFromHex(DesignTokens.Accent)),StrokeThickness=2,Opacity=name==savedColor ? 1:0,IsHitTestVisible=false };
            var fill=new Microsoft.UI.Xaml.Shapes.Ellipse { Width=24,Height=24,Fill=new SolidColorBrush(ColorFromHex(hex)),Stroke=new SolidColorBrush(Microsoft.UI.Colors.White),StrokeThickness=1,IsHitTestVisible=false };
            face.Children.Add(ring);face.Children.Add(fill);colorRings[name]=ring;
            var swatch = new Button { Width=32,Height=32,MinWidth=32,MinHeight=32,Padding=new Thickness(0),Content=face,Style=(Style)Application.Current.Resources["ColorSwatchButton"],Tag=name };
            AutomationProperties.SetName(swatch, name);
            swatch.Click += (_, _) => { Preferences.Choices["appearance.color"] = name; RefreshAppCursor(); if (previewPulse is not null) previewPulse.Background = new SolidColorBrush(ColorFromHex(hex)); if (pointerPreview is not null && Preferences.Choices.GetValueOrDefault("appearance.style") == "Personalizado") { pointerPreview.Children.Clear(); pointerPreview.Children.Add(IconFactory.Pointer("Personalizado",pointerPreview.Width,AccentColor(name))); } foreach(var choice in colorRings) choice.Value.Opacity=choice.Key==name ? 1:0; QueueSave(); };
            colors.Children.Add(swatch);
        }
        controls.Children.Add(colors);
        controls.Children.Add(ToggleSetting("appearance.clickIndicator", "Indicador de clic", true));
        controls.Children.Add(VisualChoices("appearance.clickStyle", ["Ninguno", "Ráfaga", "Punto", "Ondas", "Destello"], "Ráfaga"));
        controls.Children.Add(NumberSetting("appearance.clickSize", "Tamaño del indicador", 50, 180, 100, "%", size =>
        {
            if (previewPulse is not null) { previewPulse.Width = 26 * size / 100; previewPulse.Height = 26 * size / 100; }
        }));
        var pointerCard = Card();
        var indicatorCard = Card();
        var pointerControls = new StackPanel { Spacing = 18 };
        var indicatorControls = new StackPanel { Spacing = 18 };
        var appearanceChildren = controls.Children.ToArray();
        controls.Children.Clear();
        for (var i = 0; i < appearanceChildren.Length; i++)
            (i < 5 ? pointerControls : indicatorControls).Children.Add(appearanceChildren[i]);
        var pointerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var applyCursor = ActionButton("Aplicar a Windows", ApplySystemCursorAsync);
        applyCursor.Background = new SolidColorBrush(ColorFromHex(DesignTokens.Accent)); applyCursor.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        pointerActions.Children.Add(applyCursor);
        pointerActions.Children.Add(ActionButton("Restaurar", async () => { SystemCursorService.Restore(); _settings.Toggles["appearance.systemApplied"] = false; RefreshAppCursor(); await SaveNowAsync(); await ShowSheetAsync("Puntero restaurado", "Windows volvió a cargar tu esquema de punteros."); }));
        pointerControls.Children.Add(pointerActions);
        pointerCard.Child = pointerControls;
        indicatorCard.Child = indicatorControls;
        controls.Children.Add(pointerCard);
        controls.Children.Add(indicatorCard);
        grid.Children.Add(controls);
        var preview = Card();
        var previewStack = new StackPanel { Spacing = 12 };
        previewStack.Children.Add(MouseIllustration(190, 280));
        previewStack.Children.Add(Text("Previsualización en tiempo real", 14, "#172235", true));
        var previewArea = new Grid { Height = 170, Background = new SolidColorBrush(ColorFromHex(DesignTokens.PreviewSurface)) };
        var halo = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush(ColorFromHex(DesignTokens.Accent)), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0, RenderTransform = new ScaleTransform() };
        previewArea.Children.Add(halo);
        var pulse = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(ColorFromHex(DesignTokens.Accent)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.75, RenderTransform = new ScaleTransform() };
        previewPulse = pulse;
        var clickSize = Preferences.Numbers.GetValueOrDefault("appearance.clickSize", 100);
        pulse.Width = pulse.Height = 26 * clickSize / 100;
        pulse.Background = new SolidColorBrush(ColorFromHex(savedColor switch { "Violeta" => "#8050E8", "Rosa" => "#EF5276", "Naranja" => "#FF8B33", "Amarillo" => "#F4BF20", "Verde" => "#36B875", "Gris" => "#9AA5B4", _ => "#1683FF" }));
        previewArea.Children.Add(pulse);
        pointerPreview = new Grid { Width = 28 * Preferences.Numbers.GetValueOrDefault("appearance.size", 120) / 120, Height = 34, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(145, 65, 0, 0), IsHitTestVisible = false };
        pointerPreview.Children.Add(IconFactory.Pointer(Preferences.Choices.GetValueOrDefault("appearance.style", "Automático"), pointerPreview.Width, AccentColor(Preferences.Choices.GetValueOrDefault("appearance.color","Azul"))));
        previewArea.Children.Add(pointerPreview);
        previewArea.PointerMoved += (_, e) =>
        {
            var position = e.GetCurrentPoint(previewArea).Position;
            pointerPreview.Visibility = Visibility.Collapsed;
            pulse.HorizontalAlignment = halo.HorizontalAlignment = HorizontalAlignment.Left;
            pulse.VerticalAlignment = halo.VerticalAlignment = VerticalAlignment.Top;
            pulse.Margin = new Thickness(position.X - pulse.Width / 2, position.Y - pulse.Height / 2, 0, 0);
            halo.Margin = new Thickness(position.X - halo.Width / 2, position.Y - halo.Height / 2, 0, 0);
        };
        previewArea.PointerEntered += (_,_) => pointerPreview.Visibility = Visibility.Collapsed;
        previewArea.PointerExited += (_,_) => { pointerPreview.Visibility = Visibility.Visible; pointerPreview.Margin = new Thickness(145,65,0,0); };
        previewArea.Tapped += (_, _) => AnimateClick(pulse, halo);
        previewStack.Children.Add(previewArea);
        previewStack.Children.Add(Text("Haz clic en el área para probar la animación.", 11, "#748198"));
        previewStack.Children.Add(Text("El estilo se aplica a toda la app. Para usarlo en Windows, pulsa Aplicar a Windows.", 10, "#8793A5"));
        preview.Child = previewStack; Grid.SetColumn(preview, 1); grid.Children.Add(preview);
        PageContent.Children.Add(grid);
    }

    private void BuildAdvanced()
    {
        _lifecycleToggles.Clear();
        PageContent.Children.Add(SectionCard("Aplicación", "Preferencias del sistema y ejecución en segundo plano.",
            LifecycleToggle("app.startup", "Iniciar con Windows", StartupService.IsEnabled(), StartupService.SetEnabled),
            LifecycleToggle("app.tray", "Mostrar icono en la bandeja", _tray is not null, enabled => { if (!enabled) _settings.Toggles["app.background"] = false; EnsureTray(enabled); }),
            LifecycleToggle("app.background", "Continuar en segundo plano al cerrar", _settings.Toggles.GetValueOrDefault("app.background"), enabled => { if (enabled) { EnsureTray(true); _settings.Toggles["app.tray"] = true; } }),
            ActionButton("Acerca de Magic Mouse", ShowCreatorAsync),
            ActionButton("Volver a ver la bienvenida", () => { ShowIntroduction(1); return Task.CompletedTask; }),
            Text("Los registros y ajustes permanecen locales. No hay telemetría ni actualizaciones automáticas conectadas.", 11, "#718097")));
        PageContent.Children.Add(DriverSetupCard());
#if DEBUG
        BuildSimulationPanel();
#endif
        var diagnostics = Card();
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Text("Diagnóstico", 17, "#172235", true));
        stack.Children.Add(Text($"Aplicación: Magic Mouse for Windows · 0.1.2\nDispositivo detectado: {_currentMouseDevice?.Name ?? "No disponible"}\nCaptura HID: {(_capture.IsCapturing ? "activa" : "detenida")} · reportes observados: {_capture.ReportCount}\nVersión de ajustes: {_settings.SettingsSchemaVersion}", 12, "#64728A"));
        stack.Children.Add(Notice("La captura lee únicamente reportes que Windows permita abrir. Los bytes se guardan localmente y no se interpretan como contactos hasta verificar el protocolo."));
        _captureSummary = Text(_capture.Metadata is null ? "Captura detenida · sin reportes" : CaptureDescription(), 12, "#536884");
        stack.Children.Add(_captureSummary);
        _startCaptureButton = ActionButton("Iniciar captura HID", StartHidCaptureAsync);
        _stopCaptureButton = ActionButton("Detener captura", StopHidCaptureAsync);
        _exportCaptureButton = ActionButton("Exportar captura JSON", ExportCaptureAsync);
        stack.Children.Add(_startCaptureButton);
        stack.Children.Add(_stopCaptureButton);
        stack.Children.Add(ActionButton("Limpiar reportes", () => { _capture.Clear(); if (_captureSummary is not null) _captureSummary.Text = CaptureDescription(); UpdateCaptureButtons(); return Task.CompletedTask; }));
        stack.Children.Add(_exportCaptureButton);
        UpdateCaptureButtons();
        stack.Children.Add(ActionButton("Abrir carpeta de logs", () => { Directory.CreateDirectory(_logsPath); Process.Start(new ProcessStartInfo("explorer.exe", _logsPath) { UseShellExecute = true }); return Task.CompletedTask; }));
        stack.Children.Add(ActionButton("Exportar configuración", ExportSettingsAsync));
        stack.Children.Add(ActionButton("Importar configuración", ImportSettingsAsync));
        stack.Children.Add(ActionButton("Restablecer configuración", ResetSettingsAsync));
        stack.Children.Add(ActionButton("Abrir ajustes Bluetooth", async () => await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"))));
        stack.Children.Add(Text("Las actualizaciones se distribuyen manualmente. El inspector está listo para capturar reportes cuando conectes un Magic Mouse que Windows permita abrir.", 11, "#748198"));
        diagnostics.Child = stack; PageContent.Children.Add(diagnostics);
    }

    private void BuildSimulationPanel()
    {
        var layout = new Grid { ColumnSpacing = 24 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var visual = new Grid { Width = 180, Height = 240 };
        visual.Children.Add(MouseIllustration(160, 230));
        _simulationContacts = new Canvas { Width = 180, Height = 240, IsHitTestVisible = false };
        visual.Children.Add(_simulationContacts); layout.Children.Add(visual);
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
        var names = new[] { "1 dedo ←", "1 dedo →", "2 dedos ←", "2 dedos →", "2 dedos ↑", "2 dedos ↓", "Tap 1 dedo", "Tap 2 dedos", "Tap 3 dedos", "Scroll", "Doble tap 1 dedo", "Doble tap 2 dedos" };
        for (var i = 0; i < 6; i++) actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < names.Length; i++)
        {
            var action = names[i]; var button = ActionButton(action, () => SimulateGestureAsync(action));
            button.HorizontalAlignment = HorizontalAlignment.Stretch; Grid.SetColumn(button, i % 2); Grid.SetRow(button, i / 2); actions.Children.Add(button);
        }
        Grid.SetColumn(actions, 1); layout.Children.Add(actions);
        _simulationStatus = Text("DEBUG · simulador listo", 12, "#536884");
        PageContent.Children.Add(SectionCard("Simulador de Magic Mouse · DEBUG", "Reproduce contactos y acciones configuradas sin enviar entrada a Windows ni representar una conexión Bluetooth.", layout, _simulationStatus));
    }
    private void SetSimulationStatus(string message)
    {
        if (_simulationStatus is not null) _simulationStatus.Text = message;
        _ = LogAsync(message);
    }

    private void UpdateCaptureButtons()
    {
        if(_closed) return;
        if (_startCaptureButton is not null) _startCaptureButton.IsEnabled = _currentMouseDevice is not null && !_capture.IsCapturing;
        if (_stopCaptureButton is not null) _stopCaptureButton.IsEnabled = _capture.IsCapturing;
        if (_exportCaptureButton is not null) _exportCaptureButton.IsEnabled = _capture.ReportCount > 0;
    }

    private UIElement NumberSetting(string key, string title, double min, double max, double initial, string suffix, Action<double>? changed = null)
    {
        var preferences = Preferences;
        if (!preferences.Numbers.ContainsKey(key)) preferences.Numbers[key] = initial;
        var safeValue = Math.Clamp(preferences.Numbers[key], min, max);
        preferences.Numbers[key] = safeValue;
        var slider = new Slider { Foreground = new SolidColorBrush(ColorFromHex(DesignTokens.Accent)), Background = new SolidColorBrush(ColorFromHex("#DCDDE4")), Minimum = min, Maximum = max, Value = safeValue, StepFrequency = 1, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 100 };
        AutomationProperties.SetName(slider, title);
        var value = Text($"{slider.Value:0}{suffix}", 12, "#718097");
        slider.ValueChanged += (_, e) => { preferences.Numbers[key] = e.NewValue; value.Text = $"{e.NewValue:0}{suffix}"; changed?.Invoke(e.NewValue); QueueSave(); };
        var row = new Grid { ColumnSpacing = 14 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Text(title, 13, "#26354C")); Grid.SetColumn(slider, 1); row.Children.Add(slider); Grid.SetColumn(value, 2); row.Children.Add(value);
        return row;
    }

    private UIElement ToggleSetting(string key, string title, bool initial, Action<bool>? changed = null)
    {
        var preferences = Preferences;
        if (!preferences.Toggles.ContainsKey(key)) preferences.Toggles[key] = initial;
        var toggle = new ToggleSwitch { IsOn = preferences.Toggles[key], Style = (Style)Application.Current.Resources["CompactSwitch"], HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(toggle, title);
        toggle.Toggled += (_, _) => { preferences.Toggles[key] = toggle.IsOn; changed?.Invoke(toggle.IsOn); QueueSave(); };
        var row = new Grid { MinHeight = 34, ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Text(title, 12, DesignTokens.BodyText); label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label); Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
        return row;
    }

    private UIElement Choice(string key, IReadOnlyList<string> values, string? defaultValue = null, Action<string>? changed = null)
    {
        var preferences = Preferences;
        var fallback = defaultValue is not null && values.Contains(defaultValue) ? defaultValue : values[0];
        var selectedValue = preferences.Choices.TryGetValue(key, out var value) && values.Contains(value) ? value : fallback;
        preferences.Choices[key] = selectedValue;
        var combo = new ComboBox { ItemsSource = values, SelectedItem = selectedValue, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 120 };
        AutomationProperties.SetName(combo, key);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string selected) { preferences.Choices[key] = selected; changed?.Invoke(selected); QueueSave(); } };
        return combo;
    }

    private UIElement SectionCard(string title, string subtitle, params UIElement[] children)
    {
        var stack = new StackPanel { Spacing = 0 };
        if (!string.IsNullOrWhiteSpace(title)) { var heading = Text(title, 17, "#172235", true); heading.Margin = new Thickness(0,0,0,6); stack.Children.Add(heading); }
        if (!string.IsNullOrWhiteSpace(subtitle)) { var description = Text(subtitle, 12, "#718097"); description.LineHeight = 18; description.Margin = new Thickness(0,0,0,8); stack.Children.Add(description); }
        foreach (var child in children)
        {
            if (stack.Children.Count > 0) stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(ColorFromHex("#EEEEF1")), Margin = new Thickness(0, 8, 0, 8) });
            stack.Children.Add(child);
        }
        var card = Card(); card.Child = stack; return card;
    }

    private Border Card() => new() { Background = new SolidColorBrush(ColorFromHex(DesignTokens.CardFill)), BorderBrush = new SolidColorBrush(ColorFromHex(DesignTokens.CardBorder)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(DesignTokens.CardRadius), Padding = new Thickness(16) };
    private static UIElement Unavailable(UIElement element)
    {
        if (element is Control control) control.IsEnabled = false;
        if (element is Microsoft.UI.Xaml.Controls.Panel panel) foreach(var child in panel.Children) Unavailable(child);
        if (element is FrameworkElement framework) ToolTipService.SetToolTip(framework,"Pendiente de controlador de filtro; no disponible en esta versión.");
        return element;
    }
    private Border Notice(string text) => new() { Background = new SolidColorBrush(ColorFromHex("#1A1683FF")), BorderBrush = new SolidColorBrush(ColorFromHex("#331683FF")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), Child = Text(text, 12, "#536884") };
    private static TextBlock Text(string text, double size, string color, bool bold = false) => new() { Text = text, FontSize = size, Foreground = new SolidColorBrush(ColorFromHex(color)), FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, TextWrapping = TextWrapping.Wrap };

    private FrameworkElement MouseIllustration(double width, double height)
    {
        var viewport = new Grid { Width = width, Height = height };
        var image = DeviceImage("MagicMouse.png", height * 1.04, height * 1.04, "Apple Magic Mouse");
        image.HorizontalAlignment = HorizontalAlignment.Center; image.VerticalAlignment = VerticalAlignment.Center;
        viewport.Children.Add(image);
        return viewport;
    }

    private static Image DeviceImage(string assetName, double width, double height, string accessibleName)
    {
        var image = new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri($"ms-appx:///Assets/{assetName}")),
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(image, accessibleName);
        return image;
    }

    private FrameworkElement GestureMouseMap()
    {
        var map = new Grid { Width = 340, Height = 400 };
        var mouse = MouseIllustration(155, 300);
        mouse.HorizontalAlignment = HorizontalAlignment.Center;
        mouse.VerticalAlignment = VerticalAlignment.Center;
        map.Children.Add(mouse);
        foreach (var (label, x, y) in new[] { ("2 dedos · arriba", 105, 8), ("← 1 / 2 dedos", 0, 145), ("1 / 2 dedos →", 245, 145), ("2 dedos · abajo", 105, 367) })
        {
            var anchor = GestureAnchor(label); anchor.HorizontalAlignment = HorizontalAlignment.Left; anchor.VerticalAlignment = VerticalAlignment.Top;
            anchor.Margin = new Thickness(x, y, 0, 0); map.Children.Add(anchor);
        }
        return map;
    }
    private static Border GestureAnchor(string label) => new()
    {
        Background = new SolidColorBrush(ColorFromHex("#E8F2FF")),
        BorderBrush = new SolidColorBrush(ColorFromHex("#B9D9FF")),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(7, 5, 7, 5),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Child = Text(label, 10, "#3F6EAA")
    };

    private void AnimateClick(Border pulse, Border halo)
    {
        if (!_settings.Toggles.GetValueOrDefault("appearance.clickIndicator", true) || _settings.Choices.GetValueOrDefault("appearance.clickStyle", "Ráfaga") == "Ninguno") return;
        var transform = (ScaleTransform)pulse.RenderTransform;
        var style = _settings.Choices.GetValueOrDefault("appearance.clickStyle", "Ráfaga");
        halo.BorderBrush = pulse.Background;
        var haloTransform = (ScaleTransform)halo.RenderTransform;
        haloTransform.ScaleX = haloTransform.ScaleY = 0.6; halo.Opacity = style == "Ondas" ? 0.8 : 0;
        transform.ScaleX = style == "Punto" ? 1 : 0.6; transform.ScaleY = style == "Punto" ? 1 : 0.6; pulse.Opacity = 1;
        var storyboard = new Storyboard();
        var from = style == "Punto" ? 1.0 : 0.6;
        var to = style == "Punto" ? 1.35 : style == "Destello" ? 1.7 : 2.6;
        var scaleX = new DoubleAnimation { From = from, To = to, Duration = DesignTokens.MotionNormal, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var scaleY = new DoubleAnimation { From = from, To = to, Duration = DesignTokens.MotionNormal, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var opacity = new DoubleAnimation { From = style == "Destello" ? 1 : 0.9, To = 0, Duration = DesignTokens.MotionNormal };
        Storyboard.SetTarget(scaleX, transform); Storyboard.SetTargetProperty(scaleX, "ScaleX"); Storyboard.SetTarget(scaleY, transform); Storyboard.SetTargetProperty(scaleY, "ScaleY"); Storyboard.SetTarget(opacity, pulse); Storyboard.SetTargetProperty(opacity, "Opacity");
        storyboard.Children.Add(scaleX); storyboard.Children.Add(scaleY); storyboard.Children.Add(opacity); storyboard.Begin();
        if (style == "Ondas")
        {
            var waveX = new DoubleAnimation { From = 0.6, To = 3.2, Duration = DesignTokens.MotionNormal, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var waveY = new DoubleAnimation { From = 0.6, To = 3.2, Duration = DesignTokens.MotionNormal, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var waveFade = new DoubleAnimation { From = 0.8, To = 0, Duration = DesignTokens.MotionNormal };
            Storyboard.SetTarget(waveX, haloTransform); Storyboard.SetTargetProperty(waveX, "ScaleX"); Storyboard.SetTarget(waveY, haloTransform); Storyboard.SetTargetProperty(waveY, "ScaleY"); Storyboard.SetTarget(waveFade, halo); Storyboard.SetTargetProperty(waveFade, "Opacity");
            var wave = new Storyboard(); wave.Children.Add(waveX); wave.Children.Add(waveY); wave.Children.Add(waveFade); wave.Begin();
        }
    }

    private Button ActionButton(string label, Func<Task> action)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 8, 14, 8), CornerRadius = new CornerRadius(10) };
        var executing=false;
        button.Click += async (_, _) =>
        {
            if(executing || _closed) return; executing=true;
            try { await RunUiActionAsync(label,action); }
            finally { executing=false; }
        };
        return button;
    }

    private async Task AddProfileAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop, ViewMode = PickerViewMode.List };
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        if (_settings.Profiles.All(p => !string.Equals(p.ExecutablePath, file.Path, StringComparison.OrdinalIgnoreCase)))
            _settings.Profiles.Add(new AppProfile(file.Path, Path.GetFileNameWithoutExtension(file.Name), false));
        await SaveNowAsync(); ShowPage("Aplicaciones");
        UpdateForegroundProfile(_foregroundExecutablePath);
    }

    private UIElement ProfileRow(AppProfile profile)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(ColorFromHex("#EAF3FF")), Child = Text("▣", 20, "#1683FF") };
        var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text(profile.DisplayName, 14, "#26354C", true), Text(profile.ExecutablePath, 10, "#8793A5") } };
        var mode = new ComboBox { ItemsSource = new[] { "Usar ajustes globales", "Perfil personalizado" }, SelectedIndex = profile.UseCustomSettings ? 1 : 0, MinWidth = 180 };
        mode.SelectionChanged += async (_, _) => await RunUiActionAsync("Cambiar perfil",async()=>{ var i = _settings.Profiles.FindIndex(p => p.ExecutablePath == profile.ExecutablePath); if (i >= 0) _settings.Profiles[i] = _settings.Profiles[i] with { UseCustomSettings = mode.SelectedIndex == 1 }; await SaveNowAsync(); UpdateForegroundProfile(_foregroundExecutablePath); });
        var remove = new Button { Content = "Quitar", Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(9) };
        remove.Click += async (_, _) => await RunUiActionAsync("Quitar perfil",async()=>{ _settings.Profiles.RemoveAll(p => p.ExecutablePath == profile.ExecutablePath); await SaveNowAsync(); if(_closed) return; UpdateForegroundProfile(_foregroundExecutablePath); ShowPage("Aplicaciones"); });
        row.Children.Add(icon); Grid.SetColumn(label, 1); row.Children.Add(label); Grid.SetColumn(mode, 2); row.Children.Add(mode); Grid.SetColumn(remove, 3); row.Children.Add(remove);
        var container = new StackPanel { Spacing = 12 };
        container.Children.Add(row); container.Children.Add(ActionButton("Editar ajustes de esta aplicación", () => EditProfileAsync(profile)));
        var card = Card(); card.Padding = new Thickness(14); card.Child = container; return card;
    }

    private async Task ExportSettingsAsync()
    {
        var picker = new FileSavePicker { SuggestedFileName = "magic-mouse-settings.json" };
        picker.FileTypeChoices.Add("JSON", [".json"]); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync(); if (file is null) return;
        await File.WriteAllTextAsync(file.Path, System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task ImportSettingsAsync()
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".json"); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        var imported = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(await File.ReadAllTextAsync(file.Path));
        if (imported is null) throw new InvalidDataException("El archivo no contiene una configuración válida.");
        imported.Validate();
        if (!await ShowSheetAsync("Importar configuración", "Se reemplazarán tus preferencias y perfiles por los del archivo seleccionado.", confirm: true, accept: "Importar")) return;
        _editingPreferences = null; _editingProfileName = null; _settings = imported; RefreshAppCursor(); ApplyTheme(_settings.Choices.GetValueOrDefault("app.theme","Claro")); EnsureTray(_settings.Toggles.GetValueOrDefault("app.tray")); await SaveNowAsync(); ShowPage("Avanzado");
        await ShowSheetAsync("Configuración importada", "Tus preferencias y perfiles se han guardado correctamente.");
    }

    private async Task ResetSettingsAsync()
    {
        if (!await ShowSheetAsync("Restablecer configuración", "Se borrarán tus preferencias y los perfiles guardados en esta aplicación. Los dispositivos y controladores de Windows no se modificarán.", confirm: true, accept: "Restablecer")) return;
        _saveDebounce?.Cancel(); if (_settings.Toggles.GetValueOrDefault("appearance.systemApplied")) SystemCursorService.Restore(); if (StartupService.IsEnabled()) StartupService.SetEnabled(false); EnsureTray(false); _inputConnected = false; _inputTimer?.Stop(); _editingPreferences = null; _editingProfileName = null; _settings = new UserSettings(); ApplyTheme("Claro"); RefreshAppCursor(); ResetInputEngine(); await _store.ResetAsync(); RebuildForTheme(); ShowPage("Avanzado");
    }

    private async void QueueSave()
    {
        if(_closed) return;
        _saveDebounce?.Cancel(); _saveDebounce?.Dispose();
        var cts = _saveDebounce = new CancellationTokenSource();
        try { await Task.Delay(300, cts.Token); await SaveNowAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await LogAsync($"Settings save failed: {ex.Message}"); }
    }

    private async Task SaveNowAsync()
    {
        await _store.SaveAsync(_settings); UpdateForegroundProfile(_foregroundExecutablePath); ResetInputEngine(); await LogAsync("Settings saved");
    }

    private async Task RefreshDevicesAsync() => await _discovery.RefreshAsync();

    private void OnDeviceChanged(DeviceInformation? mouse)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if(_closed) return;
            if (_currentMouseDevice?.Id != mouse?.Id) { _capture.Stop(); _inputConnected = false; ResetInputEngine(); UpdateBatteryIcon(MagicMouse.Core.Devices.BatteryReading.Unknown); }
            _currentMouseDevice = mouse;
            if (mouse is null) { _reconnectTimer?.Stop();_connectionRetry.Reset();_capture.Stop(); _inputConnected = false; _inputTimer?.Stop(); ResetInputEngine(); }
            DeviceStatusText.Text = mouse is null ? "Conecta tu Magic Mouse" : mouse.Name;
            DeviceDetailText.Text = mouse is null ? "No se encontró un Magic Mouse compatible." : "Magic Mouse HID detectado · reportes táctiles aún no verificados.";
            if (PageTitle.Text == "Avanzado") ShowPage("Avanzado");
            else UpdateCaptureButtons();
            await RefreshBatteryAsync();
            if(_closed) return;
            if (_currentMouseDevice?.Id != mouse?.Id) return;
            if (mouse is not null && !_inputConnected && _settings.Toggles.GetValueOrDefault("input.enabled"))
            {
                try { await ConnectInputCoreAsync(false); }
                catch(Exception exception) { StopInputOnFailure(exception); }
            }
        });
        if (mouse is not null) _ = LogAsync($"Magic Mouse HID candidate discovered: {mouse.Name}");
    }

    private readonly SemaphoreSlim _captureStartGate = new(1, 1);
    private Task StartHidCaptureAsync() => StartHidCaptureCoreAsync(false);

    private async Task StartHidCaptureCoreAsync(bool requireTouchReports)
    {
        await _captureStartGate.WaitAsync();
        try { if (!_closed) await StartHidCaptureAttemptAsync(requireTouchReports); }
        finally { _captureStartGate.Release(); }
    }

    private async Task StartHidCaptureAttemptAsync(bool requireTouchReports)
    {
        if (_currentMouseDevice is null)
        {
            if (_captureSummary is not null) _captureSummary.Text = "Conecta un Magic Mouse antes de iniciar la captura.";
            return;
        }
        try
        {
            _capture.Dispose();

            HidCaptureMetadata? metadata = null; Exception? lastFailure = null;
            foreach (var candidate in _discovery.Candidates.Prepend(_currentMouseDevice).DistinctBy(device => device.Id))
            {
                if (_closed) return;
                _capture.Dispose();
                var source = _capture = new HidReportCaptureService();
                AttachCaptureEvents(source);
                try
                {
                    metadata = await Task.Run(async () =>
                    {
                        var opened = await source.StartAsync(candidate);
                        if (requireTouchReports) source.RequestTouchReports();
                        return opened;
                    });
                    if (_closed || !ReferenceEquals(source, _capture)) { source.Dispose(); return; }
                    break;
                }
                catch(Exception exception) { metadata = null; lastFailure = exception; source.Dispose(); }
            }
            if (metadata is null) throw new InvalidOperationException("Windows no permitió leer las colecciones disponibles del Magic Mouse. Las colecciones mouse son exclusivas del controlador; la lectura táctil requiere una colección accesible o un controlador compatible.",lastFailure);
            if (_captureSummary is not null) _captureSummary.Text = $"VID {metadata.VendorId:X4} · PID {metadata.ProductId:X4} · Usage {metadata.UsagePage:X4}:{metadata.UsageId:X4}\n{CaptureDescription()}";
            await LogAsync("HID collection opened for local report capture");
            UpdateCaptureButtons();
        }
        catch (Exception ex)
        {
            if (_captureSummary is not null) _captureSummary.Text = $"Windows no permitió iniciar la captura: {ex.Message}";
            UpdateCaptureButtons();
            await LogAsync($"HID capture open failed: {ex.Message}");
        }
    }

    private async Task ExportCaptureAsync()
    {
        var picker = new FileSavePicker { SuggestedFileName = "magic-mouse-hid-capture.json" };
        picker.FileTypeChoices.Add("JSON", [".json"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file is not null) await _capture.ExportAsync(file.Path);
    }

    private void OnReportCaptured(HidReportCaptureService source,HidReportSnapshot report)
    {
        DispatcherQueue.TryEnqueue(() => {if(!_closed && ReferenceEquals(source,_capture)) ProcessInputReport(report);});
        var now = DateTimeOffset.UtcNow.Ticks;
        var previous = Interlocked.Read(ref _lastCaptureUiUpdateTicks);
        if (now - previous < TimeSpan.TicksPerSecond / 10) return;
        Interlocked.Exchange(ref _lastCaptureUiUpdateTicks, now);
        DispatcherQueue.TryEnqueue(() =>
        {
            if(_closed || !ReferenceEquals(source,_capture)) return;
            if (_captureSummary is not null) _captureSummary.Text = CaptureDescription() + $"\nÚltimo report ID {report.ReportId:X2} ({report.Length} bytes): {report.BytesHex}";
            UpdateCaptureButtons();
        });
    }

    private void OnCaptureFailed(HidReportCaptureService source,Exception exception)
    {
        DispatcherQueue.TryEnqueue(() => { if(_closed || !ReferenceEquals(source,_capture)) return; StopInputOnFailure(exception); if (_captureSummary is not null) _captureSummary.Text = $"La lectura HID se interrumpió: {exception.Message}"; UpdateCaptureButtons(); });
        _ = LogAsync($"HID report read failed: {exception.Message}");
    }

    private void OnCaptureStateChanged(bool isCapturing) => DispatcherQueue.TryEnqueue(UpdateCaptureButtons);

    private void UpdateForegroundProfile(string? executablePath)
    {
        if(_closed) return;
        _foregroundExecutablePath = executablePath;
        var resolved = ProfileResolver.Resolve(executablePath, _settings.Profiles);
        if (!string.Equals(resolved?.ExecutablePath, _foregroundProfile?.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            _ = LogAsync($"Profile resolved: {resolved?.DisplayName ?? "Global settings"}");
        _foregroundProfile = resolved;
        _activePreferences = ProfileResolver.EffectiveSettings(_settings, resolved);
        ResetInputEngine();
        if (_foregroundSummary is not null) _foregroundSummary.Text = ForegroundSummary();
    }

    private string ForegroundSummary()
    {
        if (string.IsNullOrWhiteSpace(_foregroundExecutablePath)) return "Aplicación activa: No disponible";
        var activeName = Path.GetFileName(_foregroundExecutablePath);
        return _foregroundProfile is null
            ? $"Aplicación activa: {activeName} · ajustes globales"
            : $"Aplicación activa: {activeName} · {(_foregroundProfile.UseCustomSettings ? "perfil personalizado seleccionado" : "ajustes globales seleccionados")}";
    }

    private Task StopHidCaptureAsync()
    {
        _capture.Stop();
        if (_captureSummary is not null) _captureSummary.Text = $"Captura detenida · {_capture.ReportCount} reportes retenidos";
        UpdateCaptureButtons();
        return Task.CompletedTask;
    }

    private string CaptureDescription()
    {
        var metadata = _capture.Metadata;
        if (metadata is null) return "Captura detenida · sin reportes";
        var reportIds = string.Join(", ", _capture.Snapshot().Select(report => report.ReportId.ToString("X2")).Distinct().Order());
        return $"{metadata.DeviceName}\nVID {metadata.VendorId:X4} · PID {metadata.ProductId:X4} · versión HID {metadata.Version:X4}\nUsage {metadata.UsagePage:X4}:{metadata.UsageId:X4}\nEntrada: {metadata.InputReportByteLength} · salida: {metadata.OutputReportByteLength} · feature: {metadata.FeatureReportByteLength} bytes\nRuta: {metadata.DevicePath}\nIDs observados: {(reportIds.Length == 0 ? "ninguno" : reportIds)} · {_capture.ReportCount} reportes · {_capture.ReportsPerSecond:0} reportes/s";
    }

    private async Task LogAsync(string message)
    {
        try { Directory.CreateDirectory(_logsPath); await File.AppendAllTextAsync(Path.Combine(_logsPath, "app.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
        catch { /* Diagnostics must not interrupt input or UI. */ }
    }

    private static global::Windows.UI.Color ColorFromHex(string hex)
    {
        var value = DesignTokens.ThemeColor(hex).TrimStart('#');
        if (value.Length == 8) return global::Windows.UI.Color.FromArgb(Convert.ToByte(value[..2], 16), Convert.ToByte(value[2..4], 16), Convert.ToByte(value[4..6], 16), Convert.ToByte(value[6..8], 16));
        return global::Windows.UI.Color.FromArgb(255, Convert.ToByte(value[..2], 16), Convert.ToByte(value[2..4], 16), Convert.ToByte(value[4..6], 16));
    }
}
