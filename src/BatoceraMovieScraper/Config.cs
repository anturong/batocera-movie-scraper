using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatoceraMovieScraper;

/// <summary>
/// 外部配置（含密钥），不进代码、不入库。查找顺序：
/// --config 参数 → 环境变量 BATOSCRAPE_CONFIG → 当前目录/config.json
/// → exe 同目录/config.json → repo 根（src/&lt;proj&gt;/ 上溯）
/// </summary>
public sealed class AppConfig
{
    public ScraperConfig Scraper { get; set; } = new();
    public TmdbConfig Tmdb { get; set; } = new();
    public ProxyConfig Proxy { get; set; } = new();
    public LibraryConfig Library { get; set; } = new();
    public SubtitleConfig Subtitles { get; set; } = new();

    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string? path = null)
    {
        path ??= Environment.GetEnvironmentVariable("BATOSCRAPE_CONFIG");
        path ??= FindDefault();
        if (path is null || !File.Exists(path))
            throw new FileNotFoundException(
                "找不到 config.json（用 --config <路径> 或环境变量 BATOSCRAPE_CONFIG 指定）");

        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Opts)
               ?? throw new InvalidDataException($"配置解析为空: {path}");
    }

    static string? FindDefault()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
            Path.Combine(AppContext.BaseDirectory, "config.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "config.json")),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}

/// <summary>刮削核心选择。id 与 CoreRegistry 对应，未知 id 由 CLI 报错。</summary>
public sealed class ScraperConfig
{
    // 国内优先：默认豆瓣核心（免 key、国内直连）。
    public string Core { get; set; } = "douban";
}

public sealed class TmdbConfig
{
    public string? ApiKey { get; set; }
    public string? ReadAccessToken { get; set; }
    public string Language { get; set; } = "zh-CN";
}

public sealed class ProxyConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = "";
}

public sealed class LibraryConfig
{
    public string SourceDir { get; set; } = "";
}

/// <summary>字幕下载。默认走国内「射手网(伪) assrt.net」（免翻墙、中文片源多），也可切回 OpenSubtitles.com。</summary>
public sealed class SubtitleConfig
{
    public bool Enabled { get; set; }

    /// <summary>字幕源："assrt"（默认，国内）/ "opensubtitles"。见 <see cref="Scrape.SubtitleSourceFactory"/>。</summary>
    public string Source { get; set; } = "assrt";

    /// <summary>语言优先序，逗号分隔；字幕文件名用命中的实际语言，如 "zh-cn,en"。也可写旧码 chi/zht 自动归一。</summary>
    public string Languages { get; set; } = "zh-cn,en";

    /// <summary>射手网(伪) assrt.net 的 API Token（32 位；登录后到「用户面板」查看）。国内源用它。</summary>
    public string? AssrtToken { get; set; }

    /// <summary>OpenSubtitles.com 免费 API-Key（切到 opensubtitles 源时必需；在账号设置里生成）。</summary>
    public string? ApiKey { get; set; }

    /// <summary>可选：OpenSubtitles 账号邮箱。实测只带 Api-Key 就能下载；填账号是为挂配额。留空即可。</summary>
    public string? Username { get; set; }

    /// <summary>可选：账号密码（配合 Username 登录挂配额）。</summary>
    public string? Password { get; set; }

    /// <summary>REST 接口要求的自定义 User-Agent（形如 "应用名 v版本"；禁用浏览器串）。</summary>
    public string UserAgent { get; set; } = "batoscrape v1.0";

    /// <summary>单次运行最多下载几条（0=不限，仍受账号每日配额限制）。</summary>
    public int MaxDownloads { get; set; } = 0;
}
