using Cove.Ai.Abstractions;
using Cove.Core.Interfaces;
using Cove.Plugins;
using Cove.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace AiProvider;

public sealed class AiProviderExtension : CoveExtensionBase
{
    public const string SettingsTabKey = "ai-provider";

    public override UIManifest GetUIManifest()
        => ManifestBuilder()
            .AddSettingsTab(
                SettingsTabKey,
                "AI Provider",
                order: 40,
                icon: "sparkles",
                description: "Shared OpenAI-compatible API (Venice by default) used by other Cove AI plugins for chat, speech, and images.",
                searchKeywords: ["ai", "venice", "openai", "provider", "api", "key", "image"])
            .AddSettingsSection(SettingsTabKey, "Provider", "SettingsPanel")
            .Build();

    public override void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
        services.AddHttpClient("ai-provider", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });
        services.AddSingleton<IAiProvider>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai-provider");
            var config = sp.GetRequiredService<CoveConfiguration>();
            return new OpenAiCompatibleAiProvider(http, config);
        });
    }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        PublishContributions<IAiProvider>(services);
        return Task.CompletedTask;
    }
}
