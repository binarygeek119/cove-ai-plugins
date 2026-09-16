namespace Cove.Ai.Abstractions;

/// <summary>
/// OpenAI-compatible AI backend published by the AI Provider plugin.
/// Other Cove plugins resolve this through <c>IExtensionServiceExchange.GetAll&lt;IAiProvider&gt;()</c>.
/// </summary>
public interface IAiProvider
{
    string Id { get; }

    string DisplayName { get; }

    bool IsConfigured { get; }

    int MaxSpeechCharacters { get; }

    string DefaultSpeechModel { get; }

    string DefaultSpeechVoice { get; }

    string DefaultSpeechFormat { get; }

    string DefaultChatModel { get; }

    string DefaultImageModel { get; }

    Task<byte[]> SpeechAsync(AiSpeechRequest request, CancellationToken cancellationToken = default);

    Task<string> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    Task<AiImageResult> ImageAsync(AiImageRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiSpeechRequest(
    string Text,
    string? Model = null,
    string? Voice = null,
    string? Format = null);

public sealed record AiChatRequest(
    string Prompt,
    string? Model = null,
    string? SystemPrompt = null,
    double? Temperature = null);

public sealed record AiImageRequest(
    string Prompt,
    string? NegativePrompt = null,
    string? Model = null,
    string AspectRatio = "16:9",
    bool SafeMode = false);

public sealed record AiImageResult(byte[] Bytes, string ContentType);
