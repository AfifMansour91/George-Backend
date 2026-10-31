using George.DB;

namespace George.Services.AiStudio;

/// <summary>
/// The looks a shop can ask for (spec §5) and their prompt templates. Prompts are English (what image models read best);
/// labels are what the shop sees. The code holds the defaults; the super-admin's <see cref="AiStylePreset"/> rows override
/// label / prompt / provider / price / active per look (phase 4) and are applied with <see cref="Apply"/>.
/// </summary>
public static class AiStudioLooks
{
    public const string StudioWhite = "studio_white";
    public const string Dark = "dark";
    public const string Marble = "marble";
    public const string Ambience = "ambience";
    public const string Enhance = "enhance";
    public const string Generate = "generate";

    /// <summary>Appended to every edit prompt. The spike showed the models obey this literally - which is also why
    /// <see cref="PromptBuilder"/> must say what is NOT the product.</summary>
    public const string FixedRules =
        "Keep the product EXACTLY as in the product photo: same shape, proportions, colours, texture, label text and logos. " +
        "Do not add, remove, rotate or restyle the product itself. No added text, no watermark, no people, no hands. " +
        "Photorealistic commercial product photography, high resolution, tack-sharp focus on the product.";

    /// <summary><paramref name="Provider"/> null = AiStudio:DefaultProvider. <paramref name="IsActive"/> false = hidden from the shops
    /// (an inactive look still resolves for old jobs' labels).</summary>
    public sealed record LookDef(string Key, string Label, string Description, int Credits, bool NeedsSource, string Template, string? Provider = null, bool IsActive = true);

    /// <summary>The code defaults, in UI order. <see cref="All"/> is these with the super-admin's AiStylePreset rows applied.</summary>
    public static readonly IReadOnlyList<LookDef> Defaults = new List<LookDef>
    {
        new(StudioWhite, "רקע לבן", "סטודיו מקצועי, רקע לבן נקי", 4, true,
            "Re-shoot this {category} product as a professional studio product photo on a seamless pure white background. " +
            "Large softbox key light from the upper left, gentle fill from the right, a soft natural contact shadow under the product and a very subtle floor reflection. " +
            "85mm lens, f/8, neutral white balance, {ratio} composition with the product centred and filling about 70% of the frame."),
        new(Dark, "רקע כהה", "צפחה כהה, תאורה דרמטית (סגנון deliz)", 4, true,
            "Re-shoot this {category} product in the exact photographic style of the style-reference image(s): dark charcoal slate surface, " +
            "low-key moody lighting with a single dramatic soft light from the side, deep shadows, premium butcher-shop feel. " +
            "For fresh meat or fish keep the surfaces glossy and fresh. Eye-level or slight top-down view, {ratio}, product centred. " +
            "The reference images are only for style, do not copy their products."),
        new(Marble, "שיש בהיר", "שיש לבן, אור יום רך (סגנון meat-shop)", 4, true,
            "Re-shoot this {category} product in the exact photographic style of the style-reference image(s): white marble surface with soft grey veins, " +
            "bright airy daylight from a side window, high-key, clean, shallow depth of field, premium delicatessen feel. " +
            "Top-down or slight angle, {ratio}, product centred. The reference images are only for style, do not copy their products."),
        new(Ambience, "אווירה", "סצנה עם אביזרים לפי סוג המוצר", 6, true,
            "Re-shoot this {category} product as an editorial lifestyle photo: {scene}. The product is the hero, in sharp focus, centred, " +
            "filling about 60% of the frame; props are tasteful, out of focus and never cover the product. Warm natural light, shallow depth of field, {ratio}."),
        new(Enhance, "שיפור תמונה", "אותה תמונה, תאורה וחדות של צלם", 2, true,
            "Enhance this phone photo of a {category} product like a professional retoucher would, keeping the exact same composition, framing, background and product. " +
            "Fix exposure and white balance (remove yellow/green casts from shop lighting), recover shadows and highlights, increase sharpness and micro-contrast, " +
            "remove noise, glare and reflections, make fresh meat/fish look fresh and appetizing. Do not move, add or remove anything. {ratio} crop centred on the product."),
        new(Generate, "צור תמונה מתיאור", "ללא צילום, לפי שם המוצר והתיאור (המחשה)", 5, false,
            // Placeholder only: the real template comes from GenerateTemplateFor(background).
            "Professional studio product photograph of {brief}."),
    };

    private static volatile IReadOnlyList<LookDef> _current = Defaults;

    /// <summary>False until the first <see cref="Apply"/> of this process (the API applies lazily on the first /Status; the worker every 5 min).</summary>
    public static bool Applied { get; private set; }

    /// <summary>The looks as the shops see them: code defaults overridden by the global AiStylePreset rows (label, description,
    /// prompt, provider, credits, active). Refreshed by <see cref="Apply"/> at startup, every few minutes and after an admin save.</summary>
    public static IReadOnlyList<LookDef> All => _current;

    /// <summary>Only the looks the shops may start (inactive ones are kept in <see cref="All"/> for labels of old jobs).</summary>
    public static IEnumerable<LookDef> Active => _current.Where(l => l.IsActive);

    /// <summary>Overlay the global presets (AccountId NULL) on the code defaults. Idempotent; a missing row = code default.</summary>
    public static void Apply(IEnumerable<AiStylePreset>? presets)
    {
        var byLook = new Dictionary<string, AiStylePreset>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in presets ?? Array.Empty<AiStylePreset>())
            if (p.AccountId == null && !string.IsNullOrWhiteSpace(p.Look))
                byLook[p.Look.Trim()] = p;
        var merged = Defaults.Select((d, i) => (Look: byLook.TryGetValue(d.Key, out var p) ? Merge(d, p) : d, Order: byLook.TryGetValue(d.Key, out var q) && q.SortOrder > 0 ? q.SortOrder : 1000 + i)).ToList();
        _current = merged.OrderBy(x => x.Order).Select(x => x.Look).ToList();
        Applied = true;
    }

    /// <summary>One look with a preset row applied (the global overlay, and per-account overrides at job time).</summary>
    public static LookDef Merge(LookDef d, AiStylePreset p) => d with
    {
        Label = string.IsNullOrWhiteSpace(p.Label) ? d.Label : p.Label.Trim(),
        Description = string.IsNullOrWhiteSpace(p.Description) ? d.Description : p.Description.Trim(),
        // The generate look's template comes from GenerateTemplateFor(background), not from the row.
        Template = string.IsNullOrWhiteSpace(p.Prompt) || d.Key == Generate ? d.Template : p.Prompt.Trim(),
        Provider = string.IsNullOrWhiteSpace(p.Provider) ? d.Provider : p.Provider.Trim().ToLowerInvariant(),
        Credits = p.Credits is >= 0 ? p.Credits.Value : d.Credits,
        IsActive = p.IsActive,
    };

    /// <summary>The looks a shop can pick as the BACKGROUND of a generated (no-photo) image, in UI order.</summary>
    public static readonly IReadOnlyList<string> BackgroundKeys = new[] { StudioWhite, Dark, Marble, Ambience };

    public static bool IsBackground(string? key) =>
        !string.IsNullOrWhiteSpace(key) && BackgroundKeys.Any(b => string.Equals(b, key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Prompt template for the no-photo look, per chosen background. {brief} is Claude's visual brief (what the camera sees),
    /// {scene} the ambience props, {ratio} the composition. Same lighting words as the matching edit look, so a generated
    /// image sits next to an edited one in the same shop without looking foreign.
    /// </summary>
    public static string GenerateTemplateFor(string? background)
    {
        const string tail = " Photorealistic commercial product photography, high resolution, tack-sharp focus on the product. " +
                            "Show the food itself, not a packaged or branded product: no printed labels, no logos, no text, no watermark, no people, no hands.";
        var key = IsBackground(background) ? background!.Trim().ToLowerInvariant() : StudioWhite;
        return key switch
        {
            Dark =>
                "Professional product photograph of {brief}, in the exact photographic style of the style-reference image(s): dark charcoal slate surface, " +
                "low-key moody lighting with a single dramatic soft light from the side, deep shadows, premium butcher-shop feel. " +
                "For fresh meat or fish keep the surfaces glossy and fresh. Eye-level or slight top-down view, {ratio}, product centred and filling about 65% of the frame. " +
                "The reference images are only for style, do not copy their products." + tail,
            Marble =>
                "Professional product photograph of {brief}, in the exact photographic style of the style-reference image(s): white marble surface with soft grey veins, " +
                "bright airy daylight from a side window, high-key, clean, shallow depth of field, premium delicatessen feel. " +
                "Top-down or slight angle, {ratio}, product centred and filling about 65% of the frame. The reference images are only for style, do not copy their products." + tail,
            Ambience =>
                "Editorial lifestyle product photograph of {brief}: {scene}. The product is the hero, in sharp focus, centred, filling about 60% of the frame; " +
                "props are tasteful, out of focus and never cover the product. Warm natural light, shallow depth of field, {ratio}." + tail,
            _ =>
                "Professional studio product photograph of {brief}. Seamless pure white background, large softbox key light from the upper left, gentle fill from the right, " +
                "a soft natural contact shadow under the product and a very subtle floor reflection. 85mm lens, f/8, neutral white balance, {ratio} composition, " +
                "product centred and filling about 70% of the frame." + tail,
        };
    }

    public static LookDef? Find(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : All.FirstOrDefault(l => string.Equals(l.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Scene + props for the ambience look, by category keyword (Hebrew or English, matched loosely).</summary>
    public static string SceneFor(string? category)
    {
        var c = (category ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(c, "עוף", "פרגית", "הודו", "chicken", "poultry", "turkey"))
            return "on a rustic wooden butcher block with a sprig of rosemary, a few garlic cloves and coarse salt beside it, dark blurred kitchen behind";
        if (ContainsAny(c, "דג", "סלמון", "לוקוס", "fish", "seafood", "salmon"))
            return "on a bed of crushed ice on a slate tray with a lemon wedge and fresh dill, cool blue-white light, a quiet dark blurred background";
        if (ContainsAny(c, "בשר", "בקר", "טלה", "כבש", "סטייק", "beef", "meat", "lamb", "steak"))
            return "on a wooden board with cracked black pepper, rosemary and a small bowl of coarse salt, dark blurred kitchen behind";
        if (ContainsAny(c, "שמן", "רוטב", "רטבים", "oil", "sauce"))
            return "on a rustic wooden table next to a grilled steak on a board and a few fresh herbs, warm evening light, blurred backyard grill behind";
        if (ContainsAny(c, "ירק", "פטרי", "פירות", "vegetable", "mushroom", "produce", "fruit"))
            return "on natural linen on a wooden table with fresh herbs and a knife beside it, soft daylight, blurred farm kitchen behind";
        if (ContainsAny(c, "מאפ", "לחם", "bakery", "bread", "pastry"))
            return "on a marble counter dusted with flour, wheat ears and a cup of coffee beside it, warm morning light";
        if (ContainsAny(c, "גבינ", "חלב", "cheese", "dairy"))
            return "on a slate board with grapes, a drizzle of honey and walnuts beside it, soft window light";
        if (ContainsAny(c, "רהיט", "furniture"))
            return "in a styled room that matches the item, natural window light, a plant and a rug in the blurred background";
        if (ContainsAny(c, "קוסמט", "טיפוח", "cosmetic", "beauty"))
            return "on a glossy surface with a pastel backdrop, water droplets and a few petals, soft studio light";
        return "on a neutral studio table with a soft gradient backdrop and a single tasteful prop that suits the product, warm natural light";
    }

    private static bool ContainsAny(string s, params string[] needles) => needles.Any(s.Contains);
}
