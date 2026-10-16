using BatoceraMovieScraper;
using BatoceraMovieScraper.Model;
using BatoceraMovieScraper.Render;
using BatoceraMovieScraper.Scrape;

try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* 重定向等场景下可能失败，忽略 */ }

return Cli.Run(args);

internal static class Cli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var rest = args.Skip(1).ToArray();
        return args[0].ToLowerInvariant() switch
        {
            "render" => Render(rest),
            "scrape" => Scrape(rest),
            _ => Unknown(args[0]),
        };
    }

    static int Render(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("用法: batoscrape render <manifest.json> [--out <dir>]");
            return 2;
        }

        var manifestPath = args[0];
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"找不到清单: {manifestPath}");
            return 3;
        }

        var outDir = Opt(args, "--out") ?? Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (string.IsNullOrEmpty(outDir)) outDir = ".";
        Directory.CreateDirectory(outDir);

        var manifest = Manifest.Load(manifestPath);
        var outFile = Path.Combine(outDir, "gamelist.xml");
        GamelistRenderer.Write(manifest, outFile);

        Console.WriteLine($"已生成 {outFile}（{manifest.Movies.Count} 条）");
        return 0;
    }

    static int Scrape(string[] args)
    {
        AppConfig cfg;
        try
        {
            cfg = AppConfig.Load(Opt(args, "--config"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"配置加载失败: {ex.Message}");
            return 4;
        }

        var sourceDir = args.Length >= 1 && !args[0].StartsWith("--") ? args[0] : cfg.Library.SourceDir;
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"影片目录不存在: {sourceDir}");
            return 5;
        }

        var outDir = Opt(args, "--out") ?? sourceDir;
        Directory.CreateDirectory(outDir);

        var coreId = Opt(args, "--core") ?? cfg.Scraper.Core;
        var core = CoreRegistry.Find(coreId);
        if (core is null)
        {
            Console.Error.WriteLine($"未知刮削核心: {coreId}（可选: {CoreRegistry.Ids}）");
            return 8;
        }
        Console.Error.WriteLine($"刮削核心: {core.Display}");

        var only = Opt(args, "--only");
        if (only is not null)
        {
            try { return ScrapeOnly(cfg, core, sourceDir, outDir, only); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"刮削失败: {ex.Message}");
                return 6;
            }
        }

        try
        {
            var manifest = core.ScrapeAsync(cfg, sourceDir, outDir, Console.Error).GetAwaiter().GetResult();

            var manifestFile = Path.Combine(outDir, "manifest.json");
            manifest.Save(manifestFile);
            Console.WriteLine($"清单已保存 {manifestFile}（{manifest.Movies.Count} 部）");

            var gamelistFile = Path.Combine(outDir, "gamelist.xml");
            GamelistRenderer.Write(manifest, gamelistFile);
            Console.WriteLine($"gamelist 已渲染 {gamelistFile}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"刮削失败: {ex.Message}");
            return 6;
        }
    }

    /// <summary>单独刮削一个文件，并把结果并回既有清单（其余条目原样保留），再重渲 gamelist。</summary>
    static int ScrapeOnly(AppConfig cfg, IScrapeCore core, string sourceDir, string outDir, string only)
    {
        var file = Path.IsPathRooted(only) ? only : Path.Combine(sourceDir, only);
        if (!File.Exists(file)) file = Path.Combine(sourceDir, Path.GetFileName(only));
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"找不到影片: {only}");
            return 10;
        }

        var manifestFile = Path.Combine(outDir, "manifest.json");
        Manifest manifest;
        if (File.Exists(manifestFile))
        {
            try { manifest = Manifest.Load(manifestFile); }
            catch { manifest = new Manifest(); }
        }
        else manifest = new Manifest();
        manifest.SourceDir ??= sourceDir;

        Console.Error.WriteLine($"单独刮削: {Path.GetFileName(file)}");
        var entry = core.ScrapeOneAsync(cfg, file, outDir, Console.Error).GetAwaiter().GetResult();
        if (entry is null)
        {
            Console.Error.WriteLine("未命中，清单未改动。");
            return 9;
        }

        manifest.Movies.RemoveAll(m => string.Equals(m.VideoPath, entry.VideoPath, StringComparison.Ordinal));
        manifest.Movies.Add(entry);
        manifest.Movies.Sort((a, b) => string.Compare(a.VideoPath, b.VideoPath, StringComparison.OrdinalIgnoreCase));
        manifest.ScrapedAt = DateTime.UtcNow.ToString("o");
        manifest.Save(manifestFile);
        Console.WriteLine($"清单已更新 {manifestFile}（{manifest.Movies.Count} 部）");

        var gamelistFile = Path.Combine(outDir, "gamelist.xml");
        GamelistRenderer.Write(manifest, gamelistFile);
        Console.WriteLine($"gamelist 已渲染 {gamelistFile}");
        return 0;
    }

    static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"未知子命令: {cmd}");
        PrintUsage();
        return 7;
    }

    static string? Opt(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void PrintUsage()
    {
        Console.WriteLine("batoscrape — Batocera 影视刮削 / 渲染");
        Console.WriteLine("  batoscrape scrape <影片目录> [--out <dir>] [--config <path>] [--core <id>]   扫描 → 清单 + gamelist");
        Console.WriteLine("  batoscrape scrape <影片目录> --only <影片> [--out <dir>] [--config <path>]    只刮一部，并回既有清单");
        Console.WriteLine("  batoscrape render <manifest.json> [--out <dir>]                              清单 → gamelist.xml");
        Console.WriteLine($"  刮削核心 --core: {CoreRegistry.Ids}（缺省取 config 的 scraper.core）");
    }
}
