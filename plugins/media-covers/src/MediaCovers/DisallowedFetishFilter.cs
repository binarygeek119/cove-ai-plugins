using System.Text;
using System.Text.RegularExpressions;

namespace MediaCovers;

/// <summary>
/// Removes fetishes image APIs and this plugin do not allow. Allowed adult kinks stay.
/// Never send minor, animal, or snuff-related fetish terms to an image model.
/// </summary>
internal static partial class DisallowedFetishFilter
{
    [GeneratedRegex(
        @"\b(?:age[\s-]*plays?|loli(?:con|ta)?s?|shotas?|jailbaits?|pedos?|pedophiles?|under[\s-]*age(?:d)?|under[\s-]*18|minors?|children|child|kids?|teens?|teenagers?|pre[\s-]*teens?|tweens?|toddlers?|infants?|newborns?|babies|baby|diapers?|abdls?|infantilism|little[\s-]+(?:girl|boy|girls|boys|kid)s?|young[\s-]+(?:girl|boy)s?|bestiality|zoophilia|zoophiles?|zoo[\s-]*sex|animal[\s-]*sex|ferals?|cubs?|snuff|necro(?:phil(?:ia|e|ic))?|guro|coprophilia|copro|scat)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Blocked();

    public static bool IsBlocked(string? value)
        => !string.IsNullOrWhiteSpace(value) && Blocked().IsMatch(value);

    public static IReadOnlyList<string> FilterTags(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value) && !IsBlocked(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FilterText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return CollapseWhitespace(Blocked().Replace(value, " "));
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
