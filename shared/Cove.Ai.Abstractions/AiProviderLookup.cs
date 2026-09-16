namespace Cove.Ai.Abstractions;

public static class AiProviderLookup
{
    public static IAiProvider Require(IEnumerable<IAiProvider> providers)
    {
        var configured = providers.FirstOrDefault(provider => provider.IsConfigured);
        if (configured is null)
        {
            throw new InvalidOperationException(
                "Configure the AI Provider plugin (Settings → AI Provider) with an API URL and key, then try again.");
        }

        return configured;
    }
}
