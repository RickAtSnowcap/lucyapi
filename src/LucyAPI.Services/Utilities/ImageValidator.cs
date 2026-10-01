using SkiaSharp;

namespace LucyAPI.Services.Utilities;

/// <summary>An image that passed <see cref="ImageValidator.Validate"/>: real type from its bytes, size from the codec.</summary>
public sealed record ValidatedImage(byte[] Bytes, string MimeType, string Extension, int Width, int Height, int FrameCount);

/// <summary>Image rejected by validation. An InvalidOperationException, so callers map it to 400 like other bad input.</summary>
public sealed class ImageValidationException(string message) : InvalidOperationException(message);

/// <summary>
/// The one validation path for every image LucyAPI stores or sends on (uploads, Gemini output, source images).
/// Order matters: magic bytes → size cap → SKCodec.Create → pixel cap (before any decode) → strict full decode.
/// The declared mime type / file name is never trusted. See hint #225.
/// </summary>
public static class ImageValidator
{
    /// <summary>Upload cap for every path (LucyAdmin multipart and the upload_image MCP tool). Phone photos run 5–12 MB.</summary>
    public const int MaxUploadBytes = 25 * 1024 * 1024;

    /// <summary>Decompression-bomb guard: reject before decoding if width*height exceeds this.</summary>
    public const long MaxPixels = 40_000_000;

    public static ValidatedImage Validate(byte[] bytes, int maxBytes)
    {
        if (bytes.Length == 0)
            throw new ImageValidationException("Image is empty");

        var (mimeType, extension, expectedFormat) = DetectType(bytes)
            ?? throw new ImageValidationException("Not a supported image (PNG, JPEG, WebP or GIF)");

        if (bytes.Length > maxBytes)
            throw new ImageValidationException(
                $"Image is {bytes.Length / (1024.0 * 1024.0):0.0} MB; the limit here is {maxBytes / (1024 * 1024)} MB");

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data)
            ?? throw new ImageValidationException("Image could not be read (corrupt or not really an image)");

        if (codec.EncodedFormat != expectedFormat)
            throw new ImageValidationException("Image content doesn't match its file signature");

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0 || height <= 0)
            throw new ImageValidationException("Image has no pixels");
        if ((long)width * height > MaxPixels)
            throw new ImageValidationException(
                $"Image is {width}x{height} ({(long)width * height / 1_000_000.0:0.#} MP); the limit is {MaxPixels / 1_000_000} MP");

        // Strict decode: only Success counts. Truncated files pass SKCodec.Create but return IncompleteInput.
        using var bitmap = DecodeFirstFrame(codec);

        return new ValidatedImage(bytes, mimeType, extension, width, height, Math.Max(1, codec.FrameCount));
    }

    /// <summary>
    /// What lands on disk: WebP is converted to PNG (keeps alpha; Google Docs can't embed WebP).
    /// Animated WebP is flattened to its first frame — Skia has no GIF encoder. Everything else is stored byte-for-byte
    /// (so animated GIFs keep their animation). Returns a notice for the caller when a conversion happened.
    /// </summary>
    public static (ValidatedImage Image, string? Notice) NormalizeForStorage(ValidatedImage image)
    {
        if (image.MimeType != "image/webp")
            return (image, null);

        var png = ConvertToPng(image.Bytes);
        var notice = image.FrameCount > 1
            ? $"Animated WebP ({image.FrameCount} frames) was flattened to its first frame and saved as PNG."
            : "WebP was converted to PNG.";
        return (new ValidatedImage(png, "image/png", ".png", image.Width, image.Height, 1), notice);
    }

    /// <summary>
    /// Re-encodes a validated image as PNG (keeps alpha). Animated images give frame 0 only.
    /// Used for Google Docs, which embeds only PNG/JPEG/GIF.
    /// </summary>
    public static byte[] ConvertToPng(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data)
            ?? throw new ImageValidationException("Image could not be read (corrupt or not really an image)");
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
            throw new ImageValidationException("Image is over the pixel limit");
        using var bitmap = DecodeFirstFrame(codec);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("PNG encoding failed");
        return png.ToArray();
    }

    private static SKBitmap DecodeFirstFrame(SKCodec codec)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        try
        {
            var result = codec.GetPixels(info, bitmap.GetPixels());
            if (result != SKCodecResult.Success)
                throw new ImageValidationException(result == SKCodecResult.IncompleteInput
                    ? "Image is truncated (incomplete file)"
                    : $"Image could not be decoded ({result})");
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();   // never keep partial pixels
            throw;
        }
    }

    /// <summary>Type from the file signature only.</summary>
    public static (string MimeType, string Extension, SKEncodedImageFormat Format)? DetectType(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return ("image/png", ".png", SKEncodedImageFormat.Png);
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ("image/jpeg", ".jpg", SKEncodedImageFormat.Jpeg);
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp", SKEncodedImageFormat.Webp);
        if (b.Length >= 6 && (b[..6].SequenceEqual("GIF87a"u8) || b[..6].SequenceEqual("GIF89a"u8)))
            return ("image/gif", ".gif", SKEncodedImageFormat.Gif);
        return null;
    }
}
