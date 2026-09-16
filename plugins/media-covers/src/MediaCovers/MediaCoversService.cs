using Cove.Ai.Abstractions;
using Cove.Core.Entities;
using Cove.Core.Interfaces;
using Cove.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaCovers;

internal sealed class MediaCoversService(
    IServiceProvider services,
    IExtensionServiceExchange serviceExchange,
    CoveConfiguration config,
    IBlobService blobService,
    ILogger<MediaCoversService> logger)
{
    public const string ExtensionId = "com.binarygeek119.media-covers";

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".nfo", ".html", ".htm",
    };

    public async Task GenerateAsync(
        IReadOnlyDictionary<string, string>? parameters,
        Cove.Plugins.IJobProgress progress,
        CancellationToken ct)
    {
        var provider = AiProviderLookup.Require(serviceExchange.GetAll<IAiProvider>());
        config.PluginConfigurations.TryGetValue(ExtensionId, out var pluginConfig);
        var options = MediaCoversOptions.FromPluginConfig(pluginConfig, parameters);
        options.Validate();

        var db = CoveDb.Require(services);
        var requestedId = GetIntParameter(parameters, "id");
        var targets = await LoadTargetsAsync(db, options, requestedId, ct).ConfigureAwait(false);
        if (targets.Count == 0)
        {
            progress.Report(
                1,
                options.ReplaceAll
                    ? "No audio or text items were found."
                    : "No audio or text items are missing covers.");
            return;
        }

        var generated = 0;
        var skipped = 0;
        var failed = 0;

        for (var i = 0; i < targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var target = targets[i];
            progress.Report((double)i / targets.Count, $"[{i + 1}/{targets.Count}] {target.Kind} #{target.Id} {target.Title}");

            try
            {
                if (!options.ReplaceAll && await AlreadyHasCoverAsync(db, target, ct).ConfigureAwait(false))
                {
                    skipped++;
                    continue;
                }

                var image = await GenerateCoverAsync(provider, target, ct).ConfigureAwait(false);
                await using var stream = new MemoryStream(image.Bytes, writable: false);
                var blobId = await blobService.StoreBlobAsync(stream, image.ContentType, ct).ConfigureAwait(false);
                await AssignCoverAsync(db, target, blobId, ct).ConfigureAwait(false);
                generated++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Failed generating cover for {Kind} {Id}", target.Kind, target.Id);
                progress.Report((double)(i + 1) / targets.Count, $"Failed {target.Kind} #{target.Id}: {ex.Message}");
            }
        }

        progress.Report(
            1,
            options.ReplaceAll
                ? $"Done. Replaced {generated}, failed {failed}."
                : $"Done. Generated {generated}, skipped {skipped}, failed {failed}.");
    }

    private async Task<AiImageResult> GenerateCoverAsync(
        IAiProvider provider,
        CoverTarget target,
        CancellationToken ct)
    {
        var attempts = CoverPromptBuilder.BuildAttempts(target);
        Exception? lastReject = null;

        foreach (var attempt in attempts)
        {
            try
            {
                return await provider.ImageAsync(
                    new AiImageRequest(
                        attempt.Prompt,
                        attempt.NegativePrompt,
                        AspectRatio: CoverPromptBuilder.AspectRatio,
                        SafeMode: false),
                    ct).ConfigureAwait(false);
            }
            catch (AiImageRejectedException ex)
            {
                lastReject = ex;
                logger.LogInformation(
                    "Image refused for {Kind} {Id} at layer {Layer}; retrying with less content.",
                    target.Kind,
                    target.Id,
                    attempt.Layer);
            }
        }

        throw lastReject ?? new InvalidOperationException("The image API refused every prompt variant.");
    }

    private async Task<IReadOnlyList<CoverTarget>> LoadTargetsAsync(
        DbContext db,
        MediaCoversOptions options,
        int? requestedId,
        CancellationToken ct)
    {
        var targets = new List<CoverTarget>();

        if (options.IncludeAudio)
        {
            var query = db.Set<Audio>().AsNoTracking();
            if (!options.ReplaceAll)
                query = query.Where(item => item.ImageBlobId == null);
            if (requestedId is int audioId)
                query = query.Where(item => item.Id == audioId);

            var audios = await query.OrderBy(item => item.Id).ToListAsync(ct).ConfigureAwait(false);
            var names = await LoadNamesAsync(db, audios.SelectMany(item => item.TagIds), audios.SelectMany(item => item.PerformerIds), ct)
                .ConfigureAwait(false);
            foreach (var audio in audios)
            {
                targets.Add(new CoverTarget(
                    "audio",
                    audio.Id,
                    FirstTitle(audio.Title, audio.MinPath, $"Audio {audio.Id}"),
                    audio.Details,
                    Lookup(names.Tags, audio.TagIds),
                    Lookup(names.Performers, audio.PerformerIds),
                    null));
            }
        }

        if (options.IncludeText)
        {
            var query = db.Set<TextDocument>().AsNoTracking();
            if (!options.ReplaceAll)
                query = query.Where(item => item.ImageBlobId == null);
            if (requestedId is int textId)
                query = query.Where(item => item.Id == textId);

            var documents = await query.OrderBy(item => item.Id).ToListAsync(ct).ConfigureAwait(false);
            var documentIds = documents.Select(item => item.Id).ToList();
            var files = await db.Set<TextFile>()
                .AsNoTracking()
                .Where(file => file.TextDocumentId != null && documentIds.Contains(file.TextDocumentId.Value))
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var filesByDocument = files
                .GroupBy(file => file.TextDocumentId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
            var names = await LoadNamesAsync(db, documents.SelectMany(item => item.TagIds), documents.SelectMany(item => item.PerformerIds), ct)
                .ConfigureAwait(false);

            foreach (var document in documents)
            {
                filesByDocument.TryGetValue(document.Id, out var documentFiles);
                targets.Add(new CoverTarget(
                    "text",
                    document.Id,
                    FirstTitle(document.Title, document.MinPath, $"Text {document.Id}"),
                    document.Details,
                    Lookup(names.Tags, document.TagIds),
                    Lookup(names.Performers, document.PerformerIds),
                    ReadTextContent(documentFiles, options.MaxTextCharacters)));
            }
        }

        if (options.MaxItems > 0 && targets.Count > options.MaxItems)
            return targets.Take(options.MaxItems).ToList();

        return targets;
    }

    private static async Task<(Dictionary<int, string> Tags, Dictionary<int, string> Performers)> LoadNamesAsync(
        DbContext db,
        IEnumerable<int> tagIds,
        IEnumerable<int> performerIds,
        CancellationToken ct)
    {
        var tags = tagIds.Distinct().ToList();
        var performers = performerIds.Distinct().ToList();

        var tagNames = tags.Count == 0
            ? []
            : await db.Set<Tag>()
                .AsNoTracking()
                .Where(tag => tags.Contains(tag.Id))
                .ToDictionaryAsync(tag => tag.Id, tag => tag.Name, ct)
                .ConfigureAwait(false);

        var performerNames = performers.Count == 0
            ? []
            : await db.Set<Performer>()
                .AsNoTracking()
                .Where(performer => performers.Contains(performer.Id))
                .ToDictionaryAsync(performer => performer.Id, performer => performer.Name, ct)
                .ConfigureAwait(false);

        return (tagNames, performerNames);
    }

    private static IReadOnlyList<string> Lookup(IReadOnlyDictionary<int, string> names, int[] ids)
    {
        var values = new List<string>();
        foreach (var id in ids)
        {
            if (names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
                values.Add(name);
        }

        return values;
    }

    private string? ReadTextContent(IReadOnlyList<TextFile>? files, int maxCharacters)
    {
        if (files is null || files.Count == 0)
            return null;

        foreach (var file in files.OrderByDescending(item => item.WordCount ?? 0))
        {
            if (!string.IsNullOrWhiteSpace(file.ExcerptText))
                return Clip(file.ExcerptText, maxCharacters);

            var path = file.Path;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            if (!TextExtensions.Contains(Path.GetExtension(path)))
                continue;

            try
            {
                using var reader = new StreamReader(path);
                var buffer = new char[maxCharacters];
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read > 0)
                    return new string(buffer, 0, read);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "Could not read text file {Path}", path);
            }
        }

        return null;
    }

    private static async Task<bool> AlreadyHasCoverAsync(DbContext db, CoverTarget target, CancellationToken ct)
    {
        if (target.Kind == "audio")
        {
            var blobId = await db.Set<Audio>()
                .AsNoTracking()
                .Where(item => item.Id == target.Id)
                .Select(item => item.ImageBlobId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(blobId);
        }

        var textBlobId = await db.Set<TextDocument>()
            .AsNoTracking()
            .Where(item => item.Id == target.Id)
            .Select(item => item.ImageBlobId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(textBlobId);
    }

    private async Task AssignCoverAsync(DbContext db, CoverTarget target, string blobId, CancellationToken ct)
    {
        string? previousBlobId;
        if (target.Kind == "audio")
        {
            var audio = await db.Set<Audio>().FirstAsync(item => item.Id == target.Id, ct).ConfigureAwait(false);
            previousBlobId = audio.ImageBlobId;
            audio.ImageBlobId = blobId;
            audio.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var document = await db.Set<TextDocument>().FirstAsync(item => item.Id == target.Id, ct).ConfigureAwait(false);
            previousBlobId = document.ImageBlobId;
            document.ImageBlobId = blobId;
            document.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(previousBlobId)
            && !string.Equals(previousBlobId, blobId, StringComparison.Ordinal))
        {
            await blobService.DeleteBlobAsync(previousBlobId, ct).ConfigureAwait(false);
        }
    }

    private static string FirstTitle(string? title, string? path, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();
        if (!string.IsNullOrWhiteSpace(path))
            return Path.GetFileNameWithoutExtension(path);
        return fallback;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static int? GetIntParameter(IReadOnlyDictionary<string, string>? parameters, string key)
    {
        if (parameters is null)
            return null;

        foreach (var pair in parameters)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(pair.Value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
