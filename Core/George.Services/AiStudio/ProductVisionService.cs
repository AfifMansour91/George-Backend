using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Services.AiStudio;

/// <summary>What Claude sees in the shop's photo before any image credit is spent.</summary>
public sealed class ProductIdentification
{
    public string? ProductName { get; set; }
    /// <summary>Free-text category in English for the prompt ("raw chicken thighs", "bottled BBQ sauce").</summary>
    public string? CategoryEn { get; set; }
    public bool IsPackaged { get; set; }
    /// <summary>False when no complete product is visible (blurry close-up, half a tray) - the job fails before the provider is called.</summary>
    public bool Usable { get; set; } = true;
    public string? UnusableReason { get; set; }
    /// <summary>English sentence naming what is NOT the product (foil, tray, hand, counter) and telling the model to remove it. Empty when nothing.</summary>
    public string? Incidental { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

/// <summary>Claude's answer for the no-photo look: what to draw, or why it cannot know.</summary>
public sealed class VisualBrief
{
    public string? Brief { get; set; }
    public string? CategoryEn { get; set; }
    public bool IsBrandedPackaged { get; set; }
    /// <summary>True when name + description do not say what the image must show; <see cref="Question"/> is the Hebrew ask for the shop.</summary>
    public bool Ambiguous { get; set; }
    public string? Question { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

/// <summary>Product copy Claude writes from the shop's photo for the quick-product flow (spec §6.1 step 4).</summary>
public sealed class ProductCopy
{
    public string? Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    /// <summary>One of the category names the caller offered, or null when none fits.</summary>
    public string? CategoryName { get; set; }
    /// <summary>Brand printed on the packaging, when readable; null for unbranded fresh food.</summary>
    public string? BrandName { get; set; }
    public bool IsPackaged { get; set; }
    /// <summary>True for fresh meat / fish / poultry / produce that shops sell by weight.</summary>
    public bool IsWeighted { get; set; }
    /// <summary>Up to 4 typical per-unit weights in kg for a weighted product (0.5, 1, 1.5, 2).</summary>
    public List<decimal> WeightOptionsKg { get; set; } = new();
    /// <summary>A packaged product's net weight in kg when printed on the label.</summary>
    public decimal? UnitWeightKg { get; set; }
    public bool Usable { get; set; } = true;
    public string? UnusableReason { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    /// <summary>Web searches Claude ran for the barcode (0 when the tool was off).</summary>
    public int WebSearches { get; set; }
    /// <summary>Claude found the product on the web and used it.</summary>
    public bool FoundOnWeb { get; set; }
    public string? WebSource { get; set; }
}

public sealed class ImageJudgement
{
    public decimal Fidelity { get; set; }
    public decimal Aesthetic { get; set; }
    public bool? LabelTextOk { get; set; }
    public List<string> Issues { get; set; } = new();
    /// <summary>ship | retry | reject.</summary>
    public string Verdict { get; set; } = "ship";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
}

/// <summary>
/// The eyes of the studio (spec §9): Claude identifies the product and what around it is incidental, writes the visual brief
/// for the no-photo look, and judges a result against the original. Claude never generates images.
/// </summary>
public class ProductVisionService
{
    private readonly AiStudioOptions _options;
    private readonly ILogger<ProductVisionService> _logger;
    private readonly Lazy<AnthropicClient?> _client;

    public ProductVisionService(IOptions<AiStudioOptions> options, ILogger<ProductVisionService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<AnthropicClient?>(() =>
            string.IsNullOrWhiteSpace(_options.Anthropic.ApiKey) ? null : new AnthropicClient { ApiKey = _options.Anthropic.ApiKey!.Trim() });
    }

    public bool IsConfigured => _options.UseVision && _client.Value != null;
    /// <summary>The barcode web lookup needs the configured model (web_search_20260209 runs on the Claude 4.6+ / 5 family, which every model name here is).</summary>
    public bool WebSearchAvailable => IsConfigured;
    private string Model => string.IsNullOrWhiteSpace(_options.Anthropic.Model) ? "claude-opus-5-5" : _options.Anthropic.Model!.Trim();

    private static readonly IReadOnlyDictionary<string, string> Languages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["he"] = "Hebrew", ["en"] = "English", ["ar"] = "Arabic", ["ru"] = "Russian",
    };

    private static readonly IReadOnlyDictionary<string, string> Tones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["warm"] = "warm, appetising, concise - like a good butcher talking to a regular customer",
        ["neutral"] = "factual and neutral: what it is, cut / origin, how to store; no taste adjectives, no marketing",
        ["premium"] = "premium and restrained, like a high-end delicatessen: refined vocabulary, provenance and craft, no exclamation marks",
        ["playful"] = "light and friendly with a touch of humour, short punchy sentences, still accurate",
    };

    /// <summary>The Claude model every call here uses (for the provider spend journal).</summary>
    public string ModelName => Model;

    /// <summary>USD estimate of one call from its token counts (list price in <see cref="AiStudioOptions"/>); null when no usage came back.</summary>
    public decimal? CostUsd(int? inputTokens, int? outputTokens)
    {
        if (inputTokens == null && outputTokens == null) return null;
        return Math.Round(((inputTokens ?? 0) * _options.AnthropicInputUsdPerMTok + (outputTokens ?? 0) * _options.AnthropicOutputUsdPerMTok) / 1_000_000m, 5);
    }

    public async Task<ProductIdentification?> IdentifyAsync(ImageInput photo, string? productNameHint, string? categoryHint, CancellationToken cancelToken)
    {
        if (!IsConfigured)
            return null;

        var hint = string.Join(" ", new[]
        {
            string.IsNullOrWhiteSpace(productNameHint) ? null : $"The shop calls this product \"{productNameHint}\".",
            string.IsNullOrWhiteSpace(categoryHint) ? null : $"Shop category: \"{categoryHint}\".",
        }.Where(s => s != null));

        var text = await AskAsync(
            "You prepare product photos for an online grocery / butcher / deli shop. Answer ONLY with a JSON object, no prose.",
            new List<ContentBlockParam>
            {
                ImageBlock(photo),
                new TextBlockParam
                {
                    Text = hint + "\nLook at this photo a shop owner took of a product they sell. Return JSON with keys: " +
                           "product_name (short, in Hebrew if the hint is Hebrew, else English), " +
                           "category_en (2-4 English words describing the kind of product, e.g. \"raw chicken thighs\", \"bottled BBQ sauce\", \"vacuum-packed frozen fish fillet\"), " +
                           "is_packaged (true if the product itself is sold in this packaging: a labelled tray, bottle, box, vacuum bag), " +
                           "usable (false if no complete product is visible - extreme blurry close-up, only part of a tray, a whole display case with many products), " +
                           "unusable_reason (short Hebrew sentence for the shop owner, or empty), " +
                           "incidental (one English sentence naming everything in the frame that is NOT the product and must be removed - aluminium foil, styrofoam tray, cling film, plate, hand, counter, stove, price tag - and instructing to place the product directly on the new surface; empty string if the product is already isolated or the packaging IS the product).",
                },
            }, 600, cancelToken).ConfigureAwait(false);
        if (text == null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(ExtractJson(text.Text));
            var r = doc.RootElement;
            return new ProductIdentification
            {
                ProductName = Str(r, "product_name"),
                CategoryEn = Str(r, "category_en"),
                IsPackaged = Bool(r, "is_packaged") ?? false,
                Usable = Bool(r, "usable") ?? true,
                UnusableReason = Str(r, "unusable_reason"),
                Incidental = Str(r, "incidental"),
                InputTokens = text.InputTokens,
                OutputTokens = text.OutputTokens,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio identify: Claude returned non-JSON: {Text}", text.Text);
            return null;
        }
    }

    /// <summary>
    /// The no-photo look: expand a product name (+ category + the shop's description) into what the camera should see.
    /// When the words do not say what the thing IS (a bundle called "מארז כמות", a code name) Claude says so and the job
    /// asks the shop for a description instead of drawing a random box - that was the QA complaint of 29.10.2026.
    /// </summary>
    public async Task<VisualBrief?> BriefAsync(string productName, string? category, string? description, CancellationToken cancelToken)
    {
        if (!IsConfigured)
            return null;

        var text = await AskAsync(
            "You write visual briefs for a food / grocery product photographer. The shop is Israeli: names are usually Hebrew (meat, poultry, fish, deli, produce, bakery). Answer ONLY with a JSON object.",
            new List<ContentBlockParam>
            {
                new TextBlockParam
                {
                    Text = $"Product name: \"{productName}\". Category: \"{category}\". Shop description: \"{description}\".\n" +
                           "Return JSON with keys: " +
                           "\"ambiguous\": true if, from these words alone, you cannot tell what physical items the photo must show (e.g. a bundle/box/\"מארז\" whose contents are not listed, a code name, a quantity without a product) - do not guess; " +
                           "\"question\": when ambiguous, one short Hebrew sentence asking the shop what the photo should show (with an example), else empty; " +
                           "\"brief\": when not ambiguous, one English sentence (max 50 words) describing exactly what the camera sees - every item, the cut, colour, texture, quantity and how it is arranged; for a bundle list each component; no background, lighting or packaging words; " +
                           "\"category_en\": 2-4 English words for the kind of product (\"raw beef steaks\", \"fresh whole fish\", \"mixed meat bundle\"); " +
                           "\"is_branded_packaged\": true if this is a specific branded packaged product whose packaging a model cannot know",
                },
            }, 500, cancelToken).ConfigureAwait(false);
        if (text == null)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(ExtractJson(text.Text));
            var r = doc.RootElement;
            return new VisualBrief
            {
                Brief = Str(r, "brief"),
                CategoryEn = Str(r, "category_en"),
                IsBrandedPackaged = Bool(r, "is_branded_packaged") ?? false,
                Ambiguous = Bool(r, "ambiguous") ?? false,
                Question = Str(r, "question"),
                InputTokens = text.InputTokens,
                OutputTokens = text.OutputTokens,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio brief: Claude returned non-JSON: {Text}", text.Text);
            return null;
        }
    }

    public async Task<ImageJudgement?> JudgeAsync(ImageInput? original, ImageInput generated, string productName, string look, CancellationToken cancelToken)
    {
        if (!IsConfigured)
            return null;

        var blocks = new List<ContentBlockParam>();
        var intro = original != null
            ? $"Product: {productName}. Requested look: {look}. First image = ORIGINAL shop photo, second image = GENERATED result."
            : $"Product: {productName}. Requested look: {look}. The image was generated from a text description (no original photo).";
        blocks.Add(new TextBlockParam { Text = intro });
        if (original != null)
            blocks.Add(ImageBlock(original));
        blocks.Add(ImageBlock(generated));
        blocks.Add(new TextBlockParam
        {
            Text = original != null
                ? "Return JSON: {\"fidelity\": 0-1 (is it the SAME product: shape, colour, cut, label text, logo, quantity; packaging that was not the product may be gone), " +
                  "\"aesthetic\": 0-1 (does it look like a professional studio/editorial product photo matching the requested look), " +
                  "\"label_text_ok\": true|false|null (null if no text on the product), \"issues\": [\"short bullet\"], \"verdict\": \"ship\"|\"retry\"|\"reject\"}"
                : "Return JSON: {\"fidelity\": 0-1 (does it plausibly depict that product), \"aesthetic\": 0-1, \"label_text_ok\": null, \"issues\": [\"short bullet\"], \"verdict\": \"ship\"|\"retry\"|\"reject\"}",
        });

        var text = await AskAsync("You are a strict photo editor for an online grocery / butcher shop. Compare the AI-generated product photo against the original. Answer ONLY with a JSON object.", blocks, 600, cancelToken).ConfigureAwait(false);
        if (text == null)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(ExtractJson(text.Text));
            var r = doc.RootElement;
            var j = new ImageJudgement
            {
                Fidelity = Dec(r, "fidelity") ?? 0,
                Aesthetic = Dec(r, "aesthetic") ?? 0,
                LabelTextOk = Bool(r, "label_text_ok"),
                Verdict = Str(r, "verdict") ?? "ship",
                InputTokens = text.InputTokens,
                OutputTokens = text.OutputTokens,
            };
            if (r.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
                j.Issues = issues.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => s.Length > 0).Take(6).ToList();
            return j;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio judge: Claude returned non-JSON: {Text}", text.Text);
            return null;
        }
    }

    /// <summary>
    /// "כתוב ב-AI" in the quick-product flow: name, descriptions, category (from the shop's own list), brand and the
    /// weight suggestion, all from the photo. Hebrew output; tone per the account. Synchronous, 5-8 seconds.
    /// </summary>
    public async Task<ProductCopy?> DescribeAsync(ImageInput photo, string? nameHint, string? categoryHint, IReadOnlyList<string> categoryNames, string? barcode, string? tone, string? language, bool webSearch, CancellationToken cancelToken)
    {
        if (!IsConfigured)
            return null;

        var lang = Languages.TryGetValue(language ?? string.Empty, out var l) ? l : "Hebrew";
        var toneLine = Tones.TryGetValue(tone ?? string.Empty, out var tl) ? tl : Tones["warm"];
        var cats = categoryNames.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().Take(300).ToList();
        var hints = string.Join("\n", new[]
        {
            string.IsNullOrWhiteSpace(nameHint) ? null : $"The shop already typed this name: \"{nameHint}\" - keep it unless it is clearly wrong, and polish it.",
            string.IsNullOrWhiteSpace(categoryHint) ? null : $"The shop already picked the category \"{categoryHint}\".",
            string.IsNullOrWhiteSpace(barcode) ? null : webSearch
                ? $"Barcode scanned: {barcode}. Use the web_search tool (at most 3 searches) to look this barcode up - the official product name, brand, net weight and what it is; prefer Israeli retailers and the brand's own site (an Israeli barcode starts with 729). If nothing reliable comes up, rely on the photo alone and say so."
                : $"Barcode / PLU scanned: {barcode} (a 2-5 digit code is the shop's own PLU and says nothing about the product).",
            cats.Count == 0 ? "The shop has no categories yet: return null for category_name." : "The shop's categories (pick exactly one of these, verbatim, or null): " + string.Join(" | ", cats),
        }.Where(s => s != null));
        var nameExamples = lang == "Hebrew" ? "e.g. \"אנטריקוט טרי\", \"פילה סלמון נורבגי\", \"קטשופ היינץ 570 גרם\"" : "e.g. \"Fresh Entrecôte\", \"Norwegian Salmon Fillet\", \"Heinz Ketchup 570 g\"";

        var text = await AskAsync(
            $"You write product listings for an Israeli online butcher / grocery / deli (Giorgio). Everything you write is in {lang}, " +
            "natural and specific, never generic marketing fluff. Answer ONLY with a JSON object, no prose" + (webSearch ? " (after any searches, the final answer is the JSON object alone)." : "."),
            new List<ContentBlockParam>
            {
                ImageBlock(photo),
                new TextBlockParam
                {
                    Text = hints + "\nLook at this photo a shop owner took of a product they sell and write its listing. Tone: " + toneLine + ".\n" +
                           "Return JSON with keys: " +
                           $"\"usable\" (false if no complete product is visible), \"unusable_reason\" (short {lang} sentence or empty), " +
                           $"\"name\" ({lang} product name, 2-6 words, the way it appears on a shop shelf: cut / kind + notable attribute, {nameExamples}), " +
                           $"\"short_description\" (one {lang} sentence, max 20 words, what it is and what it is good for), " +
                           $"\"long_description\" (2-3 {lang} sentences: origin / cut / texture, how to cook or serve, storage; no invented certifications or prices), " +
                           "\"category_name\" (one of the shop's categories verbatim, or null), " +
                           "\"brand_name\" (brand printed on the packaging when readable, else null), " +
                           "\"is_packaged\" (true if sold in this packaging: labelled tray, bottle, box, vacuum bag), " +
                           "\"is_weighted\" (true for fresh meat / poultry / fish / cheese / produce that is sold by weight, false for packaged goods), " +
                           "\"weight_options_kg\" (when is_weighted: up to 4 typical per-unit weights in kg a customer would order, ascending, e.g. [0.5, 1, 1.5, 2] for beef, [1, 1.5, 2, 3] for a whole fish; else []), " +
                           "\"unit_weight_kg\" (packaged product: net weight in kg from the label, else null)" +
                           (webSearch ? ", \"found_on_web\" (true if a web result identified this barcode and you used it), \"web_source\" (the URL of that result, else null)." : "."),
                },
            }, 900, cancelToken, webSearch ? new List<ToolUnion> { new ToolUnion(new WebSearchTool20260209 { MaxUses = 3 }) } : null).ConfigureAwait(false);
        if (text == null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(ExtractJson(text.Text));
            var r = doc.RootElement;
            var copy = new ProductCopy
            {
                Name = Str(r, "name"),
                ShortDescription = Str(r, "short_description"),
                LongDescription = Str(r, "long_description"),
                CategoryName = Str(r, "category_name"),
                BrandName = Str(r, "brand_name"),
                IsPackaged = Bool(r, "is_packaged") ?? false,
                IsWeighted = Bool(r, "is_weighted") ?? false,
                UnitWeightKg = Num(r, "unit_weight_kg"),
                Usable = Bool(r, "usable") ?? true,
                UnusableReason = Str(r, "unusable_reason"),
                InputTokens = text.InputTokens,
                OutputTokens = text.OutputTokens,
                WebSearches = text.WebSearches,
                FoundOnWeb = webSearch && (Bool(r, "found_on_web") ?? false),
                WebSource = webSearch ? Str(r, "web_source") : null,
            };
            if (copy.WebSource != null && !copy.WebSource.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                copy.WebSource = null;
            if (r.TryGetProperty("weight_options_kg", out var w) && w.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in w.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetDecimal(out var d) && d > 0 && d <= 50)
                        copy.WeightOptionsKg.Add(Math.Round(d, 3));
                }
                copy.WeightOptionsKg = copy.WeightOptionsKg.Distinct().OrderBy(d => d).Take(4).ToList();
            }
            // The category must be one the shop really has; Claude sometimes "fixes" spelling.
            if (copy.CategoryName != null && cats.Count > 0 && !cats.Contains(copy.CategoryName))
            {
                var loose = cats.FirstOrDefault(c => string.Equals(c, copy.CategoryName, StringComparison.OrdinalIgnoreCase))
                            ?? cats.FirstOrDefault(c => c.Contains(copy.CategoryName, StringComparison.OrdinalIgnoreCase) || copy.CategoryName.Contains(c, StringComparison.OrdinalIgnoreCase));
                copy.CategoryName = loose;
            }
            return copy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio describe: Claude returned non-JSON: {Text}", text.Text);
            return null;
        }
    }

    //*************************    Private    *************************//

    private sealed record Answer(string Text, int? InputTokens, int? OutputTokens, int WebSearches = 0);

    private async Task<Answer?> AskAsync(string system, List<ContentBlockParam> content, int maxTokens, CancellationToken cancelToken, List<ToolUnion>? tools = null)
    {
        var client = _client.Value;
        if (client == null)
            return null;
        try
        {
            var message = await client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                // A web lookup needs room for the search turns + the JSON; the plain calls stay tight.
                MaxTokens = tools == null ? maxTokens : Math.Max(maxTokens, 2500),
                System = system,
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Tools = tools,
                Messages = new List<MessageParam> { new MessageParam { Role = Role.User, Content = content } },
            }, cancellationToken: cancelToken).ConfigureAwait(false);

            if (message.StopReason == "refusal")
            {
                _logger.LogWarning("AiStudio vision: Claude refused ({Category})", message.StopDetails?.Category);
                return null;
            }
            // With server tools the answer has several text blocks ("let me look that up…", then the JSON): take the last one that
            // holds an object; without tools, the whole text as before.
            var texts = message.Content.Select(b => b.TryPickText(out var t) ? t.Text : null).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList();
            var text = tools != null ? texts.LastOrDefault(s => s.Contains('{')) ?? string.Concat(texts) : string.Concat(texts);
            var searches = tools == null ? 0 : (int)(message.Usage.ServerToolUse?.WebSearchRequests ?? 0);
            return new Answer(text, (int?)message.Usage.InputTokens, (int?)message.Usage.OutputTokens, searches);
        }
        catch (Exception ex)
        {
            // Vision is advisory: a failure here must never block the image job.
            _logger.LogWarning(ex, "AiStudio vision call failed");
            return null;
        }
    }

    private static ImageBlockParam ImageBlock(ImageInput img) => new()
    {
        Source = new Base64ImageSource
        {
            Data = Convert.ToBase64String(img.Bytes),
            MediaType = img.Mime,
        },
    };

    private static string ExtractJson(string text)
    {
        var a = text.IndexOf('{');
        var z = text.LastIndexOf('}');
        return a >= 0 && z > a ? text.Substring(a, z - a + 1) : text;
    }

    private static string? Str(JsonElement r, string key) =>
        r.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;

    private static bool? Bool(JsonElement r, string key) =>
        r.TryGetProperty(key, out var v) ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null } : null;

    private static decimal? Num(JsonElement r, string key) =>
        r.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d > 0 ? d : null;

    private static decimal? Dec(JsonElement r, string key) =>
        r.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? Math.Clamp(d, 0, 1) : null;
}
