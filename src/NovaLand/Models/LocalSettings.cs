using System.Text.Json;
using System.IO;

namespace NovaIsland.Models;

public sealed class LocalSettings
{
    public bool WelcomeShown { get; set; } = false;
    public string IslandEdge { get; set; } = "Top";
    public string IslandMonitor { get; set; } = "";
    public double IslandAnchorX { get; set; } = 0.5;
    public double IslandAnchorY { get; set; } = 0.15;
    public bool IslandPositionLocked { get; set; } = false;
    public int FocusMinutes { get; set; } = 25;
    public bool StartWithWindows { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoHide { get; set; } = true;
    public bool CaptureClipboard { get; set; } = true;
    public bool BlockSocialSites { get; set; } = false;
    public bool WaterReminder { get; set; } = false;
    public List<string> FilePaths { get; set; } = new();
}

public sealed class FileEntry(string fullPath)
{
    public string FullPath { get; } = fullPath;
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar));
    public string Directory => Path.GetDirectoryName(FullPath) ?? "";
}

public static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland");
    private static readonly string FileName = Path.Combine(Folder, "settings.json");

    public static LocalSettings Load()
    {
        try
        {
            return File.Exists(FileName)
                ? JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(FileName)) ?? new()
                : new();
        }
        catch { return new(); } // Invalid/corrupted local configuration must not prevent launch.
    }

    public static void Save(LocalSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var temp = FileName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FileName, true);
    }
}
