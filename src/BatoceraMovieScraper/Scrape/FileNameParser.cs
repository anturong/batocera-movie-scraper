using System.Text.RegularExpressions;

namespace BatoceraMovieScraper.Scrape;

/// <summary>从影片文件名里拆出片名与年份。中文名原样保留。</summary>
public static partial class FileNameParser
{
    // 年份两侧只挡 ASCII 数字/字母，不挡汉字——中文片名常把年份直接粘在汉字后（"月食2010"）
    [GeneratedRegex(@"(?<![\dA-Za-z])(19\d{2}|20\d{2})(?![\dA-Za-z])")]
    private static partial Regex YearRx();

    // 质量/来源/编码等噪声标记：从第一个命中处截断（其后皆为发布信息）
    [GeneratedRegex(@"(?i)\b(2160p|1080p|720p|480p|4k|uhd|blu-?ray|bdrip|brrip|web-?dl|webrip|hdrip|dvdrip|dvd|remux|hdtv|x264|x265|h\.?264|h\.?265|hevc|avc|aac|ac3|eac3|dts(-hd)?|truehd|atmos|flac|10bit|8bit|hdr10?|dolby|sdr|proper|repack|internal|extended|uncut|remastered)\b")]
    private static partial Regex NoiseRx();

    public static (string Title, int? Year) Parse(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);

        var noise = NoiseRx().Match(name);
        if (noise.Success && noise.Index > 0)
            name = name[..noise.Index];

        name = name.Replace('.', ' ').Replace('_', ' ');

        int? year = null;
        var maxYear = DateTime.Now.Year + 1;
        var years = YearRx().Matches(name)
            .Where(m => int.TryParse(m.Value, out var y) && y >= 1900 && y <= maxYear)
            .ToList();
        if (years.Count > 0)
        {
            var last = years[^1];
            year = int.Parse(last.Value);
            name = name[..last.Index];
        }

        name = name.Replace('(', ' ').Replace(')', ' ')
                   .Replace('[', ' ').Replace(']', ' ')
                   .Replace('（', ' ').Replace('）', ' ');
        name = Regex.Replace(name, @"\s+", " ").Trim();

        return (name, year);
    }
}
