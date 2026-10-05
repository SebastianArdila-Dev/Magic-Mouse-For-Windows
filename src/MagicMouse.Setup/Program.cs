using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MagicMouse.Setup;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

    [STAThread]
    private static void Main()
    {
        var extractionRoot = Path.Combine(Path.GetTempPath(), "MagicMouseSetup", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(extractionRoot);
            using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("MagicMouse.Payload.zip")
                ?? throw new InvalidDataException("No se encontró la aplicación en el instalador.");
            // ZipFile validates that each entry stays inside the extraction directory.
            ZipFile.ExtractToDirectory(payload, extractionRoot);
            var executable = Path.Combine(extractionRoot, "MagicMouse.Windows.App.exe");
            if (!File.Exists(executable)) throw new InvalidDataException("El instalador está incompleto.");
            using var process = Process.Start(new ProcessStartInfo(executable, "--install")
            {
                WorkingDirectory = extractionRoot,
                UseShellExecute = true
            }) ?? throw new InvalidOperationException("No se pudo abrir la instalación.");
            // Keep the payload available if the user selects portable mode or minimises to the tray.
            process.WaitForExit();
        }
        catch (Exception exception)
        {
            MessageBoxW(IntPtr.Zero, "No se pudo abrir la instalación.\n\n" + exception.Message,
                "Magic Mouse for Windows", 0x10);
        }
        finally
        {
            try { if (Directory.Exists(extractionRoot)) Directory.Delete(extractionRoot, true); }
            catch (IOException) { /* Windows may still be releasing runtime files. */ }
            catch (UnauthorizedAccessException) { /* Never interrupt the installed application. */ }
        }
    }
}
