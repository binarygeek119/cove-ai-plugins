using System.Text.RegularExpressions;

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

internal static partial class CoverPromptBuilder
{
    public const string AspectRatio = "16:9";

    [GeneratedRegex(
        @"\b(?:gilfs?|grann(?:y|ies)|grandmas?|grandmothers?|nanas?|grandpas?|grandfathers?|elderly|geriatric|old\s+(?:man|men|woman|women|lady|ladies|guy|guys)|senior\s+citizens?|grey-?haired|gray-?haired|white-?haired|wrinkl(?:e|ed|es|y)|cougars?|silver\s+fox(?:es)?|[6-9]0(?:s|\s*(?:year|yr)s?\s*old))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OlderAdultCue();

    public static IReadOnlyList<CoverPrompt> BuildAttempts(CoverTarget target)
    {
        var title = DisallowedFetishFilter.FilterText(PromptSanitizer.Sanitize(target.Title));
        if (string.IsNullOrWhiteSpace(title))
            title = $"{target.Kind} {target.Id}";

        var details = DisallowedFetishFilter.FilterText(PromptSanitizer.Sanitize(target.Details));
        var body = DisallowedFetishFilter.FilterText(PromptSanitizer.Sanitize(target.TextContent));
        var tags = DisallowedFetishFilter.FilterTags(
            PromptSanitizer.SanitizeAll(DisallowedFetishFilter.FilterTags(target.Tags)));
        var people = DisallowedFetishFilter.FilterTags(
            PromptSanitizer.SanitizeAll(DisallowedFetishFilter.FilterTags(target.Performers)));
        var olderNeeded = NeedsOlderAdults(target, title, details, body, tags, people);

        var attempts = new List<CoverPrompt>
        {
            Compose(title, details, body, tags, people, "full", olderNeeded),
        };

        if (!string.IsNullOrWhiteSpace(details) || !string.IsNullOrWhiteSpace(body))
            attempts.Add(Compose(title, null, null, tags, people, "without-content", olderNeeded));

        if (tags.Count > 0)
            attempts.Add(Compose(title, null, null, tags, [], "title-and-fetishes", olderNeeded));

        if (people.Count > 0)
            attempts.Add(Compose(title, null, null, [], people, "without-tags", olderNeeded));

        attempts.Add(Compose(title, null, null, [], [], "title-only", olderNeeded));
        return attempts;
    }

    private static CoverPrompt Compose(
        string title,
        string? details,
        string? body,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> people,
        string layer,
        bool olderNeeded)
    {
        var sections = new List<string>
        {
            "Widescreen 16:9 unrated adult erotic movie poster, theatrical one-sheet, cinematic lighting, dramatic composition, photorealistic.",
            "Adult-rated NSFW film marketing still, sexually charged, fetish-friendly, explicit-allowed XXX / NC-17 cover energy, attractive adult bodies, not a family-friendly, vanilla, or PG poster.",
            $"The stylized movie title \"{title}\" is painted on the poster in large custom lettering, movie-title typography, clearly readable.",
            "Every person shown is a consenting adult clearly over 18. No minors.",
            olderNeeded
                ? "Include older adult characters only where the story or tags call for them; everyone is still clearly 18+."
                : "Lead characters are attractive young adults in their early to mid 20s: youthful adult faces, smooth skin, fit bodies. Do not depict middle-aged or elderly people.",
        };

        if (tags.Count > 0)
        {
            sections.Add(
                "Show the fetish and kink themes on the cover in costume, props, pose, and setting. Do not hide them or replace them with a generic vanilla couple.");
        }

        if (!string.IsNullOrWhiteSpace(body))
            sections.Add("Story content to illustrate, including fetishes described: " + Trim(body, 1800));
        else if (!string.IsNullOrWhiteSpace(details))
            sections.Add("Synopsis, including fetishes described: " + Trim(details, 800));

        if (people.Count > 0)
            sections.Add("Characters: " + string.Join(", ", people.Take(12)));

        if (tags.Count > 0)
            sections.Add("Fetishes and kinks that must appear in the image: " + string.Join(", ", tags.Take(24)));

        var negative = new List<string>
        {
            "child", "minor", "underage", "teen", "loli", "shota", "kid", "ageplay",
            "bestiality", "snuff",
            "family-friendly", "PG poster", "Disney", "sfw wholesome",
            "vanilla romance", "generic Hollywood couple", "ignoring fetishes",
            "blurry", "low quality", "watermark", "extra fingers", "deformed face",
            "subtitle bar", "busy collage", "text other than the movie title",
        };

        if (!olderNeeded)
        {
            negative.AddRange(
            [
                "elderly", "old man", "old woman", "grandma", "grandpa", "geriatric",
                "wrinkles", "sagging skin", "grey hair", "white hair", "liver spots",
                "middle-aged", "60 years old", "heavy aging",
            ]);
        }

        return new CoverPrompt(string.Join(" ", sections), string.Join(", ", negative), layer);
    }

    private static bool NeedsOlderAdults(
        CoverTarget target,
        string title,
        string? details,
        string? body,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> people)
    {
        var parts = new List<string?>
        {
            target.Title,
            target.Details,
            target.TextContent,
            title,
            details,
            body,
        };
        parts.AddRange(target.Tags);
        parts.AddRange(target.Performers);
        parts.AddRange(tags);
        parts.AddRange(people);

        var haystack = string.Join(" ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        return OlderAdultCue().IsMatch(haystack);
    }

    private static string Trim(string value, int max)
    {
        if (value.Length <= max)
            return value;
        return value[..max].Trim() + "…";
    }
}
