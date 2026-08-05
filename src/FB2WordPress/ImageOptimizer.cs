using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FB2WordPress;

internal sealed class OptimizedImage : IDisposable
{
    public string Path { get; init; } = "";
    public bool IsTemporary { get; init; }
    public long OriginalBytes { get; init; }
    public long UploadBytes => new FileInfo(Path).Length;
    public void Dispose() { if (IsTemporary) try { File.Delete(Path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

internal static class ImageOptimizer
{
    const int MaxDimension = 2560;
    const long JpegQuality = 88;

    public static OptimizedImage Prepare(string source)
    {
        var originalBytes = new FileInfo(source).Length;
        var extension = System.IO.Path.GetExtension(source).ToLowerInvariant();
        // Preserve formats where recompression risks text, transparency or animation.
        if (extension is not ".jpg" and not ".jpeg" || originalBytes < 300 * 1024)
            return new() { Path = source, OriginalBytes = originalBytes };

        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FB2WordPress", "optimized-" + Guid.NewGuid().ToString("N") + ".jpg");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(temp)!);
        try
        {
            using var input = Image.FromFile(source);
            ApplyOrientation(input);
            var scale = Math.Min(1d, MaxDimension / (double)Math.Max(input.Width, input.Height));
            var width = Math.Max(1, (int)Math.Round(input.Width * scale)); var height = Math.Max(1, (int)Math.Round(input.Height * scale));
            using var output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            output.SetResolution(Math.Max(72, input.HorizontalResolution), Math.Max(72, input.VerticalResolution));
            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.White); graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; graphics.DrawImage(input, 0, 0, width, height);
            }
            var codec = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1); parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
            output.Save(temp, codec, parameters);
            var optimizedBytes = new FileInfo(temp).Length;
            if (optimizedBytes >= originalBytes * 0.9) { File.Delete(temp); return new() { Path = source, OriginalBytes = originalBytes }; }
            return new() { Path = temp, IsTemporary = true, OriginalBytes = originalBytes };
        }
        catch
        {
            try { File.Delete(temp); } catch { }
            return new() { Path = source, OriginalBytes = originalBytes };
        }
    }

    static void ApplyOrientation(Image image)
    {
        const int orientationId = 0x0112;
        if (!image.PropertyIdList.Contains(orientationId)) return;
        var property = image.GetPropertyItem(orientationId);
        if (property?.Value is not { Length: > 0 } values) return;
        var orientation = values[0];
        var flip = orientation switch { 2 => RotateFlipType.RotateNoneFlipX, 3 => RotateFlipType.Rotate180FlipNone, 4 => RotateFlipType.Rotate180FlipX, 5 => RotateFlipType.Rotate90FlipX, 6 => RotateFlipType.Rotate90FlipNone, 7 => RotateFlipType.Rotate270FlipX, 8 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone };
        image.RotateFlip(flip);
    }
}
