namespace BatoceraMovieScraper.Scrape;

/// <summary>搜索候选：只够挑片，不含完整元数据。</summary>
public sealed class MovieHit
{
    public string Id { get; init; } = "";
    public string? Title { get; init; }
    public string? OriginalTitle { get; init; }
    public int? Year { get; init; }

    /// <summary>热度，仅作同级排序兜底；无此概念的源（豆瓣）填 0。</summary>
    public double Popularity { get; init; }
    public int VoteCount { get; init; }
}

/// <summary>命中详情：字段已归一化——类型拼成一个字符串、评分统一 0..10、日期统一 ISO。</summary>
public sealed class MovieInfo
{
    public string Id { get; init; } = "";
    public string? Title { get; init; }
    public string? OriginalTitle { get; init; }
    public int? Year { get; init; }

    /// <summary>ISO 日期 yyyy-MM-dd；缺失时渲染层退化为年份。</summary>
    public string? ReleaseDate { get; init; }

    public string? Plot { get; init; }
    public string? Genre { get; init; }

    /// <summary>0..10，与 TMDB vote_average 同量纲。</summary>
    public double? Rating { get; init; }

    /// <summary>海报引用，含义由各源自定（TMDB 是图片相对路径，豆瓣是完整 URL），只回传给 DownloadPosterAsync。</summary>
    public string? PosterRef { get; init; }

    /// <summary>别名/外文名集合（豆瓣 aka、备选标题）。字幕检索按序尝试，兜住「中文名搜不到」。</summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    /// <summary>IMDb id（如 tt1856101）。有则字幕按 id 精确检索，最可靠。</summary>
    public string? ImdbId { get; init; }
}

/// <summary>
/// 可替换的元数据源：搜索 → 详情 → 海报。Scraper 只依赖本接口，换源不动管线。
/// TMDB 与豆瓣各实现一份；将来 Jellyfin / Nova 也接到这里。
/// </summary>
public interface IMovieSource : IDisposable
{
    /// <summary>来源标识，写进清单的 movie.source（"tmdb" / "douban"）。</summary>
    string IdName { get; }

    Task<IReadOnlyList<MovieHit>> SearchAsync(string title, int? year, CancellationToken ct = default);

    Task<MovieInfo?> GetInfoAsync(string id, CancellationToken ct = default);

    Task DownloadPosterAsync(string id, string posterRef, string destFile, CancellationToken ct = default);
}
