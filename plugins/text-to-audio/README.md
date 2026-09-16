# Text to Audio

A Cove extension that reads text files, sends them through the shared **AI Provider** plugin, writes audio files into a folder you set, and optionally imports those files into the Cove library.

Install **AI Provider** first (`com.binarygeek119.ai-provider`). Set the API key and URL there.

## What it does

1. You set an **input folder** of `.txt` / `.md` files and an **output folder** for audio.
2. From Cove, run the **Convert text files to audio** task.
3. Each text file is converted with the provider's speech API and saved next to the same relative path in the output folder (`notes/chapter-1.txt` → `notes/chapter-1.mp3`).
4. When **Import into Cove library** is enabled (the default), each generated file is imported as a Cove audio item.

Long files are split into chunks using the provider's character limit, then concatenated.

## Settings

In Cove: **Settings → AI Provider** for the API key/URL, then **Settings → Text to Audio** for folders and voice.

| Setting | Purpose |
| --- | --- |
| Input folder | Folder of text files to convert. Searched recursively. |
| Output folder | Where generated audio is written. |
| Model | Speech model. Empty uses the AI Provider default (`tts-kokoro`). |
| Voice | Voice for that model. Empty uses the AI Provider default (`af_sky`). |
| Audio format | `mp3` (default), `opus`, `aac`, `flac`, or `wav`. |
| Import into Cove library | Import each generated file after it is written. |
| Skip existing audio | Leave already-generated files alone. |

Add the output folder as a Cove library path if you also want later library scans to find the files.

## Build and install

Requires the .NET 10 SDK. From the repo root:

```bash
dotnet publish plugins/ai-provider/src/AiProvider/AiProvider.csproj -c Release -o artifacts/ai-provider -p:UseLocalCoveSdk=false
dotnet publish plugins/text-to-audio/src/TextToAudio/TextToAudio.csproj -c Release -o artifacts/text-to-audio -p:UseLocalCoveSdk=false
```

Install **AI Provider** first, then **Text to Audio**, from **Settings → Extensions → Install from ZIP**.

The job accepts optional parameters:

- `path` — convert one file instead of the whole input folder
- `overwrite` — `true` to regenerate files that already exist
- `voice`, `model`, `format`, `inputFolder`, `outputFolder` — override settings for that run
