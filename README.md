# Cove AI plugins

A Cove extension registry and plugin pack: [github.com/binarygeek119/cove-ai-plugins](https://github.com/binarygeek119/cove-ai-plugins). Point a Cove **dev** instance at this repository and the host can list, build, and install these plugins.

Every plugin in this repo is **100% AI-coded**. They are tested and written to be as safe as possible, but they have **not** been reviewed by a human code reviewer. Install them only if you accept that risk.

## Point Cove at this repo

Cove reads a GitHub registry as:

- `index.json`
- `extensions/{extension-id}.json`

On a current Cove build, set the registry base URL to the raw `main` branch of this repo:

```json
{
  "ExtensionRegistryBaseUrl": "https://raw.githubusercontent.com/binarygeek119/cove-ai-plugins/main/"
}
```

Put that in Cove `appsettings.json` / `cove-config.json`, or pass it as configuration when you run Cove from source. After restart, **Settings → Extensions** lists the plugins from this catalog.

Install a built zip without the registry from **Settings → Extensions → Install from ZIP**.

## Plugins

| Plugin | Id | Version |
| --- | --- | --- |
| [AI Provider](plugins/ai-provider/README.md) | `com.binarygeek119.ai-provider` | 0.2.0 |
| [Text to Audio](plugins/text-to-audio/README.md) | `com.yourcove.text-to-audio` | 0.2.0 |
| [Media Covers](plugins/media-covers/README.md) | `com.binarygeek119.media-covers` | 0.1.0 |

**AI Provider** is the shared OpenAI-compatible backend (Venice by default). Other plugins call it for chat, speech, and images.

**Text to Audio** converts `.txt` / `.md` files through that provider and writes audio into a folder you set. Install AI Provider first.

**Media Covers** fills in missing audio and text covers as 16:9 movie posters. Install AI Provider 0.2.0+ first.

## GitHub Actions

### Build plugins

[`.github/workflows/build.yml`](.github/workflows/build.yml) builds **only the plugins that changed**.

- Version comes from that plugin’s `extension.json` (or from a tag `text-to-audio/v1.2.3`).
- The zip is named `{id}-{version}.zip`.
- On `main` (and on version tags), a GitHub Release is created when that version tag does not already exist.

To cut a release by hand:

```bash
git tag text-to-audio/v0.1.4
git push origin text-to-audio/v0.1.4
```

### Security and safety

[`.github/workflows/security.yml`](.github/workflows/security.yml) runs on pull requests, pushes to `main`, and weekly:

- **Gitleaks** — hardcoded secrets
- **Plugin safety scan** — dangerous APIs (`Process.Start`, `eval`, open network `*`, native imports, …)
- **NuGet audit** — known-vulnerable packages, including transitives
- **CodeQL** — C# and JavaScript, with security-extended queries
- **Dependency review** — on pull requests

High-severity safety hits fail the job. The safety report is uploaded as an artifact.

## Add a plugin

1. Put the project under `plugins/<slug>/`.
2. Add a row to `plugins/catalog.json`.
3. Add `extensions/<id>.json` and list the id in `index.json`.
4. Bump `version` in `extension.json` when you want a new zip / GitHub Release.

## Local build

Requires the .NET 10 SDK.

```bash
dotnet publish plugins/ai-provider/src/AiProvider/AiProvider.csproj \
  -c Release -o artifacts/ai-provider -p:UseLocalCoveSdk=false
dotnet publish plugins/text-to-audio/src/TextToAudio/TextToAudio.csproj \
  -c Release -o artifacts/text-to-audio -p:UseLocalCoveSdk=false
dotnet publish plugins/media-covers/src/MediaCovers/MediaCovers.csproj \
  -c Release -o artifacts/media-covers -p:UseLocalCoveSdk=false
```

If this repo sits beside a `cove` checkout, builds use the local Cove SDK automatically.
