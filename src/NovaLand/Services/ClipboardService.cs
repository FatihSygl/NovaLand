using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
namespace NovaIsland.Services;
// Receives native Windows change events. History is never written to disk.
public sealed class ClipboardService : IDisposable
{
    private readonly HwndSource _source;
    private readonly DispatcherTimer _retry = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private int _attempts;
    private bool _disposed;
    public bool Enabled { get; set; } = true;
    public event Action<string>? TextReceived;
    public event Action<byte[]>? ImageReceived;
    public bool Listening { get; }
    public ClipboardService(HwndSource source)
    {
        _source = source;
        _source.AddHook(Hook);
        Listening = AddClipboardFormatListener(_source.Handle);
        _retry.Tick += (_, _) => ReadCurrent();
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x031D && Enabled && !_disposed) { _attempts = 0; ReadCurrent(); }
        return IntPtr.Zero;
    }
    private void ReadCurrent()
    {
        _retry.Stop();
        if (!Enabled || _disposed) return;
        try
        {
            if (System.Windows.Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;
            if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image is not null) { var png = ImageTools.Encode(image); if (png is not null) ImageReceived?.Invoke(png); }
                return;
            }
            if (System.Windows.Clipboard.GetData("PNG") is System.IO.MemoryStream pngStream)
            {
                var bytes = pngStream.ToArray();
                if (bytes.Length <= 32 * 1024 * 1024) { var png = ImageTools.Encode(ImageTools.Decode(bytes)); if (png is not null) ImageReceived?.Invoke(png); }
                return;
            }
            if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText))
                TextReceived?.Invoke(System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText));
        }
        catch (Exception) { if (++_attempts < 4) _retry.Start(); }
    }
    public static async Task<bool> CopyAsync(string text)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try { System.Windows.Clipboard.SetText(text); return true; }
            catch (COMException) { await Task.Delay(75); }
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
                    System.Windows.Clipboard.SetDataObject(data, true);
                    return true;
                }
                catch (COMException) { await Task.Delay(75); }
            }
        }
        catch { }
        return false;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _retry.Stop();
        if (Listening) RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(Hook);
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
