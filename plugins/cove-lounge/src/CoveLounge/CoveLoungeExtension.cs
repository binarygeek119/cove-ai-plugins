using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cove.Core.Auth;
using Cove.Core.Interfaces;
using Cove.Plugins;
using Cove.Sdk;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;

namespace CoveLounge;

public sealed class CoveLoungeExtension : FullExtensionBase, IStatefulExtension
{
    public const string SettingsTabKey = "cove-lounge";

    private IServiceProvider? _services;
    private IExtensionStore? _store;

    public override UIManifest GetUIManifest()
        => ManifestBuilder()
            .AddSettingsTab(
                SettingsTabKey,
                "CoveLounge",
                order: 95,
                icon: "link",
                description: "Connect CoveLounge apps to this Cove server using the quick PIN relay.",
                searchKeywords: ["covelounge", "quickpin", "pin", "connect", "relay"])
            .AddSettingsSection(SettingsTabKey, "Connection", "SettingsPanel")
            .Build();

    public override void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
        services.AddHttpClient("cove-lounge", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        return Task.CompletedTask;
    }

    void IStatefulExtension.SetStore(IExtensionStore store) => _store = store;

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var prefix = $"/api/plugins/{Id}";

        endpoints.MapGet($"{prefix}/apps", async (CancellationToken ct) =>
        {
            var apps = await LoadAppsAsync(ct);
            return Results.Ok(apps);
        });

        endpoints.MapGet($"{prefix}/health", async (CancellationToken ct) =>
        {
            try
            {
                await using var scope = (_services ?? throw new InvalidOperationException("Extension not initialized."))
                    .GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope();
                using var db = CoveDb.Require(scope.ServiceProvider);
                var canConnect = await db.Database.CanConnectAsync(ct);
                return canConnect
                    ? Results.Ok(new { status = "ok" })
                    : Results.Problem("Database connectivity check failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).AllowCoveAnonymous();

        endpoints.MapPost($"{prefix}/connect", async (HttpContext context, ConnectRequest request, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Pin))
                return Results.BadRequest(new { error = "Pin is required." });

            var services = _services ?? throw new InvalidOperationException("Extension not initialized.");
            var config = services.GetRequiredService<CoveConfiguration>();
            var pluginConfig = GetPluginConfig(config);

            const string relayUrl = "https://covelounge.duckdns.org";

            var publicUrl = GetConfig(pluginConfig, "covePublicUrl");
            var internalUrl = GetConfig(pluginConfig, "coveInternalUrl");

            var principalAccessor = services.GetRequiredService<ICurrentPrincipalAccessor>();
            var principal = principalAccessor.Current;
            if (principal?.UserId is not int userId)
                return Results.Unauthorized();

            string apiKey;
            Guid apiTokenId;
            await using (var scope = services.GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope())
            {
                var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
                var issued = await tokenService.CreateApiTokenAsync(userId, "CoveLounge", null, null, principal, ct);
                apiKey = issued.PlaintextToken;
                apiTokenId = issued.Id;
            }

            var primary = string.IsNullOrWhiteSpace(publicUrl) ? internalUrl : publicUrl;

            var payload = JsonSerializer.Serialize(new
            {
                serverUrl = primary,
                apiToken = apiKey,
                serverUrlPublic = publicUrl,
                serverUrlInternal = internalUrl,
            });

            var normalizedPin = NormalizePin(request.Pin);
            var ciphertext = Encrypt(normalizedPin, payload);

            using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("cove-lounge");
            using var response = await client.PostAsJsonAsync($"{relayUrl.TrimEnd('/')}/v1/pins/{normalizedPin}/deliver", new { ciphertext }, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                var apps = await LoadAppsAsync(ct);
                apps.Add(new ConnectedApp(
                    Guid.NewGuid().ToString("n"),
                    string.IsNullOrWhiteSpace(request.Label) ? $"CoveLounge app {apps.Count + 1}" : request.Label!,
                    primary,
                    apiKey,
                    apiTokenId,
                    DateTimeOffset.UtcNow.ToString("O")));
                await SaveAppsAsync(apps, ct);
                return Results.Ok(new { connected = true });
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return Results.BadRequest(new { error = string.IsNullOrWhiteSpace(body) ? $"Relay returned {(int)response.StatusCode}." : body });
        });

        endpoints.MapDelete($"{prefix}/apps/{{appId}}", async (string appId, CancellationToken ct) =>
        {
            var apps = await LoadAppsAsync(ct);
            var app = apps.FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.Ordinal));
            if (app is null)
                return Results.NotFound();

            if (app.ApiTokenId.HasValue)
            {
                try
                {
                    await using var scope = (_services ?? throw new InvalidOperationException("Extension not initialized."))
                        .GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope();
                    var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
                    var principalAccessor = (_services).GetRequiredService<ICurrentPrincipalAccessor>();
                    await tokenService.RevokeApiTokenAsync(app.ApiTokenId.Value, principalAccessor.Current, ct);
                }
                catch
                {
                    // Best-effort revocation; still remove the local record.
                }
            }

            apps.RemoveAll(a => string.Equals(a.Id, appId, StringComparison.Ordinal));
            await SaveAppsAsync(apps, ct);
            return Results.NoContent();
        });
    }

    private static Dictionary<string, object?> GetPluginConfig(CoveConfiguration config)
    {
        if (config.PluginConfigurations.TryGetValue("com.binarygeek119.cove-lounge", out var cfg) && cfg is Dictionary<string, object?> dict)
            return dict;
        return new Dictionary<string, object?>();
    }

    private static string GetConfig(Dictionary<string, object?> cfg, string key)
    {
        if (!cfg.TryGetValue(key, out var value) || value is null)
            return string.Empty;
        if (value is string s)
            return s;
        if (value is System.Text.Json.JsonElement je)
            return je.ValueKind == System.Text.Json.JsonValueKind.String ? je.GetString() ?? string.Empty : je.ToString();
        return value.ToString() ?? string.Empty;
    }

    private async Task<List<ConnectedApp>> LoadAppsAsync(CancellationToken ct)
    {
        if (_store is null)
            return [];
        var json = await _store.GetAsync("connectedApps", ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<ConnectedApp>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveAppsAsync(List<ConnectedApp> apps, CancellationToken ct)
    {
        if (_store is null)
            return;
        await _store.SetAsync("connectedApps", JsonSerializer.Serialize(apps), ct);
    }

    private static string NormalizePin(string pin)
        => new string(pin.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string Encrypt(string pin, string plaintext)
    {
        var key = SHA256.HashData(Encoding.ASCII.GetBytes("ChannelFlow QuickPin v1" + pin));
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(key, 16))
        {
            gcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }
        var blob = new byte[12 + cipherBytes.Length + 16];
        Buffer.BlockCopy(nonce, 0, blob, 0, 12);
        Buffer.BlockCopy(cipherBytes, 0, blob, 12, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, blob, 12 + cipherBytes.Length, 16);
        return Convert.ToBase64String(blob);
    }

    private sealed record ConnectedApp(string Id, string Label, string ServerUrl, string ApiKey, Guid? ApiTokenId = null, string DeliveredAt = "");
    private sealed record ConnectRequest(string Pin, string? Label);
}