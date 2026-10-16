namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 字幕语言码归一。OpenSubtitles.com 用 "zh-cn"/"zh-tw"/"en" 这套；用户也可能写旧码
/// （chi/zht/eng），统一换算到同一套，保证「请求过滤」与「命中挑选」用同一个码。
/// </summary>
public static class SubtitleLang
{
    /// <summary>把单个语言码归一（chi→zh-cn、zht→zh-tw、eng→en…）未知码原样小写返回。</summary>
    public static string Normalize(string code) => (code ?? "").Trim().ToLowerInvariant() switch
    {
        "chi" or "zho" or "zh" or "zh-hans" or "chs" or "cn" => "zh-cn",
        "zht" or "zh-hant" or "cht" or "zh-hk" or "tw" => "zh-tw",
        "eng" => "en",
        var c => c,
    };

    /// <summary>逗号分隔的语言列表 → 归一、去重，保持配置里的先后（＝优先序，勿排序）。</summary>
    public static List<string> Split(string languages) =>
        (languages ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
