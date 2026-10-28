using System.Numerics;
namespace NovaIsland.Services;
// Four real FFT bands; no random heights or repeating animation.
public sealed class FrequencyAnalyzer
{
    private const int Size = 2048;
    private static readonly double[] Window = Enumerable.Range(0, Size).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Size - 1))).ToArray();
    private readonly double[] _samples = new double[Size];
    private readonly Complex[] _fft = new Complex[Size];
    private readonly double[] _bands = new double[4];
    private int _index;
    public void Add(float sample, int sampleRate)
    {
        _samples[_index++] = float.IsFinite(sample) ? sample : 0;
        if (_index != Size) return;
        _index = 0;
        for (int i = 0; i < Size; i++) _fft[i] = new Complex(_samples[i] * Window[i], 0);
        for (int i = 1, j = 0; i < Size; i++)
        {
            int bit = Size >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit;
            if (i < j) (_fft[i], _fft[j]) = (_fft[j], _fft[i]);
        }
        for (int length = 2; length <= Size; length <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (int start = 0; start < Size; start += length)
            {
                Complex w = Complex.One;
                for (int j = 0; j < length / 2; j++)
                {
                    var even = _fft[start + j]; var odd = w * _fft[start + j + length / 2];
                    _fft[start + j] = even + odd; _fft[start + j + length / 2] = even - odd; w *= step;
                }
            }
        }
        Span<double> peaks = stackalloc double[4];
        peaks.Clear();
        for (int i = 1; i < Size / 2; i++)
        {
            double hz = (double)i * sampleRate / Size;
            if (hz < 45 || hz > 14000) continue;
            int band = hz < 250 ? 0 : hz < 1000 ? 1 : hz < 4000 ? 2 : 3;
            double power = _fft[i].Real * _fft[i].Real + _fft[i].Imaginary * _fft[i].Imaginary;
            peaks[band] = Math.Max(peaks[band], power);
        }
        for (int band = 0; band < 4; band++)
        {
            double magnitude = Math.Sqrt(peaks[band]) * 4 / Size;
            _bands[band] = Math.Clamp((20 * Math.Log10(Math.Max(magnitude, 1e-9)) + 65) / 53, 0, 1);
        }
    }
    public void ReadInto(Span<double> destination)
    {
        if (destination.Length < 4) throw new ArgumentException("Four bands required.", nameof(destination));
        _bands.AsSpan().CopyTo(destination);
    }
    public double[] Read() => (double[])_bands.Clone();
    public void Reset() { _index = 0; Array.Clear(_bands); Array.Clear(_samples); }
}
