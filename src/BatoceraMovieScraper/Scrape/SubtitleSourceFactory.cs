namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 按配置里的 subtitles.source 造字幕源。默认 assrt（国内、免翻墙、中文片源多）。
/// 未启用字幕下载时返回 null，调用方据此跳过整块字幕流程。
/// </summary>
public static class SubtitleSourceFactory
{
    public const string Assrt = "assrt";
    public const string OpenSubtitles = "opensubtitles";

    public static ISubtitleSource? Create(AppConfig cfg)
    {
        if (!cfg.Subtitles.Enabled) return null;
        return Id(cfg) == OpenSubtitles ? new OpenSubtitlesClient(cfg) : new AssrtClient(cfg);
    }

    /// <summary>归一 source id；空值默认国内 assrt。</summary>
    public static string Id(AppConfig cfg)
    {
        var s = (cfg.Subtitles.Source ?? "").Trim().ToLowerInvariant();
        return s.Length == 0 ? Assrt : s;
    }
}
