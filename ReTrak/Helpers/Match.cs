using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.DataContracts.Users.Collection;
using ReTrak.Api.DataContracts.Users.Playback;
using ReTrak.Api.DataContracts.Users.Watched;

namespace ReTrak.Helpers
{
    class Match
    {
        public static ReTrakShowWatched FindMatch(Series item, IEnumerable<ReTrakShowWatched> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.show));
        }

        public static ReTrakPlaybackEpisode FindMatch(Episode item, IEnumerable<ReTrakPlaybackEpisode> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.episode));
        }

        public static Series FindMatch(ReTrakShow item, IEnumerable<Series> results)
        {
            return results.FirstOrDefault(i => IsMatch(i, item));
        }

        public static ReTrakShowCollected FindMatch(Series item, IEnumerable<ReTrakShowCollected> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.show));
        }

        public static ReTrakMovieWatched FindMatch(BaseItem item, IEnumerable<ReTrakMovieWatched> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.movie));
        }

        public static ReTrakPlaybackMovie FindMatch(BaseItem item, IEnumerable<ReTrakPlaybackMovie> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.movie));
        }

        public static IEnumerable<ReTrakMovieCollected> FindMatches(BaseItem item, IEnumerable<ReTrakMovieCollected> results)
        {
            return results.Where(i => IsMatch(item, i.movie)).ToList();
        }

        public static IEnumerable<BaseItem> FindMatches(ReTrakMovieCollected item, IEnumerable<BaseItem> results)
        {
            return results.Where(i => IsMatch(i, item.movie)).ToList();
        }

        public static bool IsMatch(BaseItem item, ReTrakMovie movie)
        {
            var imdb = item.GetProviderId(MetadataProviders.Imdb);

            if (!String.IsNullOrWhiteSpace(imdb) &&
                String.Equals(imdb, movie.ids.imdb, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var tmdb = item.GetProviderId(MetadataProviders.Tmdb);

            if (movie.ids.tmdb.HasValue && String.Equals(tmdb, movie.ids.tmdb.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (item.Name == movie.title && item.ProductionYear == movie.year)
            {
                return true;
            }

            return false;
        }

        public static bool IsMatch(BaseItem item, ReTrakShow show)
        {
            return
            MatchIds(item.GetProviderId(MetadataProviders.Tvdb), show.ids.tvdb) ||
            MatchIds(item.GetProviderId(MetadataProviders.Imdb), show.ids.imdb) ||
            MatchIds(item.GetProviderId(MetadataProviders.Tmdb), show.ids.tmdb) ||
            MatchIds(item.GetProviderId(MetadataProviders.TvRage), show.ids.tvrage);

        }

        public static bool IsMatch(BaseItem item, ReTrakEpisode episode)
        {
            return
            MatchIds(item.GetProviderId(MetadataProviders.Tvdb), episode.ids.tvdb) ||
            MatchIds(item.GetProviderId(MetadataProviders.Imdb), episode.ids.imdb) ||
            MatchIds(item.GetProviderId(MetadataProviders.Tmdb), episode.ids.tmdb) ||
            MatchIds(item.GetProviderId(MetadataProviders.TvRage), episode.ids.tvrage);

        }

        public static bool MatchIds(string a, string b)
        {
            return !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchIds(string a, int? b)
        {
            return !string.IsNullOrWhiteSpace(a)
                   && b.HasValue
                   && string.Equals(a, b.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }
    }
}
