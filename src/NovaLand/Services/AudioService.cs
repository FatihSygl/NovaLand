using NAudio.CoreAudioApi;

namespace NovaIsland.Services;

// Use the same Windows endpoint API as the audio output picker and second headphone.
public static class AudioService
{
    public static int? ReadVolume()
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var output = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return (int)Math.Round(output.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
        }
        catch { return null; }
    }

    public static bool SetVolume(int percentage)
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var output = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            output.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(percentage, 0, 100) / 100f;
            return true;
        }
        catch { return false; }
    }
}
