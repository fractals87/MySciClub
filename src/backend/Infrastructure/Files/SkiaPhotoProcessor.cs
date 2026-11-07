using MySciClub.Application.Features.Members;
using SkiaSharp;

namespace MySciClub.Infrastructure.Files;

internal sealed class SkiaPhotoProcessor : IPhotoProcessor
{
    private const int MaximumBytes = 5 * 1024 * 1024;
    private const int MaximumEdge = 512;
    private const int WebpQuality = 80;

    public async Task<ProcessedPhoto> ProcessAsync(Stream input, string declaredContentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead) throw new InvalidDataException("Photo stream is not readable.");
        if (declaredContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new InvalidDataException("Only JPEG, PNG, and WebP images are accepted.");
        await using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bounded.Length + read > MaximumBytes) throw new InvalidDataException("Photo exceeds 5 MB.");
            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var bytes = bounded.ToArray();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported image data.");
        var actualType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => throw new InvalidDataException("Only JPEG, PNG, and WebP images are accepted.")
        };
        if (!string.Equals(actualType, declaredContentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Declared image format does not match file contents.");
        using var source = SKBitmap.Decode(data) ?? throw new InvalidDataException("Image could not be decoded.");
        if (source.Width <= 0 || source.Height <= 0) throw new InvalidDataException("Invalid image dimensions.");
        var scale = Math.Min(1d, Math.Min((double)MaximumEdge / source.Width, (double)MaximumEdge / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var destination = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(destination))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)width / source.Width, (float)height / source.Height);
            canvas.DrawBitmap(source, 0, 0);
            canvas.Flush();
        }
        using var image = SKImage.FromBitmap(destination);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality) ?? throw new InvalidDataException("WebP encoding failed.");
        var output = encoded.ToArray();
        return new ProcessedPhoto(new MemoryStream(output, writable: false), "socio-photo.webp", "image/webp");
    }
}
