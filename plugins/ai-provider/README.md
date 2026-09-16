# AI Provider

Shared OpenAI-compatible AI backend for this plugin pack. Other plugins (starting with Text to Audio) call it for **chat** and **speech** so each plugin does not store its own API key and URL.

Default backend is [Venice AI](https://docs.venice.ai/): `https://api.venice.ai/api/v1`.

## Settings

In Cove: **Settings → AI Provider**.

| Setting | Purpose |
| --- | --- |
| API key | Bearer token. Falls back to `VENICE_API_KEY`, then `OPENAI_API_KEY`. |
| API URL | OpenAI-compatible `/v1` root. |
| Chat model | Default model for `IAiProvider.ChatAsync`. |
| Speech model / voice / format | Defaults for `IAiProvider.SpeechAsync`. |

Network allowlist is `api.venice.ai` and `api.openai.com`. A custom host must be added to `extension.json` permissions.

## How other plugins use it

1. Reference `shared/Cove.Ai.Abstractions`.
2. List `Cove.Ai.Abstractions` in `sharedAssemblies`.
3. Depend on `com.binarygeek119.ai-provider`.
4. Resolve the provider from Cove’s service exchange:

```csharp
var provider = AiProviderLookup.Require(
    services.GetRequiredService<IExtensionServiceExchange>().GetAll<IAiProvider>());

var audio = await provider.SpeechAsync(new AiSpeechRequest(text, model, voice, format), ct);
var reply = await provider.ChatAsync(new AiChatRequest(prompt, model), ct);
```

The AI Provider plugin publishes `IAiProvider` during `InitializeAsync`.
