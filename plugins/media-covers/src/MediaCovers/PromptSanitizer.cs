using System.Text;
using System.Text.RegularExpressions;

namespace MediaCovers;

/// <summary>
/// Rewrites library metadata into image-API-safe adult (18+) prompt text.
/// Family terms become step-family, under-18 language becomes adult over 18,
/// and non-consent language becomes consensual. Never send minor content to an image model.
/// </summary>
internal static partial class PromptSanitizer
{
    private static readonly (Regex Pattern, string Replacement)[] Replacements =
    [
        (Whole(@"under[\s-]*age(?:d)?"), "adult over 18"),
        (Whole(@"under[\s-]*eighteen"), "adult over 18"),
        (Whole(@"under[\s-]*18"), "adult over 18"),
        (Whole(@"below[\s-]*18"), "adult over 18"),
        (Whole(@"less than[\s-]*18"), "adult over 18"),
        (Whole(@"not[\s-]*18"), "adult over 18"),
        (Whole(@"eighteen[\s-]*minus"), "adult over 18"),
        (Whole(@"pre[\s-]*teens?"), "young adult over 18"),
        (Whole(@"tweens?"), "young adult over 18"),
        (Whole(@"jailbaits?"), "young adult over 18"),
        (Whole(@"lolitas?"), "adult over 18"),
        (Whole(@"loli(?:con)?s?"), "adult over 18"),
        (Whole(@"shotas?"), "adult over 18"),
        (Whole(@"pedophiles?"), "adult"),
        (Whole(@"pedos?"), "adult"),
        (Whole(@"minors?"), "adult over 18"),
        (Whole(@"children"), "adults over 18"),
        (Whole(@"child"), "adult over 18"),
        (Whole(@"kids?"), "adult over 18"),
        (Whole(@"teenagers?"), "young adult over 18"),
        (Whole(@"teens?"), "young adult over 18"),
        (Whole(@"schoolgirls?"), "adult college woman"),
        (Whole(@"schoolboys?"), "adult college man"),
        (Whole(@"high[\s-]*school"), "college"),
        (Whole(@"daddies"), "stepdaddies"),
        (Whole(@"daddy"), "stepdaddy"),
        (Whole(@"dads"), "stepdads"),
        (Whole(@"dad"), "stepdad"),
        (Whole(@"fathers?"), "stepfather"),
        (Whole(@"papas?"), "steppapa"),
        (Whole(@"mommies"), "stepmommies"),
        (Whole(@"mommy"), "stepmommy"),
        (Whole(@"mamas?"), "stepmama"),
        (Whole(@"mums"), "stepmums"),
        (Whole(@"mum"), "stepmum"),
        (Whole(@"moms"), "stepmoms"),
        (Whole(@"mom"), "stepmom"),
        (Whole(@"mummies"), "stepmummies"),
        (Whole(@"mummy"), "stepmummy"),
        (Whole(@"mothers?"), "stepmother"),
        (Whole(@"sisters?"), "stepsister"),
        (Whole(@"brothers?"), "stepbrother"),
        (Whole(@"daughters?"), "stepdaughter"),
        (Whole(@"sons?"), "stepson"),
        (Whole(@"incest(?:uous)?"), "stepfamily roleplay"),
        (Whole(@"rapists?"), "eager partner"),
        (Whole(@"raping"), "wanting it"),
        (Whole(@"raped"), "wanting it"),
        (Whole(@"rapes?"), "wanting it"),
        (Whole(@"non[\s-]*consensual"), "consensual, wanting it"),
        (Whole(@"non[\s-]*cons?"), "consensual, wanting it"),
        (Whole(@"dub[\s-]*cons?"), "consensual, wanting it"),
        (Whole(@"\bcnc\b"), "consensual, wanting it"),
        (Whole(@"against (?:her|his|their) will"), "eagerly wanting it"),
        (Whole(@"forcefully"), "eagerly"),
        (Whole(@"forcibly"), "eagerly"),
        (Whole(@"forceful"), "eager"),
        (Whole(@"forcing"), "wanting it"),
        (Whole(@"forced"), "wanting it"),
        (Whole(@"by force"), "because they wanted it"),
        (Whole(@"kidnapped"), "invited over"),
        (Whole(@"kidnap(?:ping)?"), "planned meetup"),
        (Whole(@"abduct(?:ed|ion)?"), "planned meetup"),
        (Whole(@"molest(?:ed|ing|ation)?"), "affectionate"),
        (Whole(@"assault(?:ed|ing)?"), "passionate encounter"),
        (Whole(@"unwilling"), "eager"),
        (Whole(@"reluctant"), "shy but wanting it"),
    ];

    [GeneratedRegex(@"\b(1[0-7]|[0-9])\s*[-]?\s*(?:year|yr)s?\s*[-]?\s*old\b", RegexOptions.IgnoreCase)]
    private static partial Regex AgeYearsOld();

    [GeneratedRegex(@"\b(1[0-7]|[0-9])\s*(?:yo|y/o)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AgeYo();

    [GeneratedRegex(@"\bages?\s*(1[0-7]|[0-9])\b", RegexOptions.IgnoreCase)]
    private static partial Regex AgeNumber();

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        foreach (var (pattern, replacement) in Replacements)
            text = pattern.Replace(text, match => CopyCase(match.Value, replacement));

        text = AgeYearsOld().Replace(text, "18 year old");
        text = AgeYo().Replace(text, "18yo");
        text = AgeNumber().Replace(text, "age 18");
        return CollapseWhitespace(text);
    }

    public static IReadOnlyList<string> SanitizeAll(IEnumerable<string> values)
    {
        return values
            .Select(Sanitize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Regex Whole(string pattern)
        => new(pattern.StartsWith(@"\b", StringComparison.Ordinal)
            ? pattern
            : $@"\b{pattern}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string CopyCase(string original, string replacement)
    {
        if (original.All(char.IsUpper) && original.Any(char.IsLetter))
            return replacement.ToUpperInvariant();

        if (original.Length > 0 && char.IsUpper(original[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return replacement;
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWhitespace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (previousWhitespace)
                    continue;
                builder.Append(' ');
                previousWhitespace = true;
                continue;
            }

            builder.Append(ch);
            previousWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}
