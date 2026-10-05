using MagicMouse.Core.Settings;
using MagicMouse.Core.Gestures;
using MagicMouse.Core.Touch;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class RegressionTests
{
    [Fact]
    public void Movement_that_returns_to_origin_is_not_a_tap()
    {
        var start = DateTimeOffset.UtcNow;
        var recognizer = new GestureRecognizer();
        recognizer.Process(new TouchFrame(start, [new TouchContact(1, .3, .5)]));
        recognizer.Process(new TouchFrame(start.AddMilliseconds(60), [new TouchContact(1, .8, .5)]));
        recognizer.Process(new TouchFrame(start.AddMilliseconds(120), [new TouchContact(1, .3, .5)]));
        Assert.Null(recognizer.Process(new TouchFrame(start.AddMilliseconds(180), [])));
    }

    [Fact]
    public void Custom_profile_overrides_only_its_values_without_changing_global_settings()
    {
        var global = new UserSettings();
        global.Numbers["scroll.speed"] = 8;
        global.Toggles["scroll.enabled"] = true;
        var overrides = new SettingsOverrides();
        overrides.Numbers["scroll.speed"] = 14;
        var profile = new AppProfile("C:\\Apps\\Editor.exe", "Editor", true, overrides);
        var effective = ProfileResolver.EffectiveSettings(global, profile);
        Assert.Equal(14, effective.Numbers["scroll.speed"]);
        Assert.True(effective.Toggles["scroll.enabled"]);
        Assert.Equal(8, global.Numbers["scroll.speed"]);
        Assert.Equal(8, ProfileResolver.EffectiveSettings(global, profile with { UseCustomSettings = false }).Numbers["scroll.speed"]);
    }

    [Fact]
    public async Task Profile_overrides_survive_disk_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var overrides = new SettingsOverrides(); overrides.Choices["gesture:Tap con 2 dedos"] = "Clic secundario";
            var settings = new UserSettings(); settings.Profiles.Add(new AppProfile("C:\\Editor.exe", "Editor", true, overrides));
            var store = new SettingsStore(path); await store.SaveAsync(settings);
            Assert.Equal("Clic secundario", (await store.LoadAsync()).Profiles[0].Overrides!.Choices["gesture:Tap con 2 dedos"]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Invalid_configuration_is_rejected_before_replacing_valid_settings()
    {
        Assert.Throws<InvalidDataException>(() => new UserSettings { Choices = null! }.Validate());
        Assert.Throws<InvalidDataException>(() => new UserSettings { SettingsSchemaVersion = 900 }.Validate());
        var settings = new UserSettings(); settings.Numbers["pointer.speed"] = double.NaN;
        Assert.Throws<InvalidDataException>(() => settings.Validate());
    }
}
