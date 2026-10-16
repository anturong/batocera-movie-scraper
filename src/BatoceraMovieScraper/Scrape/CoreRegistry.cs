namespace BatoceraMovieScraper.Scrape;

/// <summary>刮削核心注册表。新增核心＝加一条；设置界面的下拉与 CLI 的 --core 都由此驱动。</summary>
public static class CoreRegistry
{
    // 国内优先：豆瓣放最前，下拉默认选它（免 key、国内直连）。
    public static readonly IReadOnlyList<IScrapeCore> All = new IScrapeCore[]
    {
        new DoubanCore(),
        new TmdbCore(),
        new NovaCore(),
    };

    public static IScrapeCore? Find(string? id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>逗号分隔的已知 id，用于错误提示。</summary>
    public static string Ids => string.Join(", ", All.Select(c => c.Id));
}
