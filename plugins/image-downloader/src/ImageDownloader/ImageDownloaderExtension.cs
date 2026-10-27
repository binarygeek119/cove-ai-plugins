using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cove.Core.DTOs;
using Cove.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace ImageDownloader;

public sealed class ImageDownloaderExtension : IDownloaderProvider
{
    private const string ExtensionId = "com.binarygeek119.image-downloader";
    public const string ImageDownloaderId = "com.binarygeek119.image-downloader/image";
    private IServiceProvider? _services;

    private static readonly DownloaderDescriptor ImageDownloader = new(
        ImageDownloaderId,
        "Common Image Sites",
        DownloaderEntity.Image,
        ["rule34.xxx/*", "*.rule34.xxx/*", "e621.net/*", "*.e621.net/*", "rule34.paheal.net/*", "*.paheal.net/*", "gelbooru.com/*", "*.gelbooru.com/*", "hentai-foundry.com/*", "*.hentai-foundry.com/*", "rule34.us/*", "*.rule34.us/*"],
        DownloaderCapabilities.InlineMetadata);

    public string Id => ExtensionId;
    public string Name => "Image Downloader";
    public string Version => OfficialDownloaderUtilities.GetExtensionVersion(typeof(ImageDownloaderExtension));
    public string? Description => "Downloads images from common image/booru pages such as rule34.xxx, e621, paheal, gelbooru, hentai-foundry, and rule34.us, scraping their metadata.";
    public string? Author => "binarygeek119";
    public string? Url => "https://github.com/binarygeek119/cove-ai-plugins";
    public string? IconUrl => null;
    public IReadOnlyList<string> Categories => [ExtensionCategories.Downloader];

    public void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        return Task.CompletedTask;
    }

    public IReadOnlyList<DownloaderDescriptor> GetDownloaders() => [ImageDownloader];

    public async Task<DownloaderUrlMatch?> MatchAsync(string url, CancellationToken ct)
    {
        if (!IsSupportedUrl(url))
            return null;

        var info = await TryGetImageInfoAsync(url, ct);
        return new DownloaderUrlMatch(
            ImageDownloader.Id,
            url,
            null,
            info?.Title ?? OfficialDownloaderUtilities.DeriveTitleFromUrl(url, "Image"));
    }

    public async Task<DownloaderResult?> DownloadAsync(DownloaderRequest request, IDownloaderHost host, CancellationToken ct)
    {
        if (!string.Equals(request.DownloaderId, ImageDownloader.Id, StringComparison.OrdinalIgnoreCase))
            return null;

        if (request.Entity != DownloaderEntity.Image)
            throw new InvalidOperationException("The common image downloader only supports image downloads.");

        if (!IsSupportedUrl(request.Url))
            throw new InvalidOperationException("This downloader only supports the configured image sites.");

        host.ReportProgress(0.1d, "Scraping image metadata...");
        var info = await GetImageInfoAsync(request.Url, ct);

        if (string.IsNullOrWhiteSpace(info.ImageUrl))
            throw new InvalidOperationException("The image page was downloaded, but no image file URL could be found.");

        host.ReportProgress(0.25d, $"Downloading {info.Title}...");
        var client = GetHttpClient();
        using var response = await client.GetAsync(info.ImageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var extension = OfficialDownloaderUtilities.EnsureExtensionFromUrl(info.ImageUrl, ".jpg");
        var localName = "downloaded" + extension;
        var localPath = Path.Combine(host.TempDirectory, localName);
        await using (var input = await response.Content.ReadAsStreamAsync(ct))
        await using (var output = File.Create(localPath))
        {
            await input.CopyToAsync(output, ct);
        }

        var scraped = new ScrapedImageDto
        {
            SourceScraperId = ExtensionId,
            Title = info.Title,
            Details = info.Details,
            Photographer = info.Author,
            StudioName = info.Studio,
            PerformerNames = info.Performers,
            TagNames = info.Tags,
            ImageUrl = info.ImageUrl,
            Urls = new List<string> { info.ImageUrl },
        };

        host.ReportProgress(0.95d, "Image download completed.");
        return new DownloaderResult(localName, OfficialDownloaderUtilities.SanitizeFileName(info.Title + extension), new Dictionary<string, string>(), null, null, scraped);
    }

    private static bool IsSupportedUrl(string url)
    {
        if (!OfficialDownloaderUtilities.IsHttpUrl(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return OfficialDownloaderUtilities.IsHost(uri, "rule34.xxx")
            || OfficialDownloaderUtilities.IsHost(uri, "e621.net")
            || OfficialDownloaderUtilities.IsHost(uri, "rule34.paheal.net")
            || OfficialDownloaderUtilities.IsHost(uri, "paheal.net")
            || OfficialDownloaderUtilities.IsHost(uri, "gelbooru.com")
            || OfficialDownloaderUtilities.IsHost(uri, "hentai-foundry.com")
            || OfficialDownloaderUtilities.IsHost(uri, "rule34.us");
    }

    private async Task<ImagePageInfo?> TryGetImageInfoAsync(string url, CancellationToken ct)
    {
        try
        {
            return await GetImageInfoAsync(url, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ImagePageInfo> GetImageInfoAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("The image URL is not a valid absolute URL.");

        if (OfficialDownloaderUtilities.IsHost(uri, "e621.net"))
            return await GetE621InfoAsync(url, ct);

        if (OfficialDownloaderUtilities.IsHost(uri, "rule34.xxx"))
            return await GetRule34XxxInfoAsync(url, ct);

        if (OfficialDownloaderUtilities.IsHost(uri, "paheal.net"))
            return await GetPahealInfoAsync(url, ct);

        if (OfficialDownloaderUtilities.IsHost(uri, "gelbooru.com"))
            return await GetGelbooruInfoAsync(url, ct);

        if (OfficialDownloaderUtilities.IsHost(uri, "hentai-foundry.com"))
            return await GetHentaiFoundryInfoAsync(url, ct);

        if (OfficialDownloaderUtilities.IsHost(uri, "rule34.us"))
            return await GetRule34UsInfoAsync(url, ct);

        throw new InvalidOperationException("This image site is not supported.");
    }

    // ---------- e621 ----------
    private async Task<ImagePageInfo> GetE621InfoAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Invalid e621 URL.");

        var id = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(id) || !id.All(char.IsDigit))
            throw new InvalidOperationException("Could not determine the e621 post id from the URL.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://e621.net/posts/{id}.json");
        request.Headers.UserAgent.ParseAdd("CoveCommonImageDownloader/1.0");
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);
        var post = document.RootElement.GetProperty("post");

        var tags = post.GetProperty("tags");
        var artistTags = GetStringList(tags, "artist");
        var characterTags = GetStringList(tags, "character");
        var generalTags = GetStringList(tags, "general");
        var allTags = new List<string>();
        allTags.AddRange(artistTags);
        allTags.AddRange(characterTags);
        allTags.AddRange(generalTags);
        allTags.AddRange(GetStringList(tags, "copyright"));
        allTags = allTags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var imageUrl = post.GetProperty("file").GetProperty("url").GetString()
            ?? throw new InvalidOperationException("e621 did not return an image URL.");

        var title = characterTags.Count > 0
            ? string.Join(", ", characterTags)
            : $"e621 post {id}";
        if (string.IsNullOrWhiteSpace(title))
            title = $"e621 post {id}";

        var details = post.TryGetProperty("description", out var desc) ? desc.GetString() : null;
        var sources = new List<string>();
        if (post.TryGetProperty("sources", out var srcs) && srcs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in srcs.EnumerateArray())
            {
                var sText = s.GetString();
                if (!string.IsNullOrWhiteSpace(sText))
                    sources.Add(sText!);
            }
        }

        return new ImagePageInfo(
            url,
            title,
            imageUrl,
            artistTags.Count > 0 ? string.Join(", ", artistTags) : null,
            artistTags.Count > 0 ? string.Join(", ", artistTags) : null,
            characterTags,
            allTags,
            string.IsNullOrWhiteSpace(details) ? null : details);
    }

    // ---------- rule34.xxx ----------
    private async Task<ImagePageInfo> GetRule34XxxInfoAsync(string url, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, "CoveCommonImageDownloader/1.0", ct);
        var imageUrl = ExtractFirstAttribute(html, "img", "id", "image", "src")
            ?? ExtractMetaContent(html, "og:image")
            ?? ExtractFirstRegex(html, @"https?://[^\s""']+\.(?:jpg|jpeg|png|webp|gif)", RegexOptions.IgnoreCase);

        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new InvalidOperationException("Could not find the image URL on the rule34.xxx page.");

        imageUrl = ResolveUrl(url, imageUrl!);

        var (artists, characters, generals, all) = ExtractGelbooruTags(html);
        if (all.Count == 0)
        {
            var keywords = ExtractMetaContent(html, "keywords");
            if (!string.IsNullOrWhiteSpace(keywords))
                all = keywords!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var title = characters.Count > 0 ? string.Join(", ", characters) : OfficialDownloaderUtilities.DeriveTitleFromUrl(url, "rule34.xxx image");
        return new ImagePageInfo(url, title, imageUrl, artists.Count > 0 ? string.Join(", ", artists) : null, artists.Count > 0 ? string.Join(", ", artists) : null, characters, all, null);
    }

    // ---------- paheal ----------
    private async Task<ImagePageInfo> GetPahealInfoAsync(string url, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, "CoveCommonImageDownloader/1.0", ct);
        var imageUrl = ExtractMetaContent(html, "og:image")
            ?? ExtractMetaContent(html, "twitter:image")
            ?? ExtractMetaContent(html, "twitter:image:src");

        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new InvalidOperationException("Could not find the image URL on the paheal page.");

        imageUrl = ResolveUrl(url, imageUrl!);

        var title = ExtractMetaContent(html, "og:title")
            ?? OfficialDownloaderUtilities.ExtractHtmlTitle(html)
            ?? OfficialDownloaderUtilities.DeriveTitleFromUrl(url, "paheal image");

        var keywords = ExtractMetaContent(html, "keywords");
        var tags = string.IsNullOrWhiteSpace(keywords)
            ? new List<string>()
            : keywords!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return new ImagePageInfo(url, CleanTitle(title!), imageUrl, null, null, new List<string>(), tags, ExtractMetaContent(html, "description"));
    }

    // ---------- gelbooru ----------
    private async Task<ImagePageInfo> GetGelbooruInfoAsync(string url, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, "CoveCommonImageDownloader/1.0", ct);
        var imageUrl = ExtractFirstAttribute(html, "img", "id", "image", "src")
            ?? ExtractMetaContent(html, "og:image")
            ?? ExtractFirstRegex(html, @"https?://[^\s""']+\.(?:jpg|jpeg|png|webp|gif)", RegexOptions.IgnoreCase);

        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new InvalidOperationException("Could not find the image URL on the gelbooru page.");

        imageUrl = ResolveUrl(url, imageUrl!);

        var (artists, characters, generals, all) = ExtractGelbooruTags(html);
        if (all.Count == 0)
        {
            var keywords = ExtractMetaContent(html, "keywords");
            if (!string.IsNullOrWhiteSpace(keywords))
                all = keywords!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var title = characters.Count > 0 ? string.Join(", ", characters) : OfficialDownloaderUtilities.DeriveTitleFromUrl(url, "gelbooru image");
        return new ImagePageInfo(url, title, imageUrl, artists.Count > 0 ? string.Join(", ", artists) : null, artists.Count > 0 ? string.Join(", ", artists) : null, characters, all, null);
    }

    // ---------- hentai-foundry ----------
    private async Task<ImagePageInfo> GetHentaiFoundryInfoAsync(string url, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, "CoveCommonImageDownloader/1.0", ct);
        if (html.Contains("Making sure you", StringComparison.OrdinalIgnoreCase) || html.Contains("anubis", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("hentai-foundry is protected by an anti-bot challenge and could not be scraped.");

        var imageUrl = ExtractMetaContent(html, "og:image")
            ?? ExtractMetaContent(html, "twitter:image")
            ?? ExtractMetaContent(html, "twitter:image:src");

        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new InvalidOperationException("Could not find the image URL on the hentai-foundry page.");

        imageUrl = ResolveUrl(url, imageUrl!);

        var title = ExtractMetaContent(html, "og:title")
            ?? OfficialDownloaderUtilities.ExtractHtmlTitle(html)
            ?? OfficialDownloaderUtilities.DeriveTitleFromUrl(url, "hentai-foundry image");

        var keywords = ExtractMetaContent(html, "keywords");
        var tags = string.IsNullOrWhiteSpace(keywords)
            ? new List<string>()
            : keywords!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var author = ExtractMetaContent(html, "author");
        return new ImagePageInfo(url, CleanTitle(title!), imageUrl, string.IsNullOrWhiteSpace(author) ? null : author, null, new List<string>(), tags, ExtractMetaContent(html, "description"));
    }

    // ---------- rule34.us ----------
    private async Task<ImagePageInfo> GetRule34UsInfoAsync(string url, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, "CoveCommonImageDownloader/1.0", ct);
        var imageUrl = ExtractFirstRegex(html, @"https?://img2\.rule34\.us/images/[^\s""']+", RegexOptions.IgnoreCase)
            ?? ExtractFirstRegex(html, @"https?://[^\s""']+\.rule34\.us/[^\s""']+\.(?:jpg|jpeg|png|webp|gif)", RegexOptions.IgnoreCase);

        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new InvalidOperationException("Could not find the image URL on the rule34.us page.");

        imageUrl = ResolveUrl(url, imageUrl!);

        var artistTags = ExtractClassLinks(html, "artist-tag");
        var characterTags = ExtractClassLinks(html, "character-tag");
        var generalTags = ExtractClassLinks(html, "general-tag");
        var metadataTags = ExtractClassLinks(html, "metadata-tag");

        var allTags = new List<string>();
        allTags.AddRange(artistTags);
        allTags.AddRange(characterTags);
        allTags.AddRange(generalTags);
        allTags.AddRange(metadataTags);
        allTags = allTags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (allTags.Count == 0)
        {
            var keywords = ExtractMetaContent(html, "keywords");
            if (!string.IsNullOrWhiteSpace(keywords))
                allTags = keywords!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var title = allTags.Count > 0
            ? string.Join(", ", allTags.Take(6))
            : OfficialDownloaderUtilities.DeriveTitleFromUrl(url, "rule34.us image");

        return new ImagePageInfo(url, title, imageUrl, artistTags.Count > 0 ? string.Join(", ", artistTags) : null, artistTags.Count > 0 ? string.Join(", ", artistTags) : null, characterTags, allTags, null);
    }

    // ---------- helpers ----------
    private async Task<string> FetchHtmlAsync(string url, string userAgent, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(userAgent);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string? ExtractMetaContent(string html, string propertyName)
        => OfficialDownloaderUtilities.ExtractMetaContent(html, propertyName);

    private static string? ExtractFirstRegex(string html, string pattern, RegexOptions options)
    {
        var match = Regex.Match(html, pattern, options | RegexOptions.Singleline);
        return match.Success ? match.Value : null;
    }

    private static string? ExtractFirstAttribute(string html, string tag, string attrName, string attrValue, string targetAttr)
    {
        // <tag ... attrName="attrValue" ... targetAttr="..." ...>
        var pattern = $@"(?is)<{tag}\b[^>]*{Regex.Escape(attrName)}\s*=\s*['""]{Regex.Escape(attrValue)}['""][^>]*{Regex.Escape(targetAttr)}\s*=\s*['""](?<v>[^'""]+)['""]";
        var match = Regex.Match(html, pattern);
        if (match.Success)
            return match.Groups["v"].Value;

        pattern = $@"(?is)<{tag}\b[^>]*{Regex.Escape(targetAttr)}\s*=\s*['""](?<v>[^'""]+)['""][^>]*{Regex.Escape(attrName)}\s*=\s*['""]{Regex.Escape(attrValue)}['""]";
        match = Regex.Match(html, pattern);
        return match.Success ? match.Groups["v"].Value : null;
    }

    private static (List<string> artists, List<string> characters, List<string> generals, List<string> all) ExtractGelbooruTags(string html)
    {
        var artists = new List<string>();
        var characters = new List<string>();
        var generals = new List<string>();

        foreach (Match m in Regex.Matches(html, @"(?is)<li\b[^>]*class\s*=\s*['""][^'""]*tag-type-artist[^'""]*['""][^>]*>.*?<a\b[^>]*>(?<t>.*?)</a>"))
        {
            var t = OfficialDownloaderUtilities.CleanHtmlFragment(m.Groups["t"].Value);
            if (!string.IsNullOrWhiteSpace(t)) artists.Add(t);
        }
        foreach (Match m in Regex.Matches(html, @"(?is)<li\b[^>]*class\s*=\s*['""][^'""]*tag-type-character[^'""]*['""][^>]*>.*?<a\b[^>]*>(?<t>.*?)</a>"))
        {
            var t = OfficialDownloaderUtilities.CleanHtmlFragment(m.Groups["t"].Value);
            if (!string.IsNullOrWhiteSpace(t)) characters.Add(t);
        }
        foreach (Match m in Regex.Matches(html, @"(?is)<li\b[^>]*class\s*=\s*['""][^'""]*tag-type-(?:general|metadata)[^'""]*['""][^>]*>.*?<a\b[^>]*>(?<t>.*?)</a>"))
        {
            var t = OfficialDownloaderUtilities.CleanHtmlFragment(m.Groups["t"].Value);
            if (!string.IsNullOrWhiteSpace(t)) generals.Add(t);
        }

        var all = new List<string>();
        all.AddRange(artists);
        all.AddRange(characters);
        all.AddRange(generals);
        all = all.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (artists, characters, generals, all);
    }

    private static List<string> ExtractClassLinks(string html, string cssClass)
    {
        var results = new List<string>();
        foreach (Match m in Regex.Matches(html, $@"(?is)<li\b[^>]*class\s*=\s*['""][^'""]*{Regex.Escape(cssClass)}[^'""]*['""][^>]*>.*?<a\b[^>]*>(?<t>.*?)</a>"))
        {
            var t = OfficialDownloaderUtilities.CleanHtmlFragment(m.Groups["t"].Value);
            if (!string.IsNullOrWhiteSpace(t) && !results.Contains(t, StringComparer.OrdinalIgnoreCase))
                results.Add(t);
        }
        return results;
    }

    private static List<string> GetStringList(JsonElement element, string propertyName)
    {
        var results = new List<string>();
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        results.Add(s!);
                }
            }
        }
        return results;
    }

    private static string ResolveUrl(string baseUrl, string candidate)
    {
        if (Uri.TryCreate(candidate, UriKind.Absolute, out _))
            return candidate;
        if (Uri.TryCreate(new Uri(baseUrl), candidate, out var relative))
            return relative.ToString();
        return candidate;
    }

    private static string CleanTitle(string title)
    {
        var cleaned = OfficialDownloaderUtilities.CleanHtmlFragment(title).Trim();
        cleaned = Regex.Replace(cleaned, @"\s*[-|]\s*(?:www\.)?[a-z0-9.-]+\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? title.Trim() : cleaned;
    }

    private HttpClient GetHttpClient()
    {
        if (_services == null)
            throw new InvalidOperationException("The common image downloader has not been initialized yet.");

        return _services.GetRequiredService<IHttpClientFactory>().CreateClient();
    }

    private sealed record ImagePageInfo(
        string PageUrl,
        string Title,
        string ImageUrl,
        string? Author,
        string? Studio,
        List<string> Performers,
        List<string> Tags,
        string? Details);
}