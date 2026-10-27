# Text Downloader

A second Cove downloader provider for text stories. It leaves `CommonTextDownloader` and any addon-specific downloader plugins untouched.

## Add a site

1. Add a new `DownloaderDescriptor` in `TextDownloaderExtension.cs`.
2. Add a matching `Is...Url` check, `MatchAsync` branch, and download branch.
3. Add the host(s) to `extension.json` under `permissions.network`.

## Build

```bash
dotnet publish plugins/text-downloader/src/TextDownloader/TextDownloader.csproj \
  -c Release -o artifacts/text-downloader -p:UseLocalCoveSdk=false
```