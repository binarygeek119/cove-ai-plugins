using Cove.Plugins;
using Cove.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace TextToAudio;

public sealed class TextToAudioExtension : JobExtensionBase
{
    public const string SettingsTabKey = "text-to-audio";

    private IServiceProvider? _services;

    public override UIManifest GetUIManifest()
        => ManifestBuilder()
            .AddSettingsTab(
                SettingsTabKey,
                "Text to Audio",
                order: 80,
                icon: "music",
                description: "Convert text files to speech through the shared AI Provider plugin and write audio into a folder you choose.",
                searchKeywords: ["tts", "openai", "speech", "audio", "text"])
            .AddSettingsSection(SettingsTabKey, "Speech conversion", "SettingsPanel")
            .Build();

    public override void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
        services.AddScoped<TextToAudioService>();
    }

    public override Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        return Task.CompletedTask;
    }

    protected override void DefineJobs()
    {
        Job(
            "convert-text-files",
            "Convert text files to audio",
            ConvertTextFilesAsync,
            "Reads text files from the configured input folder, generates speech through the AI Provider plugin, writes audio to the output folder, and optionally imports it into the Cove library.",
            supportsParameters: true,
            showInTaskList: true);
    }

    private async Task ConvertTextFilesAsync(
        IReadOnlyDictionary<string, string>? parameters,
        IJobProgress progress,
        CancellationToken ct)
    {
        if (_services is null)
            throw new InvalidOperationException("The text-to-audio extension has not initialized yet.");

        await using var scope = _services.GetRequiredService<IExtensionServiceScopeFactory>().CreateAsyncScope();
        var converter = scope.ServiceProvider.GetRequiredService<TextToAudioService>();
        await converter.ConvertAsync(parameters, progress, ct).ConfigureAwait(false);
    }
}
