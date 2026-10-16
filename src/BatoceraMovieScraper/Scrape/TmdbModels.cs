namespace BatoceraMovieScraper.Scrape;

public sealed class TmdbSearchResponse
{
    public int Page { get; set; }
    public List<TmdbMovie> Results { get; set; } = new();
}

public sealed class TmdbMovie
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? ReleaseDate { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public double Popularity { get; set; }

    /// <summary>仅电影详情接口返回；搜索接口为 null。</summary>
    public string? ImdbId { get; set; }

    /// <summary>search 结果里是 genre_ids，详情里才是 genres。</summary>
    public List<int> GenreIds { get; set; } = new();
    public List<TmdbGenre> Genres { get; set; } = new();
}

public sealed class TmdbGenre
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class TmdbGenreList
{
    public List<TmdbGenre> Genres { get; set; } = new();
}
