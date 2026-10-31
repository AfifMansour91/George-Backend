namespace George.Services.AiStudio;

/// <summary>A source or reference image handed to a provider.</summary>
public sealed record ImageInput(byte[] Bytes, string Mime, string FileName);

/// <summary>One provider call. <see cref="Source"/> null = text-to-image (the <c>generate</c> look).</summary>
public sealed class ImageModelRequest
{
    public ImageInput? Source { get; init; }
    public List<ImageInput> References { get; init; } = new();
    public string Prompt { get; init; } = string.Empty;
    /// <summary>1:1 | 4:5 | 3:4 | 4:3 | 16:9.</summary>
    public string Ratio { get; init; } = "1:1";
    /// <summary>Longest edge in px (OpenAI) - the provider snaps it to what it supports.</summary>
    public int LongestPx { get; init; } = 1024;
    /// <summary>low | medium | high (OpenAI quality) - Gemini maps this to 1K/2K.</summary>
    public string Quality { get; init; } = "low";
    /// <summary>4K final: Gemini image_size 4K; OpenAI gets the larger <see cref="LongestPx"/> the caller already set.</summary>
    public bool Hd { get; init; }
    public string? ModelOverride { get; init; }
}

public sealed class ImageModelResult
{
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public string Mime { get; init; } = "image/jpeg";
    public string Model { get; init; } = string.Empty;
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    /// <summary>Best-effort USD estimate from the provider's price list.</summary>
    public decimal? CostUsd { get; init; }
}

/// <summary>What an image model must do for the studio (spec §9). Implementations: OpenAI, Gemini.</summary>
public interface IImageModelProvider
{
    string Key { get; }
    bool IsConfigured { get; }
    Task<ImageModelResult> EditAsync(ImageModelRequest request, CancellationToken cancelToken);
    Task<ImageModelResult> GenerateAsync(ImageModelRequest request, CancellationToken cancelToken);
}

public class ImageModelException : Exception
{
    public int? HttpStatus { get; }
    public ImageModelException(string message, int? httpStatus = null, Exception? inner = null) : base(message, inner)
    {
        HttpStatus = httpStatus;
    }
}
