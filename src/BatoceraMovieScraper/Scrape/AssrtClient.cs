using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 射手网(伪) assrt.net 字幕源（国内直连，api.assrt.net/v1）。
/// 搜索 GET /sub/search?token=&q=&cnt=15；详情 GET /sub/detail?token=&id=。
/// 站点要求 token（32 位，用户面板生成）且配额 20 次/分钟（token 与 IP 共用）。
/// 关键点：详情里的 filelist[].url 是**解压后的单文件直链**（on-the-fly），
/// 所以不必自己解 rar/7z——优先走它；退回整包 url 时才尝试解 zip。
/// 语言用 langlist 的对象键（langchs/langcht/langdou/langeng…）换算到配置侧码。
/// </summary>
public sealed class AssrtClient : ISubtitleSource
{
    const string Base = "https://api.assrt.net/v1";

    readonly HttpClient _http;         // 直连（国内站优先走这条）
    readonly HttpClient? _httpProxy;   // 直连不通时的兜底（仅当配置了代理）
    readonly string _token;
    readonly string _ua;

    // 静态注册代码页提供程序（GBK/GB18030 转码用）；幂等，重复调用无副作用。
    static AssrtClient() { try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { /* 极简运行时忽略 */ } }

    public AssrtClient(AppConfig cfg)
    {
        var sc = cfg.Subtitles;
        // assrt 是国内站：优先直连。若外面配了代理（多半是给 TMDB 翻墙用的），
        // 只在直连失败时回退到代理，免得代理没开反而把国内源也拖死。
        _http = new HttpClient(NewHandler(null)) { Timeout = TimeSpan.FromSeconds(30) };
        if (cfg.Proxy.Enabled && !string.IsNullOrWhiteSpace(cfg.Proxy.Url))
            _httpProxy = new HttpClient(NewHandler(cfg.Proxy.Url)) { Timeout = TimeSpan.FromSeconds(30) };
        _token = sc.AssrtToken?.Trim() ?? "";
        _ua = string.IsNullOrWhiteSpace(sc.UserAgent) ? "batoscrape v1.0" : sc.UserAgent!.Trim();
    }

    static HttpClientHandler NewHandler(string? proxyUrl)
    {
        var h = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (!string.IsNullOrWhiteSpace(proxyUrl)) { h.Proxy = new WebProxy(proxyUrl); h.UseProxy = true; }
        return h;
    }

    // ---- ISubtitleSource ----

    public async Task<IReadOnlyList<SubtitleHit>> SearchAsync(
        string? imdbId, IReadOnlyList<string> titles, int? year, string languages, CancellationToken ct = default)
    {
        if (_token.Length == 0)
            throw new InvalidOperationException("未配置射手网(assrt.net) Token（配置项 subtitles.assrtToken）。");
        var wanted = SubtitleLang.Split(languages);

        // 按片名别名逐个试，命中即止。带年份更准；限 3 次搜索以顾配额（20 次/分钟）。
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var n = 0;
        foreach (var t in titles)
        {
            var title = (t ?? "").Trim();
            if (title.Length < 3 || !tried.Add(title)) continue; // 接口要求关键词 ≥3 字符
            var q = year is int y ? title + " " + y : title;
            var hits = await QueryAsync(q, wanted, ct);
            if (hits.Count > 0) return hits;
            if (++n >= 3) break;
        }
        return Array.Empty<SubtitleHit>();
    }

    public async Task<SubtitleFile?> DownloadAsync(SubtitleHit hit, CancellationToken ct = default)
    {
        using var resp = await SendAsync($"{Base}/sub/detail?token={Uri.EscapeDataString(_token)}&id={Uri.EscapeDataString(hit.Id)}", ct);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        ThrowIfError(root, "详情");

        if (!root.TryGetProperty("sub", out var sub) || sub.ValueKind != JsonValueKind.Object) return null;
        if (!sub.TryGetProperty("subs", out var subs) || subs.ValueKind != JsonValueKind.Array || subs.GetArrayLength() == 0) return null;
        var s0 = subs[0];

        // ① 优先 filelist 的单文件直链（on-the-fly 解压，免自解压包）。
        string? dlUrl = null, dlName = null;
        var hasFilelist = s0.TryGetProperty("filelist", out var fl)
                          && fl.ValueKind == JsonValueKind.Array && fl.GetArrayLength() > 0;
        if (hasFilelist)
        {
            var pick = PickFile(fl, hit.Language);
            if (pick is { } p) { dlUrl = p.Url; dlName = p.Name; }
        }
        // ② 退回整包 url（可能是 rar/zip，能解 zip 就解）。仅在无 filelist 时兜底：
        //    有 filelist 却挑不出（全是外语）时宁可放弃，也别退回整包瞎猜。
        if (!hasFilelist)
        {
            dlUrl = Str(s0, "url");
            dlName = Str(s0, "filename");
        }
        if (string.IsNullOrWhiteSpace(dlUrl)) return null;

        using var file = await SendAsync(dlUrl, ct); // 直链域名是 file0.assrt.net，同样直连优先
        file.EnsureSuccessStatusCode();
        var raw = await file.Content.ReadAsByteArrayAsync(ct);

        var (content, name) = Extract(raw, dlName);
        if (content is null || content.Length == 0) return null;
        return new SubtitleFile(EnsureUtf8(content), name ?? hit.FileName);
    }

    // ---- 搜索 ----

    async Task<List<SubtitleHit>> QueryAsync(string q, List<string> wanted, CancellationToken ct)
    {
        var url = $"{Base}/sub/search?token={Uri.EscapeDataString(_token)}&q={Uri.EscapeDataString(q)}&cnt=15";
        using var resp = await SendAsync(url, ct);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        if (IsNoResult(root)) return new();      // 101 关键词过短 / 20900 无结果 → 当作空
        ThrowIfError(root, "搜索");
        if (!root.TryGetProperty("sub", out var sub) || sub.ValueKind != JsonValueKind.Object) return new();
        if (!sub.TryGetProperty("subs", out var subs) || subs.ValueKind != JsonValueKind.Array) return new();

        var hits = new List<SubtitleHit>();
        foreach (var s in subs.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.Object) continue;
            var id = Num(s, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var langs = MapLangs(s);
            var pick = wanted.FirstOrDefault(w => langs.Contains(w, StringComparer.OrdinalIgnoreCase));
            if (pick is null) continue; // 不含请求语言，跳过（挑片阶段也会滤掉）

            hits.Add(new SubtitleHit
            {
                Id = id!,
                Language = pick,
                Format = FormatOf(Str(s, "subtype")),
                FileName = Str(s, "videoname"),
                MovieName = NativeName(s),
                Downloads = int.TryParse(Num(s, "vote_score"), out var v) ? v : 0,
                LangConfidence = HasLanglist(s) ? 2 : 1, // langlist 明写 > 靠 desc 猜
                MatchedBy = Str(s, "release_site"),
            });
        }
        return hits;
    }

    // ---- 语言映射：langlist 对象键 → 配置侧码 ----

    static readonly Dictionary<string, string> LangCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chs"] = "zh-cn", ["chn"] = "zh-cn", ["hans"] = "zh-cn",
        ["cht"] = "zh-tw", ["twn"] = "zh-tw", ["hant"] = "zh-tw",
        ["eng"] = "en", ["jap"] = "ja", ["jp"] = "ja", ["kor"] = "ko",
        ["fra"] = "fr", ["fre"] = "fr", ["esp"] = "es", ["spa"] = "es",
        ["dou"] = "zh-cn", // 中英双语，含中文
        ["简体"] = "zh-cn", ["簡體"] = "zh-cn", ["繁体"] = "zh-tw", ["繁體"] = "zh-tw", ["英文"] = "en",
    };

    /// <summary>langlist 里是否真有显式语言项（非空且非全 false）——用来判语言标注可信度。</summary>
    static bool HasLanglist(JsonElement s) =>
        s.TryGetProperty("lang", out var lang) && lang.ValueKind == JsonValueKind.Object
        && lang.TryGetProperty("langlist", out var ll) && ll.ValueKind == JsonValueKind.Object
        && ll.EnumerateObject().Any(p => p.Value.ValueKind != JsonValueKind.False);

    static List<string> MapLangs(JsonElement s)
    {
        var outp = new List<string>();
        if (!s.TryGetProperty("lang", out var lang) || lang.ValueKind != JsonValueKind.Object) return outp;

        if (lang.TryGetProperty("langlist", out var ll) && ll.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in ll.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.False) continue;
                var key = p.Name;
                if (key.StartsWith("lang", StringComparison.OrdinalIgnoreCase)) key = key[4..];
                var code = LangCode.TryGetValue(key, out var c) ? c : SubtitleLang.Normalize(key);
                if (code.Length > 0 && !outp.Contains(code)) outp.Add(code);
            }
        }
        if (outp.Count == 0) // 少数条目没 langlist，退回 desc 关键词
        {
            var desc = Str(lang, "desc") ?? "";
            if (desc.Contains('简') || desc.Contains("双语")) outp.Add("zh-cn");
            else if (desc.Contains('繁')) outp.Add("zh-tw");
            else if (desc.Contains('英')) outp.Add("en");
        }
        return outp;
    }

    static string? FormatOf(string? subtype)
    {
        var s = (subtype ?? "").ToLowerInvariant();
        if (s.Contains("srt")) return "srt";
        if (s.Contains("ssa")) return "ssa";
        if (s.Contains("ass")) return "ass";
        if (s.Contains("vobsub")) return "sub";
        if (s.Contains("vtt")) return "vtt";
        if (s.Contains("sub")) return "sub";
        return s.Length > 0 ? s : null;
    }

    static string? NativeName(JsonElement s)
    {
        if (!s.TryGetProperty("native_name", out var n)) return null;
        if (n.ValueKind == JsonValueKind.String) return n.GetString();
        if (n.ValueKind == JsonValueKind.Array)
            foreach (var e in n.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String) return e.GetString();
        return null;
    }

    // ---- 从 filelist 挑单文件：语言为主键、格式为次键 ----
    // 权重：命中请求语言 > 文件无任何语言标记（中性） > 明确是别的语言。
    // 之前把格式（.srt=100）压过语言（+20），导致 zh-cn 请求挑中 .eng.srt。现在语言乘 1000 做主轴。

    static (string Url, string? Name)? PickFile(JsonElement filelist, string lang)
    {
        (string Url, string? Name)? best = null;
        var bestScore = int.MinValue;
        var bestRank = -1;
        var want = SubtitleLang.Normalize(lang);
        foreach (var f in filelist.EnumerateArray())
        {
            var u = Str(f, "url");
            if (string.IsNullOrWhiteSpace(u)) continue;
            var name = Str(f, "f") ?? "";
            var lower = name.ToLowerInvariant();
            var ext = Path.GetExtension(lower);
            var fmt = ext == ".srt" ? 100 : ext is ".ass" or ".ssa" ? 50 : ext is ".sub" or ".idx" ? 10 : 1;
            var rank = LangRank(lower, want);
            var score = rank * 1000 + fmt;
            if (score > bestScore) { bestScore = score; bestRank = rank; best = (u!, string.IsNullOrWhiteSpace(name) ? null : name); }
        }
        return bestRank == 0 ? null : best; // 整个 filelist 都明确是别的语言 → 宁可不下载，也别把外语存成中文
    }

    // 文件名里的语言标记：按分隔符切段后做整段匹配（"eng"/"chs"/"cht"…），
    // 避免 "network" 里的 "tw" 之类子串误判。中日韩系文字（简/繁/中英）另按子串。
    static readonly Dictionary<string, string> FileLangToken = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cn"] = "zh-cn", ["chs"] = "zh-cn", ["chn"] = "zh-cn", ["sc"] = "zh-cn", ["gb"] = "zh-cn",
        ["hans"] = "zh-cn", ["sck"] = "zh-cn", ["简体"] = "zh-cn",
        ["tw"] = "zh-tw", ["cht"] = "zh-tw", ["twn"] = "zh-tw", ["tc"] = "zh-tw", ["hk"] = "zh-tw",
        ["hant"] = "zh-tw", ["big5"] = "zh-tw", ["繁体"] = "zh-tw",
        ["en"] = "en", ["eng"] = "en", ["english"] = "en",
        ["ja"] = "ja", ["jp"] = "ja", ["jap"] = "ja", ["jpn"] = "ja", ["japanese"] = "ja",
        ["ko"] = "ko", ["kor"] = "ko", ["korean"] = "ko",
        ["fr"] = "fr", ["fre"] = "fr", ["fra"] = "fr", ["french"] = "fr",
        ["es"] = "es", ["spa"] = "es", ["esp"] = "es", ["spanish"] = "es",
        ["de"] = "de", ["ger"] = "de", ["deu"] = "de", ["german"] = "de",
        ["ru"] = "ru", ["rus"] = "ru", ["russian"] = "ru",
    };

    static readonly char[] FileSep = { '.', '_', '-', ' ', '[', ']', '(', ')', '{', '}', '+', ',', '&', '·', '@' };

    static HashSet<string> DetectLangs(string lower)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seg in lower.Split(FileSep, StringSplitOptions.RemoveEmptyEntries))
            if (FileLangToken.TryGetValue(seg, out var code)) set.Add(code);
        if (lower.Contains('简') || lower.Contains("双语") || lower.Contains("中英")) set.Add("zh-cn");
        if (lower.Contains('繁')) set.Add("zh-tw");
        return set;
    }

    /// <summary>2=命中请求语言，1=无语言标记（中性），0=明确是别的语言。</summary>
    static int LangRank(string lower, string want)
    {
        var langs = DetectLangs(lower);
        if (langs.Count == 0) return 1;
        return langs.Contains(want) ? 2 : 0;
    }

    // ---- 归档 / 编码 ----

    static readonly (byte[] Magic, string Kind)[] Archives =
    {
        (new byte[] { 0x50, 0x4b, 0x03, 0x04 }, "zip"),
        (new byte[] { 0x52, 0x61, 0x72, 0x21 }, "rar"),   // "Rar!"
        (new byte[] { 0x37, 0x7a, 0xbc, 0xaf }, "7z"),
    };

    /// <summary>解出字幕正文。zip 解出首个 srt/ass；rar/7z 无法解 → 放弃（正常走 filelist 直链不会到这）。</summary>
    static (byte[]? Content, string? Name) Extract(byte[] bytes, string? name)
    {
        var kind = DetectArchive(bytes);

        if (kind == "zip")
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                ZipArchiveEntry? best = null;
                var bestScore = 0;
                foreach (var e in zip.Entries)
                {
                    if (e.Length == 0) continue;
                    var ext = Path.GetExtension(e.Name).ToLowerInvariant();
                    var score = ext == ".srt" ? 100 : ext is ".ass" or ".ssa" ? 50 : ext is ".sub" or ".idx" ? 10 : 0;
                    if (score > bestScore) { bestScore = score; best = e; }
                }
                if (best is null) return (null, null);
                using var es = best.Open();
                using var buf = new MemoryStream();
                es.CopyTo(buf);
                return (buf.ToArray(), best.Name);
            }
            catch { return (null, null); }
        }
        if (kind is "rar" or "7z") return (null, null);
        return (bytes, name); // 已是裸字幕
    }

    static string? DetectArchive(byte[] b)
    {
        foreach (var (magic, kind) in Archives)
        {
            if (b.Length < magic.Length) continue;
            var ok = true;
            for (var i = 0; i < magic.Length; i++)
                if (b[i] != magic[i]) { ok = false; break; }
            if (ok) return kind;
        }
        return null;
    }

    /// <summary>
    /// 国内站字幕常见 GBK/GB18030。已是 UTF-8（含 BOM）则原样；否则按 GB18030 解码转 UTF-8，
    /// 免得播放器把中文显示成乱码。转不了（缺代码页）就退回原字节。
    /// </summary>
    static byte[] EnsureUtf8(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return bytes[3..];
        try { _ = new UTF8Encoding(false, true).GetString(bytes); return bytes; }
        catch (DecoderFallbackException) { }
        try { return new UTF8Encoding(false).GetBytes(Encoding.GetEncoding("GB18030").GetString(bytes)); }
        catch { return bytes; }
    }

    // ---- 传输 / 取值 ----

    async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        try { return await _http.SendAsync(NewRequest(url), ct); }
        catch (Exception ex) when (_httpProxy is not null && !ct.IsCancellationRequested
                                   && ex is HttpRequestException or TaskCanceledException)
        {
            return await _httpProxy!.SendAsync(NewRequest(url), ct); // 直连失败 → 走代理
        }
    }

    HttpRequestMessage NewRequest(string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", _ua);
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
        return req;
    }

    static bool IsNoResult(JsonElement root)
    {
        var s = Num(root, "status");
        return s is "101" or "20900"; // 关键词过短 / 字幕不存在
    }

    static void ThrowIfError(JsonElement root, string what)
    {
        var s = Num(root, "status");
        if (s is null or "0") return;
        var msg = Str(root, "errmsg") ?? "";
        var hint = s switch
        {
            "20001" => "（Token 无效，请在射手网用户面板重新复制）",
            "30900" => "（配额超限，20 次/分钟，稍后重试）",
            "101" => "（关键词需 ≥3 字符）",
            _ => "",
        };
        throw new InvalidOperationException($"射手网{what}失败: status={s} {msg}{hint}".Trim());
    }

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

    public void Dispose() { _http.Dispose(); _httpProxy?.Dispose(); }
}
