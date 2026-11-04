namespace NovaIsland.Models;
public static class UpdateArrival
{
    public static bool ShouldNotify(string? previous, string current, bool usedBefore, bool updaterLaunch)
    {
        if (!Version.TryParse(current, out var now)) return false;
        if (Version.TryParse(previous, out var old)) return now > old;
        return usedBefore || updaterLaunch;
    }
}
