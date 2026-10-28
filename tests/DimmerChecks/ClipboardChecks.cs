using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NovaIsland.Services;
internal static class ClipboardChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "novaland-clipboard-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var paths = new[] { Path.Combine(root, "one.pdf"), Path.Combine(root, "two.docx") };
        foreach (var path in paths) File.WriteAllText(path, "test");
        try
        {
            using var source = new HwndSource(new HwndSourceParameters("Clipboard check") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) });
            using var listener = new ClipboardService(source);
            if (!listener.Listening) throw new Exception("clipboard listener registration");
            int received = 0; string[]? incoming = null;
            listener.FilesReceived += files => { received++; incoming = files; };
            if (!ClipboardService.CopyFilesAsync(paths).GetAwaiter().GetResult()) throw new Exception("file copy write");
            Pump();
            if (received != 0) throw new Exception("own file copy must not recreate history rows");
            if (!System.Windows.Clipboard.ContainsFileDropList() || !System.Windows.Clipboard.GetFileDropList().Cast<string>().SequenceEqual(paths)) throw new Exception("file-drop round trip");
            if (System.Windows.Clipboard.GetData("Preferred DropEffect") is not MemoryStream effect || BitConverter.ToInt32(effect.ToArray()) != 1) throw new Exception("files must use COPY, never MOVE");
            var external = new System.Collections.Specialized.StringCollection(); external.Add(paths[1]);
            System.Windows.Clipboard.SetFileDropList(external);
            Pump();
            if (received == 0 || incoming is null || incoming.Length != 1 || incoming[0] != paths[1]) throw new Exception("external file copy reaches history");
            Console.WriteLine("5 native clipboard checks passed");
        }
        finally { System.Windows.Clipboard.Clear(); Directory.Delete(root, true); }
    }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
