using System.Text;
using BatoceraMovieScraper.Model;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 扫描影片目录 → 匹配 → 下载海报 →（可选）下载字幕 → 产出中立清单。
/// 元数据源（IMovieSource）与字幕源（ISubtitleSource）都可替换，管线不变。
/// </summary>
public sealed class Scraper
{
    static readonly string[] VideoExt =
    {
        ".mp4", ".mkv", ".avi", ".mov", ".m4v", ".wmv", ".flv", ".webm",
        ".ts", ".mpg", ".mpeg", ".rmvb", ".rm", ".vob", ".m2ts", ".iso",
    };

    readonly IMovieSource _src;
    readonly TextWriter _log;
    readonly ISubtitleSource? _subs;
    readonly SubtitleConfig _subCfg;
    int _subDownloaded;

    public Scraper(IMovieSource src, TextWriter log, ISubtitleSource? subs = null, SubtitleConfig? subCfg = null)
    {
        _src = src;
        _log = log;
        _subs = subs;
        _subCfg = subCfg ?? new SubtitleConfig();
    }

    /// <summary>整目录批量刮削，产出一份全新清单。</summary>
    public async Task<Manifest> RunAsync(string sourceDir, string outDir, CancellationToken ct = default)
    {
        var manifest = new Manifest
        {
            SourceDir = sourceDir,
            ScrapedAt = DateTime.UtcNow.ToString("o"),
        };

        var mediaDir = Path.Combine(outDir, "media");
        var files = Directory.EnumerateFiles(sourceDir)
            .Where(f => VideoExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var entry = await ScrapeFileAsync(file, sourceDir, mediaDir, ct);
            if (entry is not null) manifest.Movies.Add(entry);
        }

        return manifest;
    }

    /// <summary>只刮削一个文件（ES「单独刮削」用）；解析或匹配失败返回 null。</summary>
    public async Task<MovieEntry?> RunOneAsync(string file, string outDir, CancellationToken ct = default)
    {
        var sourceDir = Path.GetDirectoryName(Path.GetFullPath(file)) ?? outDir;
        return await ScrapeFileAsync(file, sourceDir, Path.Combine(outDir, "media"), ct);
    }

    async Task<MovieEntry?> ScrapeFileAsync(string file, string sourceDir, string mediaDir, CancellationToken ct)
    {
        var (title, year) = FileNameParser.Parse(file);
        if (string.IsNullOrWhiteSpace(title))
        {
            _log.WriteLine($"跳过（无法解析）: {Path.GetFileName(file)}");
            return null;
        }

        var hits = await _src.SearchAsync(title, year, ct);
        var best = PickBest(hits, title, year);
        if (best is null)
        {
            _log.WriteLine($"未命中: {title} ({year})  <- {Path.GetFileName(file)}");
            return null;
        }

        var info = await _src.GetInfoAsync(best.Id, ct) ?? new MovieInfo
        {
            Id = best.Id,
            Title = best.Title,
            OriginalTitle = best.OriginalTitle,
            Year = best.Year,
        };

        var entry = new MovieEntry
        {
            Title = string.IsNullOrWhiteSpace(info.Title) ? title : info.Title!,
            OriginalTitle = info.OriginalTitle,
            Year = info.Year ?? year,
            ReleaseDate = info.ReleaseDate,
            Plot = info.Plot,
            Genre = info.Genre,
            Rating = info.Rating,
            Source = _src.IdName,
            SourceId = info.Id,
            VideoPath = "./" + Path.GetFileName(file),
        };

        if (!string.IsNullOrWhiteSpace(info.PosterRef))
        {
            var posterName = $"poster-{info.Id}.jpg";
            try
            {
                await _src.DownloadPosterAsync(info.Id, info.PosterRef!, Path.Combine(mediaDir, posterName), ct);
                entry.PosterPath = "./media/" + posterName;
            }
            catch (Exception ex)
            {
                _log.WriteLine($"海报下载失败 {entry.Title}: {ex.Message}");
            }
        }

        if (_subs is not null && _subCfg.Enabled)
        {
            try { await FetchSubtitleAsync(entry, info, title, sourceDir, ct); }
            catch (Exception ex) { _log.WriteLine($"字幕下载失败 {entry.Title}: {ex.Message}"); }
        }

        _log.WriteLine($"OK  {entry.Title} ({entry.Year})  {_src.IdName}={entry.SourceId}  <- {Path.GetFileName(file)}");
        return entry;
    }

    /// <summary>按语言优先序取字幕：命中里挑该语言、srt 优先、下载量最大的那条，落在影片同目录。</summary>
    async Task FetchSubtitleAsync(MovieEntry entry, MovieInfo info, string fileTitle, string sourceDir, CancellationToken ct)
    {
        if (_subCfg.MaxDownloads > 0 && _subDownloaded >= _subCfg.MaxDownloads)
        {
            _log.WriteLine($"  字幕跳过：已达本次上限 {_subCfg.MaxDownloads} 条");
            return;
        }

        var titles = SubtitleTitles(info, entry, fileTitle);
        var hits = await _subs!.SearchAsync(info.ImdbId, titles, entry.Year, _subCfg.Languages, ct);
        var candidates = PickSubtitle(hits, _subCfg.Languages);
        if (candidates.Count == 0)
        {
            _log.WriteLine($"  字幕未命中: {string.Join(" / ", titles)} ({entry.Year})");
            return;
        }

        // 逐条候选下载，直到取到「内容语言与请求相符」的一条。assrt 上有些条目 langlist 为空、
        // 靠 desc 猜成中文，实际文件却是英语轨；逐条试 + 正文校验能自动滑到真正的中文字幕。
        SubtitleHit? sub = null;
        SubtitleFile? file = null;
        foreach (var c in candidates)
        {
            SubtitleFile? f;
            try { f = await _subs!.DownloadAsync(c, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.WriteLine($"  字幕下载失败，换下一条：{ex.Message}");
                continue;
            }
            if (f is null || f.Bytes.Length == 0) continue;
            if (!LooksLikeLanguage(f.Bytes, c.Language))
            {
                _log.WriteLine($"  字幕语言不符，换下一条：{c.Language} <- {f.FileName ?? c.FileName}");
                continue;
            }
            sub = c; file = f; break;
        }
        if (sub is null || file is null)
        {
            _log.WriteLine($"  字幕未取到可用文件: {string.Join(" / ", titles)} ({entry.Year})");
            return;
        }
        _subDownloaded++;

        var baseName = Path.GetFileNameWithoutExtension(entry.VideoPath);
        var ext = SubtitleExt(file.FileName);
        var fileName = $"{baseName}.{sub.Language}.{ext}";
        await File.WriteAllBytesAsync(Path.Combine(sourceDir, fileName), file.Bytes, ct);
        entry.SubtitlePath = "./" + fileName;
        _log.WriteLine($"  字幕 OK  {fileName}  <- {file.FileName ?? sub.FileName}");
    }

    /// <summary>
    /// 落盘扩展名以「下载接口返回的真实文件名」为准：检索接口的 files[].file_name 不带后缀，
    /// 拿它取后缀只会得到 "ag"/"ac3"/"x264-amiable" 这类噪声。异常则退回 srt。
    /// </summary>
    static string SubtitleExt(string? downloadFileName)
    {
        var ext = Path.GetExtension(downloadFileName ?? "").TrimStart('.').ToLowerInvariant();
        return ext.Length is > 0 and <= 5 && ext.All(char.IsLetterOrDigit) ? ext : "srt";
    }

    /// <summary>字幕检索用的片名候选（按优先级、去重）：原片名 → 别名/aka → 译名 → 文件名解析名。</summary>
    static List<string> SubtitleTitles(MovieInfo info, MovieEntry entry, string fileTitle)
    {
        var list = new List<string>();
        void Add(string? s)
        {
            var t = s?.Trim();
            if (!string.IsNullOrEmpty(t) && !list.Contains(t, StringComparer.OrdinalIgnoreCase))
                list.Add(t);
        }
        Add(info.OriginalTitle);
        foreach (var a in info.Aliases) Add(a);
        Add(info.Title);
        Add(entry.Title);
        Add(fileTitle);
        return list;
    }

    /// <summary>
    /// 按语言优先序排出候选字幕（不急着定一条）：每种语言内 srt 优先、再看下载量，
    /// 语言之间按请求顺序。交给上层逐条试下载，避免一条坏候选就把整部片子的字幕判死。
    /// </summary>
    static List<SubtitleHit> PickSubtitle(IReadOnlyList<SubtitleHit> hits, string languages)
    {
        var ordered = new List<SubtitleHit>();
        if (hits.Count == 0) return ordered;
        foreach (var lang in SubtitleLang.Split(languages))
            ordered.AddRange(hits
                .Where(h => string.Equals(SubtitleLang.Normalize(h.Language), lang, StringComparison.OrdinalIgnoreCase))
                .OrderBy(h => string.Equals(h.Format, "srt", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(h => h.LangConfidence)   // 语言标注明确的排前，desc 猜的排后
                .ThenByDescending(h => h.Downloads));
        return ordered;
    }

    /// <summary>
    /// 正文语言与目标语言是否相符：中文请求要求正文含 CJK 汉字，否则视为错语
    /// （assrt 有 desc 猜成中文、文件却是英语轨的条目，靠这道校验拦下，免得把英语当中文存）。
    /// 非中文请求不校验。
    /// </summary>
    static bool LooksLikeLanguage(byte[] bytes, string language)
    {
        var l = SubtitleLang.Normalize(language);
        if (!l.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var ch in Encoding.UTF8.GetString(bytes))
            if (ch is >= '㐀' and <= '鿿') return true;
        return false;
    }

    /// <summary>
    /// 相关性挑选三级：① 片名精确对得上（title / original）② 互相包含 ③ 兜底。
    /// 同级内优先年份吻合（豆瓣同名系列靠年份分辨），再比热度/票数。
    /// </summary>
    static MovieHit? PickBest(IReadOnlyList<MovieHit> hits, string query, int? year)
    {
        if (hits.Count == 0) return null;

        // 年份是比片名更硬的信号：文件名带年份且确有同年的命中，就先按年份收窄，
        // 否则「Spirited Away」(2001) 会被同名的 2017 纪录片按片名包含关系抢走。
        if (year is int y)
        {
            var sameYear = hits.Where(h => h.Year == y).ToList();
            if (sameYear.Count > 0) hits = sameYear;
        }

        var q = Normalize(query);
        if (q.Length == 0) return Best(hits, year);

        var exact = hits.Where(h => NormEq(h.Title, q) || NormEq(h.OriginalTitle, q)).ToList();
        if (exact.Count > 0) return Best(exact, year);

        var contains = hits.Where(h => NormContains(h.Title, q) || NormContains(h.OriginalTitle, q)).ToList();
        if (contains.Count > 0) return Best(contains, year);

        return Best(hits, year);
    }

    static MovieHit Best(IReadOnlyList<MovieHit> hits, int? year)
    {
        if (year is int y && hits.Any(h => h.Year == y))
            hits = hits.Where(h => h.Year == y).ToList();
        return hits.OrderByDescending(h => h.Popularity).ThenByDescending(h => h.VoteCount).First();
    }

    static bool NormEq(string? a, string q) => Normalize(a) is { Length: > 0 } n && n == q;

    static bool NormContains(string? a, string q)
    {
        var n = Normalize(a);
        return n.Length > 0 && (n.Contains(q) || q.Contains(n));
    }

    static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
