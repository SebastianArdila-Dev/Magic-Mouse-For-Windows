using MagicMouse.Core.Settings;
using Xunit;

namespace MagicMouse.Core.Tests;

public class SettingsTests
{
    [Fact]
    public async Task Settings_round_trip_through_json_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(Path.Combine(folder, "settings.json"));
        var settings = new UserSettings();
        settings.Toggles["scroll.enabled"] = true;
        settings.Profiles.Add(new AppProfile("C:\\Apps\\Editor.exe", "Editor", true));

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal(1, loaded.SettingsSchemaVersion);
        Assert.True(loaded.Toggles["scroll.enabled"]);
        Assert.Equal("Editor", Assert.Single(loaded.Profiles).DisplayName);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void Profile_resolves_case_insensitive_executable_path()
    {
        var profile = new AppProfile("C:\\Apps\\Editor.exe", "Editor", true);

        Assert.Same(profile, ProfileResolver.Resolve("c:\\apps\\editor.EXE", [profile]));
        Assert.Null(ProfileResolver.Resolve(null, [profile]));
    }
}
