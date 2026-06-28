using MediaBrowser.Model.Querying;
using ReTrak.Api.DataContracts.Sync.Collection;
using ReTrakMovieCollected = ReTrak.Api.DataContracts.Users.Collection.ReTrakMovieCollected;

namespace ReTrak.ScheduledTasks
{
    using MediaBrowser.Common.Net;
    using MediaBrowser.Controller;
    using MediaBrowser.Controller.Entities;
    using MediaBrowser.Controller.Entities.Movies;
    using MediaBrowser.Controller.Entities.TV;
    using MediaBrowser.Controller.Library;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.IO;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;
    using MediaBrowser.Model.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ReTrak.Api;
    using ReTrak.Api.DataContracts.Sync;
    using ReTrak.Helpers;
    using ReTrak.Model;

    /// <summary>
    /// Task that will Sync each users local library with their respective retrak.tv profiles. This task will only include 
    /// titles, watched states will be synced in other tasks.
    /// </summary>
    public class SyncLibraryTask : IScheduledTask
    {
        //private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;

        private readonly IUserManager _userManager;

        private readonly ILogger _logger;

        private readonly ReTrakApi _retrakApi;

        private readonly IUserDataManager _userDataManager;

        private readonly ILibraryManager _libraryManager;

        public SyncLibraryTask(
            ILogManager logger,
            IJsonSerializer jsonSerializer,
            IUserManager userManager,
            IUserDataManager userDataManager,
            IHttpClient httpClient,
            IServerApplicationHost appHost,
            IFileSystem fileSystem,
            ILibraryManager libraryManager)
        {
            _jsonSerializer = jsonSerializer;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _logger = logger.GetLogger("ReTrak");
            _retrakApi = new ReTrakApi(jsonSerializer, _logger, httpClient, appHost, userDataManager, fileSystem);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }

        public string Key
        {
            get
            {
                return "ReTrakSyncLibraryTask";
            }
        }

        /// <summary>
        /// Gather users and call <see cref="SyncUserLibrary"/>
        /// </summary>
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var users = _userManager.Users.Where(u => UserHelper.GetReTrakUser(u) != null).ToList();

            // No point going further if we don't have users.
            if (users.Count == 0)
            {
                _logger.Info("No Users returned");
                return;
            }

            foreach (var user in users)
            {
                var retrakUser = UserHelper.GetReTrakUser(user);

                // I'll leave this in here for now, but in reality this continue should never be reached.
                if (string.IsNullOrEmpty(retrakUser?.LinkedMbUserId))
                {
                    _logger.Error("retrakUser is either null or has no linked MB account");
                    continue;
                }

                await
                    SyncUserLibrary(user, retrakUser, progress.Split(users.Count), cancellationToken)
                        .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Count media items and call <see cref="SyncMovies"/> and <see cref="SyncShows"/>
        /// </summary>
        /// <returns></returns>
        private async Task SyncUserLibrary(
            User user,
            ReTrakUser retrakUser,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            await SyncMovies(user, retrakUser, progress.Split(2), cancellationToken).ConfigureAwait(false);
            await SyncShows(user, retrakUser, progress.Split(2), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sync watched and collected status of <see cref="Movie"/>s with retrak.
        /// </summary>
        private async Task SyncMovies(
            User user,
            ReTrakUser retrakUser,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            /*
             * In order to sync watched status to retrak.tv we need to know what's been watched on ReTrak already. This
             * will stop us from endlessly incrementing the watched values on the site.
             */
            var retrakWatchedMovies = await _retrakApi.SendGetAllWatchedMoviesRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            var retrakCollectedMovies = await _retrakApi.SendGetAllCollectedMoviesRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            var libraryMovies =
                _libraryManager.GetItemList(
                        new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { typeof(Movie).Name },
                            IsVirtualItem = false,
                            OrderBy = new[]
                            {
                                new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                            }
                        })
                    .Where(x => _retrakApi.CanSync(x, retrakUser))
                    .ToList();
            var collectedMovies = new List<Movie>();
            var uncollectedMovies = new List<ReTrakMovieCollected>();
            var playedMovies = new List<Movie>();
            var unplayedMovies = new List<Movie>();

            var decisionProgress = progress.Split(4).Split(libraryMovies.Count);
            foreach (var child in libraryMovies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var libraryMovie = child as Movie;
                var userData = _userDataManager.GetUserData(user, child);

                // if movie is not collected, or (export media info setting is enabled and every collected matching movie has different metadata), collect it
                var collectedMathingMovies = Match.FindMatches(libraryMovie, retrakCollectedMovies).ToList();
                if (!collectedMathingMovies.Any()
                    || (retrakUser.ExportMediaInfo
                        && collectedMathingMovies.All(
                            collectedMovie => collectedMovie.MetadataIsDifferent(libraryMovie))))
                {
                    collectedMovies.Add(libraryMovie);
                }

                var movieWatched = Match.FindMatch(libraryMovie, retrakWatchedMovies);

                // if the movie has been played locally and is unplayed on retrak.tv then add it to the list
                if (userData.Played)
                {
                    if (movieWatched == null)
                    {
                        if (retrakUser.PostWatchedHistory)
                        {
                            playedMovies.Add(libraryMovie);
                        }
                        else if (!retrakUser.SkipUnwatchedImportFromReTrak)
                        {
                            if (userData.Played)
                            {
                                userData.Played = false;

                                _userDataManager.SaveUserData(
                                    user.InternalId,
                                    libraryMovie,
                                    userData,
                                    UserDataSaveReason.Import,
                                    cancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    // If the show has not been played locally but is played on retrak.tv then add it to the unplayed list
                    if (movieWatched != null)
                    {
                        unplayedMovies.Add(libraryMovie);
                    }
                }

                decisionProgress.Report(100);
            }

            foreach (var retrakCollectedMovie in retrakCollectedMovies)
            {
                if (!Match.FindMatches(retrakCollectedMovie, libraryMovies).Any())
                {
                    _logger.Debug("No matches for {0}, will be uncollected on ReTrak", _jsonSerializer.SerializeToString(retrakCollectedMovie.movie));
                    uncollectedMovies.Add(retrakCollectedMovie);
                }
            }

            if (retrakUser.SyncCollection)
            {
                // send movies to mark collected
                await SendMovieCollectionAdds(retrakUser, collectedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);

                // send movies to mark uncollected
                await SendMovieCollectionRemoves(retrakUser, uncollectedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);
            }
            // send movies to mark watched
            await SendMoviePlaystateUpdates(true, retrakUser, playedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);

            // send movies to mark unwatched
            await SendMoviePlaystateUpdates(false, retrakUser, unplayedMovies, progress.Split(4), cancellationToken).ConfigureAwait(false);
        }

        private async Task SendMovieCollectionRemoves(
            ReTrakUser retrakUser,
            List<ReTrakMovieCollected> movies,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Info("Movies to remove from collection: " + movies.Count);
            if (movies.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await
                            _retrakApi.SendCollectionRemovalsAsync(
                                movies.Select(m => m.movie).ToList(),
                                retrakUser,
                                cancellationToken).ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var retrakSyncResponse in dataContracts)
                        {
                            LogReTrakResponseDataContract(retrakSyncResponse);
                        }
                    }
                }
                catch (ArgumentNullException argNullEx)
                {
                    _logger.ErrorException("ArgumentNullException handled sending movies to retrak.tv", argNullEx);
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Exception handled sending movies to retrak.tv", e);
                }

                progress.Report(100);
            }
        }

        private async Task SendMovieCollectionAdds(
            ReTrakUser retrakUser,
            List<Movie> movies,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Info("Movies to add to collection: " + movies.Count);
            if (movies.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await
                            _retrakApi.SendLibraryUpdateAsync(
                                movies,
                                retrakUser,
                                cancellationToken,
                                EventType.Add).ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var retrakSyncResponse in dataContracts)
                        {
                            LogReTrakResponseDataContract(retrakSyncResponse);
                        }
                    }
                }
                catch (ArgumentNullException argNullEx)
                {
                    _logger.ErrorException("ArgumentNullException handled sending movies to retrak.tv", argNullEx);
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Exception handled sending movies to retrak.tv", e);
                }

                progress.Report(100);
            }
        }

        private async Task SendMoviePlaystateUpdates(
            bool seen,
            ReTrakUser retrakUser,
            List<Movie> playedMovies,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Info("Movies to set " + (seen ? string.Empty : "un") + "watched: " + playedMovies.Count);
            if (playedMovies.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await _retrakApi.SendMoviePlaystateUpdates(playedMovies, retrakUser, false, seen, cancellationToken).ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var retrakSyncResponse in dataContracts)
                        {
                            LogReTrakResponseDataContract(retrakSyncResponse);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Error updating movie play states", e);
                }

                progress.Report(100);
            }
        }

        /// <summary>
        /// Sync watched and collected status of <see cref="Movie"/>s with retrak.
        /// </summary>
        private async Task SyncShows(
            User user,
            ReTrakUser retrakUser,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var retrakWatchedShows = await _retrakApi.SendGetWatchedShowsRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            var retrakCollectedShows = await _retrakApi.SendGetCollectedShowsRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            var episodeItems =
                _libraryManager.GetItemList(
                        new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { typeof(Episode).Name },
                            IsVirtualItem = false,
                            OrderBy = new[]
                            {
                                new ValueTuple<string, SortOrder>(ItemSortBy.SeriesSortName, SortOrder.Ascending)
                            }
                        })
                    .Where(x => _retrakApi.CanSync(x, retrakUser))
                    .ToList();

            var series =
                _libraryManager.GetItemList(
                        new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { typeof(Series).Name },
                            IsVirtualItem = false
                        })
                    .Where(x => _retrakApi.CanSync(x, retrakUser))
                    .OfType<Series>()
                    .ToList();

            var collectedEpisodes = new List<Episode>();
            var uncollectedShows = new List<Api.DataContracts.Sync.Collection.ReTrakShowCollected>();
            var playedEpisodes = new List<Episode>();
            var unplayedEpisodes = new List<Episode>();


            var decisionProgress = progress.Split(4).Split(episodeItems.Count);
            foreach (var child in episodeItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var episode = child as Episode;
                var userData = _userDataManager.GetUserData(user, episode);
                var isPlayedReTrakTv = false;
                var retrakWatchedShow = Match.FindMatch(episode.Series, retrakWatchedShows);

                if (retrakWatchedShow?.seasons != null && retrakWatchedShow.seasons.Count > 0)
                {
                    isPlayedReTrakTv =
                        retrakWatchedShow.seasons.Any(
                            season =>
                                season.number == episode.GetSeasonNumber() && season.episodes != null
                                && season.episodes.Any(te => te.number == episode.IndexNumber && te.plays > 0));
                }

                // if the show has been played locally and is unplayed on retrak.tv then add it to the list
                if (userData != null && userData.Played && !isPlayedReTrakTv)
                {
                    if (retrakUser.PostWatchedHistory)
                    {
                        playedEpisodes.Add(episode);
                    }
                    else if (!retrakUser.SkipUnwatchedImportFromReTrak)
                    {
                        if (userData.Played)
                        {
                            userData.Played = false;

                            _userDataManager.SaveUserData(
                                user.InternalId,
                                episode,
                                userData,
                                UserDataSaveReason.Import,
                                cancellationToken);
                        }
                    }
                }
                else if (userData != null && !userData.Played && isPlayedReTrakTv)
                {
                    // If the show has not been played locally but is played on retrak.tv then add it to the unplayed list
                    unplayedEpisodes.Add(episode);
                }

                var retrakCollectedShow = Match.FindMatch(episode.Series, retrakCollectedShows);
                if (retrakCollectedShow?.seasons == null
                    || retrakCollectedShow.seasons.All(x => x.number != episode.ParentIndexNumber)
                    || retrakCollectedShow.seasons.First(x => x.number == episode.ParentIndexNumber)
                        .episodes.All(e => e.number != episode.IndexNumber))
                {
                    collectedEpisodes.Add(episode);
                }

                decisionProgress.Report(100);
            }
            // Check if we have all the collected items, add missing to uncollectedShows
            foreach (var retrakShowCollected in retrakCollectedShows)
            {
                _logger.Debug(_jsonSerializer.SerializeToString(series));
                var seriesMatch = Match.FindMatch(retrakShowCollected.show, series);
                if (seriesMatch != null)
                {
                    var seriesEpisodes = episodeItems.OfType<Episode>().Where(e => e.Series.Id == seriesMatch.Id);

                    var uncollectedSeasons = new List<ReTrakShowCollected.ReTrakSeasonCollected>();
                    foreach (var retrakSeasonCollected in retrakShowCollected.seasons)
                    {
                        var uncollectedEpisodes =
                            new List<ReTrakEpisodeCollected>();
                        foreach (var retrakEpisodeCollected in retrakSeasonCollected.episodes)
                        {
                            if (seriesEpisodes.Any(e =>
                                e.ParentIndexNumber == retrakSeasonCollected.number &&
                                e.IndexNumber == retrakEpisodeCollected.number))
                            {

                            }
                            else
                            {
                                _logger.Debug("Could not match S{0}E{1} from {2} to any Emby episode, marking for collection removal", retrakSeasonCollected.number, retrakEpisodeCollected.number, _jsonSerializer.SerializeToString(retrakShowCollected.show));
                                uncollectedEpisodes.Add(new ReTrakEpisodeCollected() { number = retrakEpisodeCollected.number });
                            }
                        }

                        if (uncollectedEpisodes.Any())
                        {
                            uncollectedSeasons.Add(new ReTrakShowCollected.ReTrakSeasonCollected() { number = retrakSeasonCollected.number, episodes = uncollectedEpisodes });
                        }
                    }

                    if (uncollectedSeasons.Any())
                    {
                        uncollectedShows.Add(new ReTrakShowCollected() { ids = retrakShowCollected.show.ids, title = retrakShowCollected.show.title, year = retrakShowCollected.show.year, seasons = uncollectedSeasons });
                    }

                }
                else
                {
                    _logger.Debug("Could not match {0} to any Emby show, marking for collection removal", _jsonSerializer.SerializeToString(retrakShowCollected.show));
                    uncollectedShows.Add(new ReTrakShowCollected() { ids = retrakShowCollected.show.ids, title = retrakShowCollected.show.title, year = retrakShowCollected.show.year });
                }



            }

            if (retrakUser.SyncCollection)
            {
                await SendEpisodeCollectionAdds(retrakUser, collectedEpisodes, progress.Split(4), cancellationToken)
                    .ConfigureAwait(false);

                await SendEpisodeCollectionRemovals(retrakUser, uncollectedShows, progress.Split(5), cancellationToken)
                    .ConfigureAwait(false);
            }

            await SendEpisodePlaystateUpdates(true, retrakUser, playedEpisodes, progress.Split(4), cancellationToken).ConfigureAwait(false);

            await SendEpisodePlaystateUpdates(false, retrakUser, unplayedEpisodes, progress.Split(4), cancellationToken).ConfigureAwait(false);
        }

        private async Task SendEpisodePlaystateUpdates(
            bool seen,
            ReTrakUser retrakUser,
            List<Episode> playedEpisodes,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Info("Episodes to set " + (seen ? string.Empty : "un") + "watched: " + playedEpisodes.Count);
            if (playedEpisodes.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await _retrakApi.SendEpisodePlaystateUpdates(playedEpisodes, retrakUser, false, seen, cancellationToken).ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var con in dataContracts)
                        {
                            LogReTrakResponseDataContract(con);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Error updating episode play states", e);
                }

                progress.Report(100);
            }
        }

        private async Task SendEpisodeCollectionAdds(
            ReTrakUser retrakUser,
            List<Episode> collectedEpisodes,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Info("Episodes to add to Collection: " + collectedEpisodes.Count);
            if (collectedEpisodes.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await
                            _retrakApi.SendLibraryUpdateAsync(
                                collectedEpisodes,
                                retrakUser,
                                cancellationToken,
                                EventType.Add).ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var retrakSyncResponse in dataContracts)
                        {
                            LogReTrakResponseDataContract(retrakSyncResponse);
                        }
                    }
                }
                catch (ArgumentNullException argNullEx)
                {
                    _logger.ErrorException("ArgumentNullException handled sending episodes to retrak.tv", argNullEx);
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Exception handled sending episodes to retrak.tv", e);
                }

                progress.Report(100);
            }
        }

        private async Task SendEpisodeCollectionRemovals(
            ReTrakUser retrakUser,
            List<Api.DataContracts.Sync.Collection.ReTrakShowCollected> uncollectedEpisodes,
            ISplittableProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _logger.Info("Episodes to remove from Collection: " + uncollectedEpisodes.Count);
            if (uncollectedEpisodes.Count > 0)
            {
                try
                {
                    var dataContracts =
                        await
                            _retrakApi.SendLibraryRemovalsAsync(
                                uncollectedEpisodes,
                                retrakUser,
                                cancellationToken).ConfigureAwait(false);
                    if (dataContracts != null)
                    {
                        foreach (var retrakSyncResponse in dataContracts)
                        {
                            LogReTrakResponseDataContract(retrakSyncResponse);
                        }
                    }
                }
                catch (ArgumentNullException argNullEx)
                {
                    _logger.ErrorException("ArgumentNullException handled sending episodes to retrak.tv", argNullEx);
                }
                catch (Exception e)
                {
                    _logger.ErrorException("Exception handled sending episodes to retrak.tv", e);
                }

                progress.Report(100);
            }
        }

        public string Name => "Sync library to retrak.tv";

        public string Category => "ReTrak";

        public string Description
            => "Adds any media that is in each users retrak monitored locations to their retrak.tv profile";

        private void LogReTrakResponseDataContract(ReTrakSyncResponse dataContract)
        {
            try
            {
                _logger.Debug("ReTrakResponse Added Movies: " + dataContract?.added?.movies);
                _logger.Debug("ReTrakResponse Added Shows: " + dataContract?.added?.shows);
                _logger.Debug("ReTrakResponse Added Seasons: " + dataContract?.added?.seasons);
                _logger.Debug("ReTrakResponse Added Episodes: " + dataContract?.added?.episodes);

                _logger.Debug("ReTrakResponse Deleted Movies: " + dataContract?.deleted?.movies);
                _logger.Debug("ReTrakResponse Deleted Shows: " + dataContract?.deleted?.shows);
                _logger.Debug("ReTrakResponse Deleted Seasons: " + dataContract?.deleted?.seasons);
                _logger.Debug("ReTrakResponse Deleted Episodes: " + dataContract?.deleted?.episodes);

                _logger.Debug("ReTrakResponse Existing Movies: " + dataContract?.existing?.movies);
                _logger.Debug("ReTrakResponse Existing Shows: " + dataContract?.existing?.shows);
                _logger.Debug("ReTrakResponse Existing Seasons: " + dataContract?.existing?.seasons);
                _logger.Debug("ReTrakResponse Existing Episodes: " + dataContract?.existing?.episodes);

                foreach (var retrakMovie in dataContract.not_found.movies)
                {
                    _logger.Error("ReTrakResponse not Found:" + _jsonSerializer.SerializeToString(retrakMovie));
                }

                foreach (var retrakShow in dataContract.not_found.shows)
                {
                    _logger.Error("ReTrakResponse not Found:" + _jsonSerializer.SerializeToString(retrakShow));
                }

                foreach (var retrakSeason in dataContract.not_found.seasons)
                {
                    _logger.Error("ReTrakResponse not Found:" + _jsonSerializer.SerializeToString(retrakSeason));
                }

                foreach (var retrakEpisode in dataContract.not_found.episodes)
                {
                    _logger.Error("ReTrakResponse not Found:" + _jsonSerializer.SerializeToString(retrakEpisode));
                }
            }
            catch (NullReferenceException e)
            {
                _logger.ErrorException("Couldn't decode retrak response", e);
                _logger.Debug("Response object: {0}", _jsonSerializer.SerializeToString(dataContract));
            }
        }
    }
}
