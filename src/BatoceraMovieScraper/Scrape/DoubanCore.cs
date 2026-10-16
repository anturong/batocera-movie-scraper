using BatoceraMovieScraper.Model;

namespace BatoceraMovieScraper.Scrape;

/// <summary>内置豆瓣核心：搜索 → 详情 → 海报 →（可选字幕）→ 中立清单。无需 key。</summary>
public sealed class DoubanCore : IScrapeCore
{
    public string Id => "douban";
    public string Display => "豆瓣（内置）";
    public bool RequiresKeys => false;

    public async Task<Manifest> ScrapeAsync(AppConfig cfg, string sourceDir, string outDir, TextWriter log, CancellationToken ct = default)
    {
        using var src = new DoubanClient(cfg);
        using var subs = SubtitleSourceFactory.Create(cfg);
        return await new Scraper(src, log, subs, cfg.Subtitles).RunAsync(sourceDir, outDir, ct);
    }

    public async Task<MovieEntry?> ScrapeOneAsync(AppConfig cfg, string file, string outDir, TextWriter log, CancellationToken ct = default)
    {
        using var src = new DoubanClient(cfg);
        using var subs = SubtitleSourceFactory.Create(cfg);
        return await new Scraper(src, log, subs, cfg.Subtitles).RunOneAsync(file, outDir, ct);
    }
}
