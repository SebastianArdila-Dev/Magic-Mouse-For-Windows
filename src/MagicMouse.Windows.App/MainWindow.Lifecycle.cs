using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MagicMouse.Windows.App.Devices;
using Microsoft.UI.Xaml.Automation;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private TrayService? _tray;
    private bool _forceExit;
    private readonly Dictionary<string, ToggleSwitch> _lifecycleToggles = [];
    private bool _syncingLifecycle;

    private void EnsureTray(bool enabled)
    {
        if (enabled && _tray is null)
        {
            _tray = new TrayService(this);
            _tray.ExitRequested += async () => { _forceExit = true; await SaveNowAsync(); Close(); };
        }
        if (!enabled) { _tray?.Dispose(); _tray = null; }
    }
    private async Task RequestCloseAsync()
    {
        await SaveNowAsync();
        if (!_forceExit && _settings.Toggles.GetValueOrDefault("app.background"))
        { EnsureTray(true); AppWindow.Hide(); }
        else Close();
    }
    private void InitializeLifecycle()
    {
        EnsureTray(_settings.Toggles.GetValueOrDefault("app.tray"));
        AppWindow.Closing += (_, e) =>
        {
            if (!_forceExit && _tray is not null && _settings.Toggles.GetValueOrDefault("app.background"))
            {
                e.Cancel = true; AppWindow.Hide();
            }
        };
        if (Environment.GetCommandLineArgs().Contains("--background"))
        {
            EnsureTray(true); AppWindow.Hide();
        }
    }
    private UIElement LifecycleToggle(string key, string label, bool initial, Action<bool> apply)
    {
        var toggle = new ToggleSwitch { IsOn = initial, Style = (Style)Application.Current.Resources["CompactSwitch"], HorizontalAlignment = HorizontalAlignment.Right };
        _settings.Toggles[key] = initial;
        _lifecycleToggles[key] = toggle;
        AutomationProperties.SetName(toggle, label);
        var changing = false;
        toggle.Toggled += async (_, _) =>
        {
            if (changing || _syncingLifecycle) return;
            try
            {
                apply(toggle.IsOn); _settings.Toggles[key] = toggle.IsOn;
                _syncingLifecycle = true;
                try { foreach (var item in _lifecycleToggles) item.Value.IsOn = _settings.Toggles.GetValueOrDefault(item.Key); }
                finally { _syncingLifecycle = false; }
                await SaveNowAsync();
            }
            catch (Exception exception) { changing = true; toggle.IsOn = !toggle.IsOn; changing = false; await ShowSheetAsync("No se pudo cambiar la opción", exception.Message); }
        };
        var row = new Grid { MinHeight = 44, ColumnSpacing = 20 };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = Text(label, 13, DesignTokens.BodyText); text.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(text); Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
        return row;
    }
}
