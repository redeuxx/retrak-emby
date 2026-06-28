using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using ReTrak.Api.DataContracts;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.DataContracts.Scrobble;
using ReTrak.Api.DataContracts.Sync;
using ReTrak.Api.DataContracts.Sync.Ratings;
using ReTrak.Api.DataContracts.Sync.Watched;
using ReTrak.Helpers;
using ReTrak.Model;
using MediaBrowser.Model.Entities;
using ReTrakMovieCollected = ReTrak.Api.DataContracts.Sync.Collection.ReTrakMovieCollected;
using ReTrakEpisodeCollected = ReTrak.Api.DataContracts.Sync.Collection.ReTrakEpisodeCollected;
using ReTrakShowCollected = ReTrak.Api.DataContracts.Sync.Collection.ReTrakShowCollected;
using MediaBrowser.Model.IO;

namespace ReTrak.Api
{
    /// <summary>
    /// 
    /// </summary>
    public class ReTrakApi
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IServerApplicationHost _appHost;
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;

        public ReTrakApi(IJsonSerializer jsonSerializer, ILogger logger, IHttpClient httpClient,
            IServerApplicationHost appHost, IUserDataManager userDataManager, IFileSystem fileSystem)
        {
            _httpClient = httpClient;
            _appHost = appHost;
            _userDataManager = userDataManager;
            _fileSystem = fileSystem;
            _jsonSerializer = jsonSerializer;
            _logger = logger;
        }

        /// <summary>
        /// Checks whether it's possible/allowed to sync a <see cref="BaseItem"/> for a <see cref="ReTrakUser"/>.
        /// </summary>
        /// <param name="item">
        /// Item to check.
        /// </param>
        /// <param name="retrakUser">
        /// The retrak user to check for.
        /// </param>
        /// <returns>
        /// <see cref="bool"/> indicates if it's possible/allowed to sync this item.
        /// </returns>
        public bool CanSync(BaseItem item, ReTrakUser retrakUser)
        {
            if (item.Path == null || item.LocationType == LocationType.Virtual)
            {
                return false;
            }

            if (retrakUser.LocationsExcluded != null && retrakUser.LocationsExcluded.Any(s => _fileSystem.ContainsSubPath(s.AsSpan(), item.Path.AsSpan())))
            {
                return false;
            }

            if (item is Movie movie)
            {
                return !string.IsNullOrEmpty(movie.GetProviderId(MetadataProviders.Imdb)) ||
                    !string.IsNullOrEmpty(movie.GetProviderId(MetadataProviders.Tmdb));
            }

            if (item is Episode episode && episode.Series != null && !episode.IsMissingEpisode && (episode.IndexNumber.HasValue || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProviders.Tvdb))))
            {
                var series = episode.Series;

                return !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Imdb)) ||
                    !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Tvdb));
            }

            if (item is Series show)
            {

                return !string.IsNullOrEmpty(show.GetProviderId(MetadataProviders.Imdb)) ||
                       !string.IsNullOrEmpty(show.GetProviderId(MetadataProviders.Tvdb)) ||
                       !string.IsNullOrEmpty(show.GetProviderId(MetadataProviders.Tmdb)) ||
                       !string.IsNullOrEmpty(show.GetProviderId(MetadataProviders.TvRage));
            }


            return false;
        }

        /// <summary>
        /// Report to retrak.tv that a movie is being watched, or has been watched.
        /// </summary>
        /// <param name="movie">The movie being watched/scrobbled</param>
        /// <param name="mediaStatus">MediaStatus enum dictating whether item is being watched or scrobbled</param>
        /// <param name="retrakUser">The user that watching the current movie</param>
        /// <param name="progressPercent"></param>
        /// <returns>A standard ReTrakResponse Data Contract</returns>
        public async Task<ReTrakScrobbleResponse> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, ReTrakUser retrakUser, float progressPercent, CancellationToken cancellationToken)
        {
            var movieData = new ReTrakScrobbleMovie
            {
                app_date = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd"),
                app_version = _appHost.ApplicationVersion.ToString(),
                progress = progressPercent,
                movie = new ReTrakMovie
                {
                    title = movie.Name,
                    year = movie.ProductionYear,
                    ids = new ReTrakMovieId
                    {
                        imdb = movie.GetProviderId(MetadataProviders.Imdb),
                        tmdb = movie.GetProviderId(MetadataProviders.Tmdb).ConvertToInt()
                    }
                }
            };

            string url;
            switch (mediaStatus)
            {
                case MediaStatus.Watching:
                    url = ReTrakUris.ScrobbleStart;
                    break;
                case MediaStatus.Paused:
                    url = ReTrakUris.ScrobblePause;
                    break;
                default:
                    url = ReTrakUris.ScrobbleStop;
                    break;
            }

            using (var response = await PostToReTrak(url, movieData, cancellationToken, retrakUser).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<ReTrakScrobbleResponse>(response).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Reports to retrak.tv that an episode is being watched. Or that Episode(s) have been watched.
        /// </summary>
        /// <param name="episode">The episode being watched</param>
        /// <param name="status">Enum indicating whether an episode is being watched or scrobbled</param>
        /// <param name="retrakUser">The user that's watching the episode</param>
        /// <param name="progressPercent"></param>
        /// <returns>A List of standard ReTrakResponse Data Contracts</returns>
        public async Task<List<ReTrakScrobbleResponse>> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, ReTrakUser retrakUser, float progressPercent, CancellationToken cancellationToken)
        {
            var episodeDatas = new List<ReTrakScrobbleEpisode>();
            var tvDbId = episode.GetProviderId(MetadataProviders.Tvdb);

            if (!string.IsNullOrEmpty(tvDbId) && (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue || episode.IndexNumberEnd <= episode.IndexNumber))
            {
                episodeDatas.Add(new ReTrakScrobbleEpisode
                {
                    app_date = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd"),
                    app_version = _appHost.ApplicationVersion.ToString(),
                    progress = progressPercent,
                    episode = new ReTrakEpisode
                    {
                        season = episode.GetSeasonNumber(),
                        number = episode.IndexNumber,
                        ids = new ReTrakEpisodeId
                        {
                            tvdb = tvDbId.ConvertToInt()
                        },
                    },
                    show = new ReTrakShow
                    {
                        title = episode.Series.Name,
                        year = episode.Series.ProductionYear,
                        ids = new ReTrakShowId
                        {
                            tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                            imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                            tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
                        }
                    }
                });
            }
            else if (episode.IndexNumber.HasValue)
            {
                var indexNumber = episode.IndexNumber.Value;
                var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

                for (var number = indexNumber; number <= finalNumber; number++)
                {
                    episodeDatas.Add(new ReTrakScrobbleEpisode
                    {
                        app_date = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd"),
                        app_version = _appHost.ApplicationVersion.ToString(),
                        progress = progressPercent,
                        episode = new ReTrakEpisode
                        {
                            season = episode.GetSeasonNumber(),
                            number = number
                        },
                        show = new ReTrakShow
                        {
                            title = episode.Series.Name,
                            year = episode.Series.ProductionYear,
                            ids = new ReTrakShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
                            }
                        }
                    });
                }
            }

            string url;
            switch (status)
            {
                case MediaStatus.Watching:
                    url = ReTrakUris.ScrobbleStart;
                    break;
                case MediaStatus.Paused:
                    url = ReTrakUris.ScrobblePause;
                    break;
                default:
                    url = ReTrakUris.ScrobbleStop;
                    break;
            }
            var responses = new List<ReTrakScrobbleResponse>();
            foreach (var retrakScrobbleEpisode in episodeDatas)
            {
                using (var response = await PostToReTrak(url, retrakScrobbleEpisode, cancellationToken, retrakUser).ConfigureAwait(false))
                {
                    responses.Add(await _jsonSerializer.DeserializeFromStreamAsync<ReTrakScrobbleResponse>(response).ConfigureAwait(false));
                }
            }
            return responses;
        }

        /// <summary>
        /// Add or remove a list of movies to/from the users retrak.tv library
        /// </summary>
        /// <param name="movies">The movies to add</param>
        /// <param name="retrakUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{ReTrakResponseDataContract}.</returns>
        public async Task<IEnumerable<ReTrakSyncResponse>> SendCollectionRemovalsAsync(List<ReTrakMovie> movies, ReTrakUser retrakUser,
            CancellationToken cancellationToken)
        {
            if (movies == null)
                throw new ArgumentNullException("movies");
            if (retrakUser == null)
                throw new ArgumentNullException("retrakUser");

            var responses = new List<ReTrakSyncResponse>();
            var chunks = movies.ToChunks(100);
            foreach (var chunk in chunks)
            {
                var data = new ReTrakSyncUncollected
                {
                    movies = chunk.ToList()
                };
                using (var response = await PostToReTrak(ReTrakUris.SyncCollectionRemove, data, cancellationToken, retrakUser).ConfigureAwait(false))
                {
                    responses.Add(await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false));
                }
            }
            return responses;
        }

        /// <summary>
        /// Add or remove a list of movies to/from the users retrak.tv library
        /// </summary>
        /// <param name="movies">The movies to add</param>
        /// <param name="retrakUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{ReTrakResponseDataContract}.</returns>
        public async Task<IEnumerable<ReTrakSyncResponse>> SendLibraryUpdateAsync(List<Movie> movies, ReTrakUser retrakUser,
            CancellationToken cancellationToken, EventType eventType)
        {
            if (movies == null)
                throw new ArgumentNullException("movies");
            if (retrakUser == null)
                throw new ArgumentNullException("retrakUser");

            if (eventType == EventType.Update) return null;

            var moviesPayload = movies.Select(m =>
            {
                var audioStream = m.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
                var retrakMovieCollected = new ReTrakMovieCollected
                {
                    collected_at = m.DateCreated.ToISO8601(),
                    title = m.Name,
                    year = m.ProductionYear,
                    ids = new ReTrakMovieId
                    {
                        imdb = m.GetProviderId(MetadataProviders.Imdb),
                        tmdb = m.GetProviderId(MetadataProviders.Tmdb).ConvertToInt()
                    }
                };
                if (retrakUser.ExportMediaInfo)
                {
                    //retrakMovieCollected.Is3D = m.Is3D;
                    retrakMovieCollected.audio_channels = audioStream.GetAudioChannels();
                    retrakMovieCollected.audio = audioStream.GetCodecRepresetation();
                    retrakMovieCollected.resolution = m.GetDefaultVideoStream().GetResolution();
                }
                return retrakMovieCollected;
            }).ToList();
            var url = eventType == EventType.Add ? ReTrakUris.SyncCollectionAdd : ReTrakUris.SyncCollectionRemove;

            var responses = new List<ReTrakSyncResponse>();
            var chunks = moviesPayload.ToChunks(100);
            foreach (var chunk in chunks)
            {
                var data = new ReTrakSyncCollected
                {
                    movies = chunk.ToList()
                };
                using (var response = await PostToReTrak(url, data, cancellationToken, retrakUser).ConfigureAwait(false))
                {
                    responses.Add(await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false));
                }
            }
            return responses;
        }

        public async Task<List<ReTrakSyncResponse>> SendLibraryRemovalsAsync(List<ReTrakShowCollected> uncollectedEpisodes, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            if (uncollectedEpisodes == null)
                throw new ArgumentNullException(nameof(uncollectedEpisodes));

            if (retrakUser == null)
                throw new ArgumentNullException(nameof(retrakUser));

            var responses = new List<ReTrakSyncResponse>();
            var chunks = uncollectedEpisodes.ToChunks(100);
            foreach (var chunk in chunks)
            {
                var data = new ReTrakSyncUncollected
                {
                    shows = chunk.ToList()
                };

                var url = ReTrakUris.SyncCollectionRemove;

                using (var response = await PostToReTrak(url, data, cancellationToken, retrakUser).ConfigureAwait(false))
                {
                    responses.Add(await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false));
                }
            }

            return responses;
        }

        /// <summary>
        /// Add or remove a list of Episodes to/from the users retrak.tv library
        /// </summary>
        /// <param name="episodes">The episodes to add</param>
        /// <param name="retrakUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{ReTrakResponseDataContract}.</returns>
        public async Task<IEnumerable<ReTrakSyncResponse>> SendLibraryUpdateAsync(IReadOnlyList<Episode> episodes,
            ReTrakUser retrakUser, CancellationToken cancellationToken, EventType eventType)
        {
            if (episodes == null)
                throw new ArgumentNullException("episodes");

            if (retrakUser == null)
                throw new ArgumentNullException("retrakUser");

            if (eventType == EventType.Update) return null;
            var responses = new List<ReTrakSyncResponse>();
            var chunks = episodes.ToChunks(100);
            foreach (var chunk in chunks)
            {
                responses.Add(await SendLibraryUpdateInternalAsync(chunk.ToList(), retrakUser, cancellationToken, eventType).ConfigureAwait(false));
            }
            return responses;
        }

        private async Task<ReTrakSyncResponse> SendLibraryUpdateInternalAsync(IEnumerable<Episode> episodes,
            ReTrakUser retrakUser, CancellationToken cancellationToken, EventType eventType)
        {
            var episodesPayload = new List<ReTrakEpisodeCollected>();
            var showPayload = new List<ReTrakShowCollected>();
            foreach (Episode episode in episodes)
            {
                var audioStream = episode.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
                var tvDbId = episode.GetProviderId(MetadataProviders.Tvdb);

                if (!string.IsNullOrEmpty(tvDbId) &&
                    (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue ||
                     episode.IndexNumberEnd <= episode.IndexNumber))
                {
                    var retrakEpisodeCollected = new ReTrakEpisodeCollected
                    {
                        collected_at = episode.DateCreated.ToISO8601(),
                        ids = new ReTrakEpisodeId
                        {
                            tvdb = tvDbId.ConvertToInt()
                        }
                    };
                    if (retrakUser.ExportMediaInfo)
                    {
                        //retrakEpisodeCollected.Is3D = episode.Is3D;
                        retrakEpisodeCollected.audio_channels = audioStream.GetAudioChannels();
                        retrakEpisodeCollected.audio = audioStream.GetCodecRepresetation();
                        retrakEpisodeCollected.resolution = episode.GetDefaultVideoStream().GetResolution();
                    }
                    episodesPayload.Add(retrakEpisodeCollected);
                }
                else if (episode.IndexNumber.HasValue)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
                    var syncShow =
                        showPayload.FirstOrDefault(
                            sre =>
                                sre.ids != null &&
                                sre.ids.tvdb == episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt());
                    if (syncShow == null)
                    {
                        syncShow = new ReTrakShowCollected
                        {
                            ids = new ReTrakShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
                            },
                            seasons = new List<ReTrakShowCollected.ReTrakSeasonCollected>()
                        };
                        showPayload.Add(syncShow);
                    }
                    var syncSeason =
                        syncShow.seasons.FirstOrDefault(ss => ss.number == episode.GetSeasonNumber());
                    if (syncSeason == null)
                    {
                        syncSeason = new ReTrakShowCollected.ReTrakSeasonCollected
                        {
                            number = episode.GetSeasonNumber(),
                            episodes = new List<ReTrakEpisodeCollected>()
                        };
                        syncShow.seasons.Add(syncSeason);
                    }
                    for (var number = indexNumber; number <= finalNumber; number++)
                    {
                        var ids = new ReTrakEpisodeId();

                        if (number == indexNumber)
                        {
                            // Omit this from the rest because then we end up attaching the tvdb of the first episode to the subsequent ones
                            ids.tvdb = tvDbId.ConvertToInt();

                        }
                        var retrakEpisodeCollected = new ReTrakEpisodeCollected
                        {
                            number = number,
                            collected_at = episode.DateCreated.ToISO8601(),
                            ids = ids
                        };
                        if (retrakUser.ExportMediaInfo)
                        {
                            //retrakEpisodeCollected.Is3D = episode.Is3D;
                            retrakEpisodeCollected.audio_channels = audioStream.GetAudioChannels();
                            retrakEpisodeCollected.audio = audioStream.GetCodecRepresetation();
                            retrakEpisodeCollected.resolution = episode.GetDefaultVideoStream().GetResolution();
                        }
                        syncSeason.episodes.Add(retrakEpisodeCollected);
                    }
                }
            }

            var data = new ReTrakSyncCollected
            {
                episodes = episodesPayload.ToList(),
                shows = showPayload.ToList()
            };

            var url = eventType == EventType.Add ? ReTrakUris.SyncCollectionAdd : ReTrakUris.SyncCollectionRemove;
            using (var response = await PostToReTrak(url, data, cancellationToken, retrakUser).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// Add or remove a Show(Series) to/from the users retrak.tv library
        /// </summary>
        /// <param name="show">The show to remove</param>
        /// <param name="retrakUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{ReTrakResponseDataContract}.</returns>
        public async Task<ReTrakSyncResponse> SendLibraryUpdateAsync(Series show, ReTrakUser retrakUser, CancellationToken cancellationToken, EventType eventType)
        {
            if (show == null)
                throw new ArgumentNullException("show");
            if (retrakUser == null)
                throw new ArgumentNullException("retrakUser");

            if (eventType == EventType.Update) return null;

            var showPayload = new List<ReTrakShowCollected>
            {
                new ReTrakShowCollected
                {
                    title = show.Name,
                    year = show.ProductionYear,
                    ids = new ReTrakShowId
                    {
                        tvdb = show.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                        imdb = show.GetProviderId(MetadataProviders.Imdb),
                        tvrage = show.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
                    },
                }
            };

            var data = new ReTrakSyncCollected
            {
                shows = showPayload.ToList()
            };

            var url = eventType == EventType.Add ? ReTrakUris.SyncCollectionAdd : ReTrakUris.SyncCollectionRemove;
            using (var response = await PostToReTrak(url, data, cancellationToken, retrakUser).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// Rate an item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rating"></param>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<ReTrakSyncResponse> SendItemRating(BaseItem item, int rating, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            object data = new { };
            if (item is Movie)
            {
                data = new
                {
                    movies = new[]
                    {
                        new ReTrakMovieRated
                        {
                            title = item.Name,
                            year = item.ProductionYear,
                            ids = new ReTrakMovieId
                            {
                                imdb = item.GetProviderId(MetadataProviders.Imdb),
                                tmdb = item.GetProviderId(MetadataProviders.Tmdb).ConvertToInt()
                            },
                            rating = rating
                        }
                    }
                };

            }
            else if (item is Episode)
            {
                var episode = item as Episode;

                if (string.IsNullOrEmpty(episode.GetProviderId(MetadataProviders.Tvdb)))
                {
                    if (episode.IndexNumber.HasValue)
                    {
                        var indexNumber = episode.IndexNumber.Value;
                        var show = new ReTrakShowRated
                        {
                            ids = new ReTrakShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
                            },
                            seasons = new List<ReTrakShowRated.ReTrakSeasonRated>
                            {
                                new ReTrakShowRated.ReTrakSeasonRated
                                {
                                    number = episode.GetSeasonNumber(),
                                    episodes = new List<ReTrakEpisodeRated>
                                    {
                                        new ReTrakEpisodeRated
                                        {
                                            number = indexNumber,
                                            rating = rating
                                        }
                                    }
                                }
                            }
                        };
                        data = new
                        {
                            shows = new[]
                            {
                                show
                            }
                        };
                    }
                }
                else
                {
                    data = new
                    {
                        episodes = new[]
                        {
                            new ReTrakEpisodeRated
                            {
                                rating = rating,
                                ids = new ReTrakEpisodeId
                                {
                                    tvdb = episode.GetProviderId(MetadataProviders.Tvdb).ConvertToInt()
                                }
                            }
                        }
                    };
                }
            }
            else // It's a Series
            {
                data = new
                {
                    shows = new[]
                    {
                        new ReTrakShowRated
                        {
                            rating = rating,
                            title = item.Name,
                            year = item.ProductionYear,
                            ids = new ReTrakShowId
                            {
                                imdb = item.GetProviderId(MetadataProviders.Imdb),
                                tvdb = item.GetProviderId(MetadataProviders.Tvdb).ConvertToInt()
                            }
                        }
                    }
                };
            }

            using (var response = await PostToReTrak(ReTrakUris.SyncRatingsAdd, data, cancellationToken, retrakUser).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="comment"></param>
        /// <param name="containsSpoilers"></param>
        /// <param name="retrakUser"></param>
        /// <param name="isReview"></param>
        /// <returns></returns>
        public async Task<object> SendItemComment(BaseItem item, string comment, bool containsSpoilers, ReTrakUser retrakUser, bool isReview = false)
        {
            return null;
            //TODO: This functionallity is not available yet
            //            string url;
            //            var data = new Dictionary<string, string>
            //                           {
            //                               {"username", retrakUser.UserName},
            //                               {"password", retrakUser.Password}
            //                           };
            //
            //            if (item is Movie)
            //            {
            //                if (item.ProviderIds != null && item.ProviderIds.ContainsKey("Imdb"))
            //                    data.Add("imdb_id", item.ProviderIds["Imdb"]);
            //                
            //                data.Add("title", item.Name);
            //                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
            //                url = ReTrakUris.CommentMovie;
            //            }
            //            else
            //            {
            //                var episode = item as Episode;
            //                if (episode != null)
            //                {
            //                    if (episode.Series.ProviderIds != null)
            //                    {
            //                        if (episode.Series.ProviderIds.ContainsKey("Imdb"))
            //                            data.Add("imdb_id", episode.Series.ProviderIds["Imdb"]);
            //
            //                        if (episode.Series.ProviderIds.ContainsKey("Tvdb"))
            //                            data.Add("tvdb_id", episode.Series.ProviderIds["Tvdb"]);
            //                    }
            //
            //                    data.Add("season", episode.AiredSeasonNumber.ToString());
            //                    data.Add("episode", episode.IndexNumber.ToString());
            //                    url = ReTrakUris.CommentEpisode;   
            //                }
            //                else // It's a Series
            //                {
            //                    data.Add("title", item.Name);
            //                    data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
            //
            //                    if (item.ProviderIds != null)
            //                    {
            //                        if (item.ProviderIds.ContainsKey("Imdb"))
            //                            data.Add("imdb_id", item.ProviderIds["Imdb"]);
            //
            //                        if (item.ProviderIds.ContainsKey("Tvdb"))
            //                            data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
            //                    }
            //                    
            //                    url = ReTrakUris.CommentShow;
            //                }
            //            }
            //
            //            data.Add("comment", comment);
            //            data.Add("spoiler", containsSpoilers.ToString());
            //            data.Add("review", isReview.ToString());
            //
            //            Stream response =
            //                await
            //                _httpClient.Post(url, data, Plugin.Instance.ReTrakResourcePool,
            //                                                 CancellationToken.None).ConfigureAwait(false);
            //
            //            return await _jsonSerializer.DeserializeFromStreamAsync<ReTrakResponseDataContract>(response);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<ReTrakMovie>> SendMovieRecommendationsRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.RecommendationsMovies, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<ReTrakMovie>>(response).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<ReTrakShow>> SendShowRecommendationsRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.RecommendationsShows, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<ReTrakShow>>(response).ConfigureAwait(false);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<DataContracts.Users.Watched.ReTrakMovieWatched>> SendGetAllWatchedMoviesRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.WatchedMovies, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<DataContracts.Users.Watched.ReTrakMovieWatched>>(response).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<DataContracts.Users.Watched.ReTrakShowWatched>> SendGetWatchedShowsRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.WatchedShows, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<DataContracts.Users.Watched.ReTrakShowWatched>>(response).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<DataContracts.Users.Collection.ReTrakMovieCollected>> SendGetAllCollectedMoviesRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.CollectedMovies, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<DataContracts.Users.Collection.ReTrakMovieCollected>>(response).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<DataContracts.Users.Collection.ReTrakShowCollected>> SendGetCollectedShowsRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.CollectedShows, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<DataContracts.Users.Collection.ReTrakShowCollected>>(response).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<DataContracts.Users.Playback.ReTrakPlaybackMovie>> SendGetPlaybackMoviesRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.PlaybackMovies, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<DataContracts.Users.Playback.ReTrakPlaybackMovie>>(response).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="retrakUser"></param>
        /// <returns></returns>
        public async Task<List<DataContracts.Users.Playback.ReTrakPlaybackEpisode>> SendGetPlaybackShowsRequest(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            using (var response = await GetFromReTrak(ReTrakUris.PlaybackShows, retrakUser, cancellationToken).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<List<DataContracts.Users.Playback.ReTrakPlaybackEpisode>>(response).ConfigureAwait(false);
            }
        }

        private int? ParseId(string value)
        {
            int parsed;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// Send a list of movies to retrak.tv that have been marked watched or unwatched
        /// </summary>
        /// <param name="movies">The list of movies to send</param>
        /// <param name="retrakUser">The retrak user profile that is being updated</param>
        /// <param name="seen">True if movies are being marked seen, false otherwise</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns></returns>
        public async Task<List<ReTrakSyncResponse>> SendMoviePlaystateUpdates(List<Movie> movies, ReTrakUser retrakUser, bool forceUpdate, bool seen, CancellationToken cancellationToken)
        {
            if (movies == null)
                throw new ArgumentNullException("movies");
            if (retrakUser == null)
                throw new ArgumentNullException("retrakUser");
            if (!forceUpdate && !retrakUser.PostWatchedHistory)
                return new List<ReTrakSyncResponse>();

            var moviesPayload = movies.Select(m =>
            {
                var lastPlayedDate = seen
                    ? _userDataManager.GetUserData(retrakUser.LinkedMbUserId, m).LastPlayedDate
                    : null;
                return new ReTrakMovieWatched
                {
                    title = m.Name,
                    ids = new ReTrakMovieId
                    {
                        imdb = m.GetProviderId(MetadataProviders.Imdb),
                        tmdb =
                            string.IsNullOrEmpty(m.GetProviderId(MetadataProviders.Tmdb))
                                ? (int?)null
                                : ParseId(m.GetProviderId(MetadataProviders.Tmdb))
                    },
                    year = m.ProductionYear,
                    watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                };
            }).ToList();
            var chunks = moviesPayload.ToChunks(100).ToList();
            var retrakResponses = new List<ReTrakSyncResponse>();

            foreach (var chunk in chunks)
            {
                var data = new ReTrakSyncWatched
                {
                    movies = chunk.ToList()
                };
                var url = seen ? ReTrakUris.SyncWatchedHistoryAdd : ReTrakUris.SyncWatchedHistoryRemove;

                using (var response = await PostToReTrak(url, data, cancellationToken, retrakUser).ConfigureAwait(false))
                {
                    if (response != null)
                        retrakResponses.Add(await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false));
                }
            }
            return retrakResponses;
        }



        /// <summary>
        /// Send a list of episodes to retrak.tv that have been marked watched or unwatched
        /// </summary>
        /// <param name="episodes">The list of episodes to send</param>
        /// <param name="retrakUser">The retrak user profile that is being updated</param>
        /// <param name="seen">True if episodes are being marked seen, false otherwise</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns></returns>
        public async Task<List<ReTrakSyncResponse>> SendEpisodePlaystateUpdates(List<Episode> episodes, ReTrakUser retrakUser, bool forceUpdate, bool seen, CancellationToken cancellationToken)
        {
            if (episodes == null)
                throw new ArgumentNullException("episodes");

            if (retrakUser == null)
                throw new ArgumentNullException("retrakUser");
            if (!forceUpdate && !retrakUser.PostWatchedHistory)
                return new List<ReTrakSyncResponse>();

            var chunks = episodes.ToChunks(100).ToList();
            var retrakResponses = new List<ReTrakSyncResponse>();

            foreach (var chunk in chunks)
            {
                var response = await SendEpisodePlaystateUpdatesInternalAsync(chunk, retrakUser, seen, cancellationToken).ConfigureAwait(false);

                if (response != null)
                    retrakResponses.Add(response);
            }
            return retrakResponses;
        }


        private async Task<ReTrakSyncResponse> SendEpisodePlaystateUpdatesInternalAsync(IEnumerable<Episode> episodeChunk, ReTrakUser retrakUser, bool seen, CancellationToken cancellationToken)
        {
            var data = new ReTrakSyncWatched { episodes = new List<ReTrakEpisodeWatched>(), shows = new List<ReTrakShowWatched>() };
            foreach (var episode in episodeChunk)
            {
                var tvDbId = episode.GetProviderId(MetadataProviders.Tvdb);
                var lastPlayedDate = seen
                    ? _userDataManager.GetUserData(retrakUser.LinkedMbUserId, episode)
                        .LastPlayedDate
                    : null;
                if (!string.IsNullOrEmpty(tvDbId) && (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue || episode.IndexNumberEnd <= episode.IndexNumber))
                {

                    data.episodes.Add(new ReTrakEpisodeWatched
                    {
                        ids = new ReTrakEpisodeId
                        {
                            tvdb = int.Parse(tvDbId)
                        },
                        watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : DateTimeOffset.Now.ToISO8601()
                    });
                }
                else if (episode.IndexNumber != null)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

                    var syncShow = data.shows.FirstOrDefault(sre => sre.ids != null && sre.ids.tvdb == episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt());
                    if (syncShow == null)
                    {
                        syncShow = new ReTrakShowWatched
                        {
                            ids = new ReTrakShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
                            },
                            seasons = new List<ReTrakSeasonWatched>()
                        };
                        data.shows.Add(syncShow);
                    }
                    var syncSeason = syncShow.seasons.FirstOrDefault(ss => ss.number == episode.GetSeasonNumber());
                    if (syncSeason == null)
                    {
                        syncSeason = new ReTrakSeasonWatched
                        {
                            number = episode.GetSeasonNumber(),
                            episodes = new List<ReTrakEpisodeWatched>()
                        };
                        syncShow.seasons.Add(syncSeason);
                    }
                    for (var number = indexNumber; number <= finalNumber; number++)
                    {
                        syncSeason.episodes.Add(new ReTrakEpisodeWatched
                        {
                            number = number,
                            watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : DateTimeOffset.Now.ToISO8601()
                        });
                    }
                }
            }
            var url = seen ? ReTrakUris.SyncWatchedHistoryAdd : ReTrakUris.SyncWatchedHistoryRemove;

            using (var response = await PostToReTrak(url, data, cancellationToken, retrakUser).ConfigureAwait(false))
            {
                return await _jsonSerializer.DeserializeFromStreamAsync<ReTrakSyncResponse>(response).ConfigureAwait(false);
            }
        }

        public async Task RefreshUserAuth(ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            var data = new ReTrakUserTokenRequest
            {
                client_id = ReTrakUris.Id,
                client_secret = ReTrakUris.Secret,
                redirect_uri = "urn:ietf:wg:oauth:2.0:oob"
            };

            if (!string.IsNullOrWhiteSpace(retrakUser.PIN))
            {
                data.code = retrakUser.PIN;
                data.grant_type = "authorization_code";
            }
            else if (!string.IsNullOrWhiteSpace(retrakUser.RefreshToken))
            {
                data.refresh_token = retrakUser.RefreshToken;
                data.grant_type = "refresh_token";
            }
            else
            {
                _logger.Error("Tried to reauthenticate with ReTrak, but neither PIN nor refreshToken was available");
            }

            ReTrakUserToken userToken;
            using (var response = await PostToReTrak(ReTrakUris.Token, data, null, cancellationToken).ConfigureAwait(false))
            {
                userToken = await _jsonSerializer.DeserializeFromStreamAsync<ReTrakUserToken>(response).ConfigureAwait(false);
            }

            if (userToken != null)
            {
                retrakUser.AccessToken = userToken.access_token;
                retrakUser.RefreshToken = userToken.refresh_token;
                retrakUser.PIN = null;
                retrakUser.AccessTokenExpiration = DateTimeOffset.Now.AddMonths(2);
                Plugin.Instance.SaveConfiguration();
            }
        }

        private Task<Stream> GetFromReTrak(string url, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            return GetFromReTrak(url, cancellationToken, retrakUser);
        }

        private async Task<Stream> GetFromReTrak(string url, CancellationToken cancellationToken, ReTrakUser retrakUser)
        {
            var options = GetHttpRequestOptions();
            options.Url = url;
            options.CancellationToken = cancellationToken;

            if (retrakUser != null)
            {
                await SetRequestHeaders(options, retrakUser, cancellationToken).ConfigureAwait(false);
            }

            await Plugin.Instance.ReTrakResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await Retry(async () => await _httpClient.Get(options).ConfigureAwait(false)).ConfigureAwait(false);
            }
            finally
            {
                Plugin.Instance.ReTrakResourcePool.Release();
            }
        }

        private Task<Stream> PostToReTrak(string url, object data, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            return PostToReTrak(url, data, cancellationToken, retrakUser);
        }

        /// <summary>
        ///     Posts data to url, authenticating with <see cref="ReTrakUser"/>.
        /// </summary>
        /// <param name="retrakUser">If null, authentication headers not added.</param>
        private async Task<Stream> PostToReTrak(string url, object data, CancellationToken cancellationToken,
            ReTrakUser retrakUser)
        {
            var requestContent = data == null ? string.Empty : _jsonSerializer.SerializeToString(data);
            if (retrakUser != null && retrakUser.ExtraLogging) _logger.Debug("POST " + requestContent);
            var options = GetHttpRequestOptions();
            options.Url = url;
            options.CancellationToken = cancellationToken;
            options.RequestContent = requestContent.AsMemory();

            if (retrakUser != null)
            {
                await SetRequestHeaders(options, retrakUser, cancellationToken).ConfigureAwait(false);
            }

            await Plugin.Instance.ReTrakResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var retryResponse = await Retry(async () => await _httpClient.Post(options).ConfigureAwait(false)).ConfigureAwait(false);
                return retryResponse.Content;
            }
            finally
            {
                Plugin.Instance.ReTrakResourcePool.Release();
            }
        }

        private async Task<T> Retry<T>(Func<Task<T>> function)
        {
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch { }
            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch { }
            await Task.Delay(500).ConfigureAwait(false);
            return await function().ConfigureAwait(false);
        }

        private HttpRequestOptions GetHttpRequestOptions()
        {
            var options = new HttpRequestOptions
            {
                RequestContentType = "application/json",
                TimeoutMs = 120000,
                LogErrorResponseBody = false,
                LogRequest = true,
                BufferContent = false,
                EnableHttpCompression = false,
                EnableKeepAlive = false
            };
            options.RequestHeaders.Add("retrak-api-version", "2");
            options.RequestHeaders.Add("retrak-api-key", ReTrakUris.Id);
            return options;
        }

        private Task SetRequestHeaders(HttpRequestOptions options, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(retrakUser.AccessToken))
            {
                options.RequestHeaders.Add("Authorization", "Bearer " + retrakUser.AccessToken);
            }
            return Task.CompletedTask;
        }
    }
}
