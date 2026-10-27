using System.IO;
namespace NovaIsland.Services;
public static class DiagnosticLog
{
    public static void Write(string component, Exception error)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland");
            Directory.CreateDirectory(folder);var path=Path.Combine(folder,"diagnostics.log");
            if(File.Exists(path)&&new FileInfo(path).Length>262144)File.WriteAllText(path,"");
            // No clipboard content, device names, browser addresses or stack traces.
            File.AppendAllText(path,$"{DateTime.Now:O} {component}: {error.GetType().Name} 0x{error.HResult:X8}{Environment.NewLine}");
        }
        catch { }
    }
}
