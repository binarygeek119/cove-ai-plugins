using Cove.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace TextToAudio;

internal sealed class TextToAudioService(
    OpenAiSpeechClient speech,
    CoveConfiguration config,
    IScanService scanService,
    ILogger<TextToAudioService> logger)
{
    public const string ExtensionId = "com.yourcove.text-to-audio";

    public async Task ConvertAsync(
        IReadOnlyDictionary<string, string>? parameters,
        Cove.Plugins.IJobProgress progress,
        CancellationToken ct)
    {
        config.PluginConfigurations.TryGetValue(ExtensionId, out var pluginConfig);
        var options = TextToAudioOptions.FromPluginConfig(pluginConfig, parameters);
        options.Validate();

        Directory.CreateDirectory(options.OutputFolder);

        var requestedFile = GetParameter(parameters, "path");

        var files = DiscoverTextFiles(options.InputFolder, requestedFile).ToList();
        if (files.Count == 0)
        {
            progress.Report(1, "No text files found.");
            return;
        }

        var converted = 0;
        var skipped = 0;
        var imported = 0;
        var failed = 0;

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sourcePath = files[i];
            var relative = Path.GetRelativePath(options.InputFolder, sourcePath);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                relative = Path.GetFileName(sourcePath);
            var outputPath = Path.Combine(
                options.OutputFolder,
                Path.ChangeExtension(relative, options.OutputExtension));

            progress.Report(
                (double)i / files.Count,
                $"[{i + 1}/{files.Count}] {relative}");

            try
            {
                if (options.SkipExisting && File.Exists(outputPath))
                {
                    skipped++;
                    logger.LogInformation("Skipping existing audio {Output}", outputPath);
                    continue;
                }

                var text = await File.ReadAllTextAsync(sourcePath, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    skipped++;
                    logger.LogInformation("Skipping empty text file {Source}", sourcePath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await WriteSpeechAsync(options, text, outputPath, ct).ConfigureAwait(false);
                converted++;

                if (options.ImportIntoLibrary)
                {
                    await scanService.ImportDownloadedAudioAsync(outputPath, audioId: null, ct)
                        .ConfigureAwait(false);
                    imported++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Failed converting {Source}", sourcePath);
                progress.Report((double)(i + 1) / files.Count, $"Failed {relative}: {ex.Message}");
            }
        }

        progress.Report(
            1,
            $"Done. Converted {converted}, skipped {skipped}, imported {imported}, failed {failed}.");
    }

    private async Task WriteSpeechAsync(
        TextToAudioOptions options,
        string text,
        string outputPath,
        CancellationToken ct)
    {
        var chunks = SplitForSpeech(text, OpenAiSpeechClient.MaxInputCharacters).ToList();
        if (chunks.Count == 1)
        {
            var audio = await speech.SynthesizeAsync(
                options.SpeechUrl, options.ApiKey, options.Model, options.Voice, options.Format, chunks[0], ct)
                .ConfigureAwait(false);
            await File.WriteAllBytesAsync(outputPath, audio, ct).ConfigureAwait(false);
            return;
        }

        await using var output = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        for (var i = 0; i < chunks.Count; i++)
        {
            logger.LogInformation("Synthesizing chunk {Index}/{Count} for {Output}", i + 1, chunks.Count, outputPath);
            var audio = await speech.SynthesizeAsync(
                options.SpeechUrl, options.ApiKey, options.Model, options.Voice, options.Format, chunks[i], ct)
                .ConfigureAwait(false);
            await output.WriteAsync(audio, ct).ConfigureAwait(false);
        }
    }

    private static string? GetParameter(IReadOnlyDictionary<string, string>? parameters, string key)
    {
        if (parameters is null)
            return null;

        foreach (var pair in parameters)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value))
                return pair.Value;
        }

        return null;
    }

    private static IEnumerable<string> DiscoverTextFiles(string inputFolder, string? requestedFile)
    {
        if (!string.IsNullOrWhiteSpace(requestedFile))
        {
            var fullPath = Path.GetFullPath(requestedFile);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("The requested text file was not found.", fullPath);

            yield return fullPath;
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(inputFolder, "*", SearchOption.AllDirectories))
        {
            if (TextToAudioOptions.TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                yield return file;
        }
    }

    internal static IEnumerable<string> SplitForSpeech(string text, int maxCharacters)
    {
        var normalized = text.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxCharacters)
        {
            yield return normalized;
            yield break;
        }

        var remaining = normalized;
        while (remaining.Length > maxCharacters)
        {
            var window = remaining[..maxCharacters];
            var splitAt = LastBreak(window);
            if (splitAt <= 0)
                splitAt = maxCharacters;

            yield return remaining[..splitAt].Trim();
            remaining = remaining[splitAt..].TrimStart();
        }

        if (remaining.Length > 0)
            yield return remaining;
    }

    private static int LastBreak(string window)
    {
        var paragraph = window.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (paragraph >= window.Length / 4)
            return paragraph;

        var newline = window.LastIndexOf('\n');
        if (newline >= window.Length / 4)
            return newline;

        var sentence = Math.Max(
            window.LastIndexOf(". ", StringComparison.Ordinal),
            Math.Max(
                window.LastIndexOf("? ", StringComparison.Ordinal),
                window.LastIndexOf("! ", StringComparison.Ordinal)));
        if (sentence >= window.Length / 4)
            return sentence + 1;

        var space = window.LastIndexOf(' ');
        return space >= window.Length / 4 ? space : -1;
    }
}
