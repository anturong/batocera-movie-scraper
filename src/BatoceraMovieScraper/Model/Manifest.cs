using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatoceraMovieScraper.Model;

/// <summary>
/// 中立清单：刮削层与所有展示层之间的唯一真源（见项目记忆 batocera_movie_scraper）。
/// 路径一律 POSIX（正斜杠）、相对影片根目录。
/// </summary>
public sealed class Manifest
{
    public int Version { get; set; } = 1;
    public string? SourceDir { get; set; }
    public string? ScrapedAt { get; set; }
    public List<MovieEntry> Movies { get; set; } = new();

    static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Manifest Load(string path) =>
        JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), Opts)
            ?? throw new InvalidDataException($"清单解析为空: {path}");

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Opts));
}

public sealed class MovieEntry
{
    public string Title { get; set; } = "";
    public string? SortTitle { get; set; }
    public int? Year { get; set; }
    public string? Plot { get; set; }
    public string? Genre { get; set; }

    /// <summary>TMDB vote_average，范围 0..10（渲染层再折算成 ES 的 0..1）。</summary>
    public double? Rating { get; set; }

    /// <summary>ISO 日期，如 2013-05-16；缺失时渲染层退化为年份。</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>来源核心标识，如 "tmdb" / "douban"。</summary>
    public string? Source { get; set; }

    /// <summary>该来源内的条目 id（TMDB id / 豆瓣 subject id）。</summary>
    public string? SourceId { get; set; }

    /// <summary>原始片名（多为外文名），字幕检索优先用它。</summary>
    public string? OriginalTitle { get; set; }

    /// <summary>视频文件，POSIX、相对影片根目录，如 "./Shrek.mp4"。</summary>
    public string VideoPath { get; set; } = "";

    /// <summary>海报，POSIX、相对影片根目录，如 "./media/Shrek-poster.jpg"。</summary>
    public string? PosterPath { get; set; }

    /// <summary>字幕，POSIX、相对影片根目录，如 "./Shrek.zh-cn.srt"。</summary>
    public string? SubtitlePath { get; set; }

    /// <summary>背景图（宽图）；gamelist 无对应槽位，留给将来的网页渲染器。</summary>
    public string? FanartPath { get; set; }
}
