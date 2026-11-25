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
    private IServiceProvider? _services;
    private ILogger? _logger;

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

        var jobId = StartScan(options);
        _logger?.LogInformation(
            "Stash bridge metadataScan: jobId={JobId}, paths={Paths}, covers={Covers}, previews={Previews}, imagePreviews={ImagePreviews}, sprites={Sprites}, phashes={Phashes}",
            jobId,
            string.Join(", ", paths),
            options.GenerateCovers,
            options.GeneratePreviews,
            options.GenerateImageThumbnails,
            options.GenerateSprites,
            options.GeneratePhashes);

        return jobId;
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
        return StartScan(new ScanOperationOptions
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

    private string StartScan(ScanOperationOptions options)
    {
        var services = _services ?? throw new InvalidOperationException("Extension not initialized.");
        using var scope = services.GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope();
        var scanService = scope.ServiceProvider.GetRequiredService<IScanService>();
        return scanService.StartScan(options);
    }

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
