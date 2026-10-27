using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
namespace NovaIsland.Services;
public static class ImageTools
{
    public static byte[]? Encode(BitmapSource image)
    {
        if ((long)image.PixelWidth * image.PixelHeight > 20_000_000) return null;
        var format = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight]; format.CopyPixels(pixels, stride, 0);
        var normalized = BitmapSource.Create(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(normalized));
        using var output = new MemoryStream(); encoder.Save(output);
        return output.Length <= 32 * 1024 * 1024 ? output.ToArray() : null;
    }
    public static BitmapImage Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    public static byte[]? ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length > 32 * 1024 * 1024) return null;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return Encode(bitmap);
    }
}
