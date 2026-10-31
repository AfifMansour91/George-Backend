using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Services.AiStudio;

/// <summary>
/// OpenAI Images API (<c>gpt-image-2.5-sunburst</c>): <c>POST /v1/images/edits</c> with the product photo + style references
/// as <c>image[]</c>, <c>POST /v1/images/generations</c> for the no-photo look. Won the 29.10.2026 spike on product fidelity and label text.
/// </summary>
public class OpenAiImageProvider : IImageModelProvider
{
    public const string HttpClientName = "AiStudio.OpenAI";
    public const string ProviderKey = "openai";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiStudioOptions _options;
    private readonly ILogger<OpenAiImageProvider> _logger;

    public OpenAiImageProvider(IHttpClientFactory httpClientFactory, IOptions<AiStudioOptions> options, ILogger<OpenAiImageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Key => ProviderKey;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAI.ApiKey);
    private string Model => string.IsNullOrWhiteSpace(_options.OpenAI.Model) ? "gpt-image-2.5-sunburst" : _options.OpenAI.Model!.Trim();

    public async Task<ImageModelResult> EditAsync(ImageModelRequest request, CancellationToken cancelToken)
    {
        if (request.Source == null)
            return await GenerateAsync(request, cancelToken).ConfigureAwait(false);

        using var form = new MultipartFormDataContent();
        var model = request.ModelOverride ?? Model;
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent(request.Prompt), "prompt");
        form.Add(new StringContent(SizeFor(request.Ratio, request.LongestPx)), "size");
        form.Add(new StringContent(NormalizeQuality(request.Quality)), "quality");
        form.Add(new StringContent("jpeg"), "output_format");
        AddImage(form, request.Source, "image[]");
        foreach (var r in request.References)
            AddImage(form, r, "image[]");

        return await SendAsync("https://api.openai.com/v1/images/edits", form, model, cancelToken).ConfigureAwait(false);
    }

    public async Task<ImageModelResult> GenerateAsync(ImageModelRequest request, CancellationToken cancelToken)
    {
        var model = request.ModelOverride ?? Model;
        if (request.References.Count > 0)
        {
            // A style reference turns the generation into an edit of the reference set: OpenAI composes from the given images.
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(model), "model");
            form.Add(new StringContent(request.Prompt), "prompt");
            form.Add(new StringContent(SizeFor(request.Ratio, request.LongestPx)), "size");
            form.Add(new StringContent(NormalizeQuality(request.Quality)), "quality");
            form.Add(new StringContent("jpeg"), "output_format");
            foreach (var r in request.References)
                AddImage(form, r, "image[]");
            return await SendAsync("https://api.openai.com/v1/images/edits", form, model, cancelToken).ConfigureAwait(false);
        }

        var body = new
        {
            model,
            prompt = request.Prompt,
            size = SizeFor(request.Ratio, request.LongestPx),
            quality = NormalizeQuality(request.Quality),
            output_format = "jpeg",
            n = 1,
        };
        using var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        return await SendAsync("https://api.openai.com/v1/images/generations", content, model, cancelToken).ConfigureAwait(false);
    }

    private async Task<ImageModelResult> SendAsync(string url, HttpContent content, string model, CancellationToken cancelToken)
    {
        if (!IsConfigured)
            throw new ImageModelException("OpenAI key missing (AiStudio:OpenAI:ApiKey)");

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAI.ApiKey);

        using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
        var text = await res.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            var message = TryReadError(text) ?? $"OpenAI HTTP {(int)res.StatusCode}";
            _logger.LogWarning("OpenAI image call failed: {Status} {Message}", (int)res.StatusCode, message);
            throw new ImageModelException(FriendlyError((int)res.StatusCode, message), (int)res.StatusCode);
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.GetArrayLength() == 0 || !data[0].TryGetProperty("b64_json", out var b64))
            throw new ImageModelException("OpenAI returned no image");

        int? inTok = null, outTok = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var it) && it.TryGetInt32(out var i)) inTok = i;
            if (usage.TryGetProperty("output_tokens", out var ot) && ot.TryGetInt32(out var o)) outTok = o;
        }

        return new ImageModelResult
        {
            Bytes = Convert.FromBase64String(b64.GetString() ?? string.Empty),
            Mime = "image/jpeg",
            Model = model,
            InputTokens = inTok,
            OutputTokens = outTok,
            // $8 / 1M input image tokens, $30 / 1M output image tokens (OpenAI price list, Oct 2026).
            CostUsd = inTok.HasValue || outTok.HasValue ? Math.Round(((inTok ?? 0) * 8m + (outTok ?? 0) * 30m) / 1_000_000m, 5) : null,
        };
    }

    private static void AddImage(MultipartFormDataContent form, ImageInput image, string field)
    {
        var part = new ByteArrayContent(image.Bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(image.Mime);
        form.Add(part, field, image.FileName);
    }

    /// <summary>OpenAI custom sizes: multiples of 16, both edges ≤ 3840, 655,360 ≤ pixels ≤ 8,294,400.</summary>
    public static string SizeFor(string ratio, int longestPx)
    {
        const int maxPixels = 8_294_400;
        var longest = Math.Clamp(longestPx, 1024, 3840);
        var (w, h) = ratio switch
        {
            "4:5" => (longest * 4 / 5, longest),
            "3:4" => (longest * 3 / 4, longest),
            "4:3" => (longest, longest * 3 / 4),
            "16:9" => (longest, longest * 9 / 16),
            _ => (longest, longest),
        };
        // A 4K square would be 14.7 Mpx - over the budget; scale both edges down together to stay inside it.
        if ((long)w * h > maxPixels)
        {
            var k = Math.Sqrt(maxPixels / ((double)w * h));
            w = (int)(w * k);
            h = (int)(h * k);
        }
        w = Math.Max(816, w / 16 * 16);
        h = Math.Max(816, h / 16 * 16);
        return $"{w}x{h}";
    }

    private static string NormalizeQuality(string? q) => (q ?? "low").Trim().ToLowerInvariant() switch
    {
        "low" => "low",
        "medium" => "medium",
        "high" => "high",
        "xhigh" => "xhigh",
        "max" => "max",
        _ => "low",
    };

    private static string? TryReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { /* not JSON */ }
        return null;
    }

    private static string FriendlyError(int status, string message)
    {
        if (status == 401) return "מפתח OpenAI לא תקין.";
        if (status == 429) return "OpenAI עמוס כרגע, נסה שוב בעוד דקה.";
        if (message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) || message.Contains("billing", StringComparison.OrdinalIgnoreCase))
            return "אין יתרה בחשבון OpenAI של הפלטפורמה.";
        if (message.Contains("safety", StringComparison.OrdinalIgnoreCase) || message.Contains("moderation", StringComparison.OrdinalIgnoreCase))
            return "המודל סירב לעבד את התמונה הזו (מסנן תוכן). נסה צילום אחר.";
        return message.Length > 300 ? message[..300] : message;
    }
}
