using System.Text;
using System.Text.RegularExpressions;
using Cove.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace TextDownloader;

// A second downloader provider. It leaves CommonTextDownloader and any
// addon-specific downloader plugins untouched. Add more sites by copying one of
// the downloaders below and giving it a unique id and URL patterns.
public sealed class TextDownloaderExtension : IDownloaderProvider
{
    private const string ExtensionId = "com.binarygeek119.text-downloader";

    public const string DaddyForBabyDownloaderId = "com.binarygeek119.text-downloader/daddyforbaby";
    public const string BeltedGirlsDownloaderId = "com.binarygeek119.text-downloader/beltedgirls";
    public const string NewSexStoryDownloaderId = "com.binarygeek119.text-downloader/newsexstory";
    public const string GayDemonDownloaderId = "com.binarygeek119.text-downloader/gaydemon";
    public const string DeviantArtDownloaderId = "com.binarygeek119.text-downloader/deviantart";
    public const string StoriesOnlineDownloaderId = "com.binarygeek119.text-downloader/storiesonline";
    public const string AsstrMirrorDownloaderId = "com.binarygeek119.text-downloader/asstrmirror";
    public const string NiftyDownloaderId = "com.binarygeek119.text-downloader/nifty";

    private IServiceProvider? _services;

    private static readonly DownloaderDescriptor DaddyForBabyDownloader = new(
        DaddyForBabyDownloaderId,
        "DaddyForBaby Blogspot Text",
        DownloaderEntity.Text,
        new[] { "daddyforbaby.blogspot.com/*" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor BeltedGirlsDownloader = new(
        BeltedGirlsDownloaderId,
        "Belted Girls Text",
        DownloaderEntity.Text,
        new[] { "beltedgirls.com/stories/*", "www.beltedgirls.com/stories/*" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor NewSexStoryDownloader = new(
        NewSexStoryDownloaderId,
        "NewSexStory Text",
        DownloaderEntity.Text,
        new[] { "newsexstory.com/*/*" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor GayDemonDownloader = new(
        GayDemonDownloaderId,
        "GayDemon Text",
        DownloaderEntity.Text,
        new[] { "gaydemon.com/stories/*.html", "www.gaydemon.com/stories/*.html" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor DeviantArtDownloader = new(
        DeviantArtDownloaderId,
        "DeviantArt Literature Text",
        DownloaderEntity.Text,
        new[] { "deviantart.com/*/art/*", "www.deviantart.com/*/art/*" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor StoriesOnlineDownloader = new(
        StoriesOnlineDownloaderId,
        "StoriesOnline Text",
        DownloaderEntity.Text,
        new[] { "storiesonline.net/s/*", "storiesonline.net/n/*", "www.storiesonline.net/s/*", "www.storiesonline.net/n/*" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor AsstrMirrorDownloader = new(
        AsstrMirrorDownloaderId,
        "ASSTR Mirror Text",
        DownloaderEntity.Text,
        new[] { "asstr-mirror.org/files/*", "www.asstr-mirror.org/files/*" },
        DownloaderCapabilities.None);

    private static readonly DownloaderDescriptor NiftyDownloader = new(
        NiftyDownloaderId,
        "Nifty Text",
        DownloaderEntity.Text,
        new[] { "nifty.org/nifty/*", "www.nifty.org/nifty/*" },
        DownloaderCapabilities.None);

    public string Id => ExtensionId;
    public string Name => "Text Downloader";
    public string Version => "0.1.2";
    public string? Description => "Downloads text stories from additional sites.";
    public string? Author => "binarygeek119";
    public string? Url => "https://github.com/binarygeek119/cove-ai-plugins";
    public string? IconUrl => null;
    public IReadOnlyList<string> Categories => new[] { "downloader", "text" };

    public void ConfigureServices(IServiceCollection services, ExtensionContext context)
    {
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _services = services;
        return Task.CompletedTask;
    }

    public IReadOnlyList<DownloaderDescriptor> GetDownloaders() => new[] { DaddyForBabyDownloader, BeltedGirlsDownloader, NewSexStoryDownloader, GayDemonDownloader, DeviantArtDownloader, StoriesOnlineDownloader, AsstrMirrorDownloader, NiftyDownloader };

    public Task<DownloaderUrlMatch?> MatchAsync(string url, CancellationToken ct)
    {
        if (IsDaddyForBabyUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(DaddyForBabyDownloader.Id, url, null, DeriveTitleFromUrl(url, "DaddyForBaby story")));

        if (IsBeltedGirlsUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(BeltedGirlsDownloader.Id, url, null, DeriveTitleFromUrl(url, "Belted Girls story")));

        if (IsNewSexStoryUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(NewSexStoryDownloader.Id, url, null, DeriveTitleFromUrl(url, "NewSexStory story")));

        if (IsGayDemonUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(GayDemonDownloader.Id, url, null, DeriveTitleFromUrl(url, "GayDemon story")));

        if (IsDeviantArtUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(DeviantArtDownloader.Id, url, null, DeriveTitleFromUrl(url, "DeviantArt literature")));

        if (IsStoriesOnlineUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(StoriesOnlineDownloader.Id, url, null, DeriveTitleFromUrl(url, "StoriesOnline story")));

        if (IsAsstrMirrorUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(AsstrMirrorDownloader.Id, url, null, DeriveTitleFromUrl(url, "ASSTR Mirror story")));

        if (IsNiftyUrl(url))
            return Task.FromResult<DownloaderUrlMatch?>(new DownloaderUrlMatch(NiftyDownloader.Id, url, null, DeriveTitleFromUrl(url, "Nifty story")));

        return Task.FromResult<DownloaderUrlMatch?>(null);
    }

    public async Task<DownloaderResult?> DownloadAsync(DownloaderRequest request, IDownloaderHost host, CancellationToken ct)
    {
        if (string.Equals(request.DownloaderId, DaddyForBabyDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsDaddyForBabyUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetDaddyForBabyTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, BeltedGirlsDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsBeltedGirlsUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetBeltedGirlsTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, NewSexStoryDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsNewSexStoryUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetNewSexStoryTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, GayDemonDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsGayDemonUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetGayDemonTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, DeviantArtDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsDeviantArtUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetDeviantArtTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, StoriesOnlineDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsStoriesOnlineUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetStoriesOnlineTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, AsstrMirrorDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsAsstrMirrorUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetAsstrMirrorTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        if (string.Equals(request.DownloaderId, NiftyDownloader.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Entity != DownloaderEntity.Text)
                throw new InvalidOperationException("This downloader only supports text downloads.");
            if (!IsNiftyUrl(request.Url))
                throw new InvalidOperationException("This downloader does not support the requested URL.");

            host.ReportProgress(0.1d, "Downloading text page...");
            var info = await GetNiftyTextInfoAsync(request.Url, ct);
            var localName = "downloaded.txt";
            await File.WriteAllTextAsync(Path.Combine(host.TempDirectory, localName), info.Body.Trim() + Environment.NewLine, Encoding.UTF8, ct);
            host.ReportProgress(0.95d, "Text download completed.");
            return new DownloaderResult(localName, SanitizeFileName(info.Title + ".txt"));
        }

        return null;
    }

    private static bool IsDaddyForBabyUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Equals("daddyforbaby.blogspot.com", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(uri.AbsolutePath)
            && uri.AbsolutePath != "/";
    }

    private static bool IsBeltedGirlsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "beltedgirls.com" && host != "www.beltedgirls.com")
            return false;

        return uri.AbsolutePath.StartsWith("/stories/", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Length > "/stories/".Length;
    }

    private static bool IsNewSexStoryUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "newsexstory.com" && host != "www.newsexstory.com")
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2;
    }

    private static bool IsGayDemonUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "gaydemon.com" && host != "www.gaydemon.com")
            return false;

        return uri.AbsolutePath.StartsWith("/stories/", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeviantArtUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "deviantart.com" && host != "www.deviantart.com")
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3
            && segments[1].Equals("art", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStoriesOnlineUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "storiesonline.net" && host != "www.storiesonline.net")
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2
            && (segments[0].Equals("s", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("n", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAsstrMirrorUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "asstr-mirror.org" && host != "www.asstr-mirror.org")
            return false;

        return uri.AbsolutePath.StartsWith("/files/", StringComparison.OrdinalIgnoreCase)
            && Path.HasExtension(uri.AbsolutePath);
    }

    private static bool IsNiftyUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        if (host != "nifty.org" && host != "www.nifty.org")
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
            && segments[0].Equals("nifty", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<TextPageInfo?> TryGetDaddyForBabyTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetDaddyForBabyTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetDaddyForBabyTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractTitle(html) ?? DeriveTitleFromUrl(url, "DaddyForBaby story");
        var body = ExtractBloggerBody(html);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetBeltedGirlsTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetBeltedGirlsTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetBeltedGirlsTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractBeltedGirlsTitle(html) ?? DeriveTitleFromUrl(url, "Belted Girls story");
        var body = ExtractBeltedGirlsBody(html);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetNewSexStoryTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetNewSexStoryTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetNewSexStoryTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractNewSexStoryTitle(html) ?? DeriveTitleFromUrl(url, "NewSexStory story");
        var body = ExtractNewSexStoryBody(html);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetGayDemonTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetGayDemonTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetGayDemonTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractGayDemonTitle(html) ?? DeriveTitleFromUrl(url, "GayDemon story");
        var body = ExtractGayDemonBody(html);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetDeviantArtTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetDeviantArtTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetDeviantArtTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractDeviantArtTitle(html) ?? DeriveTitleFromUrl(url, "DeviantArt literature");
        var body = ExtractDeviantArtBody(html);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetStoriesOnlineTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetStoriesOnlineTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetStoriesOnlineTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractStoriesOnlineTitle(html) ?? DeriveTitleFromUrl(url, "StoriesOnline story");
        var body = ExtractStoriesOnlineBody(html);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetAsstrMirrorTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetAsstrMirrorTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetAsstrMirrorTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractAsstrMirrorTitle(url, content);
        var body = ExtractAsstrMirrorBody(url, content);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private async Task<TextPageInfo?> TryGetNiftyTextInfoAsync(string url, CancellationToken ct)
    {
        try { return await GetNiftyTextInfoAsync(url, ct); }
        catch { return null; }
    }

    private async Task<TextPageInfo> GetNiftyTextInfoAsync(string url, CancellationToken ct)
    {
        using var request = CreateGetRequest(url);
        using var response = await GetHttpClient().SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractNiftyTitle(content) ?? DeriveTitleFromUrl(url, "Nifty story");
        var body = ExtractNiftyBody(content);
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("The page was downloaded, but the text could not be found.");

        return new TextPageInfo(title, body);
    }

    private static HttpRequestMessage CreateGetRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return request;
    }

    private HttpClient GetHttpClient()
    {
        if (_services is null)
            throw new InvalidOperationException("The text downloader has not been initialized yet.");

        return _services.GetRequiredService<IHttpClientFactory>().CreateClient();
    }

    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static string? ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"(?is)<title[^>]*>(.*?)</title>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return ExtractMetaContent(html, "og:title");
    }

    private static string ExtractBloggerBody(string html)
    {
        var container = ExtractBloggerPostBody(html) ?? ExtractElement(html, "article") ?? ExtractElement(html, "main") ?? html;

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(container, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(container);
    }

    private static string? ExtractBloggerPostBody(string html)
    {
        var match = Regex.Match(html, @"(?is)<div class='post-body entry-content[^']*'[^>]*>(?<content>.*?)</div>\s*<div class='post-bottom'>");
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static string? ExtractBeltedGirlsTitle(string html)
    {
        var match = Regex.Match(html, @"(?is)<h1[^>]*>(.*?)</h1>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        var titleMatch = Regex.Match(html, @"(?is)<title[^>]*>(.*?)</title>");
        if (titleMatch.Success)
        {
            var title = Regex.Replace(titleMatch.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            title = Regex.Replace(title, @"^Belted Girls\s*-\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return ExtractMetaContent(html, "og:title");
    }

    private static string ExtractBeltedGirlsBody(string html)
    {
        var match = Regex.Match(html, @"(?is)<td colspan=""4"" bgcolor=""#000000"">(?<content>.*?)</td>", RegexOptions.IgnoreCase);
        var container = match.Success ? match.Groups["content"].Value : (ExtractElement(html, "body") ?? html);

        var paragraphs = new List<string>();
        foreach (Match p in Regex.Matches(container, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(p.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph) && paragraph != "\u00A0")
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(container);
    }

    private static string? ExtractNewSexStoryTitle(string html)
    {
        var match = Regex.Match(html, @"(?is)<h1[^>]*>(.*?)</h1>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return ExtractMetaContent(html, "og:title");
    }

    private static string ExtractNewSexStoryBody(string html)
    {
        var start = html.IndexOf("<div class=\"entry-content\">", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return CleanPreservingLineBreaks(html);

        var end = html.IndexOf("This story", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = html.IndexOf("More from", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = html.Length;

        var container = html.Substring(start, end - start);

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(container, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(container);
    }

    private static string? ExtractGayDemonTitle(string html)
    {
        var match = Regex.Match(html, @"(?is)<h1[^>]*>(.*?)</h1>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        var titleMatch = Regex.Match(html, @"(?is)<title[^>]*>(.*?)</title>");
        if (titleMatch.Success)
        {
            var title = Regex.Replace(titleMatch.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            title = Regex.Replace(title, @"\s*\[Story\].*$", string.Empty, RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return ExtractMetaContent(html, "og:title");
    }

    private static string ExtractGayDemonBody(string html)
    {
        var match = Regex.Match(html, @"(?is)<div class=""textify story-text""[^>]*>(?<content>.*?)</div>", RegexOptions.IgnoreCase);
        var container = match.Success ? match.Groups["content"].Value : (ExtractElement(html, "article") ?? html);

        var paragraphs = new List<string>();
        foreach (Match p in Regex.Matches(container, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(p.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(container);
    }

    private static string? ExtractDeviantArtTitle(string html)
    {
        var match = Regex.Match(html, @"(?is)<h1[^>]*>(.*?)</h1>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return ExtractMetaContent(html, "og:title");
    }

    private static string ExtractDeviantArtBody(string html)
    {
        var start = html.IndexOf("<div id=\"devskin0\">", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return CleanPreservingLineBreaks(html);

        var end = html.IndexOf("More by", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = html.IndexOf("Recommended Literature", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = html.Length;

        var container = html.Substring(start, end - start);

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(container, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(container);
    }

    private static string? ExtractStoriesOnlineTitle(string html)
    {
        var match = Regex.Match(html, @"(?is)<h1[^>]*id=""s-title""[^>]*>(.*?)</h1>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        match = Regex.Match(html, @"(?is)<h1[^>]*>(.*?)</h1>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return ExtractMetaContent(html, "og:title");
    }

    private static string ExtractStoriesOnlineBody(string html)
    {
        var start = html.IndexOf("<article>", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            start = 0;

        var end = html.IndexOf("</article>", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            end = html.Length;

        var container = html.Substring(start, end - start);

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(container, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(container);
    }

    private static string ExtractAsstrMirrorTitle(string url, string content)
    {
        var match = Regex.Match(content, @"(?is)<title[^>]*>(.*?)</title>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        var h1 = Regex.Match(content, @"(?is)<h1[^>]*>(.*?)</h1>");
        if (h1.Success)
        {
            var title = Regex.Replace(h1.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return DeriveTitleFromUrl(url, "ASSTR Mirror story");
    }

    private static string ExtractAsstrMirrorBody(string url, string content)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : string.Empty;
        if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return content.Trim();
        }

        var start = content.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            var end = content.IndexOf("</body>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
                end = content.Length;
            else
                end += "</body>".Length;
            content = content.Substring(start, end - start);
        }

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(content, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(content);
    }

    private static string? ExtractNiftyTitle(string content)
    {
        // Nifty serves stories as plain text with email-like headers.
        foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if (line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
            {
                var title = line.Substring("Subject:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    return title;
            }
        }

        var match = Regex.Match(content, @"(?is)<title[^>]*>(.*?)</title>");
        if (match.Success)
        {
            var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return System.Net.WebUtility.HtmlDecode(title);
        }

        return null;
    }

    private static string ExtractNiftyBody(string content)
    {
        // Strip email-like headers.
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var bodyStart = 0;
        var seenSubject = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
            {
                seenSubject = true;
                continue;
            }

            if (seenSubject && string.IsNullOrWhiteSpace(lines[i]))
            {
                bodyStart = i + 1;
                break;
            }
        }

        if (bodyStart > 0 && bodyStart < lines.Length)
        {
            return string.Join(Environment.NewLine, lines.Skip(bodyStart)).Trim();
        }

        // Fallback: HTML page.
        var start = content.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            var end = content.IndexOf("</body>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
                end = content.Length;
            else
                end += "</body>".Length;
            content = content.Substring(start, end - start);
        }

        var paragraphs = new List<string>();
        foreach (Match match in Regex.Matches(content, @"(?is)<p\b[^>]*>(.*?)</p>"))
        {
            var paragraph = CleanHtmlFragment(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);
        }

        if (paragraphs.Count > 0)
            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);

        return CleanPreservingLineBreaks(content);
    }

    private static string? ExtractElement(string html, string tagName)
    {
        var match = Regex.Match(html, $@"(?is)<{tagName}\b[^>]*>(?<content>.*?)</{tagName}>");
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static string? ExtractMetaContent(string html, string propertyName)
    {
        foreach (Match metaTag in Regex.Matches(html, @"(?is)<meta\b[^>]*>"))
        {
            var attributes = ParseAttributes(metaTag.Value);
            var property = attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name");
            if (propertyName.Equals(property, StringComparison.OrdinalIgnoreCase)
                && attributes.TryGetValue("content", out var content)
                && !string.IsNullOrWhiteSpace(content))
            {
                return System.Net.WebUtility.HtmlDecode(content).Trim();
            }
        }

        return null;
    }

    private static Dictionary<string, string> ParseAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match attribute in Regex.Matches(tag, @"(?is)([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:(['""])(.*?)\2|([^\s>]+))"))
        {
            attributes[attribute.Groups[1].Value] = attribute.Groups[3].Success
                ? attribute.Groups[3].Value
                : attribute.Groups[4].Value;
        }

        return attributes;
    }

    private static string CleanHtmlFragment(string value)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(value);
        var withoutTags = Regex.Replace(decoded, "<[^>]+>", string.Empty);
        return Regex.Replace(withoutTags, @"\s+", " ").Trim();
    }

    private static string CleanPreservingLineBreaks(string html)
    {
        var text = Regex.Replace(html, @"(?i)<\s*br\s*/?\s*>", "\n");
        text = Regex.Replace(text, @"(?i)</\s*(p|div|section|article|h[1-6])\s*>", "\n\n");
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t\r\f\v]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string DeriveTitleFromUrl(string url, string fallback)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return fallback;

        var lastSegment = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(lastSegment))
            return fallback;

        lastSegment = Uri.UnescapeDataString(lastSegment);
        lastSegment = Path.GetFileNameWithoutExtension(lastSegment);
        return lastSegment.Replace('_', ' ').Replace('-', ' ').Trim();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "downloaded" : sanitized;
    }

    private sealed record TextPageInfo(string Title, string Body);
}