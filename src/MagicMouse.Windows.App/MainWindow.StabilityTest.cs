using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private async Task RunStabilityTestAsync()
    {
        var results=new List<string>();
        var originalTheme=_settings.Choices.GetValueOrDefault("app.theme","Claro");
        try
        {
            var peer=new ButtonAutomationPeer(BrandButton);
            var invoke=(IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
            for(var cycle=0;cycle<100;cycle++)
            {
                ShowPage(Pages[cycle%Pages.Length]);
                if(cycle%10==0) { ApplyTheme(cycle%20==0 ? "Oscuro":"Claro"); RebuildForTheme(); }
                invoke.Invoke(); await Task.Delay(80); RootGrid.UpdateLayout(); await Task.Delay(20);
                if(!_creatorOpen || _creatorMotion.Count==0 || _creatorFluidHost is null) throw new InvalidOperationException($"Creator failed to open on cycle {cycle}");
                var oldHost=_creatorFluidHost;
                // Double-clicking the identity must reuse the current card.
                await ShowCreatorAsync();
                if(!ReferenceEquals(oldHost,_creatorFluidHost)) throw new InvalidOperationException("Duplicate creator card");
                var donePeer=new ButtonAutomationPeer(_creatorPrimaryButton!);
                ((IInvokeProvider)donePeer.GetPattern(PatternInterface.Invoke)).Invoke();
                await Task.Delay(20); RootGrid.UpdateLayout();
                if(_creatorOpen || _creatorMotion.Count!=0 || _creatorFluidHost is not null || ModalScrim.Visibility!=Visibility.Collapsed) throw new InvalidOperationException($"Creator did not fully close on cycle {cycle}");
                // A stale layout callback may arrive after close or after reopening.
                var reopen=ShowCreatorAsync(); RootGrid.UpdateLayout();
                var before=_creatorMotion.Count; StartCreatorFluid(oldHost);
                if(_creatorMotion.Count!=before) throw new InvalidOperationException("Stale creator callback changed the new animation");
                CompleteSheet(false); await reopen;
                var sheet=ShowSheetAsync("Información","Prueba de cierre y restauración del diálogo.");
                RootGrid.UpdateLayout(); CompleteSheet(cycle%2==0); await sheet;
            }
            results.Add("PASS: 100 real logo/button invocations; 200 creator cards opened/closed; duplicate clicks and stale layout callbacks ignored");
            results.Add("PASS: all six pages, 100 regular dialogs and 10 theme changes during repeated creator interaction");
            var originalSize=AppWindow.Size;
            for(var step=0;step<=3;step++)
            {
                ShowIntroduction(step);RootGrid.UpdateLayout();await Task.Delay(50);
                if(AppWindow.Size.Width>=originalSize.Width || RootGrid.Children.Take(2).Any(item=>item.Visibility!=Visibility.Collapsed)) throw new InvalidOperationException("Setup is not compact or exposes the main shell");
                CloseIntroduction(step==3);await Task.Delay(step==3 ? 550:30);
                if(AppWindow.Size!=originalSize || RootGrid.Children.Take(2).Any(item=>item.Visibility!=Visibility.Visible || item.Opacity!=1)) throw new InvalidOperationException("Setup does not restore the main window after fade");
            }
            if(RootGrid.Children.OfType<FrameworkElement>().Any(item=>Equals(item.Tag,"introductionTraffic"))) throw new InvalidOperationException("Introduction leaked traffic controls");
            results.Add("PASS: compact setup, hidden main shell, restored window bounds, completed fade and no leftover controls");
        }
        catch(Exception exception) {results.Add($"FAIL: {exception}");}
        finally
        {
            CompleteSheet(false); CloseIntroduction();ApplyTheme(originalTheme);RebuildForTheme();ShowPage("Aspecto");
            await File.WriteAllLinesAsync(Path.Combine(AppContext.BaseDirectory,"stability-test.txt"),results);
        }
    }
}
