using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NovaIsland.Services;

public sealed record AudioOutput(string Id, string Name, bool Selected);
public static class AudioOutputService
{
    public static AudioOutput[] Read()
    {
        using var enumerator = new MMDeviceEnumerator();
        string? current = null;
        try { using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); current = device.ID; }
        catch (COMException) { }
        var result = new List<AudioOutput>();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in devices)
        {
            using (device)
            {
                try { result.Add(new(device.ID, device.FriendlyName, device.ID == current)); }
                catch (Exception ex) { DiagnosticLog.Write("AudioOutputRead", ex); }
            }
        }
        return result.OrderByDescending(x => x.Selected).ThenBy(x => x.Name).ToArray();
    }

    // PolicyConfig is a Windows COM compatibility interface used by audio switchers.
    // Change playback (console/multimedia) only; keep the communications device.
    public static bool Select(string id)
    {
        object? instance = null;
        IPolicyConfig? policy = null;
        string? oldConsole = null, oldMedia = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var target = enumerator.GetDevice(id);
            if (target.DataFlow != DataFlow.Render || target.State != DeviceState.Active) return false;
            try { using var old = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console); oldConsole = old.ID; } catch (COMException) { }
            try { using var old = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); oldMedia = old.ID; } catch (COMException) { }
            instance = new PolicyConfigClient(); policy = (IPolicyConfig)instance;
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, Role.Console));
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(id, Role.Multimedia));
            using var confirmed = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            using var confirmedConsole = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            if (confirmed.ID != id || confirmedConsole.ID != id) throw new InvalidOperationException("Output change not confirmed");
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("AudioOutputSelect", ex);
            if (policy is not null)
            {
                try { if (oldConsole is not null) policy.SetDefaultEndpoint(oldConsole, Role.Console); if (oldMedia is not null) policy.SetDefaultEndpoint(oldMedia, Role.Multimedia); }
                catch (Exception rollback) { DiagnosticLog.Write("AudioOutputRestore", rollback); }
            }
            return false;
        }
        finally { if (instance is not null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance); }
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        // Unused entries preserve the ABI order preceding SetDefaultEndpoint.
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, Role role);
    }
}
