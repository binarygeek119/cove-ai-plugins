using System.Text.Json;

namespace AiProvider;

internal sealed record AiProviderSettings(
    string ApiKey,
    string BaseUrl,
    string ChatModel,
    string SpeechModel,
    string SpeechVoice,
    string SpeechFormat)
{
    public const string ExtensionId = "com.binarygeek119.ai-provider";
    public const string DefaultBaseUrl = "https://api.venice.ai/api/v1";
    public const string DefaultSpeechModel = "tts-kokoro";
    public const string DefaultSpeechVoice = "af_sky";
    public const string DefaultSpeechFormat = "mp3";

    public static AiProviderSettings FromConfig(IReadOnlyDictionary<string, object?>? values)
    {
        return new AiProviderSettings(
            FirstNonEmpty(
                GetString(values, "apiKey"),
                Environment.GetEnvironmentVariable("VENICE_API_KEY"),
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            FirstNonEmpty(
                GetString(values, "baseUrl"),
                Environment.GetEnvironmentVariable("VENICE_BASE_URL"),
                Environment.GetEnvironmentVariable("OPENAI_BASE_URL"),
                DefaultBaseUrl),
            GetString(values, "chatModel"),
            FirstNonEmpty(GetString(values, "speechModel"), DefaultSpeechModel),
            FirstNonEmpty(GetString(values, "speechVoice"), DefaultSpeechVoice),
            FirstNonEmpty(GetString(values, "speechFormat"), DefaultSpeechFormat).TrimStart('.').ToLowerInvariant());
    }

    public string SpeechUrl => ResolveEndpoint(BaseUrl, "audio/speech");

    public string ChatUrl => ResolveEndpoint(BaseUrl, "chat/completions");

    public bool RequiresKey
    {
        get
        {
            if (!Uri.TryCreate(BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
                return true;
            return uri.Host.Equals("api.venice.ai", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsConfigured
        => Uri.TryCreate(SpeechUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && (!RequiresKey || !string.IsNullOrWhiteSpace(ApiKey));

    public void Validate()
    {
        if (!Uri.TryCreate(SpeechUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Set a valid API URL, such as https://api.venice.ai/api/v1 or a compatible /v1 endpoint.");
        }

        if (RequiresKey && string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "Set the API key in AI Provider settings, or set VENICE_API_KEY / OPENAI_API_KEY.");
        }
    }

    internal static string ResolveEndpoint(string? raw, string relative)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw.Trim().TrimEnd('/');
        if (value.EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase))
            return value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.PathAndQuery is "/" or "")
            return $"{value}/v1/{relative}";

        return $"{value}/{relative}";
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
        if (values is null)
            return string.Empty;

        foreach (var pair in values)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) || pair.Value is null)
                continue;

            return pair.Value switch
            {
                string text => text.Trim(),
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
                JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False
                    or JsonValueKind.Number => element.ToString(),
                _ => pair.Value.ToString()?.Trim() ?? string.Empty,
            };
        }

        return string.Empty;
    }
}
