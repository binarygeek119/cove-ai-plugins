using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TextToAudio;

internal sealed class OpenAiSpeechClient(HttpClient http)
{
    public const int MaxInputCharacters = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<byte[]> SynthesizeAsync(
        string speechUrl,
        string apiKey,
        string model,
        string voice,
        string format,
        string text,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, speechUrl);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new SpeechRequest(model, text, voice, format), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return body;

        var error = Encoding.UTF8.GetString(body);
        throw new InvalidOperationException(
            $"Speech request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
    }

    private sealed record SpeechRequest(string Model, string Input, string Voice, string ResponseFormat);
}
