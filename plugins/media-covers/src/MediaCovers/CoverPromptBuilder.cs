namespace MediaCovers;

internal sealed record CoverTarget(
    string Kind,
    int Id,
    string Title,
    string? Details,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Performers,
    string? TextContent);

internal sealed record CoverPrompt(string Prompt, string NegativePrompt, string Layer);

internal static class CoverPromptBuilder
{
    public const string AspectRatio = "16:9";

    public static IReadOnlyList<CoverPrompt> BuildAttempts(CoverTarget target)
    {
        var title = PromptSanitizer.Sanitize(target.Title);
        if (string.IsNullOrWhiteSpace(title))
            title = $"{target.Kind} {target.Id}";

        var details = PromptSanitizer.Sanitize(target.Details);
        var body = PromptSanitizer.Sanitize(target.TextContent);
        var tags = PromptSanitizer.SanitizeAll(target.Tags);
        var people = PromptSanitizer.SanitizeAll(target.Performers);

        var attempts = new List<CoverPrompt>
        {
            Compose(title, details, body, tags, people, "full"),
        };

        if (!string.IsNullOrWhiteSpace(details) || !string.IsNullOrWhiteSpace(body))
            attempts.Add(Compose(title, null, null, tags, people, "without-content"));

        if (tags.Count > 0)
            attempts.Add(Compose(title, null, null, [], people, "without-tags"));

        attempts.Add(Compose(title, null, null, [], [], "title-only"));
        return attempts;
    }

    private static CoverPrompt Compose(
        string title,
        string? details,
        string? body,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> people,
        string layer)
    {
        var sections = new List<string>
        {
            "Widescreen 16:9 theatrical movie poster cover art, cinematic lighting, dramatic composition, photorealistic film one-sheet.",
            $"The stylized movie title \"{title}\" is painted on the poster in large custom lettering, movie-title typography, clearly readable.",
            "Every person shown is a consenting adult clearly over 18.",
        };

        if (!string.IsNullOrWhiteSpace(body))
            sections.Add("Story content to illustrate: " + Trim(body, 1800));
        else if (!string.IsNullOrWhiteSpace(details))
            sections.Add("Synopsis: " + Trim(details, 800));

        if (people.Count > 0)
            sections.Add("Characters: " + string.Join(", ", people.Take(12)));

        if (tags.Count > 0)
            sections.Add("Themes: " + string.Join(", ", tags.Take(16)));

        var negative = string.Join(", ",
        [
            "child", "minor", "underage", "teen", "loli", "shota", "kid",
            "blurry", "low quality", "watermark", "extra fingers", "deformed face",
            "subtitle bar", "busy collage", "text other than the movie title",
        ]);

        return new CoverPrompt(string.Join(" ", sections), negative, layer);
    }

    private static string Trim(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..max].Trim() + "…";
    }
}
