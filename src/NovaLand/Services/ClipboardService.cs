using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
namespace NovaIsland.Services;
// Receives native Windows change events. History is never written to disk.
public sealed class ClipboardService : IDisposable
{
    private readonly HwndSource _source;
    private readonly bool _ownsSource;
    private readonly DispatcherTimer _retry = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DispatcherTimer _fallback = new() { Interval = TimeSpan.FromSeconds(2) };
    private int _attempts;
    private uint? _lastSequence;
    private static uint _ownSequence;
    private static bool _writing;
    private bool _disposed;
    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value || _disposed) return;
            _enabled = value;
            if (!value) { _retry.Stop(); _fallback.Stop(); }
            else { _lastSequence = null; Refresh(); if (!Listening) _fallback.Start(); }
        }
    }
    public event Action<string>? TextReceived;
    public event Action<byte[]>? ImageReceived;
    public event Action<string[]>? FilesReceived;
    public bool Listening { get; }
    // An independent message window survives island visibility, style and monitor changes.
    public ClipboardService() : this(new HwndSource(new HwndSourceParameters("NovaLand.Clipboard")
        { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) }), true) { }
    public ClipboardService(HwndSource source) : this(source, false) { }
    private ClipboardService(HwndSource source, bool ownsSource)
    {
        _source = source; _ownsSource = ownsSource;
        _source.AddHook(Hook);
        Listening = AddClipboardFormatListener(_source.Handle);
        _retry.Tick += (_, _) => ReadCurrent();
        _fallback.Tick += (_, _) => Refresh();
        if (!Listening) _fallback.Start();
        // Deferred until subscribers have attached; capture the existing clipboard too.
        Refresh();
    }
    public void Refresh()
    {
        if (!Enabled || _disposed) return;
        var sequence = GetClipboardSequenceNumber();
        if (_lastSequence == sequence || (_ownSequence != 0 && sequence == _ownSequence)) return;
        _attempts = 0; QueueRead(40);
    }
    private void QueueRead(int milliseconds)
    {
        _retry.Stop();
        _retry.Interval = TimeSpan.FromMilliseconds(milliseconds);
        _retry.Start();
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x031D && Enabled && !_disposed) Refresh();
        return IntPtr.Zero;
    }
    private void ReadCurrent()
    {
        _retry.Stop();
        if (!Enabled || _disposed) return;
        if (_writing) { QueueRead(100); return; }
        uint sequence = GetClipboardSequenceNumber();
        if (_lastSequence == sequence || (_ownSequence != 0 && sequence == _ownSequence)) return;
        try
        {
            var data = System.Windows.Clipboard.GetDataObject();
            if (data is null) return;
            // Check the actual privacy marker, not an auto-converted format.
            if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing", false))
            { _lastSequence = sequence; return; }
            string[]? files = null; byte[]? png = null; string? text = null;
            if (data.GetDataPresent(System.Windows.DataFormats.FileDrop, true))
                files = (data.GetData(System.Windows.DataFormats.FileDrop, true) as string[])?.Take(100).ToArray();
            else
            {
                // Browsers often provide PNG without a usable Windows bitmap.
                if (data.GetDataPresent("PNG", false))
                {
                    try
                    {
                        var raw = data.GetData("PNG", false);
                        var bytes = raw is System.IO.MemoryStream stream ? stream.ToArray() : raw as byte[];
                        if (bytes is { Length: > 0 and <= 32 * 1024 * 1024 })
                            png = ImageTools.Encode(ImageTools.Decode(bytes));
                    }
                    catch (COMException) { throw; }
                    catch (Exception ex) { DiagnosticLog.Write("Clipboard PNG", ex); }
                }
                if (png is null && data.GetDataPresent(System.Windows.DataFormats.Bitmap, true)
                    && data.GetData(System.Windows.DataFormats.Bitmap, true) is System.Windows.Media.Imaging.BitmapSource image)
                    png = ImageTools.Encode(image);
                if (png is null && data.GetDataPresent(System.Windows.DataFormats.UnicodeText, true))
                    text = data.GetData(System.Windows.DataFormats.UnicodeText, true) as string;
                if (png is null && text is null && data.GetDataPresent(System.Windows.DataFormats.Text, true))
                    text = data.GetData(System.Windows.DataFormats.Text, true) as string;
            }
            // A delayed renderer or another copy may change the clipboard during reading.
            if (sequence != GetClipboardSequenceNumber()) { _attempts = 0; QueueRead(40); return; }
            _lastSequence = sequence;
            if (files is { Length: > 0 }) FilesReceived?.Invoke(files);
            else if (png is not null) ImageReceived?.Invoke(png);
            else if (text is not null) TextReceived?.Invoke(text);
        }
        catch (Exception ex)
        {
            if (++_attempts < 10) QueueRead(100 + 50 * _attempts);
            else DiagnosticLog.Write("Clipboard read", ex);
        }
    }
    public static async Task<bool> CopyAsync(string text)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try { _writing = true; System.Windows.Clipboard.SetText(text); _ownSequence = GetClipboardSequenceNumber(); return true; }
            catch (COMException) { }
            finally { _writing = false; }
            await Task.Delay(75);
        }
        return false;
    }
    public static async Task<bool> CopyImageAsync(byte[] png)
    {
        try
        {
            var image = ImageTools.Decode(png);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    var data = new System.Windows.DataObject();
                    data.SetImage(image);
                    data.SetData("PNG", new System.IO.MemoryStream(png));
                    _writing = true;
                    System.Windows.Clipboard.SetDataObject(data, true);
                    _ownSequence = GetClipboardSequenceNumber();
                    return true;
                }
                catch (COMException) { }
                finally { _writing = false; }
                await Task.Delay(75);
            }
        }
        catch { }
        return false;
    }
    public static async Task<bool> CopyFilesAsync(IEnumerable<string> paths)
    {
        var files = new System.Collections.Specialized.StringCollection();
        files.AddRange(paths.Take(100).ToArray());
        if (files.Count == 0) return false;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var data = new System.Windows.DataObject();
                data.SetFileDropList(files);
                // Copy, never a cut/move operation.
                data.SetData("Preferred DropEffect", new System.IO.MemoryStream(BitConverter.GetBytes(1)));
                _writing = true;
                System.Windows.Clipboard.SetDataObject(data, true);
                _ownSequence = GetClipboardSequenceNumber();
                return true;
            }
            catch (COMException) { }
            finally { _writing = false; }
            await Task.Delay(75);
        }
        return false;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _retry.Stop(); _fallback.Stop();
        if (Listening) RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(Hook);
        if (_ownsSource) _source.Dispose();
    }
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
