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

    private enum PosterStyle
    {
        Adult,
        Soft,
        Plain,
    }

    [GeneratedRegex(
        @"\b(?:kinky|kinks?|fetishes?|xxx|nsfw|porn(?:o|ographic)?|explicit|erotic|sex(?:ual|y)?|bdsm|bondage|nude|naked)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TriggerTitleWords();

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
        var hasStory = !string.IsNullOrWhiteSpace(details) || !string.IsNullOrWhiteSpace(body);

        var attempts = new List<CoverPrompt>
        {
            Compose(title, details, body, tags, people, "full", olderNeeded, PosterStyle.Adult),
        };

        if (hasStory)
            attempts.Add(Compose(title, null, null, tags, people, "without-story", olderNeeded, PosterStyle.Adult));

        if (tags.Count > 2)
        {
            var split = Math.Max(1, tags.Count / 2);
            attempts.Add(Compose(title, null, null, tags.Take(split).ToList(), people, "first-half-tags", olderNeeded, PosterStyle.Adult));
            attempts.Add(Compose(title, null, null, tags.Skip(split).ToList(), people, "second-half-tags", olderNeeded, PosterStyle.Adult));
        }

        if (tags.Count > 0)
            attempts.Add(Compose(title, null, null, [], people, "without-tags", olderNeeded, PosterStyle.Adult));

        var safeTitle = SoftenTitle(title);
        attempts.Add(Compose(safeTitle, null, null, [], [], "title-soft", olderNeeded, PosterStyle.Soft));
        attempts.Add(Compose(safeTitle, null, null, [], [], "plain-title", olderNeeded, PosterStyle.Plain));
        return attempts;
    }

    private static CoverPrompt Compose(
        string title,
        string? details,
        string? body,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> people,
        string layer,
        bool olderNeeded,
        PosterStyle style)
    {
        if (style == PosterStyle.Plain)
        {
            return new CoverPrompt(
                string.Join(" ",
                [
                    "Widescreen 16:9 cinematic movie poster graphic, dramatic lighting, elegant empty background, no people.",
                    $"The movie title \"{title}\" is the only subject: large stylized painted lettering, theatrical typography, clearly readable.",
                    "Graphic design title card, artful text treatment, film poster layout.",
                ]),
                string.Join(", ",
                [
                    "people", "faces", "bodies", "nude", "child", "minor", "underage",
                    "blurry", "low quality", "watermark", "busy collage", "subtitle bar",
                ]),
                layer);
        }

        var sections = new List<string>
        {
            style == PosterStyle.Adult
                ? "Widescreen 16:9 unrated adult erotic movie poster, theatrical one-sheet, cinematic lighting, dramatic composition, photorealistic."
                : "Widescreen 16:9 theatrical movie poster cover art, cinematic lighting, dramatic composition, photorealistic film one-sheet.",
        };

        if (style == PosterStyle.Adult)
        {
            sections.Add(
                "Adult-rated NSFW film marketing still, sexually charged, fetish-friendly, explicit-allowed XXX / NC-17 cover energy, attractive adult bodies, not a family-friendly, vanilla, or PG poster.");
        }

        sections.Add($"The stylized movie title \"{title}\" is painted on the poster in large custom lettering, movie-title typography, clearly readable.");
        sections.Add("Every person shown is a consenting adult clearly over 18. No minors.");
        sections.Add(
            olderNeeded
                ? "Include older adult characters only where the story or tags call for them; everyone is still clearly 18+."
                : "Lead characters are attractive young adults in their early to mid 20s: youthful adult faces, smooth skin, fit bodies. Do not depict middle-aged or elderly people.");

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
            "blurry", "low quality", "watermark", "extra fingers", "deformed face",
            "subtitle bar", "busy collage", "text other than the movie title",
        };

        if (style == PosterStyle.Adult)
        {
            negative.AddRange(["family-friendly", "PG poster", "Disney", "sfw wholesome", "vanilla romance", "generic Hollywood couple"]);
            if (tags.Count > 0)
                negative.Add("ignoring fetishes");
        }

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

    private static string SoftenTitle(string title)
    {
        var softened = TriggerTitleWords().Replace(title, " ");
        softened = string.Join(" ", softened.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(softened) ? title : softened;
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
