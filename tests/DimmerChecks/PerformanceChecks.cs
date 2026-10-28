using System.Diagnostics;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using NovaIsland.Services;
internal static class PerformanceChecks
{
    public static void Run()
    {
        const string path = "M5,17 L19,17 17,14 17,9 A5,5 0 0 0 7,9 L7,14 Z M10,20 A2,2 0 0 0 14,20";
        var color = Color.FromRgb(142, 142, 147);
        var brush = FrozenVisualCache.Brush(color); var geometry = FrozenVisualCache.Shape(path);
        if (!brush.IsFrozen || !geometry.IsFrozen || brush.Color != color || geometry.Bounds != Geometry.Parse(path).Bounds)
            throw new Exception("cached visuals must preserve appearance");
        Action beforeVisuals = () => { GC.KeepAlive(new SolidColorBrush(color)); GC.KeepAlive(Geometry.Parse(path)); };
        Action afterVisuals = () => { GC.KeepAlive(FrozenVisualCache.Brush(color)); GC.KeepAlive(FrozenVisualCache.Shape(path)); };
        for (int i = 0; i < 1000; i++) { beforeVisuals(); afterVisuals(); }
        var oldVisual = Measure(beforeVisuals, 10000); var newVisual = Measure(afterVisuals, 10000);
        if (newVisual.Bytes >= oldVisual.Bytes / 10) throw new Exception("visual cache allocation regression");
        var analyzer = new FrequencyAnalyzer(); var buffer = new double[4];
        Action oldRead = () => GC.KeepAlive(analyzer.Read());
        Action newRead = () => analyzer.ReadInto(buffer);
        for (int i = 0; i < 1000; i++) { oldRead(); newRead(); }
        var oldBuffers = Measure(oldRead, 10000); var newBuffers = Measure(newRead, 10000);
        if (newBuffers.Bytes >= oldBuffers.Bytes / 10) throw new Exception("FFT buffer allocation regression");
        var baseline = new FrequencyAnalyzerBaseline(); var optimized = new FrequencyAnalyzer();
        var audio = Enumerable.Range(0, 2048).Select(i => (float)(.2 * Math.Sin(2 * Math.PI * i * 440 / 48000))).ToArray();
        Action oldFft = () => { foreach (float sample in audio) baseline.Add(sample, 48000); };
        Action newFft = () => { foreach (float sample in audio) optimized.Add(sample, 48000); };
        for (int i = 0; i < 30; i++) { oldFft(); newFft(); }
        var oldDsp = Measure(oldFft, 300); var newDsp = Measure(newFft, 300);
        Console.WriteLine($"PERF visual refresh x10000: {oldVisual.Ms:F2} -> {newVisual.Ms:F2} ms; {oldVisual.Bytes} -> {newVisual.Bytes} allocated bytes");
        Console.WriteLine($"PERF spectrum read x10000: {oldBuffers.Bytes} -> {newBuffers.Bytes} allocated bytes");
        Console.WriteLine($"PERF FFT x300: {oldDsp.Ms:F2} -> {newDsp.Ms:F2} ms (same 2048-sample window)");
        Console.WriteLine("3 native performance checks passed");
    }
    private static (double Ms, long Bytes) Measure(Action action, int count)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) action();
        return (Stopwatch.GetElapsedTime(ticks).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }
}
