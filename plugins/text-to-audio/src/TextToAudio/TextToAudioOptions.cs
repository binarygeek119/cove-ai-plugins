using System.Text.Json;

namespace TextToAudio;

internal sealed record TextToAudioOptions(
    string ApiKey,
    string BaseUrl,
    string InputFolder,
    string OutputFolder,
    string Voice,
    string Model,
    string Format,
    bool ImportIntoLibrary,
    bool SkipExisting)
{
    public const string DefaultVoice = "af_sky";
    public const string DefaultModel = "tts-kokoro";
    public const string DefaultFormat = "mp3";
    public const string DefaultBaseUrl = "https://api.venice.ai/api/v1";
    public static readonly string[] TextExtensions = [".txt", ".md", ".markdown"];

    public static TextToAudioOptions FromPluginConfig(
        IReadOnlyDictionary<string, object?>? values,
        IReadOnlyDictionary<string, string>? jobParameters = null)
    {
        var apiKey = FirstNonEmpty(
            GetString(values, "openaiApiKey"),
            Environment.GetEnvironmentVariable("VENICE_API_KEY"),
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        var baseUrl = FirstNonEmpty(
            GetString(jobParameters, "openaiUrl"),
            GetString(values, "openaiUrl"),
            Environment.GetEnvironmentVariable("VENICE_BASE_URL"),
            Environment.GetEnvironmentVariable("OPENAI_BASE_URL"),
            DefaultBaseUrl);

        var inputFolder = FirstNonEmpty(
            GetString(jobParameters, "inputFolder"),
            GetString(values, "inputFolder"));

        var outputFolder = FirstNonEmpty(
            GetString(jobParameters, "outputFolder"),
            GetString(values, "outputFolder"));

        var voice = FirstNonEmpty(
            GetString(jobParameters, "voice"),
            GetString(values, "voice"),
            DefaultVoice);

        var model = FirstNonEmpty(
            GetString(jobParameters, "model"),
            GetString(values, "model"),
            DefaultModel);

        var format = FirstNonEmpty(
            GetString(jobParameters, "format"),
            GetString(values, "format"),
            DefaultFormat).TrimStart('.').ToLowerInvariant();

        var importIntoLibrary = GetBool(values, "importIntoLibrary", defaultValue: true);
        if (TryGetBool(jobParameters, "importIntoLibrary", out var importOverride))
            importIntoLibrary = importOverride;

        var skipExisting = GetBool(values, "skipExisting", defaultValue: true);
        if (TryGetBool(jobParameters, "skipExisting", out var skipOverride))
            skipExisting = skipOverride;
        if (TryGetBool(jobParameters, "overwrite", out var overwrite) && overwrite)
            skipExisting = false;

        return new TextToAudioOptions(
            apiKey,
            baseUrl,
            inputFolder,
            outputFolder,
            voice,
            model,
            format,
            importIntoLibrary,
            skipExisting);
    }

    public string OutputExtension => "." + Format;

    public string SpeechUrl => ResolveSpeechUrl(BaseUrl);

    public void Validate()
    {
        if (!Uri.TryCreate(SpeechUrl, UriKind.Absolute, out var speechUri)
            || (speechUri.Scheme != Uri.UriSchemeHttp && speechUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Set a valid API URL, such as https://api.venice.ai/api/v1 or a compatible /v1 endpoint.");
        }

        var requiresKey = speechUri.Host.Equals("api.venice.ai", StringComparison.OrdinalIgnoreCase)
            || speechUri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);
        if (requiresKey && string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException(
                "Set the Venice API key in this extension's settings, or set the VENICE_API_KEY environment variable.");

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

    internal static string ResolveSpeechUrl(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw.Trim();
        value = value.TrimEnd('/');
        if (value.EndsWith("/audio/speech", StringComparison.OrdinalIgnoreCase))
            return value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.PathAndQuery is "/" or "")
            return $"{value}/v1/audio/speech";

        return $"{value}/audio/speech";
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
