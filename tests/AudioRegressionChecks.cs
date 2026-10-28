using NovaIsland.Services;
internal static class AudioRegressionChecks
{
    public static void Run(Action<bool, string> assert)
    {
        var random = new Random(713);
        foreach (int rate in new[] { 8000, 44100, 48000, 96000 })
        {
            var baseline = new FrequencyAnalyzerBaseline();
            var optimized = new FrequencyAnalyzer();
            for (int block = 0; block < 9; block++)
            {
                for (int i = 0; i < 2048; i++)
                {
                    double hz = block switch { 0 => 0, 1 => 100, 2 => 500, 3 => 2000, 4 => 8000, _ => 137 };
                    float sample = block switch
                    {
                        0 => 0,
                        5 => (float)(random.NextDouble() * 2 - 1),
                        6 => i % 97 == 0 ? float.NaN : i % 101 == 0 ? float.PositiveInfinity : .0001f,
                        7 => i % 2 == 0 ? float.MaxValue : -float.MaxValue,
                        _ => (float)(Math.Sin(2 * Math.PI * hz * i / rate) * .2 + Math.Sin(2 * Math.PI * 3871 * i / rate) * .0003)
                    };
                    baseline.Add(sample, rate); optimized.Add(sample, rate);
                }
                var expected = baseline.Read(); var actual = new double[4]; optimized.ReadInto(actual);
                assert(actual.Zip(expected).All(x => Math.Abs(x.First - x.Second) <= 1e-10), $"FFT matches previous release at {rate} Hz, signal {block}");
            }
            optimized.Reset();
            var cleared = new double[] { 1, 1, 1, 1 }; optimized.ReadInto(cleared);
            assert(cleared.All(x => x == 0), "FFT reset clears every band");
        }
        var analyzer = new FrequencyAnalyzer();
        try { analyzer.ReadInto(new double[3]); throw new Exception("short buffer accepted"); }
        catch (ArgumentException) { assert(true, "short FFT destination rejected"); }
    }
}
