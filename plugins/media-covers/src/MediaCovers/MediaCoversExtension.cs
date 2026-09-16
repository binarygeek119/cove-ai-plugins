using Cove.Plugins;
using Cove.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace MediaCovers;

public sealed class MediaCoversExtension : JobExtensionBase
{
    public const string SettingsTabKey = "media-covers";

    private IServiceProvider? _services;

    public override UIManifest GetUIManifest()
        => ManifestBuilder()
            .AddSettingsTab(
                SettingsTabKey,
                "Media Covers",
                order: 90,
                icon: "image",
                description: "Generate or replace 16:9 movie-style covers for audio and text items.",
                searchKeywords: ["cover", "poster", "image", "audio", "text", "ai", "replace"])
            .AddSettingsSection(SettingsTabKey, "Covers", "SettingsPanel")
            .Build();

    public override void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
        services.AddScoped<MediaCoversService>();
    }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        return Task.CompletedTask;
    }

    protected override void DefineJobs()
    {
        Job(
            "generate-missing-covers",
            "Generate missing media covers",
            GenerateMissingCoversAsync,
            "Finds audio and text library items, builds a 16:9 movie-poster prompt from their metadata (and text file content), and stores the generated image. Pass replaceAll=true to redo existing covers.",
            supportsParameters: true,
            showInTaskList: true);
    }

    private async Task GenerateMissingCoversAsync(
        IReadOnlyDictionary<string, string>? parameters,
        IJobProgress progress,
        CancellationToken ct)
    {
        if (_services is null)
            throw new InvalidOperationException("The media-covers extension has not initialized yet.");

        await using var scope = _services.GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<MediaCoversService>();
        await generator.GenerateAsync(parameters, progress, ct).ConfigureAwait(false);
    }
}
