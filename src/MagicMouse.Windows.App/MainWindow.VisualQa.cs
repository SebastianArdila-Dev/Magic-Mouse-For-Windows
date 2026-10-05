using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private async Task RenderVisualQaAsync()
    {
        var previousTheme = _settings.Choices.GetValueOrDefault("app.theme","Claro");
        var dark = Environment.GetCommandLineArgs().Contains("--visual-dark");
        _settings.Choices["app.theme"] = dark ? "Oscuro" : "Claro";
        ApplyTheme(_settings.Choices["app.theme"]); RebuildForTheme();
        var folderPath = Path.Combine(AppContext.BaseDirectory, dark ? "visual-qa-dark" : "visual-qa");
        Directory.CreateDirectory(folderPath);
        var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
        for (var index = 0; index < Pages.Length; index++)
        {
            ShowPage(Pages[index]);
            await Task.Delay(350);
            RootGrid.UpdateLayout();
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(RootGrid);
            var pixels = await bitmap.GetPixelsAsync();
            var file = await folder.CreateFileAsync($"{index + 1:00}.png", CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
            await encoder.FlushAsync();
        }
        ShowPage("Aspecto");
        if (Environment.GetCommandLineArgs().Contains("--visual-battery"))
        {
            var previousReading = _battery;
            foreach (var (name, reading) in new[] { ("08-battery-discharging",new MagicMouse.Core.Devices.BatteryReading(25,false)),("09-battery-charging",new MagicMouse.Core.Devices.BatteryReading(50,true)) })
            {
                UpdateBatteryIcon(reading);
                PageSubtitle.Text = "Muestra visual de prueba · batería simulada para verificar el icono. No es un dato del dispositivo.";
                RootGrid.UpdateLayout(); await Task.Delay(100);
                var batteryBitmap = new RenderTargetBitmap(); await batteryBitmap.RenderAsync(RootGrid);
                var batteryPixels = await batteryBitmap.GetPixelsAsync();
                var batteryFile = await folder.CreateFileAsync(name+".png",CreationCollisionOption.ReplaceExisting);
                using var batteryStream = await batteryFile.OpenAsync(FileAccessMode.ReadWrite);
                var batteryEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,batteryStream);
                batteryEncoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)batteryBitmap.PixelWidth,(uint)batteryBitmap.PixelHeight,96,96,batteryPixels.ToArray());
                await batteryEncoder.FlushAsync();
            }
            UpdateBatteryIcon(previousReading); ShowPage("Aspecto");
        }
        var sheet = ShowSheetAsync("Acerca de tu Magic Mouse", "Este es el diálogo de información de la aplicación. Los datos de batería, firmware y serial aparecerán cuando el dispositivo los proporcione.", hero: true);
        await Task.Delay(250);
        var dialogBitmap = new RenderTargetBitmap(); await dialogBitmap.RenderAsync(RootGrid);
        var dialogPixels = await dialogBitmap.GetPixelsAsync();
        var dialogFile = await folder.CreateFileAsync("07-dialog.png", CreationCollisionOption.ReplaceExisting);
        using var dialogStream = await dialogFile.OpenAsync(FileAccessMode.ReadWrite);
        var dialogEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, dialogStream);
        dialogEncoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)dialogBitmap.PixelWidth, (uint)dialogBitmap.PixelHeight, 96, 96, dialogPixels.ToArray());
        await dialogEncoder.FlushAsync(); CompleteSheet(true); await sheet;
        if(Environment.GetCommandLineArgs().Contains("--visual-creator"))
        {
            var creator=ShowCreatorAsync(); await Task.Delay(500); RootGrid.UpdateLayout();
            await SaveQaFrameAsync(folder,"10-creator.png");
            await Task.Delay(1500); await SaveQaFrameAsync(folder,"11-creator-fluid.png");
            CompleteSheet(true); await creator;
        }
        if(Environment.GetCommandLineArgs().Contains("--visual-introduction"))
        {
            for(var step=0;step<=3;step++)
            {
                ShowIntroduction(step); await Task.Delay(350);RootGrid.UpdateLayout();
                await SaveQaFrameAsync(folder,$"{12+step:00}-introduction.png");
            }
            CloseIntroduction();
        }
        _settings.Choices["app.theme"] = previousTheme; ApplyTheme(previousTheme); RebuildForTheme();
    }
    private async Task SaveQaFrameAsync(StorageFolder folder,string name)
    {
        var bitmap=new RenderTargetBitmap(); await bitmap.RenderAsync(RootGrid);
        var pixels=await bitmap.GetPixelsAsync();
        var file=await folder.CreateFileAsync(name,CreationCollisionOption.ReplaceExisting);
        using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels.ToArray());
        await encoder.FlushAsync();
    }
}
