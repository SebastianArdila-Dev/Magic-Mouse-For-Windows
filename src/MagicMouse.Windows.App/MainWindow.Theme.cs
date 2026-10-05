using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MagicMouse.Windows.App.Devices;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private bool _themeInitialized;
    private readonly global::Windows.UI.ViewManagement.UISettings _systemUiSettings = new();
    private void ApplyTheme(string mode)
    {
        var background = _systemUiSettings.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background);
        RootGrid.RequestedTheme = mode switch { "Oscuro" => ElementTheme.Dark, "Claro" => ElementTheme.Light, _ => background.R + background.G + background.B < 384 ? ElementTheme.Dark : ElementTheme.Light };
        DesignTokens.IsDark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (!_themeInitialized)
        {
            _themeInitialized = true; RootGrid.ActualThemeChanged += OnActualThemeChanged;
            _systemUiSettings.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(() => { if(!_closed && _settings.Choices.GetValueOrDefault("app.theme") == "Sistema") { ApplyTheme("Sistema"); RebuildForTheme(); } });
        }
    }
    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if(_closed) return;
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (dark == DesignTokens.IsDark) return;
        DesignTokens.IsDark = dark; RebuildForTheme();
    }
    private void RebuildForTheme()
    {
        if(_closed) return;
        var page = PageTitle.Text; NavigationPanel.Children.Clear(); foreach (var item in Pages) AddNavigation(item); ShowPage(page);
    }
    private UIElement AppearanceThemeCard()
    {
        var row = new Grid { ColumnSpacing = 14 };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Text("Apariencia de la aplicación",12,DesignTokens.BodyText));
        var choice = new ComboBox { ItemsSource = new[] { "Claro", "Oscuro", "Sistema" }, SelectedItem = _settings.Choices.GetValueOrDefault("app.theme","Claro"), Width = 150 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(choice,"Tema de la aplicación");
        choice.SelectionChanged += (_,_) => { if (choice.SelectedItem is string mode) { _settings.Choices["app.theme"] = mode; ApplyTheme(mode); DispatcherQueue.TryEnqueue(RebuildForTheme); QueueSave(); } };
        Grid.SetColumn(choice,1); row.Children.Add(choice); return row;
    }
    private void RefreshAppCursor()
    {
        var style=Preferences.Choices.GetValueOrDefault("appearance.style","Automático");
        var size=Math.Clamp(Preferences.Numbers.GetValueOrDefault("appearance.size",120),70,180);
        var accent=AccentColor(Preferences.Choices.GetValueOrDefault("appearance.color","Azul"));
        RootGrid.ChangeCursor(AppCursorService.Create(style,size,accent));
        _chrome?.SetAppCursor(style,size,accent);
        if(_editingPreferences is null && _settings.Toggles.GetValueOrDefault("appearance.systemApplied"))
            SystemCursorService.Apply(style,size,accent);
    }
    private void RestoreSavedSystemCursor()
    {
        if (!_settings.Toggles.GetValueOrDefault("appearance.systemApplied")) return;
        SystemCursorService.Apply(_settings.Choices.GetValueOrDefault("appearance.style","Automático"),_settings.Numbers.GetValueOrDefault("appearance.size",120),AccentColor(_settings.Choices.GetValueOrDefault("appearance.color","Azul")));
    }
    private static uint AccentColor(string color) => color switch { "Violeta" => 0x8050E8u, "Rosa" => 0xEF5276u, "Naranja" => 0xFF8B33u, "Amarillo" => 0xF4BF20u, "Verde" => 0x36B875u, "Gris" => 0x9AA5B4u, _ => 0x007AFFu };
    private async Task ApplySystemCursorAsync()
    {
        var color = AccentColor(Preferences.Choices.GetValueOrDefault("appearance.color", "Azul"));
        SystemCursorService.Apply(Preferences.Choices.GetValueOrDefault("appearance.style", "Automático"), Preferences.Numbers.GetValueOrDefault("appearance.size", 120), color);
        RefreshAppCursor();
        _settings.Toggles["appearance.systemApplied"] = true; await SaveNowAsync();
        await ShowSheetAsync("Puntero aplicado a Windows", "El estilo y tamaño se aplicaron al puntero de selección normal de todo Windows. Los cursores de texto, enlaces y redimensionado conservan su función. Puedes restaurar el esquema de Windows desde aquí.");
    }
}
