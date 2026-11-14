using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NovaIsland.Services;

internal sealed class DualHeadphoneService : IDisposable
{
    private CancellationTokenSource? _cancel;
    private Task _worker = Task.CompletedTask;
    private bool _disposed;

    // Settings and disposal are called from the UI thread. Only one audio route may own the devices.
    public void SetTarget(string? targetId, Action<string, CancellationToken> report)
    {
        if (_disposed) return;
        StopCurrent();
        if (string.IsNullOrWhiteSpace(targetId)) return;

        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        var previous = _worker;
        _worker = Task.Run(async () =>
        {
            await previous.ConfigureAwait(false);
            if (!token.IsCancellationRequested)
                await RunAsync(targetId, report, token).ConfigureAwait(false);
        });
    }

    private void StopCurrent()
    {
        if (_cancel is not { } previous) return;
        _cancel = null;
        previous.Cancel();
        _ = _worker.ContinueWith(_ => previous.Dispose(), TaskScheduler.Default);
    }

    private static async Task RunAsync(string targetId, Action<string, CancellationToken> report, CancellationToken token)
    {
        string? lastStatus = null;
        void Report(string message)
        {
            if (token.IsCancellationRequested || lastStatus == message) return;
            lastStatus = message;
            report(message, token);
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var devices = new MMDeviceEnumerator();
                    using var source = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (string.Equals(source.ID, targetId, StringComparison.OrdinalIgnoreCase))
                        Report("İkinci kulaklık varsayılan çıkış oldu. İlk kulaklığı tekrar varsayılan yap.");
                    else if (!IsActiveOutput(devices, targetId))
                        Report("İkinci çıkış bekleniyor. Kulaklık bağlanınca ses paylaşımı başlayacak.");
                    else
                    {
                        using var target = devices.GetDevice(targetId);
                        Play(devices, source, target, Report, token);
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    if (token.IsCancellationRequested) return;
                    if (lastStatus != "Ses paylaşılamadı. Çıkışlar tekrar kontrol ediliyor…")
                        DiagnosticLog.Write("DualHeadphones", error);
                    Report("Ses paylaşılamadı. Çıkışlar tekrar kontrol ediliyor…");
                }

                await Task.Delay(1200, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static bool IsActiveOutput(MMDeviceEnumerator devices, string id)
    {
        try
        {
            using var device = devices.GetDevice(id);
            return device.State == DeviceState.Active && device.DataFlow == DataFlow.Render;
        }
        catch (COMException) { return false; }
    }

    private static void Play(MMDeviceEnumerator devices, MMDevice source, MMDevice target,
        Action<string> report, CancellationToken token)
    {
        // The primary device keeps playing normally. Loopback audio is buffered only in memory.
        string sourceId = source.ID;
        string targetId = target.ID;
        using var playback = new WasapiOut(target, AudioClientShareMode.Shared, false, 65);
        using var capture = source.AudioClient;
        var format = capture.MixFormat;
        capture.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.Loopback,
            1_000_000, 0, format, Guid.Empty);
        var packets = capture.AudioCaptureClient;
        var audio = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromMilliseconds(420),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        playback.PlaybackStopped += (_, args) => stopped.TrySetResult(args.Exception);
        var buffer = new byte[capture.BufferSize * format.BlockAlign];

        playback.Init(audio);
        token.ThrowIfCancellationRequested();
        capture.Start();
        try
        {
            playback.Play();
            report($"Ses paylaşılıyor · {source.FriendlyName} → {target.FriendlyName}");
            long lastCheck = Environment.TickCount64;

            while (!token.IsCancellationRequested)
            {
                if (stopped.Task.IsCompleted)
                    throw new InvalidOperationException("Audio sharing playback stopped.", stopped.Task.GetAwaiter().GetResult());

                while (!token.IsCancellationRequested && packets.GetNextPacketSize() > 0)
                {
                    IntPtr data = packets.GetBuffer(out int frames, out var flags);
                    try
                    {
                        int length = frames * format.BlockAlign;
                        if ((flags & AudioClientBufferFlags.Silent) != 0 || data == IntPtr.Zero)
                            Array.Clear(buffer, 0, length);
                        else
                            Marshal.Copy(data, buffer, 0, length);

                        // Independent device clocks can accumulate latency. Bound the amount of queued audio.
                        if (audio.BufferedBytes + length > format.AverageBytesPerSecond / 4) audio.ClearBuffer();
                        audio.AddSamples(buffer, 0, length);
                    }
                    finally { packets.ReleaseBuffer(frames); }
                }

                if (Environment.TickCount64 - lastCheck >= 750)
                {
                    lastCheck = Environment.TickCount64;
                    using var current = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (!string.Equals(current.ID, sourceId, StringComparison.OrdinalIgnoreCase) ||
                        !IsActiveOutput(devices, targetId)) return;
                }

                if (token.WaitHandle.WaitOne(10)) return;
            }
        }
        finally
        {
            try { capture.Stop(); }
            catch (COMException) { /* A disconnected device may already have stopped. */ }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        StopCurrent();
    }
}
