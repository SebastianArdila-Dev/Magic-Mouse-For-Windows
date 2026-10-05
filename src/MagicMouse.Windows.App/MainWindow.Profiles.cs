using MagicMouse.Core.Settings;
using Microsoft.UI.Xaml.Controls;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private UserSettings? _editingPreferences;
    private string? _editingProfileName;
    private UserSettings Preferences => _editingPreferences ?? _settings;
    private UserSettings? _activePreferences;

    private Task EditProfileAsync(AppProfile profile)
    {
        var index = _settings.Profiles.FindIndex(item => item.ExecutablePath == profile.ExecutablePath);
        if (index < 0) return Task.CompletedTask;
        var current = _settings.Profiles[index];
        var overrides = current.Overrides ?? new SettingsOverrides
        {
            Numbers = new(_settings.Numbers), Toggles = new(_settings.Toggles), Choices = new(_settings.Choices)
        };
        _settings.Profiles[index] = current with { UseCustomSettings = true, Overrides = overrides };
        _editingPreferences = new UserSettings { Numbers = overrides.Numbers, Toggles = overrides.Toggles, Choices = overrides.Choices };
        _editingProfileName = profile.DisplayName;
        QueueSave(); ShowPage("Gestos");
        return Task.CompletedTask;
    }

    private void AddProfileEditingBanner()
    {
        if (_editingPreferences is null) return;
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text($"Editando el perfil de {_editingProfileName}", 13, DesignTokens.Accent, true));
        panel.Children.Add(ActionButton("Finalizar edición del perfil", async () => { await SaveNowAsync(); _editingPreferences = null; _editingProfileName = null; ShowPage("Aplicaciones"); }));
        var card = Card(); card.Child = panel; PageContent.Children.Add(card);
    }
}
