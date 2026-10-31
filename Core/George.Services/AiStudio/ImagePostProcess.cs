using ImageMagick;

namespace George.Services.AiStudio;

/// <summary>Magick.NET helpers: normalise the source before it goes to a provider, encode results for the shop.</summary>
public static class ImagePostProcess
{
    /// <summary>Auto-orient (EXIF), downscale to <paramref name="maxPx"/>, strip metadata (GPS included), JPEG. Phone photos arrive as 10 MB HEIC/JPEG.</summary>
    public static ImageInput PrepareSource(byte[] bytes, int maxPx, string fileName = "source.jpg")
    {
        using var img = new MagickImage(bytes);
        img.AutoOrient();
        if (img.Width > maxPx || img.Height > maxPx)
            img.Resize(new MagickGeometry((uint)maxPx, (uint)maxPx) { IgnoreAspectRatio = false, Greater = true });
        img.Strip();
        img.Format = MagickFormat.Jpeg;
        img.Quality = 90;
        return new ImageInput(img.ToByteArray(), "image/jpeg", fileName);
    }

    /// <summary>Resize a reference image for upload (long edge 1024 is plenty for style).</summary>
    public static ImageInput PrepareReference(byte[] bytes, string fileName) => PrepareSource(bytes, 1024, fileName);

    /// <summary>Encode a provider result for storage: cap the long edge, strip, JPEG at the configured quality.</summary>
    public static (byte[] Bytes, int Width, int Height) EncodeResult(byte[] bytes, int maxPx, int jpegQuality)
    {
        using var img = new MagickImage(bytes);
        img.AutoOrient();
        if (img.Width > maxPx || img.Height > maxPx)
            img.Resize(new MagickGeometry((uint)maxPx, (uint)maxPx) { Greater = true });
        img.Strip();
        img.Format = MagickFormat.Jpeg;
        img.Quality = (uint)Math.Clamp(jpegQuality, 60, 100);
        return (img.ToByteArray(), (int)img.Width, (int)img.Height);
    }
}
