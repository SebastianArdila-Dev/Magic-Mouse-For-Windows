using MagicMouse.Core.Devices;
using MagicMouse.Core.Settings;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class ConnectionRetryTests
{
    [Fact]
    public void Repeated_failures_back_off_and_remain_bounded()
    {
        var policy=new ConnectionRetryPolicy();
        Assert.Equal(new double[]{3,6,12,24,30},Enumerable.Range(0,5).Select(_=>policy.NextDelay().TotalSeconds));
        for(var attempt=0;attempt<100;attempt++) Assert.Equal(TimeSpan.FromSeconds(30),policy.NextDelay());
        policy.Reset();Assert.Equal(TimeSpan.FromSeconds(3),policy.NextDelay());
    }
    [Fact]
    public async Task Background_and_device_preferences_survive_restart()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try
        {
            var settings=new UserSettings();settings.Toggles["app.background"]=true;settings.Toggles["app.tray"]=true;
            settings.Toggles["input.enabled"]=true;settings.Choices["gesture:Tap con 2 dedos"]="Clic secundario";
            await new SettingsStore(path).SaveAsync(settings);
            var loaded=await new SettingsStore(path).LoadAsync();
            Assert.True(loaded.Toggles["app.background"]);Assert.True(loaded.Toggles["app.tray"]);Assert.True(loaded.Toggles["input.enabled"]);
            Assert.Equal("Clic secundario",loaded.Choices["gesture:Tap con 2 dedos"]);
        }
        finally {if(File.Exists(path)) File.Delete(path);}
    }
}
