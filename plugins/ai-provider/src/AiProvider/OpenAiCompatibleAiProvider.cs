using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Ai.Abstractions;
using Cove.Core.Interfaces;

namespace AiProvider;

internal sealed class OpenAiCompatibleAiProvider(HttpClient http, CoveConfiguration config) : IAiProvider
{
    public const int MaxSpeechInputCharacters = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public string Id => AiProviderSettings.ExtensionId;

    public string DisplayName => "OpenAI-compatible";

    public int MaxSpeechCharacters => MaxSpeechInputCharacters;

    public string DefaultSpeechModel => ReadSettings().SpeechModel;

    public string DefaultSpeechVoice => ReadSettings().SpeechVoice;

    public string DefaultSpeechFormat => ReadSettings().SpeechFormat;

    public string DefaultChatModel => ReadSettings().ChatModel;

    public string DefaultImageModel => ReadSettings().ImageModel;

    public bool IsConfigured => ReadSettings().IsConfigured;

    public async Task<byte[]> SpeechAsync(AiSpeechRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Speech text is empty.", nameof(request));

        var settings = ReadSettings();
        settings.Validate();

        var model = FirstNonEmpty(request.Model, settings.SpeechModel);
        var voice = FirstNonEmpty(request.Voice, settings.SpeechVoice);
        var format = FirstNonEmpty(request.Format, settings.SpeechFormat).TrimStart('.').ToLowerInvariant();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.SpeechUrl);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(new SpeechBody(model, request.Text, voice, format), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return body;

        throw new InvalidOperationException(
            $"Speech request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {Encoding.UTF8.GetString(body)}");
    }

    public async Task<string> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Chat prompt is empty.", nameof(request));

        var settings = ReadSettings();
        settings.Validate();

        var model = FirstNonEmpty(request.Model, settings.ChatModel);
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Set a chat model in AI Provider settings.");

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new ChatMessage("system", request.SystemPrompt));
        messages.Add(new ChatMessage("user", request.Prompt));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.ChatUrl);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(new ChatBody(model, messages, request.Temperature), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Chat request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (document.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                return content.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("The chat API response did not include message content.");
    }

    public async Task<AiImageResult> ImageAsync(AiImageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("Image prompt is empty.", nameof(request));

        var settings = ReadSettings();
        settings.Validate();

        var model = FirstNonEmpty(request.Model, settings.ImageModel, AiProviderSettings.DefaultImageModel);
        var aspect = string.IsNullOrWhiteSpace(request.AspectRatio) ? "16:9" : request.AspectRatio.Trim();
        var payload = settings.IsVeniceHost
            ? JsonSerializer.Serialize(
                new VeniceImageBody(
                    model,
                    request.Prompt,
                    string.IsNullOrWhiteSpace(request.NegativePrompt) ? null : request.NegativePrompt,
                    aspect,
                    "png",
                    request.SafeMode,
                    false),
                ImageJsonOptions)
            : JsonSerializer.Serialize(
                new OpenAiImageBody(
                    model,
                    request.Prompt,
                    1,
                    AspectToOpenAiSize(aspect),
                    "b64_json",
                    request.SafeMode ? "auto" : "low"),
                ImageJsonOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.ImageUrl);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var binary = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return new AiImageResult(binary, mediaType);
            throw ImageFailure(response, Encoding.UTF8.GetString(binary));
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw ImageFailure(response, body);

        if (TryParseImageResponse(body, out var image))
            return image;

        if (IsVeniceContentViolation(response) || LooksLikeContentRejection((int)response.StatusCode, body))
        {
            throw new AiImageRejectedException(
                "The image API refused this prompt and returned no image.");
        }

        throw new InvalidOperationException("The image API response did not include image data.");
    }

    private static readonly JsonSerializerOptions ImageJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static Exception ImageFailure(HttpResponseMessage response, string body)
    {
        var message = $"Image request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {Truncate(body)}";
        if (IsVeniceContentViolation(response) || LooksLikeContentRejection((int)response.StatusCode, body))
            return new AiImageRejectedException(message);
        return new InvalidOperationException(message);
    }

    private static string Truncate(string body, int max = 400)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= max)
            return body;
        return body[..max] + "…";
    }

    private static bool IsVeniceContentViolation(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-venice-is-content-violation", out var values)
            && values.Any(value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeContentRejection(int statusCode, string body)
    {
        if (statusCode is not (400 or 403 or 422 or 451))
            return false;

        var lower = body.ToLowerInvariant();
        return lower.Contains("content policy", StringComparison.Ordinal)
            || lower.Contains("safety", StringComparison.Ordinal)
            || lower.Contains("violation", StringComparison.Ordinal)
            || lower.Contains("moderation", StringComparison.Ordinal)
            || lower.Contains("not allowed", StringComparison.Ordinal)
            || lower.Contains("refused", StringComparison.Ordinal)
            || lower.Contains("blocked", StringComparison.Ordinal)
            || lower.Contains("nsfw", StringComparison.Ordinal)
            || lower.Contains("unsafe", StringComparison.Ordinal);
    }

    private static bool TryParseImageResponse(string body, out AiImageResult image)
    {
        image = null!;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = document.RootElement;

            if (root.TryGetProperty("images", out var images)
                && images.ValueKind == JsonValueKind.Array
                && images.GetArrayLength() > 0
                && images[0].ValueKind == JsonValueKind.String)
            {
                var payload = images[0].GetString();
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    var bytes = DecodeBase64Image(payload);
                    if (bytes.Length > 0)
                    {
                        image = new AiImageResult(bytes, DetectImageContentType(bytes));
                        return true;
                    }
                }
            }

            if (root.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
                && data.GetArrayLength() > 0)
            {
                var item = data[0];
                if (item.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String)
                {
                    var payload = b64.GetString();
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        var bytes = DecodeBase64Image(payload);
                        if (bytes.Length > 0)
                        {
                            image = new AiImageResult(bytes, DetectImageContentType(bytes));
                            return true;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        return false;
    }

    private static byte[] DecodeBase64Image(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("The image API returned empty image data.");

        var payload = value;
        var marker = payload.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
            payload = payload[(marker + "base64,".Length)..];

        return Convert.FromBase64String(payload);
    }

    private static string DetectImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 12
            && bytes[0] == 0x52
            && bytes[1] == 0x49
            && bytes[2] == 0x46
            && bytes[3] == 0x46
            && bytes[8] == 0x57
            && bytes[9] == 0x45
            && bytes[10] == 0x42
            && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return "image/png";
    }

    private static string AspectToOpenAiSize(string aspect)
    {
        return aspect.Replace(" ", "", StringComparison.Ordinal) switch
        {
            "16:9" or "16x9" => "1792x1024",
            "9:16" or "9x16" => "1024x1792",
            "1:1" or "1x1" => "1024x1024",
            _ => "1792x1024",
        };
    }

    private AiProviderSettings ReadSettings()
    {
        config.PluginConfigurations.TryGetValue(AiProviderSettings.ExtensionId, out var values);
        return AiProviderSettings.FromConfig(values);
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

    private sealed record SpeechBody(string Model, string Input, string Voice, string ResponseFormat);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatBody(string Model, IReadOnlyList<ChatMessage> Messages, double? Temperature);

    private sealed record VeniceImageBody(
        string Model,
        string Prompt,
        string? NegativePrompt,
        string AspectRatio,
        string Format,
        bool SafeMode,
        bool ReturnBinary);

    private sealed record OpenAiImageBody(
        string Model,
        string Prompt,
        int N,
        string Size,
        string ResponseFormat,
        string Moderation);
}
