using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Services.AiStudio;

/// <summary>
/// Google Gemini image models through the Interactions API (<c>POST /v1beta/interactions</c>): text + inline images in,
/// one image out. Faster than OpenAI (~26 s at 2K) and strong on meat/fish aesthetics, but the spike found it redraws
/// products and garbles label text - so it is the fallback / A-B engine, not the default.
/// </summary>
public class GeminiImageProvider : IImageModelProvider
{
    public const string HttpClientName = "AiStudio.Gemini";
    public const string ProviderKey = "gemini";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiStudioOptions _options;
    private readonly ILogger<GeminiImageProvider> _logger;

    public GeminiImageProvider(IHttpClientFactory httpClientFactory, IOptions<AiStudioOptions> options, ILogger<GeminiImageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Key => ProviderKey;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Gemini.ApiKey);

    private string ModelFor(string quality) =>
        quality.Equals("low", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_options.Gemini.FastModel)
            ? _options.Gemini.FastModel!.Trim()
            : string.IsNullOrWhiteSpace(_options.Gemini.ProModel) ? (_options.Gemini.Model ?? "gemini-3-pro-image") : _options.Gemini.ProModel!.Trim();

    public Task<ImageModelResult> EditAsync(ImageModelRequest request, CancellationToken cancelToken) => CallAsync(request, cancelToken);

    public Task<ImageModelResult> GenerateAsync(ImageModelRequest request, CancellationToken cancelToken) => CallAsync(request, cancelToken);

    private async Task<ImageModelResult> CallAsync(ImageModelRequest request, CancellationToken cancelToken)
    {
        if (!IsConfigured)
            throw new ImageModelException("Gemini key missing (AiStudio:Gemini:ApiKey)");

        var model = request.ModelOverride ?? ModelFor(request.Quality);
        var input = new List<object> { new { type = "text", text = request.Prompt } };
        if (request.Source != null)
            input.Add(new { type = "image", mime_type = request.Source.Mime, data = Convert.ToBase64String(request.Source.Bytes) });
        foreach (var r in request.References)
            input.Add(new { type = "image", mime_type = r.Mime, data = Convert.ToBase64String(r.Bytes) });

        var size = request.Hd ? "4K" : request.Quality.Equals("low", StringComparison.OrdinalIgnoreCase) ? _options.GeminiPreviewSize : _options.GeminiFinalSize;
        var body = new
        {
            model,
            input,
            response_format = new { type = "image", mime_type = "image/jpeg", aspect_ratio = request.Ratio, image_size = size },
        };

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-goog-api-key", _options.Gemini.ApiKey);

        using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
        var text = await res.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            var message = TryReadError(text) ?? $"Gemini HTTP {(int)res.StatusCode}";
            _logger.LogWarning("Gemini image call failed: {Status} {Message}", (int)res.StatusCode, message);
            throw new ImageModelException((int)res.StatusCode == 429 ? "Gemini עמוס כרגע, נסה שוב בעוד דקה." : message.Length > 300 ? message[..300] : message, (int)res.StatusCode);
        }

        using var doc = JsonDocument.Parse(text);
        var (data, mime) = FindImage(doc.RootElement);
        if (data == null)
            throw new ImageModelException("Gemini returned no image");

        int? inTok = null, outTok = null;
        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var it) && it.TryGetInt32(out var i)) inTok = i;
            if (usage.TryGetProperty("output_tokens", out var ot) && ot.TryGetInt32(out var o)) outTok = o;
        }

        return new ImageModelResult
        {
            Bytes = Convert.FromBase64String(data),
            Mime = mime ?? "image/jpeg",
            Model = model,
            InputTokens = inTok,
            OutputTokens = outTok,
            // Pro: $0.134 per 1K/2K image, $0.24 at 4K (Google price list, Oct 2026).
            CostUsd = size.Equals("4K", StringComparison.OrdinalIgnoreCase) ? 0.24m : 0.134m,
        };
    }

    /// <summary>The image block's location differs between SDK shapes; look for a long base64 <c>data</c> string with an image type/mime.</summary>
    private static (string? Data, string? Mime) FindImage(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (node.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    var s = d.GetString();
                    var isImage = (node.TryGetProperty("type", out var t) && t.GetString() == "image")
                                  || node.TryGetProperty("mime_type", out _) || node.TryGetProperty("mimeType", out _);
                    if (isImage && s != null && s.Length > 2000)
                    {
                        string? mime = null;
                        if (node.TryGetProperty("mime_type", out var m1)) mime = m1.GetString();
                        else if (node.TryGetProperty("mimeType", out var m2)) mime = m2.GetString();
                        return (s, mime);
                    }
                }
                foreach (var p in node.EnumerateObject())
                {
                    var r = FindImage(p.Value);
                    if (r.Data != null) return r;
                }
                break;
            }
            case JsonValueKind.Array:
                foreach (var e in node.EnumerateArray())
                {
                    var r = FindImage(e);
                    if (r.Data != null) return r;
                }
                break;
        }
        return (null, null);
    }

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
}
