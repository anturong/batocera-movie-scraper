namespace BatoceraMovieScraper.Scrape;

/// <summary>字幕检索结果：一条可下载的字幕。</summary>
public sealed class SubtitleHit
{
    public string Id { get; init; } = "";

    /// <summary>配置侧语言码（"zh-cn" / "en"），由源把自身的语言码换算回来，便于按语言优先序挑选。</summary>
    public string Language { get; init; } = "";

    /// <summary>格式，如 "srt" / "ass"。</summary>
    public string? Format { get; init; }

    public string? FileName { get; init; }
    public string? MovieName { get; init; }
    public string? MovieYear { get; init; }
    public int Downloads { get; init; }

    /// <summary>
    /// 语言标注的可信度：2=源明确标注（如 assrt 的 langlist 列出 langchs）；1=按描述猜的（如 assrt 无
    /// langlist、只能从 desc 关键字推断，常与真实文件语言不符）。挑选时同格式优先信高的。
    /// </summary>
    public int LangConfidence { get; init; } = 1;

    public string? MatchedBy { get; init; }
}

/// <summary>下载到的字幕正文及其真实文件名（真实后缀由下载接口给出，比检索结果的启发式猜测可靠）；失败返回 null。</summary>
public sealed record SubtitleFile(byte[] Bytes, string? FileName);

/// <summary>
/// 可替换的字幕源。当前只实现 OpenSubtitles；将来可接别的站。
/// </summary>
public interface ISubtitleSource : IDisposable
{
    /// <summary>
    /// 检索字幕。优先用 <paramref name="imdbId"/> 精确查找；不可用或未命中时按 <paramref name="titles"/>
    /// 顺序逐个别名尝试，返回首个非空结果。语言码同配置（"zh-cn,en"），由实现换算成站点自己的码。
    /// </summary>
    Task<IReadOnlyList<SubtitleHit>> SearchAsync(
        string? imdbId, IReadOnlyList<string> titles, int? year, string languages, CancellationToken ct = default);

    /// <summary>下载并解出字幕正文（已解压）；失败返回 null。</summary>
    Task<SubtitleFile?> DownloadAsync(SubtitleHit hit, CancellationToken ct = default);
}
