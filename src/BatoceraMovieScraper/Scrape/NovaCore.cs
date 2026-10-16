using BatoceraMovieScraper.Model;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// Nova Video Player 刮削核心（占位）。Nova 是 Android/Kotlin，匹配逻辑需重写成 .NET，
/// 规划为 TMDB 的第二套匹配源（中文片名易错时对拍）。当前尚未实现。
/// </summary>
public sealed class NovaCore : IScrapeCore
{
    public string Id => "nova";
    public string Display => "Nova（开发中）";
    public bool RequiresKeys => true;

    public Task<Manifest> ScrapeAsync(AppConfig cfg, string sourceDir, string outDir, TextWriter log, CancellationToken ct = default) =>
        throw new NotSupportedException("Nova 刮削核心尚未实现，请在「刮削设置」里改用 TMDB（内置）。");

    public Task<MovieEntry?> ScrapeOneAsync(AppConfig cfg, string file, string outDir, TextWriter log, CancellationToken ct = default) =>
        throw new NotSupportedException("Nova 刮削核心尚未实现，请在「刮削设置」里改用 TMDB（内置）。");
}
