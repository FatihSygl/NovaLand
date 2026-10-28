using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace NovaIsland.Services;
// WASAPI render-loopback: audio output only, processed in RAM, never saved or sent.
public sealed class AudioSpectrumService : IDisposable
{
    private readonly object _sync = new();
    private readonly FrequencyAnalyzer _analyzer = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly Task _worker;
    private WasapiLoopbackCapture? _capture;
    private MMDevice? _device;
    private string? _deviceId;
    private DateTime _lastData = DateTime.MinValue;
    private volatile bool _enabled, _restart, _muted;
    public AudioSpectrumService() => _worker = Task.Run(RunAsync);
    public bool Enabled { get => _enabled; set => _enabled = value; }
    public double[] Read()
    {
        var result = new double[4]; ReadInto(result); return result;
    }
    public void ReadInto(Span<double> destination)
    {
        if (destination.Length < 4) throw new ArgumentException("Four bands required.", nameof(destination));
        lock (_sync)
        {
            if (!_enabled || _muted || DateTime.UtcNow - _lastData > TimeSpan.FromMilliseconds(220)) destination[..4].Clear();
            else _analyzer.ReadInto(destination);
        }
    }
    private async Task RunAsync()
    {
        try
        {
            while (!_cancel.IsCancellationRequested)
            {
                try
                {
                    if (!_enabled) Stop();
                    else
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        using var current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        if (_capture is null || current.ID != _deviceId || _restart)
                        {
                            Stop();
                            _device = enumerator.GetDevice(current.ID); _deviceId = current.ID;
                            _capture = new WasapiLoopbackCapture(_device);
                            _capture.DataAvailable += DataAvailable;
                            _capture.RecordingStopped += (_, _) => _restart = true;
                            _restart = false;
                            _capture.StartRecording();
                        }
                        _muted = current.AudioEndpointVolume.Mute || current.AudioEndpointVolume.MasterVolumeLevelScalar <= 0.001f;
                    }
                }
                catch { Stop(); }
                await Task.Delay(700, _cancel.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally { Stop(); }
    }
    private void DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_enabled || sender is not WasapiLoopbackCapture capture) return;
        var format = capture.WaveFormat;
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat || format is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00AA00389B71");
        int bytes = format.BitsPerSample / 8;
        if (bytes is not (2 or 3 or 4 or 8) || format.Channels < 1 || format.BlockAlign < bytes * format.Channels) return;
        lock (_sync)
        {
            for (int frame = 0; frame + format.BlockAlign <= e.BytesRecorded; frame += format.BlockAlign)
            {
                // Maximum magnitude channel avoids cancelling opposite-phase stereo.
                float sample = 0;
                for (int channel = 0; channel < format.Channels; channel++)
                {
                    int pos = frame + channel * bytes;
                    float value = isFloat ? bytes == 4 ? BitConverter.ToSingle(e.Buffer, pos) : bytes == 8 ? (float)BitConverter.ToDouble(e.Buffer, pos) : 0
                        : bytes == 2 ? BitConverter.ToInt16(e.Buffer, pos) / 32768f
                        : bytes == 3 ? ((e.Buffer[pos] | e.Buffer[pos + 1] << 8 | e.Buffer[pos + 2] << 16) << 8 >> 8) / 8388608f
                        : bytes == 4 ? BitConverter.ToInt32(e.Buffer, pos) / 2147483648f : 0;
                    if (Math.Abs(value) > Math.Abs(sample)) sample = value;
                }
                _analyzer.Add(sample, format.SampleRate);
            }
            _lastData = DateTime.UtcNow;
        }
    }
    private void Stop()
    {
        var capture = _capture; _capture = null;
        if (capture is not null)
        {
            try { capture.StopRecording(); } catch { }
            capture.Dispose();
        }
        _device?.Dispose(); _device = null; _deviceId = null;
        lock (_sync) { _analyzer.Reset(); _lastData = DateTime.MinValue; }
    }
    public void Dispose()
    {
        _enabled = false; _cancel.Cancel();
        try { _worker.GetAwaiter().GetResult(); } catch { }
        _cancel.Dispose();
    }
}
