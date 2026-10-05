using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MagicMouse.Windows.App.Devices;

internal static class InstallationService
{
    public static string Destination => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","MagicMouseForWindows");
    public static bool IsInstalled => string.Equals(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),Destination,StringComparison.OrdinalIgnoreCase);

    public static async Task InstallAsync()
    {
        if(IsInstalled) return;
        var source=Path.GetFullPath(AppContext.BaseDirectory);
        var destination=Path.GetFullPath(Destination);
        await Task.Run(()=>
        {
            Directory.CreateDirectory(destination);
            foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
            {
                var relative=Path.GetRelativePath(source,file);
                if(relative.Split(Path.DirectorySeparatorChar).Any(segment=>segment.StartsWith("visual-qa",StringComparison.OrdinalIgnoreCase)) ||
                   relative.EndsWith(".pdb",StringComparison.OrdinalIgnoreCase) || relative.StartsWith("smoke-",StringComparison.OrdinalIgnoreCase)) continue;
                var target=Path.GetFullPath(Path.Combine(destination,relative));
                if(!target.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta de instalación inválida.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file,target,true);
            }
        });
        // Per-user Start menu shortcut: no elevation or system driver modification.
        var type=Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows no ofrece el servicio de accesos directos.");
        dynamic shell=Activator.CreateInstance(type)!;
        object? shortcutObject=null;
        try
        {
            var shortcutPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Magic Mouse for Windows.lnk");
            dynamic shortcut=shell.CreateShortcut(shortcutPath); shortcutObject=shortcut;
            shortcut.TargetPath=Path.Combine(destination,"MagicMouse.Windows.App.exe");
            shortcut.IconLocation=shortcut.TargetPath+",0";
            shortcut.WorkingDirectory=destination; shortcut.Description="Magic Mouse for Windows · Sebastian Ardila"; shortcut.Save();
        }
        finally
        {
            if(shortcutObject is not null && Marshal.IsComObject(shortcutObject)) Marshal.FinalReleaseComObject(shortcutObject);
            if(Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
        Process.Start(new ProcessStartInfo(Path.Combine(destination,"MagicMouse.Windows.App.exe")){UseShellExecute=true});
    }
}
