using System.Text.Json;

namespace MediaCovers;

internal sealed record MediaCoversOptions(
    bool IncludeAudio,
    bool IncludeText,
    int MaxItems,
    int MaxTextCharacters,
    bool ReplaceAll)
{
    public const int DefaultMaxTextCharacters = 2000;

    public static MediaCoversOptions FromPluginConfig(
        IReadOnlyDictionary<string, object?>? values,
        IReadOnlyDictionary<string, string>? jobParameters = null)
    {
        var includeAudio = GetBool(values, "includeAudio", defaultValue: true);
        if (TryGetBool(jobParameters, "includeAudio", out var audioOverride))
            includeAudio = audioOverride;

        var includeText = GetBool(values, "includeText", defaultValue: true);
        if (TryGetBool(jobParameters, "includeText", out var textOverride))
            includeText = textOverride;

        var kind = GetString(jobParameters, "kind").ToLowerInvariant();
        if (kind is "audio")
        {
            includeAudio = true;
            includeText = false;
        }
        else if (kind is "text")
        {
            includeAudio = false;
            includeText = true;
        }

        var maxItems = GetInt(jobParameters, "limit", 0);
        if (maxItems <= 0)
            maxItems = GetInt(values, "maxItems", 0);

        var maxText = GetInt(jobParameters, "maxTextCharacters", 0);
        if (maxText <= 0)
            maxText = GetInt(values, "maxTextCharacters", DefaultMaxTextCharacters);
        if (maxText <= 0)
            maxText = DefaultMaxTextCharacters;

        var replaceAll = false;
        if (TryGetBool(jobParameters, "replaceAll", out var replaceOverride))
            replaceAll = replaceOverride;
        else if (TryGetBool(jobParameters, "overwrite", out var overwrite))
            replaceAll = overwrite;

        return new MediaCoversOptions(includeAudio, includeText, maxItems, maxText, replaceAll);
    }

    public void Validate()
    {
        if (!IncludeAudio && !IncludeText)
            throw new InvalidOperationException("Enable audio items, text items, or both.");
    }

    private static string GetString(IReadOnlyDictionary<string, string>? values, string key)
    {
        if (values is null)
            return string.Empty;

        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool GetBool(IReadOnlyDictionary<string, object?>? values, string key, bool defaultValue)
    {
        if (values is null || !TryGetValue(values, key, out var raw) || raw is null)
            return defaultValue;

        return raw switch
        {
            bool flag => flag,
            string text when bool.TryParse(text, out var parsed) => parsed,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String
                && bool.TryParse(element.GetString(), out var parsed) => parsed,
            _ => defaultValue,
        };
    }

    private static bool TryGetBool(IReadOnlyDictionary<string, string>? values, string key, out bool value)
    {
        return bool.TryParse(GetString(values, key), out value);
    }

    private static int GetInt(IReadOnlyDictionary<string, object?>? values, string key, int defaultValue)
    {
        if (values is null || !TryGetValue(values, key, out var raw) || raw is null)
            return defaultValue;

        return raw switch
        {
            int number => number,
            long number => (int)number,
            string text when int.TryParse(text, out var parsed) => parsed,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed) => parsed,
            JsonElement element when element.ValueKind == JsonValueKind.String
                && int.TryParse(element.GetString(), out var parsed) => parsed,
            _ => defaultValue,
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, string>? values, string key, int defaultValue)
    {
        return int.TryParse(GetString(values, key), out var parsed) ? parsed : defaultValue;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> values, string key, out object? value)
    {
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
