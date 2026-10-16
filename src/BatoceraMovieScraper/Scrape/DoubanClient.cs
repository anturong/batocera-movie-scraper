using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 精简豆瓣元数据源。
/// 搜索走 movie.douban.com 的 subject_suggest（纯 JSON，中文优先/前缀匹配），
/// 命中为空时退到 search.douban.com 的 subject_search（HTML 里 window.__DATA__ 是明文 JSON，中英文都能全文搜）。
/// 详情走 m.douban.com rexxar v2；豆瓣图片 CDN 校验 Referer，下载海报必须带上。
/// 无需鉴权；国内直连可达，通常不必走代理。
/// </summary>
public sealed partial class DoubanClient : IMovieSource
{
    const string UaDesktop = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";
    const string UaMobile = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1";

    readonly HttpClient _http;

    public DoubanClient(AppConfig cfg)
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (cfg.Proxy.Enabled && !string.IsNullOrWhiteSpace(cfg.Proxy.Url))
        {
            handler.Proxy = new WebProxy(cfg.Proxy.Url);
            handler.UseProxy = true;
        }
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public string IdName => "douban";

    // ---- IMovieSource ----

    public async Task<IReadOnlyList<MovieHit>> SearchAsync(string title, int? year, CancellationToken ct = default)
    {
        var hits = await SuggestAsync(title, ct);
        if (hits.Count > 0) return hits;
        return await FullSearchAsync(title, ct);
    }

    public async Task<MovieInfo?> GetInfoAsync(string id, CancellationToken ct = default)
    {
        var url = $"https://m.douban.com/rexxar/api/v2/movie/{Uri.EscapeDataString(id)}";
        var json = await GetStringAsync(url, UaMobile, $"https://m.douban.com/movie/subject/{id}/", ct);
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;

        string? poster = null;
        if (r.TryGetProperty("pic", out var pic) && pic.ValueKind == JsonValueKind.Object)
            poster = Str(pic, "large") ?? Str(pic, "normal");
        poster ??= Str(r, "cover_url");

        double? rating = null;
        if (r.TryGetProperty("rating", out var rat) && rat.ValueKind == JsonValueKind.Object)
            rating = Num(rat, "value");

        var release = Str(r, "release_date");
        if (string.IsNullOrWhiteSpace(release)) release = FirstDate(FirstArrayString(r, "pubdate"));

        return new MovieInfo
        {
            Id = id,
            Title = Str(r, "title"),
            OriginalTitle = Str(r, "original_title"),
            Year = ParseInt(Str(r, "year")),
            ReleaseDate = release,
            Plot = Str(r, "intro"),
            Genre = JoinArray(r, "genres"),
            Rating = rating,
            PosterRef = poster,
            Aliases = StrArray(r, "aka"),
        };
    }

    public async Task DownloadPosterAsync(string id, string posterRef, string destFile, CancellationToken ct = default)
    {
        var bytes = await GetBytesAsync(posterRef, UaDesktop, "https://movie.douban.com/", ct);
        var dir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(destFile, bytes, ct);
    }

    // ---- 搜索：先 suggest，后全文 ----

    async Task<List<MovieHit>> SuggestAsync(string title, CancellationToken ct)
    {
        var url = "https://movie.douban.com/j/subject_suggest?q=" + Uri.EscapeDataString(title);
        var hits = new List<MovieHit>();
        try
        {
            var json = await GetStringAsync(url, UaDesktop, null, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return hits;
            foreach (var it in doc.RootElement.EnumerateArray())
            {
                var type = Str(it, "type");
                if (type is not null && type != "movie") continue;
                var id = Str(it, "id");
                if (string.IsNullOrEmpty(id)) continue;
                hits.Add(new MovieHit
                {
                    Id = id!,
                    Title = Str(it, "title"),
                    OriginalTitle = Str(it, "sub_title"),
                    Year = ParseInt(Str(it, "year")),
                });
            }
        }
        catch (Exception) { /* suggest 失败就交给全文搜 */ }
        return hits;
    }

    async Task<List<MovieHit>> FullSearchAsync(string title, CancellationToken ct)
    {
        var url = "https://search.douban.com/movie/subject_search?search_text="
                  + Uri.EscapeDataString(title) + "&cat=1002";
        var html = await GetStringAsync(url, UaDesktop, null, ct);
        return ParseSearchHtml(html);
    }

    /// <summary>从 subject_search 的 HTML 里取 window.__DATA__（明文 JSON）并映射为命中。</summary>
    internal static List<MovieHit> ParseSearchHtml(string html)
    {
        var hits = new List<MovieHit>();
        var jsonText = ExtractDataJson(html);
        if (jsonText is null) return hits;
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return hits;
            foreach (var it in items.EnumerateArray())
            {
                var id = Str(it, "id");
                if (string.IsNullOrEmpty(id)) continue;
                hits.Add(new MovieHit
                {
                    Id = id!,
                    Title = CleanTitle(Str(it, "title") ?? "", out var year),
                    Year = year,
                });
            }
        }
        catch (JsonException) { }
        return hits;
    }

    /// <summary>定位 window.__DATA__ 后跟的平衡花括号 JSON 片段。</summary>
    internal static string? ExtractDataJson(string html)
    {
        var idx = html.IndexOf("window.__DATA__", StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = html.IndexOf('{', idx);
        if (start < 0) return null;

        int depth = 0, end = -1;
        bool inStr = false, esc = false;
        for (int i = start; i < html.Length; i++)
        {
            var c = html[i];
            if (inStr)
            {
                if (esc) esc = false;
                else if (c == '\\') esc = true;
                else if (c == '"') inStr = false;
                continue;
            }
            if (c == '"') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) { end = i + 1; break; }
        }
        return end < 0 ? null : html[start..end];
    }

    [GeneratedRegex(@"\((\d{4})\)")]
    private static partial Regex YearInTitle();

    /// <summary>去掉标题末尾的「 (年份)」与不可见方向符，抽出年份。</summary>
    static string CleanTitle(string raw, out int? year)
    {
        year = null;
        var ms = YearInTitle().Matches(raw);
        if (ms.Count > 0 && int.TryParse(ms[^1].Groups[1].Value, out var y)) year = y;
        var t = Regex.Replace(raw, @"\s*\(\d{4}\)\s*$", "");
        return t.Replace("‎", "").Replace("‏", "").Trim();
    }

    // ---- HTTP 底层 ----

    async Task<string> GetStringAsync(string url, string ua, string? referer, CancellationToken ct)
    {
        using var resp = await SendAsync(url, ua, referer, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    async Task<byte[]> GetBytesAsync(string url, string ua, string? referer, CancellationToken ct)
    {
        using var resp = await SendAsync(url, ua, referer, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    async Task<HttpResponseMessage> SendAsync(string url, string ua, string? referer, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", ua);
        req.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        if (referer is not null) req.Headers.Referrer = new Uri(referer);
        return await _http.SendAsync(req, ct);
    }

    // ---- JSON 取值助手 ----

    static string? Str(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    static double? Num(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;
    }

    static int? ParseInt(string? s) => int.TryParse(s, out var v) ? v : null;

    static string? FirstArrayString(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return null;
        foreach (var e in a.EnumerateArray())
            if (e.ValueKind == JsonValueKind.String) return e.GetString();
        return null;
    }

    static IReadOnlyList<string> StrArray(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return a.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    static string? JoinArray(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return null;
        var items = a.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        return items.Count > 0 ? string.Join(", ", items) : null;
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex IsoDate();

    /// <summary>从 "2000-01-14(中国大陆)" 之类的串里抽出 ISO 日期。</summary>
    static string? FirstDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var m = IsoDate().Match(s);
        return m.Success ? m.Value : null;
    }

    public void Dispose() => _http.Dispose();
}
