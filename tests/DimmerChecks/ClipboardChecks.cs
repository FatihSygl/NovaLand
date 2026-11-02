using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NovaIsland.Services;
using DataObject = System.Windows.DataObject;
internal static class ClipboardChecks
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
        var root = Path.Combine(Path.GetTempPath(), "novaland-clipboard-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var paths = new[] { Path.Combine(root, "one.pdf"), Path.Combine(root, "two.docx") };
        foreach (var path in paths) File.WriteAllText(path, "test");
        try
        {
            System.Windows.Clipboard.SetText("already copied before launch");
            using var listener = new ClipboardService();
            Check(listener.Listening, "independent clipboard listener registration");
            int texts = 0, images = 0, files = 0; string? text = null; byte[]? image = null; string[]? incoming = null;
            listener.TextReceived += value => { texts++; text = value; };
            listener.ImageReceived += value => { images++; image = value; };
            listener.FilesReceived += value => { files++; incoming = value; };
            Pump();
            Check(text == "already copied before launch", "existing clipboard captured after subscribers attach");
            listener.Refresh(); Pump();
            Check(texts == 1, "opening history does not duplicate current clipboard");
            System.Windows.Clipboard.SetText("external text");
            Pump();
            Check(text == "external text" && texts == 2, "external Unicode text reaches history");

            var privateData = new DataObject();
            privateData.SetText("private");
            privateData.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(BitConverter.GetBytes(1)));
            System.Windows.Clipboard.SetDataObject(privateData, true);
            Pump();
            Check(texts == 2, "privacy marker still respected");
            System.Windows.Clipboard.SetText("after private copy");
            Pump();
            Check(text == "after private copy", "privacy exclusion does not poison later copies");

            listener.Enabled = false;
            int before = texts;
            System.Windows.Clipboard.SetText("copied while disabled");
            Pump();
            Check(texts == before, "capture off is respected");
            listener.Enabled = true;
            Pump();
            Check(text == "copied while disabled" && texts == before + 1, "reenabling captures current clipboard");
            Check(ClipboardService.CopyAsync("own text").GetAwaiter().GetResult(), "own text write succeeds");
            Pump();
            Check(texts == before + 1, "own text copy does not recreate history row");

            var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null,
                new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, 8);
            System.Windows.Clipboard.SetImage(bitmap);
            Pump();
            Check(images == 1 && image is not null && ImageTools.Decode(image).PixelWidth == 2, "bitmap copy reaches image history");
            byte[] png = ImageTools.Encode(bitmap)!;
            var pngOnly = new DataObject();
            pngOnly.SetData("PNG", new MemoryStream(png));
            System.Windows.Clipboard.SetDataObject(pngOnly, true);
            Pump();
            Check(images == 2, "PNG-only browser image reaches history");
            Check(ClipboardService.CopyImageAsync(png).GetAwaiter().GetResult(), "own image write succeeds");
            Pump();
            Check(images == 2, "own image copy does not recreate history row");
            var badPng = new DataObject();
            badPng.SetData("PNG", new MemoryStream(new byte[] { 1, 2, 3 }));
            badPng.SetText("text accompanying unsupported image");
            System.Windows.Clipboard.SetDataObject(badPng, true);
            Pump();
            Check(text == "text accompanying unsupported image", "invalid optional PNG falls back to text");

            Check(ClipboardService.CopyFilesAsync(paths).GetAwaiter().GetResult(), "file copy write");
            Pump();
            Check(files == 0, "own file copy does not recreate history row");
            Check(System.Windows.Clipboard.ContainsFileDropList() && System.Windows.Clipboard.GetFileDropList().Cast<string>().SequenceEqual(paths), "file-drop round trip");
            Check(System.Windows.Clipboard.GetData("Preferred DropEffect") is MemoryStream effect && BitConverter.ToInt32(effect.ToArray()) == 1, "files use COPY never MOVE");
            var external = new System.Collections.Specialized.StringCollection(); external.Add(paths[1]);
            System.Windows.Clipboard.SetFileDropList(external);
            Pump();
            Check(files == 1 && incoming is not null && incoming.SequenceEqual(new[] { paths[1] }), "external file copy reaches history");

            // Hold the native clipboard longer than the old reader's entire retry window.
            System.Windows.Clipboard.SetText("clipboard busy then available");
            using var opened = new ManualResetEventSlim();
            Exception? lockFailure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (!OpenClipboard(IntPtr.Zero)) throw new Exception("test clipboard lock");
                    opened.Set();
                    try { Thread.Sleep(850); } finally { CloseClipboard(); }
                }
                catch (Exception ex) { lockFailure = ex; opened.Set(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(); opened.Wait();
            if (lockFailure is not null) throw lockFailure;
            Pump(1800); thread.Join();
            Check(text == "clipboard busy then available", "busy clipboard eventually recovered without another copy");
            listener.Refresh(); Pump();
            int afterRecovery = texts;
            listener.Refresh(); Pump();
            Check(texts == afterRecovery, "recovered clipboard still deduplicated");

            listener.Dispose();
            System.Windows.Clipboard.SetText("after dispose"); Pump();
            Check(texts == afterRecovery, "disposed listener receives no further copies");
            Console.WriteLine($"{checks} native clipboard checks passed");
        }
        finally { System.Windows.Clipboard.Clear(); Directory.Delete(root, true); }
    }
    private static void Pump(int milliseconds = 320)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
