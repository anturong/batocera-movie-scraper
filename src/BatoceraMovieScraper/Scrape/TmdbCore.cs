using BatoceraMovieScraper.Model;

namespace BatoceraMovieScraper.Scrape;

/// <summary>内置精简 TMDB 核心：检索 → 相关性挑选 → 下载海报 → 中立清单。</summary>
public sealed class TmdbCore : IScrapeCore
{
    public string Id => "tmdb";
    public string Display => "TMDB（内置）";
    public bool RequiresKeys => true;

    public async Task<Manifest> ScrapeAsync(AppConfig cfg, string sourceDir, string outDir, TextWriter log, CancellationToken ct = default)
    {
        using var src = new TmdbClient(cfg);
        using var subs = SubtitleSourceFactory.Create(cfg);
        return await new Scraper(src, log, subs, cfg.Subtitles).RunAsync(sourceDir, outDir, ct);
    }

    public async Task<MovieEntry?> ScrapeOneAsync(AppConfig cfg, string file, string outDir, TextWriter log, CancellationToken ct = default)
    {
        using var src = new TmdbClient(cfg);
        using var subs = SubtitleSourceFactory.Create(cfg);
        return await new Scraper(src, log, subs, cfg.Subtitles).RunOneAsync(file, outDir, ct);
    }
}
