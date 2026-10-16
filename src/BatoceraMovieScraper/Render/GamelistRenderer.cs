using System.Globalization;
using System.Text;
using System.Xml;
using BatoceraMovieScraper.Model;

namespace BatoceraMovieScraper.Render;

/// <summary>
/// 中立清单 → EmulationStation gamelist.xml。ES 无电影字段，映射见 WriteGame 注释。
/// 用 XmlWriter 输出，转义交给框架（片名/简介里的 &amp; &lt; 不会破坏 XML）。
/// </summary>
public static class GamelistRenderer
{
    public static void Write(Manifest manifest, string outFile)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), // 无 BOM
            NewLineChars = "\n",
        };
        using var w = XmlWriter.Create(outFile, settings);
        w.WriteStartDocument();
        w.WriteStartElement("gameList");
        foreach (var m in manifest.Movies)
            WriteGame(w, m);
        w.WriteEndElement();
        w.WriteEndDocument();
    }

    static void WriteGame(XmlWriter w, MovieEntry m)
    {
        w.WriteStartElement("game");

        // 路径：ES 认正斜杠；统一转 POSIX，避免 Windows 生成的反斜杠带到盘上失效。
        w.WriteElementString("path", Posix(m.VideoPath));
        w.WriteElementString("name", m.Title);
        if (!string.IsNullOrWhiteSpace(m.SortTitle))
            w.WriteElementString("sortname", m.SortTitle);
        if (!string.IsNullOrWhiteSpace(m.Plot))
            w.WriteElementString("desc", m.Plot);
        if (!string.IsNullOrWhiteSpace(m.PosterPath))
            w.WriteElementString("image", Posix(m.PosterPath)); // 网格卡片的封面
        if (!string.IsNullOrWhiteSpace(m.Genre))
            w.WriteElementString("genre", m.Genre);
        if (m.Rating is double r)
            w.WriteElementString("rating", EsRating(r));
        if (EsRelease(m) is string rel)
            w.WriteElementString("releasedate", rel);

        w.WriteEndElement();
    }

    /// <summary>TMDB 0..10 → ES 0..1。</summary>
    static string EsRating(double tmdb) =>
        Math.Round(Math.Clamp(tmdb / 10.0, 0.0, 1.0), 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>ES 的 releasedate 格式为 yyyyMMddTHHmmss；有完整日期用它，否则退化为年份。</summary>
    static string? EsRelease(MovieEntry m)
    {
        if (!string.IsNullOrWhiteSpace(m.ReleaseDate)
            && DateTime.TryParse(m.ReleaseDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
        return m.Year is int y ? $"{y:D4}0101T000000" : null;
    }

    static string Posix(string p) => p.Replace('\\', '/');
}
