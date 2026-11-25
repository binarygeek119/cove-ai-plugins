# Whisparr Stash Bridge

Cove plugin that lets Whisparr connect to Cove through Whisparr's built-in Stash connection.

Whisparr talks to Stash over GraphQL at `/graphql`. This plugin exposes a Stash-compatible endpoint and translates Whisparr scan/generate notifications into Cove scan jobs.

## What it handles

| Whisparr Stash setting / action | Bridge behavior |
| --- | --- |
| Test connection (`systemStatus`) | Returns a simple Stash-compatible status response. |
| Scan on import / rename / delete | Triggers `IScanService.StartScan` for the paths Whisparr sends. |
| Scan: Generate Covers | Maps to Cove `GenerateCovers`. |
| Scan: Generate Previews | Maps to Cove `GeneratePreviews`. |
| Scan: Generate Image Previews | Maps to Cove `GenerateImageThumbnails`. |
| Scan: Generate Sprites | Maps to Cove `GenerateSprites`. |
| Scan: Generate Phashes | Maps to Cove `GeneratePhashes`. |
| Run Identify Task | Acknowledged and triggers a rescan for the affected paths. Full Stash stash-box/autotag identify is not 1:1 with Cove, so this is a best-effort compatibility path. |
| Identify: Set Cover Image | When set, the identify path requests cover generation during the rescan. |
| Clean on rename/delete | Acknowledged so Whisparr notifications do not fail; Cove does not expose a direct Stash clean equivalent through the plugin SDK. |

## Configure in Whisparr

1. Install and enable the plugin in Cove.
2. In Whisparr, add a **Stash** connection.
3. Set **Host** and **Port** to the Cove server that has this plugin installed.
4. Set **Use SSL** to match your Cove setup.
5. If you set an API key in the plugin settings, put the same value in Whisparr's **API Key** field. The bridge accepts the key in the `ApiKey` header (Whisparr default) or `Authorization: Bearer ...`.
6. Enable the scan/generate options you want Whisparr to trigger.

## Plugin settings

- **API key** - Optional. When set, requests must include the same key.
- **Rescan unchanged files** - Forces scans to rescan unchanged files.

## Notes

- Whisparr hardcodes the Stash GraphQL path to `/graphql`, so the bridge maps both `/graphql` and `/api/plugins/com.binarygeek119.whisparr-stash-bridge/graphql`.
- Path mapping should be handled by Whisparr's existing Stash "Map Paths From/To" settings so the path Cove receives is valid on the Cove host.
- This plugin is a compatibility bridge, not a full Stash implementation. It focuses on the Whisparr Stash connection triggers.
