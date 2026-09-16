# Text to Audio

A Cove extension that reads text files, sends them through the [Venice AI speech API](https://docs.venice.ai/guides/media/text-to-speech), writes audio files into a folder you set, and optionally imports those files into the Cove library.

## What it does

1. You set an **input folder** of `.txt` / `.md` files and an **output folder** for audio.
2. From Cove, run the **Convert text files to audio** task.
3. Each text file is converted with Venice TTS and saved next to the same relative path in the output folder (`notes/chapter-1.txt` → `notes/chapter-1.mp3`).
4. When **Import into Cove library** is enabled (the default), each generated file is imported as a Cove audio item.

Long files are split into 4096-character chunks, then concatenated.

## Settings

In Cove: **Settings → Text to Audio**.

Defaults are Venice AI: `https://api.venice.ai/api/v1`, model `tts-kokoro`, voice `af_sky`.

| Setting | Purpose |
| --- | --- |
| Venice API key | Bearer token. If empty, `VENICE_API_KEY` then `OPENAI_API_KEY` is used. |
| API URL | API base URL. Default is `https://api.venice.ai/api/v1`. Falls back to `VENICE_BASE_URL` then `OPENAI_BASE_URL`. |
| Input folder | Folder of text files to convert. Searched recursively. |
| Output folder | Where generated audio is written. |
| Model | Venice speech model (`tts-kokoro` by default). |
| Voice | Voice for that model (`af_sky` by default). |
| Audio format | `mp3` (default), `opus`, `aac`, `flac`, or `wav`. |
| Import into Cove library | Import each generated file after it is written. |
| Skip existing audio | Leave already-generated files alone. |

Add the output folder as a Cove library path if you also want later library scans to find the files.

## Build and install

Requires the .NET 10 SDK.

```bash
dotnet publish src/TextToAudio/TextToAudio.csproj -c Release -o artifacts/extension
cp src/TextToAudio/extension.json artifacts/extension/
cd artifacts/extension
zip -r ../../com.yourcove.text-to-audio-0.1.4.zip .
```

In Cove, install the zip from **Settings → Extensions → Install from ZIP**. Then open **Settings → Text to Audio**, set the folders and Venice API key, and run **Convert text files**.

The job accepts optional parameters:

- `path` — convert one file instead of the whole input folder
- `overwrite` — `true` to regenerate files that already exist
- `openaiUrl` — override the API base URL
- `voice`, `model`, `format`, `inputFolder`, `outputFolder` — override settings for that run

## Local Cove SDK

If this repo is checked out beside `cove`, the project automatically uses the local `src/Cove.Sdk` project. Force package mode with:

```bash
dotnet build -p:UseLocalCoveSdk=false
```
