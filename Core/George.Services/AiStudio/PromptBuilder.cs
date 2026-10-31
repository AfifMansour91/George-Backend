namespace George.Services.AiStudio;

/// <summary>Look template + category + scene + what-is-not-the-product + the fixed rules → one prompt (spec §5).</summary>
public static class PromptBuilder
{
    public static string Build(AiStudioLooks.LookDef look, string? categoryEn, string ratio, string? incidental, string? sceneOverride, string? brief, string? background = null, string? styleNotes = null)
    {
        var category = string.IsNullOrWhiteSpace(categoryEn) ? "food" : categoryEn.Trim();
        var ratioText = ratio switch { "4:5" => "portrait 4:5", "3:4" => "portrait 3:4", "4:3" => "landscape 4:3", "16:9" => "wide 16:9", _ => "square 1:1" };
        var scene = string.IsNullOrWhiteSpace(sceneOverride) ? AiStudioLooks.SceneFor(category) : sceneOverride.Trim();

        // generate: no source photo, so the template is the chosen BACKGROUND's one and the subject is Claude's brief.
        var template = look.Key == AiStudioLooks.Generate ? AiStudioLooks.GenerateTemplateFor(background) : look.Template;
        var body = template
            .Replace("{category}", category)
            .Replace("{ratio}", ratioText)
            .Replace("{scene}", scene)
            .Replace("{brief}", string.IsNullOrWhiteSpace(brief) ? category : brief.Trim());

        // The shop's own wishes (הגדרות › סטודיו AI › המראים שלי), after the look and before the fixed rules so they can colour the
        // scene but never override "keep the product exactly". Any language: the image models read Hebrew well enough.
        var notes = string.IsNullOrWhiteSpace(styleNotes) ? null : "Shop's style preferences (apply where they do not conflict with the instructions above): " + styleNotes.Trim().TrimEnd('.') + ".";

        if (look.Key == AiStudioLooks.Generate)
            return notes == null ? body : body + " " + notes;

        var parts = new List<string>();
        // The incidental sentence goes FIRST: the spike showed the model keeps foil/trays unless told before the style text.
        if (!string.IsNullOrWhiteSpace(incidental))
            parts.Add(incidental.Trim());
        parts.Add(body);
        if (notes != null)
            parts.Add(notes);
        parts.Add(AiStudioLooks.FixedRules);
        return string.Join(" ", parts);
    }
}
