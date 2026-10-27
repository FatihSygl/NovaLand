using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NovaIsland.Models;

namespace NovaIsland.Services;

public static class ApplicationAudioService
{
    private sealed record Found(string Key, string Name, int Volume, bool Playing, AppAudioSession Session);
    public static AppAudioSnapshot[] Read() => Read(ProcessName);
    internal static AppAudioSnapshot[] Read(Func<uint, string?> processName)
    {
        var found = new List<Found>();
        var names = new Dictionary<uint, string?>();
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                try
                {
                    var sessions = device.AudioSessionManager.Sessions;
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        using var session = sessions[i];
                        try
                        {
                            if (session.State == AudioSessionState.AudioSessionStateExpired || session.IsSystemSoundsSession) continue;
                            uint pid = session.GetProcessID;
                            if (pid == 0 || pid == Environment.ProcessId) continue;
                            if (!names.TryGetValue(pid, out var name))
                            {
                                name = processName(pid);
                                names[pid] = name;
                            }
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            var volume = session.SimpleAudioVolume;
                            found.Add(new("app:" + name.ToLowerInvariant(), FriendlyName(name), volume.Mute ? 0 : (int)Math.Round(volume.Volume * 100),
                                session.State == AudioSessionState.AudioSessionStateActive, new(device.ID, session.GetSessionInstanceIdentifier, pid)));
                        }
                        catch (Exception ex) { DiagnosticLog.Write("AppAudioSessionRead", ex); }
                    }
                }
                catch (Exception ex) { DiagnosticLog.Write("AppAudioDeviceRead", ex); }
            }
        }
        return found.GroupBy(x => x.Key).Select(g => new AppAudioSnapshot(g.Key, g.First().Name,
            g.Max(x => x.Volume), g.Any(x => x.Playing), g.Select(x => x.Session).Distinct().ToArray()))
            .OrderByDescending(x => x.Playing).ThenBy(x => x.Name).ToArray();
    }

    // Match only the exact session instances from the displayed snapshot. Never fall
    // back to a process id/name after navigation, process reuse or output switching.
    public static bool SetVolume(AppAudioSnapshot target, int percentage)
    {
        int changed = 0;
        using var enumerator = new MMDeviceEnumerator();
        foreach (var group in target.Sessions.GroupBy(x => x.DeviceId))
        {
            try
            {
                using var device = enumerator.GetDevice(group.Key);
                if (device.State != DeviceState.Active || device.DataFlow != DataFlow.Render) continue;
                var sessions = device.AudioSessionManager.Sessions;
                var expected = group.ToHashSet();
                for (int i = 0; i < sessions.Count; i++)
                {
                    using var session = sessions[i];
                    try
                    {
                        var identity = new AppAudioSession(device.ID, session.GetSessionInstanceIdentifier, session.GetProcessID);
                        if (!expected.Contains(identity) || session.State == AudioSessionState.AudioSessionStateExpired) continue;
                        var volume = session.SimpleAudioVolume;
                        volume.Volume = Math.Clamp(percentage, 0, 100) / 100f;
                        volume.Mute = false;
                        changed++;
                    }
                    catch (Exception ex) { DiagnosticLog.Write("AppAudioSessionSet", ex); }
                }
            }
            catch (Exception ex) { DiagnosticLog.Write("AppAudioDeviceSet", ex); }
        }
        return changed > 0;
    }
    private static string? ProcessName(uint pid)
    {
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName; }
        catch { return null; }
    }
    private static string FriendlyName(string name) => name.ToLowerInvariant() switch
    {
        "spotify" => "Spotify", "discord" => "Discord", "vlc" => "VLC", "chrome" => "Chrome",
        "msedge" => "Edge", "firefox" => "Firefox", "opera" => "Opera", "steam" => "Steam",
        _ => name
    };
}
