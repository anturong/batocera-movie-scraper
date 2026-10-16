using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BatoceraMovieScraper.Scrape;

/// <summary>
/// 精简 TMDB 元数据源。鉴权优先 v4 Read Access Token（Bearer 头），否则退回 v3 api_key 查询参数。
/// 走不走代理由配置决定；SOCKS5 会把主机名交给代理解析，天然绕过本地 DNS 投毒。
/// </summary>
public sealed class TmdbClient : IMovieSource
{
    const string Base = "https://api.themoviedb.org/3";
    public const string ImageBase = "https://image.tmdb.org/t/p";

    readonly HttpClient _http;
    readonly string? _apiKey;
    readonly string _language;
    readonly bool _hasBearer;
    Dictionary<int, string>? _genreCache;

    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public TmdbClient(AppConfig cfg)
    {
        _language = string.IsNullOrWhiteSpace(cfg.Tmdb.Language) ? "zh-CN" : cfg.Tmdb.Language;
        _apiKey = cfg.Tmdb.ApiKey;

        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        if (cfg.Proxy.Enabled && !string.IsNullOrWhiteSpace(cfg.Proxy.Url))
        {
            handler.Proxy = new WebProxy(cfg.Proxy.Url);
            handler.UseProxy = true;
        }

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(cfg.Tmdb.ReadAccessToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg.Tmdb.ReadAccessToken);
            _hasBearer = true;
        }
    }

    public string IdName => "tmdb";

    // ---- IMovieSource ----

    public async Task<IReadOnlyList<MovieHit>> SearchAsync(string title, int? year, CancellationToken ct = default)
    {
        var list = await SearchMovieAsync(title, year, ct);
        return list.Select(m => new MovieHit
        {
            Id = m.Id.ToString(),
            Title = m.Title,
            OriginalTitle = m.OriginalTitle,
            Year = ParseYear(m.ReleaseDate),
            Popularity = m.Popularity,
            VoteCount = m.VoteCount,
        }).ToList();
    }

    public async Task<MovieInfo?> GetInfoAsync(string id, CancellationToken ct = default)
    {
        if (!long.TryParse(id, out var mid)) return null;
        var d = await GetMovieAsync(mid, ct);
        if (d is null) return null;

        var genres = d.Genres.Select(g => g.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList();
        if (genres.Count == 0)
        {
            var map = await GenreMapAsync(ct);
            genres = d.GenreIds.Where(map.ContainsKey).Select(i => map[i]).ToList();
        }

        return new MovieInfo
        {
            Id = d.Id.ToString(),
            Title = d.Title,
            OriginalTitle = d.OriginalTitle,
            Year = ParseYear(d.ReleaseDate),
            ReleaseDate = d.ReleaseDate,
            Plot = d.Overview,
            Genre = genres.Count > 0 ? string.Join(", ", genres) : null,
            Rating = d.VoteCount > 0 ? d.VoteAverage : null,
            PosterRef = d.PosterPath,
            ImdbId = d.ImdbId,
        };
    }

    public async Task DownloadPosterAsync(string id, string posterRef, string destFile, CancellationToken ct = default)
        => await DownloadImageAsync("w500", posterRef, destFile, ct);

    // ---- 底层 ----

    string Url(string path, params (string Key, string? Val)[] query)
    {
        var parts = new List<string>();
        if (!_hasBearer && !string.IsNullOrWhiteSpace(_apiKey))
            parts.Add("api_key=" + Uri.EscapeDataString(_apiKey));
        parts.Add("language=" + Uri.EscapeDataString(_language));
        foreach (var (k, v) in query)
            if (v is not null) parts.Add($"{k}={Uri.EscapeDataString(v)}");
        return $"{Base}{path}?{string.Join('&', parts)}";
    }

    public async Task<List<TmdbMovie>> SearchMovieAsync(string title, int? year, CancellationToken ct = default)
    {
        var url = Url("/search/movie", ("query", title), ("year", year?.ToString()), ("include_adult", "false"));
        var resp = await _http.GetFromJsonAsync<TmdbSearchResponse>(url, Opts, ct);
        return resp?.Results ?? new();
    }

    public Task<TmdbMovie?> GetMovieAsync(long id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<TmdbMovie>(Url($"/movie/{id}"), Opts, ct);

    Task<Dictionary<int, string>> GenreMapAsync(CancellationToken ct) =>
        _genreCache is not null ? Task.FromResult(_genreCache) : LoadGenreMapAsync(ct);

    async Task<Dictionary<int, string>> LoadGenreMapAsync(CancellationToken ct)
    {
        var gl = await _http.GetFromJsonAsync<TmdbGenreList>(Url("/genre/movie/list"), Opts, ct);
        return _genreCache = gl?.Genres.ToDictionary(g => g.Id, g => g.Name ?? "") ?? new();
    }

    public async Task DownloadImageAsync(string size, string imagePath, string destFile, CancellationToken ct = default)
    {
        var bytes = await _http.GetByteArrayAsync($"{ImageBase}/{size}{imagePath}", ct);
        var dir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(destFile, bytes, ct);
    }

    static int? ParseYear(string? date) =>
        !string.IsNullOrWhiteSpace(date) && date.Length >= 4 && int.TryParse(date[..4], out var y) ? y : null;

    public void Dispose() => _http.Dispose();
}
