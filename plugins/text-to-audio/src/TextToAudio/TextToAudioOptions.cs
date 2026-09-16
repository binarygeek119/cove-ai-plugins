using System.Text.Json;

namespace TextToAudio;

internal sealed record TextToAudioOptions(
    string InputFolder,
    string OutputFolder,
    string Voice,
    string Model,
    string Format,
    bool ImportIntoLibrary,
    bool SkipExisting)
{
    public static readonly string[] TextExtensions = [".txt", ".md", ".markdown"];

    public static TextToAudioOptions FromPluginConfig(
        IReadOnlyDictionary<string, object?>? values,
        IReadOnlyDictionary<string, string>? jobParameters = null)
    {
        var inputFolder = FirstNonEmpty(
            GetString(jobParameters, "inputFolder"),
            GetString(values, "inputFolder"));

        var outputFolder = FirstNonEmpty(
            GetString(jobParameters, "outputFolder"),
            GetString(values, "outputFolder"));

        var voice = FirstNonEmpty(
            GetString(jobParameters, "voice"),
            GetString(values, "voice"));

        var model = FirstNonEmpty(
            GetString(jobParameters, "model"),
            GetString(values, "model"));

        var format = FirstNonEmpty(
            GetString(jobParameters, "format"),
            GetString(values, "format")).TrimStart('.').ToLowerInvariant();

        var importIntoLibrary = GetBool(values, "importIntoLibrary", defaultValue: true);
        if (TryGetBool(jobParameters, "importIntoLibrary", out var importOverride))
            importIntoLibrary = importOverride;

        var skipExisting = GetBool(values, "skipExisting", defaultValue: true);
        if (TryGetBool(jobParameters, "skipExisting", out var skipOverride))
            skipExisting = skipOverride;
        if (TryGetBool(jobParameters, "overwrite", out var overwrite) && overwrite)
            skipExisting = false;

        return new TextToAudioOptions(
            inputFolder,
            outputFolder,
            voice,
            model,
            format,
            importIntoLibrary,
            skipExisting);
    }

    public string OutputExtension(string resolvedFormat) => "." + resolvedFormat.TrimStart('.').ToLowerInvariant();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InputFolder))
            throw new InvalidOperationException("Set the input folder in this extension's settings.");

        if (string.IsNullOrWhiteSpace(OutputFolder))
            throw new InvalidOperationException("Set the output folder in this extension's settings.");

        if (!Directory.Exists(InputFolder))
            throw new DirectoryNotFoundException($"Input folder does not exist: {InputFolder}");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static string GetString(IReadOnlyDictionary<string, object?>? values, string key)
    {
        if (values is null || !TryGetValue(values, key, out var raw) || raw is null)
            return string.Empty;

        return raw switch
        {
            string text => text.Trim(),
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False
                or JsonValueKind.Number => element.ToString(),
            _ => raw.ToString()?.Trim() ?? string.Empty,
        };
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
        var text = GetString(values, key);
        return bool.TryParse(text, out value);
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
