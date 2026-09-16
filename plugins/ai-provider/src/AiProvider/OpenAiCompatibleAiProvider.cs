using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
}
