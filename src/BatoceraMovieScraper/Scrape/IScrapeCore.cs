using BatoceraMovieScraper.Model;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 可替换的「刮削核心」：一条从影片目录产出中立清单的流水线。
/// 现在只实现 TMDB；Nova / Jellyfin 作为后续核心接进同一注册表，上层（渲染器、部署层）不变。
/// </summary>
public interface IScrapeCore
{
    /// <summary>稳定标识，写进 config 的 scraper.core，如 "tmdb"。</summary>
    string Id { get; }

    /// <summary>给设置界面下拉与日志看的中文名。</summary>
    string Display { get; }

    /// <summary>是否必须填 TMDB key/token；豆瓣无需。界面据此决定要不要拦。</summary>
    bool RequiresKeys { get; }

    Task<Manifest> ScrapeAsync(AppConfig cfg, string sourceDir, string outDir, TextWriter log, CancellationToken ct = default);

    /// <summary>只刮削单个视频文件（ES「单独刮削」入口用）；无法匹配时返回 null。</summary>
    Task<MovieEntry?> ScrapeOneAsync(AppConfig cfg, string file, string outDir, TextWriter log, CancellationToken ct = default);
}
