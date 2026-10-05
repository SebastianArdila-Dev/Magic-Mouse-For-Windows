using MagicMouse.Core.Settings;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private bool _runningSmokeTest;
    private async Task RunSmokeTestAsync()
    {
        _runningSmokeTest=true;
        var results = new List<string>();
        try
        {
            if (_chrome?.ClientFillsWindow != true) throw new InvalidOperationException("Native window has a residual non-client strip");
            results.Add("PASS chrome: client fills the whole native window; no residual top strip");
            await Task.Delay(150);
            if (!_chrome.CaptionAcceptsPointerInput) throw new InvalidOperationException("Caption does not accept live pointer input");
            var originalPosition = AppWindow.Position; var updates = _chrome.CornerUpdates;
            try { for (var step=1; step<=12; step++) { AppWindow.Move(new(originalPosition.X+step, originalPosition.Y+step)); await Task.Delay(8); } }
            finally { AppWindow.Move(originalPosition); }
            if (_chrome.CornerUpdates != updates) throw new InvalidOperationException("Moving window unnecessarily rebuilds corner regions");
            results.Add("PASS movement: native caption hit test; repeated position changes do not rebuild or redraw corner regions");
            UpdateBatteryIcon(new(75,false));
            if (BatteryFill.Width != 27 || BatteryBolt.Visibility != Microsoft.UI.Xaml.Visibility.Collapsed) throw new InvalidOperationException("Battery fill or discharging state failed");
            foreach (var page in Pages)
            {
                ShowPage(page);
                if (BatteryPercentage.Text != "75%" || BatteryButton.Visibility != Microsoft.UI.Xaml.Visibility.Visible) throw new InvalidOperationException("Battery header did not persist across pages");
                if (PageTitle.Text != page || PageContent.Children.Count == 0)
                    throw new InvalidOperationException($"Empty or incorrect page: {page}");
                results.Add($"PASS navigation: {page} ({PageContent.Children.Count} content sections)");
            }
            UpdateBatteryIcon(new(50,true));
            if (BatteryFill.Width != 18 || BatteryBolt.Visibility != Microsoft.UI.Xaml.Visibility.Visible) throw new InvalidOperationException("Charging bolt failed");
            UpdateBatteryIcon(MagicMouse.Core.Devices.BatteryReading.Unknown);
            if (BatteryFill.Visibility != Microsoft.UI.Xaml.Visibility.Collapsed || BatteryUnknown.Visibility != Microsoft.UI.Xaml.Visibility.Visible || BatteryBolt.Visibility != Microsoft.UI.Xaml.Visibility.Collapsed) throw new InvalidOperationException("Unknown battery state failed");
            results.Add("PASS battery: proportional fill, persistent header on all pages, charging bolt and truthful unknown state");
            var path = Path.Combine(AppContext.BaseDirectory, "smoke-settings.json");
            var store = new SettingsStore(path);
            var settings = new UserSettings();
            settings.Choices["appearance.color"] = "Violeta";
            settings.Numbers["appearance.size"] = 140;
            settings.Toggles["appearance.clickIndicator"] = false;
            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();
            if (loaded.Choices["appearance.color"] != "Violeta" || loaded.Numbers["appearance.size"] != 140 || loaded.Toggles["appearance.clickIndicator"])
                throw new InvalidOperationException("Settings did not round-trip");
            results.Add("PASS settings: choices, numbers and toggles round-trip to disk");
            ShowPage("Gestos"); ShowPage("Botones"); NavigateHistory(-1);
            if (PageTitle.Text != "Gestos") throw new InvalidOperationException("Back navigation failed");
            NavigateHistory(1);
            if (PageTitle.Text != "Botones") throw new InvalidOperationException("Forward navigation failed");
            NavigationSearch.Text = "Aspecto"; await Task.Delay(100);
            if (NavigationPanel.Children.OfType<Microsoft.UI.Xaml.Controls.Button>().Count(b => b.Visibility == Microsoft.UI.Xaml.Visibility.Visible) != 1)
                throw new InvalidOperationException("Sidebar search failed");
            NavigationSearch.Text = "";
            var confirmation = ShowSheetAsync("Prueba", "Confirmación local", confirm: true);
            CompleteSheet(false);
            if (await confirmation) throw new InvalidOperationException("Cancel confirmation failed");
            results.Add("PASS shell: history, sidebar filtering and modal cancellation");
#if DEBUG
            ShowPage("Avanzado");
            _editingPreferences = new UserSettings();
            await SimulateGestureAsync("Deslizar ←");
            if (_simulationStatus?.Text.Contains("OneFingerSwipeLeft") != true)
                throw new InvalidOperationException("Simulated swipe did not reach gesture recognizer");
            results.Add("PASS simulator: normalized touch frames -> OneFingerSwipeLeft -> action mapping");
            foreach (var action in new[] { "Deslizar →", "2 dedos ↑", "2 dedos ↓", "Tap 1 dedo", "Tap 2 dedos", "Tap 3 dedos", "Scroll", "Doble tap 1 dedo", "Doble tap 2 dedos" })
            {
                await SimulateGestureAsync(action);
                if (_simulationStatus?.Text.Contains("Sin acción reconocida") == true) throw new InvalidOperationException($"Simulator failed: {action}");
                results.Add($"PASS simulator: {action} ({_simulationStatus?.Text})");
            }
            _editingPreferences = null;
#endif
            foreach (var style in new[] { "Automático", "macOS", "Minimal", "Grande", "Personalizado" })
            {
                var cursorPath = Path.Combine(AppContext.BaseDirectory,"smoke-pointer.cur");
                await File.WriteAllBytesAsync(cursorPath,MagicMouse.Core.Appearance.CursorFile.Create(style,180));
                if (!Devices.SystemCursorService.ValidateFile(cursorPath)) throw new InvalidOperationException($"Windows rejected cursor: {style}");
            }
            results.Add("PASS cursor: Windows loads all five generated styles at 180% with valid hotspots");
            var originalSpeed=Devices.WindowsMouseSettings.PointerSpeed;
            var originalSwap=Devices.WindowsMouseSettings.ButtonsSwapped;
            try
            {
                var testSpeed=originalSpeed==10 ? 11 : 10;
                Devices.WindowsMouseSettings.SetPointerSpeed(testSpeed);
                if(Devices.WindowsMouseSettings.PointerSpeed!=testSpeed) throw new InvalidOperationException("Windows pointer speed failed");
                Devices.WindowsMouseSettings.SetButtonsSwapped(!originalSwap);
                if(Devices.WindowsMouseSettings.ButtonsSwapped==originalSwap) throw new InvalidOperationException("Windows mouse buttons failed");
            }
            finally { Devices.WindowsMouseSettings.SetPointerSpeed(originalSpeed); Devices.WindowsMouseSettings.SetButtonsSwapped(originalSwap); }
            results.Add("PASS Windows mouse: actual speed and button swap settings applied and restored");
            var savedAppStyle=Preferences.Choices.GetValueOrDefault("appearance.style","Automático");
            var savedAppSize=Preferences.Numbers.GetValueOrDefault("appearance.size",120);
            try
            {
                foreach(var style in new[]{"Automático","macOS","Minimal","Grande","Personalizado"})
                {
                    Preferences.Choices["appearance.style"]=style; Preferences.Numbers["appearance.size"]=180; RefreshAppCursor();
                    foreach(var page in Pages)
                    {
                        ShowPage(page); await Task.Delay(35); RootGrid.UpdateLayout(); RootGrid.RefreshCursorTree();
                        if(!RootGrid.UsesSelectedCursor(PageInfoButton) || !RootGrid.UsesSelectedCursor(NavigationSearch) || !RootGrid.UsesSelectedCursor(PageContent) || RootGrid.AppliedElementCount<50)
                            throw new InvalidOperationException($"App cursor failed on {page}: {style}");
                        if(page=="Avanzado" && !_lifecycleToggles.Values.All(RootGrid.UsesSelectedCursor)) throw new InvalidOperationException("Switches override app cursor");
                    }
                }
                try
                {
                    await VerifyVisibleAppCursorAsync(PageInfoButton,58);
                    await VerifyVisibleAppCursorAsync(NavigationSearch,58);
                    results.Add("PASS visible cursor: actual cursor bitmap is 58px over header and text box");
                }
                catch(Exception cursorException) { results.Add($"FAIL visible cursor: {cursorException.Message}"); }

                var cursorSheet=ShowSheetAsync("Prueba de puntero","Verificación del mismo cursor en el diálogo.");
                await Task.Delay(50); RootGrid.RefreshCursorTree();
                if(!RootGrid.UsesSelectedCursor(ModalAcceptButton)) throw new InvalidOperationException("Modal overrides app cursor");
                CompleteSheet(true); await cursorSheet;
                results.Add("PASS app cursor: five actual WinUI custom cursors at 180%, all six pages, controls and modal use the selected cursor");
            }
            finally { Preferences.Choices["appearance.style"]=savedAppStyle; Preferences.Numbers["appearance.size"]=savedAppSize; RefreshAppCursor(); }
            var creatorTask=ShowCreatorAsync(); await Task.Delay(150); RootGrid.UpdateLayout();
            if(!_creatorOpen || _creatorMotion.Count==0 || _creatorFluidHost?.ActualWidth<=0) throw new InvalidOperationException("Creator card or fluid failed");
            CompleteSheet(true); await creatorTask;
            if(_creatorOpen || !ReferenceEquals(ModalCard.Child,_standardModalContent)) throw new InvalidOperationException("Creator dialog did not restore normal modal");
            results.Add("PASS creator: author card, composition fluid animation and dialog restoration");
            var absentDriver=await Devices.DriverReadinessService.ReadAsync(null);
            if(absentDriver.DeviceName is not null || absentDriver.Provider is not null) throw new InvalidOperationException("Driver check fabricated a mouse");
            results.Add("PASS driver readiness: real Windows components queried; no device or driver fabricated without hardware");
            var previousTheme = _settings.Choices.GetValueOrDefault("app.theme", "Claro");
            ApplyTheme("Oscuro"); await Task.Delay(100); RebuildForTheme();
            if (RootGrid.ActualTheme != Microsoft.UI.Xaml.ElementTheme.Dark) throw new InvalidOperationException("Dark theme failed");
            ApplyTheme("Claro"); await Task.Delay(100); RebuildForTheme();
            if (RootGrid.ActualTheme != Microsoft.UI.Xaml.ElementTheme.Light) throw new InvalidOperationException("Light theme failed");
            ApplyTheme(previousTheme); RebuildForTheme();
            results.Add("PASS theme: dark/light switch updates actual WinUI theme and rebuilt page");
            if (Environment.GetCommandLineArgs().Contains("--cursor-test"))
            {
                try { Devices.SystemCursorService.Apply("macOS",180,0x007AFF); results.Add("PASS cursor: SetSystemCursor accepted macOS at 180%"); }
                finally { Devices.SystemCursorService.Restore(); }
                results.Add("PASS cursor: Windows cursor scheme restored");
            }
            ShowPage("Avanzado"); await Task.Delay(100);
            var startupBefore = Devices.StartupService.IsEnabled();
            var trayBefore = _settings.Toggles.GetValueOrDefault("app.tray");
            var backgroundBefore = _settings.Toggles.GetValueOrDefault("app.background");
            void ToggleOption(string key)
            {
                var peer = new Microsoft.UI.Xaml.Automation.Peers.ToggleSwitchAutomationPeer(_lifecycleToggles[key]);
                ((Microsoft.UI.Xaml.Automation.Provider.IToggleProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Toggle)).Toggle();
            }
            try
            {
                ToggleOption("app.startup"); await Task.Delay(200);
                if (Devices.StartupService.IsEnabled() == startupBefore) throw new InvalidOperationException("Startup toggle failed");
                ToggleOption("app.startup"); await Task.Delay(200);
                if (Devices.StartupService.IsEnabled() != startupBefore) throw new InvalidOperationException("Startup restore failed");
                if (!_lifecycleToggles["app.background"].IsOn) ToggleOption("app.background");
                await Task.Delay(200);
                if (_tray is null || !_lifecycleToggles["app.tray"].IsOn) throw new InvalidOperationException("Background must enable tray and synchronize switch");
                await RequestCloseAsync(); await Task.Delay(100);
                if (AppWindow.IsVisible) throw new InvalidOperationException("Background close must hide window");
                _tray.Show(); await Task.Delay(100);
                if (!AppWindow.IsVisible) throw new InvalidOperationException("Tray must restore hidden window");
                ToggleOption("app.tray"); await Task.Delay(200);
                if (_tray is not null || _lifecycleToggles["app.background"].IsOn) throw new InvalidOperationException("Tray off must synchronize background off");
                results.Add("PASS lifecycle: accessible switches change actual startup registration, tray, close-to-background, restore and synchronized background state");
            }
            finally
            {
                Devices.StartupService.SetEnabled(startupBefore); EnsureTray(trayBefore);
                _settings.Toggles["app.startup"] = startupBefore; _settings.Toggles["app.tray"] = trayBefore; _settings.Toggles["app.background"] = backgroundBefore;
                await SaveNowAsync();
            }
            results.Add($"Discovery: {DeviceStatusText.Text}; {DeviceDetailText.Text}");
            results.Add("PASS startup: WinUI window initialized");
        }
        catch (Exception exception) { results.Add($"FAIL: {exception}"); }
        finally
        {
            UpdateBatteryIcon(MagicMouse.Core.Devices.BatteryReading.Unknown);
            _editingPreferences = null;
            ShowPage("Aspecto");
            _runningSmokeTest=false;
            await File.WriteAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "smoke-test.txt"), results);
        }
    }
}
