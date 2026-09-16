# Media Covers

Generates **16:9 movie-poster covers** for Cove **audio** and **text** items that have no image.

Install **AI Provider** 0.2.0+ first (`com.binarygeek119.ai-provider`). Set the API key, URL, and image model there (Venice default is `qwen-image-2`).

## What it does

1. Finds library audio/text items whose `ImageBlobId` is empty.
2. Builds a theatrical 16:9 poster prompt from the title (stylized on the cover), details, tags, performers, and — for text items — the text file / excerpt.
3. Rewrites prompt wording so the image API receives adult 18+ language (step-family, over-18 ageplay, consensual “wanting it”).
4. If the image model refuses the prompt, it retries with less content (drop text/details, then tags, then title only).
5. Stores the image as a Cove blob and sets it as the item cover.

## Settings

In Cove: **Settings → AI Provider** for the API key/URL/image model, then **Settings → Media Covers**.

| Setting | Purpose |
| --- | --- |
| Audio items | Generate covers for audio with no image. |
| Text items | Generate covers for text with no image, using the text as content. |
| Max items per run | Optional cap. Empty/0 processes every missing cover. |
| Max text characters | How much of a text file to include. Default 2000. |

Run **Generate missing media covers** from the settings tab or the Cove task list.

Job parameters:

- `kind` — `audio`, `text`, or omit for both
- `limit` — max items this run
- `id` — a single audio or text id
