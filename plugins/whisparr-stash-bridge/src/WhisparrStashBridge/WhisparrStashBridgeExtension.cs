using System.Text.Json;
using System.Text.RegularExpressions;
using Cove.Core.Interfaces;
using Cove.Plugins;
using Cove.Sdk;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WhisparrStashBridge;

public sealed partial class WhisparrStashBridgeExtension : FullExtensionBase
{
    private static readonly TimeSpan TriggerDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TriggerMaxWindow = TimeSpan.FromMinutes(30);

    private IServiceProvider? _services;
    private ILogger? _logger;

    private readonly object _pendingLock = new();
    private ScanOperationOptions? _pendingOptions;
    private DateTimeOffset? _pendingFirstSeen;
    private CancellationTokenSource? _pendingCts;

    public override void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
        // No extra services needed; we use host services at request time.
    }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        _logger = services.GetService<ILoggerFactory>()?.CreateLogger("WhisparrStashBridge");
        _logger?.LogInformation("Whisparr Stash Bridge initialized. Point Whisparr's Stash connection at this Cove server's /graphql endpoint.");
        return Task.CompletedTask;
    }

    public override Task ShutdownAsync(CancellationToken ct = default)
    {
        lock (_pendingLock)
        {
            ClearPendingLocked();
        }

        _logger?.LogInformation("Whisparr Stash Bridge shut down.");
        return Task.CompletedTask;
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Whisparr hardcodes POST /graphql. Map at root and also under the plugin prefix for convenience.
        var pluginPrefix = $"/api/plugins/{Id}";
        MapGraphQl(endpoints, "/graphql");
        MapGraphQl(endpoints, $"{pluginPrefix}/graphql");
    }

    private void MapGraphQl(IEndpointRouteBuilder endpoints, string route)
    {
        endpoints.MapPost(route, HandleGraphQlAsync).AllowCoveAnonymous();
        endpoints.MapGet(route, () => Results.Ok(new
        {
            status = "ok",
            message = "Whisparr Stash Bridge is active. Configure Whisparr's Stash connection to point at this Cove host."
        })).AllowCoveAnonymous();
    }

    private async Task<IResult> HandleGraphQlAsync(HttpContext context, CancellationToken ct)
    {
        if (!IsAuthorized(context))
            return Results.Json(new { errors = new[] { new { message = "Unauthorized" } } }, statusCode: StatusCodes.Status401Unauthorized);

        GraphQlRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<GraphQlRequest>(context.Request.Body, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse GraphQL request");
            return GraphQlErrors("Invalid GraphQL request body.");
        }

        var query = request?.Query ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return GraphQlErrors("Missing GraphQL query.");

        _logger?.LogDebug("Stash bridge received GraphQL: {Query}", Truncate(query, 500));

        try
        {
            if (ContainsOperation(query, "systemStatus"))
                return HandleSystemStatus();

            var data = new Dictionary<string, object?>();
            var handled = false;

            if (ContainsOperation(query, "metadataScan") || ContainsOperation(query, "metadataGenerate"))
            {
                data["metadataScan"] = ExecuteMetadataScan(query);
                handled = true;
            }

            if (ContainsOperation(query, "metadataIdentify"))
            {
                data["metadataIdentify"] = ExecuteMetadataIdentify(query, data.ContainsKey("metadataScan"));
                handled = true;
            }

            if (ContainsOperation(query, "metadataClean"))
            {
                data["metadataClean"] = ExecuteMetadataClean(query);
                handled = true;
            }

            if (!handled)
            {
                _logger?.LogInformation("Stash bridge received unsupported GraphQL operation: {Query}", Truncate(query, 200));
            }

            return Results.Json(new { data });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Stash bridge failed handling GraphQL operation");
            return GraphQlErrors(ex.Message);
        }
    }

    private IResult HandleSystemStatus()
    {
        return Results.Json(new
        {
            data = new
            {
                systemStatus = new
                {
                    databaseSchema = 0,
                    databasePath = "cove",
                    configPath = "cove",
                    appSchema = 0,
                    status = "ok"
                }
            }
        });
    }

    private string ExecuteMetadataScan(string query)
    {
        var paths = ExtractPaths(query);
        var options = new ScanOperationOptions
        {
            Paths = paths.Count > 0 ? paths : null,
            Rescan = GetPluginBoolSetting("defaultRescan") || ExtractBool(query, "rescan") == true,
            GenerateCovers = ExtractBool(query, "scanGenerateCovers") ?? false,
            GeneratePreviews = ExtractBool(query, "scanGeneratePreviews") ?? false,
            GenerateImageThumbnails = ExtractBool(query, "scanGenerateImagePreviews") ?? false,
            GenerateSprites = ExtractBool(query, "scanGenerateSprites") ?? false,
            GeneratePhashes = ExtractBool(query, "scanGeneratePhashes") ?? false,
        };

        var result = ScheduleScan(options);
        _logger?.LogInformation(
            "Stash bridge metadataScan: result={Result}, paths={Paths}, covers={Covers}, previews={Previews}, imagePreviews={ImagePreviews}, sprites={Sprites}, phashes={Phashes}",
            result,
            string.Join(", ", paths),
            options.GenerateCovers,
            options.GeneratePreviews,
            options.GenerateImageThumbnails,
            options.GenerateSprites,
            options.GeneratePhashes);

        return result;
    }

    private string ExecuteMetadataIdentify(string query, bool scanAlreadyTriggered)
    {
        var paths = ExtractPaths(query);
        _logger?.LogInformation(
            "Stash bridge metadataIdentify received for paths: {Paths}. Identify options: includeMalePerformers={IncludeMale}, setCoverImage={SetCover}, setOrganized={Organized}, skipMultipleMatches={SkipMultiple}",
            string.Join(", ", paths),
            ExtractBool(query, "includeMalePerformers"),
            ExtractBool(query, "setCoverImage"),
            ExtractBool(query, "setOrganized"),
            ExtractBool(query, "skipMultipleMatches"));

        if (scanAlreadyTriggered)
        {
            // Whisparr sent scan and identify in the same mutation; the scan already handled the path.
            return "ok";
        }

        // Best-effort compatibility: trigger a rescan so Cove picks up the affected path.
        // Full Stash stash-box/autotag identify is not 1:1 with Cove.
        return ScheduleScan(new ScanOperationOptions
        {
            Paths = paths.Count > 0 ? paths : null,
            Rescan = true,
            GenerateCovers = ExtractBool(query, "setCoverImage") ?? false,
        });
    }

    private string ExecuteMetadataClean(string query)
    {
        var paths = ExtractPaths(query);
        _logger?.LogInformation("Stash bridge metadataClean received for paths: {Paths}", string.Join(", ", paths));
        // Cove does not expose a direct Stash-equivalent clean mutation through the plugin SDK.
        // Acknowledge so Whisparr does not treat the notification as failed.
        return "ok";
    }

    private string ScheduleScan(ScanOperationOptions incoming)
    {
        lock (_pendingLock)
        {
            var now = DateTimeOffset.UtcNow;

            if (_pendingOptions is null || _pendingFirstSeen is null)
            {
                _pendingOptions = CloneOptions(incoming);
                _pendingFirstSeen = now;
                SchedulePendingTimerLocked(TriggerDelay);
                _logger?.LogInformation(
                    "Stash bridge queued scan/generate trigger; will run in {Delay}. paths={Paths}",
                    TriggerDelay,
                    DescribePaths(_pendingOptions.Paths));
                return "queued";
            }

            _pendingOptions = MergeOptions(_pendingOptions, incoming);
            var elapsed = now - _pendingFirstSeen.Value;
            if (elapsed >= TriggerMaxWindow)
            {
                _logger?.LogInformation(
                    "Stash bridge max trigger window reached ({Window}); firing queued scan/generate now. paths={Paths}",
                    TriggerMaxWindow,
                    DescribePaths(_pendingOptions.Paths));
                var options = _pendingOptions;
                ClearPendingLocked();
                return FireScan(options);
            }

            SchedulePendingTimerLocked(TriggerDelay);
            _logger?.LogInformation(
                "Stash bridge merged scan/generate trigger; timer reset to {Delay}. elapsed={Elapsed}. paths={Paths}",
                TriggerDelay,
                elapsed,
                DescribePaths(_pendingOptions.Paths));
            return "queued";
        }
    }

    private void SchedulePendingTimerLocked(TimeSpan delay)
    {
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        var cts = new CancellationTokenSource();
        _pendingCts = cts;
        _ = RunPendingAfterDelayAsync(delay, cts);
    }

    private async Task RunPendingAfterDelayAsync(TimeSpan delay, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ScanOperationOptions? options;
        lock (_pendingLock)
        {
            if (!ReferenceEquals(_pendingCts, cts) || _pendingOptions is null)
                return;
            options = _pendingOptions;
            ClearPendingLocked();
        }

        try
        {
            var jobId = FireScan(options);
            _logger?.LogInformation("Stash bridge fired debounced scan/generate. jobId={JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Stash bridge failed to fire debounced scan/generate");
        }
    }

    private string FireScan(ScanOperationOptions options)
    {
        var services = _services ?? throw new InvalidOperationException("Extension not initialized.");
        using var scope = services.GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope();
        var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();
        return scanService.StartScan(options);
    }

    private void ClearPendingLocked()
    {
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _pendingCts = null;
        _pendingOptions = null;
        _pendingFirstSeen = null;
    }

    private static ScanOperationOptions CloneOptions(ScanOperationOptions source)
        => new()
        {
            Paths = source.Paths is null ? null : new List<string>(source.Paths),
            Rescan = source.Rescan,
            GenerateCovers = source.GenerateCovers,
            GeneratePreviews = source.GeneratePreviews,
            GenerateImageThumbnails = source.GenerateImageThumbnails,
            GenerateSprites = source.GenerateSprites,
            GeneratePhashes = source.GeneratePhashes,
            GenerateImagePhashes = source.GenerateImagePhashes,
            GenerateAudioPhashes = source.GenerateAudioPhashes,
            GenerateTextPhashes = source.GenerateTextPhashes,
            GenerateMd5 = source.GenerateMd5,
            IncludeUnchangedFilesInAssetGeneration = source.IncludeUnchangedFilesInAssetGeneration,
        };

    private static ScanOperationOptions MergeOptions(ScanOperationOptions target, ScanOperationOptions incoming)
        => new()
        {
            Paths = MergePaths(target.Paths, incoming.Paths),
            Rescan = target.Rescan || incoming.Rescan,
            GenerateCovers = target.GenerateCovers || incoming.GenerateCovers,
            GeneratePreviews = target.GeneratePreviews || incoming.GeneratePreviews,
            GenerateImageThumbnails = target.GenerateImageThumbnails || incoming.GenerateImageThumbnails,
            GenerateSprites = target.GenerateSprites || incoming.GenerateSprites,
            GeneratePhashes = target.GeneratePhashes || incoming.GeneratePhashes,
            GenerateImagePhashes = target.GenerateImagePhashes || incoming.GenerateImagePhashes,
            GenerateAudioPhashes = target.GenerateAudioPhashes || incoming.GenerateAudioPhashes,
            GenerateTextPhashes = target.GenerateTextPhashes || incoming.GenerateTextPhashes,
            GenerateMd5 = target.GenerateMd5 || incoming.GenerateMd5,
            IncludeUnchangedFilesInAssetGeneration = target.IncludeUnchangedFilesInAssetGeneration || incoming.IncludeUnchangedFilesInAssetGeneration,
        };

    private static List<string>? MergePaths(List<string>? a, List<string>? b)
    {
        // null means "all paths" / no path filter; merging with any specific paths keeps "all".
        if (a is null || b is null) return null;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        void AddRange(List<string> src)
        {
            foreach (var p in src)
            {
                if (!string.IsNullOrWhiteSpace(p) && set.Add(p))
                    list.Add(p);
            }
        }
        AddRange(a);
        AddRange(b);
        return list;
    }

    private static string DescribePaths(List<string>? paths)
        => paths is null || paths.Count == 0 ? "<all>" : string.Join(", ", paths);

    private bool IsAuthorized(HttpContext context)
    {
        var expected = GetPluginStringSetting("apiKey");
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        var provided = GetProvidedApiKey(context);
        return string.Equals(expected.Trim(), provided?.Trim(), StringComparison.Ordinal);
    }

    private static string? GetProvidedApiKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("ApiKey", out var values))
        {
            var key = values.ToString();
            if (!string.IsNullOrWhiteSpace(key))
                return key;
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authValues))
        {
            var auth = authValues.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return auth["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(auth))
                return auth.Trim();
        }

        return null;
    }

    private string GetPluginStringSetting(string name)
    {
        var services = _services;
        if (services is null)
            return string.Empty;

        try
        {
            var config = services.GetService(typeof(CoveConfiguration)) as CoveConfiguration;
            if (config?.PluginConfigurations.TryGetValue(Id, out var pluginConfig) == true
                && pluginConfig is Dictionary<string, object?> dict
                && dict.TryGetValue(name, out var value))
            {
                return ValueToString(value);
            }
        }
        catch
        {
            // Ignore config read failures; treat as unset.
        }

        return string.Empty;
    }

    private bool GetPluginBoolSetting(string name)
    {
        var raw = GetPluginStringSetting(name);
        return bool.TryParse(raw, out var b) && b;
    }

    private static string ValueToString(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string s) return s;
        if (value is bool b) return b ? "true" : "false";
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => je.ToString(),
                _ => je.ToString()
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private static bool ContainsOperation(string query, string name)
        => query.Contains(name, StringComparison.OrdinalIgnoreCase);

    private static bool? ExtractBool(string query, string name)
    {
        var match = Regex.Match(query, $@"\b{Regex.Escape(name)}\s*:\s*(true|false)\b", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;
        return bool.Parse(match.Groups[1].Value);
    }

    private static List<string> ExtractPaths(string query)
    {
        var paths = new List<string>();

        // Match paths: ["..."] or paths: ["...", "..."] with JSON-style quoted strings.
        var pathsMatch = Regex.Match(query, @"\bpaths\s*:\s*\[(?<inner>[^\]]*)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (pathsMatch.Success)
        {
            var inner = pathsMatch.Groups["inner"].Value;
            foreach (Match m in Regex.Matches(inner, "\"((?:\\\\.|[^\"\\\\])*)\""))
            {
                var raw = m.Groups[1].Value;
                try
                {
                    var unescaped = JsonSerializer.Deserialize<string>("\"" + raw + "\"");
                    if (!string.IsNullOrWhiteSpace(unescaped))
                        paths.Add(unescaped);
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                        paths.Add(raw);
                }
            }
        }

        // Some clients may send a single path field.
        if (paths.Count == 0)
        {
            var single = Regex.Match(query, @"\bpath\s*:\s*""((?:\\.|[^""\\])*)""", RegexOptions.IgnoreCase);
            if (single.Success)
            {
                var raw = single.Groups[1].Value;
                try
                {
                    var unescaped = JsonSerializer.Deserialize<string>("\"" + raw + "\"");
                    if (!string.IsNullOrWhiteSpace(unescaped))
                        paths.Add(unescaped);
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                        paths.Add(raw);
                }
            }
        }

        return paths
            .Select(NormalizePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizePath(string path)
    {
        var p = path.Trim();
        if (p.Length == 0)
            return p;
        // Normalize forward slashes to OS path separators for local matching, but keep as-is otherwise.
        try
        {
            return Path.GetFullPath(p);
        }
        catch
        {
            return p;
        }
    }

    private static IResult GraphQlErrors(string message)
        => Results.Json(new { errors = new[] { new { message } } });

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GraphQlRequest
    {
        public string? Query { get; set; }
        public JsonElement? Variables { get; set; }
        public string? OperationName { get; set; }
    }
}
