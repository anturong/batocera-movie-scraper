using System.Net;
using System.Text;
using System.Text.Json;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// OpenSubtitles 字幕源（opensubtitles.com 新版 REST 接口 api.opensubtitles.com/api/v1）。
/// 老的 api.opensubtitles.org XML-RPC 匿名下载只回广告占位、且 2026 年整体对第三方关闭，已弃用。
/// 搜索：GET /subtitles（只需 Api-Key）；下载：POST /download（需账号 Bearer token）→ 拿临时 link → GET 正文。
/// 语言码用站点原生的 "zh-cn"/"zh-tw"/"en"（见 <see cref="SubtitleLang"/>）。
/// </summary>
public sealed class OpenSubtitlesClient : ISubtitleSource
{
    const string Base = "https://api.opensubtitles.com/api/v1";

    readonly HttpClient _http;
    readonly string _apiKey;
    readonly string _ua;
    readonly string? _user;
    readonly string? _pass;
    string? _token;

    public OpenSubtitlesClient(AppConfig cfg)
    {
        var sc = cfg.Subtitles;
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (cfg.Proxy.Enabled && !string.IsNullOrWhiteSpace(cfg.Proxy.Url))
        {
            handler.Proxy = new WebProxy(cfg.Proxy.Url);
            handler.UseProxy = true;
        }
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _apiKey = sc.ApiKey?.Trim() ?? "";
        _ua = string.IsNullOrWhiteSpace(sc.UserAgent) ? "batoscrape v1.0" : sc.UserAgent!.Trim();
        _user = string.IsNullOrWhiteSpace(sc.Username) ? null : sc.Username!.Trim();
        _pass = sc.Password;
    }

    // ---- ISubtitleSource ----

    public async Task<IReadOnlyList<SubtitleHit>> SearchAsync(
        string? imdbId, IReadOnlyList<string> titles, int? year, string languages, CancellationToken ct = default)
    {
        if (_apiKey.Length == 0)
            throw new InvalidOperationException("未配置 OpenSubtitles Api-Key（配置项 subtitles.apiKey）。");
        var langs = string.Join(',', SubtitleLang.Split(languages));

        // ① IMDb id 精确查找（最可靠）；源没带 id 时（豆瓣）先经 IMDb 建议接口解析出 tt id。
        var tt = NormalizeImdb(imdbId) ?? NormalizeImdb(await ResolveImdbAsync(titles, year, ct));
        if (tt is not null)
        {
            var byImdb = await QueryAsync(("imdb_id", tt), langs, year, ct);
            if (byImdb.Count == 0) // 个别实现只认纯数字
                byImdb = await QueryAsync(("imdb_id", tt[2..]), langs, year, ct);
            if (byImdb.Count > 0) return byImdb;
        }

        // ② 按片名逐个别名尝试（中文/外文/aka 都可能命中）。
        foreach (var t in titles)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            var byName = await QueryAsync(("query", t.Trim()), langs, year, ct);
            if (byName.Count > 0) return byName;
        }
        return Array.Empty<SubtitleHit>();
    }

    public async Task<SubtitleFile?> DownloadAsync(SubtitleHit hit, CancellationToken ct = default)
    {
        if (!long.TryParse(hit.Id, out var fileId)) return null;

        // 只要 Api-Key 就能下（实测）。填了账号才顺带登录挂配额，失败就退回只带 Key，不重试免触发封 IP。
        var token = await TokenOrNullAsync(ct);
        var body = JsonSerializer.Serialize(new { file_id = fileId });
        using var resp = await SendAsync(HttpMethod.Post, $"{Base}/download", body, ct, token);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenSubtitles 下载失败: HTTP {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync(ct)}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var link = doc.RootElement.TryGetProperty("link", out var l) ? l.GetString() : null;
        if (string.IsNullOrWhiteSpace(link)) return null;
        // 检索接口 files[].file_name 不带后缀，真实后缀只有这里的 file_name 才可靠（如 ".srt"）。
        var fileName = doc.RootElement.TryGetProperty("file_name", out var fn) ? fn.GetString() : null;

        using var file = await _http.GetAsync(link, ct); // link 已预授权，不再需要 Key
        file.EnsureSuccessStatusCode();
        var bytes = await file.Content.ReadAsByteArrayAsync(ct);
        return new SubtitleFile(bytes, fileName ?? hit.FileName);
    }

    // ---- 搜索 ----

    async Task<List<SubtitleHit>> QueryAsync((string Key, string Val) field, string langs, int? year, CancellationToken ct)
    {
        var prs = new List<string> { "languages=" + Uri.EscapeDataString(langs), "type=movie" };
        prs.Add($"{field.Key}={Uri.EscapeDataString(field.Val)}");
        if (year is int y) prs.Add("year=" + y);

        using var resp = await SendAsync(HttpMethod.Get, $"{Base}/subtitles?" + string.Join('&', prs), null, ct);
        if (!resp.IsSuccessStatusCode) return new();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return new();

        var hits = new List<SubtitleHit>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("attributes", out var a) || a.ValueKind != JsonValueKind.Object) continue;

            string? fileId = null, fileName = null;
            if (a.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                foreach (var f in files.EnumerateArray())
                {
                    var fid = Num(f, "file_id");
                    if (fid is null) continue;
                    fileId = fid;
                    fileName = Str(f, "file_name");
                    break;
                }
            if (fileId is null) continue;

            var fmt = Path.GetExtension(fileName ?? "").TrimStart('.').ToLowerInvariant();
            hits.Add(new SubtitleHit
            {
                Id = fileId,
                Language = SubtitleLang.Normalize(Str(a, "language") ?? ""),
                Format = fmt.Length == 0 ? null : fmt,
                FileName = fileName,
                MovieName = FeatureTitle(a),
                MovieYear = FeatureYear(a),
                Downloads = int.TryParse(Num(a, "download_count"), out var d) ? d : 0,
                MatchedBy = Str(a, "release"),
            });
        }
        return hits;
    }

    static string? FeatureTitle(JsonElement a) =>
        a.TryGetProperty("feature_details", out var fd) && fd.ValueKind == JsonValueKind.Object
            ? Str(fd, "movie_name") ?? Str(fd, "title") : null;

    static string? FeatureYear(JsonElement a)
    {
        if (!a.TryGetProperty("feature_details", out var fd) || fd.ValueKind != JsonValueKind.Object) return null;
        return Num(fd, "year");
    }

    // ---- 会话（可选：只为挂账号配额，不登录也能下） ----

    bool _tokenTried;

    async Task<string?> TokenOrNullAsync(CancellationToken ct)
    {
        if (_token is not null) return _token;
        if (_tokenTried) return null;
        _tokenTried = true;
        if (_user is null || string.IsNullOrEmpty(_pass)) return null; // 未给账号：只带 Api-Key 下载

        try
        {
            var body = JsonSerializer.Serialize(new { username = _user, password = _pass });
            using var resp = await SendAsync(HttpMethod.Post, $"{Base}/login", body, ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return _token = doc.RootElement.TryGetProperty("token", out var tEl) ? tEl.GetString() : null;
        }
        catch { return null; }
    }

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? jsonBody, CancellationToken ct, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("Api-Key", _apiKey);
        req.Headers.TryAddWithoutValidation("User-Agent", _ua);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (bearer is not null) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        if (jsonBody is not null) req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return await _http.SendAsync(req, ct);
    }

    // ---- IMDb 建议接口：片名(+年份) → tt id（豆瓣无 imdb 时兜底） ----

    /// <summary>归一 IMDb id 为 "tt"+数字；"1856101" → "tt1856101"，非法返回 null。</summary>
    static string? NormalizeImdb(string? imdb)
    {
        if (string.IsNullOrWhiteSpace(imdb)) return null;
        var digits = new string(imdb.Where(char.IsDigit).ToArray());
        return digits.Length >= 6 ? "tt" + digits : null;
    }

    async Task<string?> ResolveImdbAsync(IReadOnlyList<string> titles, int? year, CancellationToken ct)
    {
        foreach (var t in titles)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            string? id;
            try { id = await SuggestAsync(t.Trim(), year, ct); }
            catch { id = null; }
            if (id is not null) return id;
        }
        return null;
    }

    async Task<string?> SuggestAsync(string query, int? year, CancellationToken ct)
    {
        var url = "https://v3.sg.media-imdb.com/suggestion/x/" + Uri.EscapeDataString(query) + ".json";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0 Safari/537.36");
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("d", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;

        string? bestId = null;
        var bestRank = int.MaxValue;
        foreach (var item in arr.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
            if (id is null || !id.StartsWith("tt")) continue;
            var label = item.TryGetProperty("l", out var lEl) && lEl.ValueKind == JsonValueKind.String ? lEl.GetString() : null;
            var y = item.TryGetProperty("y", out var yEl) && yEl.ValueKind == JsonValueKind.Number && yEl.TryGetInt32(out var yy) ? yy : (int?)null;

            var rank = 0;
            if (year is int wanted)
            {
                if (y == wanted) rank -= 100; else if (y is not null) rank += 20;
            }
            if (string.Equals(label, query, StringComparison.OrdinalIgnoreCase)) rank -= 10;

            if (rank < bestRank) { bestRank = rank; bestId = id; }
        }
        // 只有明显吻合（年份对上或标题完全相同）才认，避免硬塞一个不相干的 id。
        return bestRank < 0 ? bestId : null;
    }

    // ---- 取值助手 ----

    static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static string? Num(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.String => v.GetString(),
            _ => null,
        };
    }

    public void Dispose() => _http.Dispose();
}
