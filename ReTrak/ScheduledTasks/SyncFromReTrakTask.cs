using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;

namespace ReTrak.ScheduledTasks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Common.Net;
    using MediaBrowser.Controller;
    using MediaBrowser.Controller.Entities;
    using MediaBrowser.Controller.Entities.Movies;
    using MediaBrowser.Controller.Entities.TV;
    using MediaBrowser.Controller.Library;
    using MediaBrowser.Model.Entities;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Serialization;

    using ReTrak.Api;
    using ReTrak.Api.DataContracts.BaseModel;
    using ReTrak.Api.DataContracts.Users.Collection;
    using ReTrak.Api.DataContracts.Users.Watched;
    using ReTrak.Api.DataContracts.Users.Playback;
    using ReTrak.Helpers;
    using MediaBrowser.Model.IO;

    /// <summary>
    /// Task that will Sync each users retrak.tv profile with their local library. This task will only include 
    /// watched states.
    /// </summary>
    class SyncFromReTrakTask : IScheduledTask
    {
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly ReTrakApi _retrakApi;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="jsonSerializer"></param>
        /// <param name="userManager"></param>
        /// <param name="userDataManager"> </param>
        /// <param name="httpClient"></param>
        /// <param name="appHost"></param>
        /// <param name="fileSystem"></param>
        public SyncFromReTrakTask(ILogManager logger, IJsonSerializer jsonSerializer, IUserManager userManager, IUserDataManager userDataManager, IHttpClient httpClient, IServerApplicationHost appHost, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            _userManager = userManager;
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _logger = logger.GetLogger("ReTrak");
            _retrakApi = new ReTrakApi(jsonSerializer, _logger, httpClient, appHost, userDataManager, fileSystem);
        }

        /// <summary>
        /// Gather users and call <see cref="SyncReTrakDataForUser"/>
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

            // purely for progress reporting
            var percentPerUser = 100 / users.Count;
            double currentProgress = 0;
            var numComplete = 0;

            foreach (var user in users)
            {
                try
                {
                    await SyncReTrakDataForUser(user, currentProgress, cancellationToken, progress, percentPerUser).ConfigureAwait(false);

                    numComplete++;
                    currentProgress = percentPerUser * numComplete;
                    progress.Report(currentProgress);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error syncing retrak data for user {0}", ex, user.Name);
                }
            }
        }

        private async Task SyncReTrakDataForUser(User user, double currentProgress, CancellationToken cancellationToken, IProgress<double> progress, double percentPerUser)
        {
            var retrakUser = UserHelper.GetReTrakUser(user);

            List<ReTrakMovieWatched> retrakWatchedMovies;
            List<ReTrakShowWatched> retrakWatchedShows;
            List<ReTrakPlaybackMovie> retrakPlaybackMovies;
            List<ReTrakPlaybackEpisode> retrakPlaybackEpisodes;

            try
            {
                /*
                 * In order to be as accurate as possible. We need to download the users show collection & the users watched shows.
                 * It's unfortunate that retrak.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
                 * like they do for movies.
                 */
                retrakWatchedMovies = await _retrakApi.SendGetAllWatchedMoviesRequest(retrakUser, cancellationToken).ConfigureAwait(false);
                retrakWatchedShows = await _retrakApi.SendGetWatchedShowsRequest(retrakUser, cancellationToken).ConfigureAwait(false);
                retrakPlaybackMovies = await _retrakApi.SendGetPlaybackMoviesRequest(retrakUser, cancellationToken).ConfigureAwait(false);
                retrakPlaybackEpisodes = await _retrakApi.SendGetPlaybackShowsRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Exception handled", ex);
                throw;
            }

            _logger.Info("ReTrak.tv watched Movies count = " + retrakWatchedMovies.Count);
            _logger.Info("ReTrak.tv watched Shows count = " + retrakWatchedShows.Count);
            _logger.Info("ReTrak.tv playback Movies count = " + retrakPlaybackMovies.Count);
            _logger.Info("ReTrak.tv playback Episodes count = " + retrakPlaybackEpisodes.Count);

            var mediaItems =
                _libraryManager.GetItemList(
                        new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { typeof(Movie).Name, typeof(Episode).Name },
                            IsVirtualItem = false,
                            OrderBy = new[]
                            {
                                new ValueTuple<string, SortOrder>(ItemSortBy.SeriesSortName, SortOrder.Ascending),
                                new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                            }
                        })
                    .Where(i => _retrakApi.CanSync(i, retrakUser)).ToList();

            // purely for progress reporting
            var percentPerItem = percentPerUser / mediaItems.Count;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedMovie = Match.FindMatch(movie, retrakWatchedMovies);

                var userData = _userDataManager.GetUserData(user, movie);
                bool changed = false;

                if (matchedMovie != null)
                {
                    _logger.Debug("Movie is in Watched list " + movie.Name);

                    
                    // set movie as watched
                    if (!userData.Played)
                    {
                        userData.Played = true;
                        userData.LastPlayedDate = DateTimeOffset.UtcNow;
                        changed = true;
                    }

                    // keep the highest play count
                    int playcount = Math.Max(matchedMovie.plays, userData.PlayCount);

                    // set movie playcount
                    if (userData.PlayCount != playcount)
                    {
                        userData.PlayCount = playcount;
                        changed = true;
                    }

                    // Set last played to whichever is most recent, remote or local time...
                    if (!string.IsNullOrEmpty(matchedMovie.last_watched_at))
                    {
                        var tLastPlayed = DateTimeOffset.Parse(matchedMovie.last_watched_at).ToUniversalTime();
                        var latestPlayed = tLastPlayed > userData.LastPlayedDate ? tLastPlayed : userData.LastPlayedDate;
                        if (userData.LastPlayedDate != latestPlayed)
                        {
                            userData.LastPlayedDate = latestPlayed;
                            changed = true;
                        }
                    }

                    
                }
                else if (!retrakUser.SkipUnwatchedImportFromReTrak)
                {
                    userData.Played = false;
                    userData.PlayCount = 0;
                    userData.LastPlayedDate = null;
                    changed = true;
                }

                // Only process if there's a change
                if (changed)
                {
                    _userDataManager.SaveUserData(
                           user.InternalId,
                           movie,
                           userData,
                           UserDataSaveReason.Import,
                           cancellationToken);
                }

                var playbackMovie = Match.FindMatch(movie, retrakPlaybackMovies);
                if (playbackMovie != null)
                {
                    _logger.Debug("Movie is in Playback list " + movie.Name);

                    UpdatePositionTicksFromReTrak(movie, user, playbackMovie.progress, playbackMovie.paused_at);
                }
                else
                {
                    UpdatePositionTicksFromReTrak(movie, user, 0, DateTime.Now);
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            foreach (var episode in mediaItems.OfType<Episode>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedShow = Match.FindMatch(episode.Series, retrakWatchedShows);

                if (matchedShow != null)
                {
                    var matchedSeason =
                        matchedShow.seasons.FirstOrDefault(
                            tSeason =>
                                tSeason.number
                                == (episode.ParentIndexNumber == 0
                                        ? 0
                                        : ((episode.ParentIndexNumber ?? 1))));

                    // if it's not a match then it means retrak doesn't know about the season, leave the watched state alone and move on
                    if (matchedSeason != null)
                    {
                        // episode is in users libary. Now we need to determine if it's watched
                        var userData = _userDataManager.GetUserData(user, episode);
                        bool changed = false;

                        var matchedEpisode =
                            matchedSeason.episodes.FirstOrDefault(x => x.number == (episode.IndexNumber ?? -1));

                        if (matchedEpisode != null)
                        {
                            _logger.Debug("Episode is in Watched list " + GetVerboseEpisodeData(episode));

                            // Set episode as watched
                            if (!userData.Played)
                            {
                                userData.Played = true;
                                userData.LastPlayedDate = DateTimeOffset.UtcNow;
                                changed = true;
                            }

                            // keep the highest play count
                            int playcount = Math.Max(matchedEpisode.plays, userData.PlayCount);

                            // set episode playcount
                            if (userData.PlayCount != playcount)
                            {
                                userData.PlayCount = playcount;
                                changed = true;
                            }
                        }
                        else if (!retrakUser.SkipUnwatchedImportFromReTrak)
                        {
                            userData.Played = false;
                            userData.PlayCount = 0;
                            userData.LastPlayedDate = null;
                            changed = true;
                        }

                        // only process if changed
                        if (changed)
                        {

                            _userDataManager.SaveUserData(
                                user.InternalId,
                                episode,
                                userData,
                                UserDataSaveReason.Import,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        _logger.Debug("No Season match in Watched shows list " + GetVerboseEpisodeData(episode));
                    }
                }
                else
                {
                    _logger.Debug("No Show match in Watched shows list " + GetVerboseEpisodeData(episode));
                }

                var playbackEpisode = Match.FindMatch(episode, retrakPlaybackEpisodes);
                if (playbackEpisode != null)
                {
                    _logger.Debug("Episode is in Playback list " + GetVerboseEpisodeData(episode));

                    UpdatePositionTicksFromReTrak(episode, user, playbackEpisode.progress, playbackEpisode.paused_at);
                }
                else
                {
                    UpdatePositionTicksFromReTrak(episode, user, 0, DateTime.Now);
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            // _logger.Info(syncItemFailures + " items not parsed");
        }

        private void UpdatePositionTicksFromReTrak(BaseItem item, User user, double progress, DateTime paused_at)
        {
            var runtimeTicks = item.RunTimeTicks;

            // Not much we can do here? 
            if (!runtimeTicks.HasValue)
            {
                return;
            }

            var userData = _userDataManager.GetUserData(user, item);

            long playbackPositionTicks = 0;
            if (progress > 0)
            {
                userData.LastPlayedDate = paused_at;

                playbackPositionTicks = Convert.ToInt64(runtimeTicks.Value * (progress / 100));
            }

            if (userData.PlaybackPositionTicks != playbackPositionTicks)
            {
                _userDataManager.UpdatePlayState(item, userData, playbackPositionTicks);

                _userDataManager.SaveUserData(
                       user,
                       item,
                       userData,
                       UserDataSaveReason.PlaybackProgress,
                       CancellationToken.None);
            }
        }

        private static string GetVerboseEpisodeData(Episode episode)
        {
            var episodeString = new StringBuilder();
            episodeString.Append("Episode: ");
            episodeString.Append(episode.ParentIndexNumber != null ? episode.ParentIndexNumber.Value.ToString(CultureInfo.InvariantCulture) : "null");
            episodeString.Append("x");
            episodeString.Append(episode.IndexNumber != null ? episode.IndexNumber.Value.ToString(CultureInfo.InvariantCulture) : "null");
            episodeString.Append(" '").Append(episode.Name).Append("' ");
            episodeString.Append("Series: '");
            episodeString.Append(episode.Series != null
                       ? !string.IsNullOrWhiteSpace(episode.Series.Name) ? episode.Series.Name : "null property"
                       : "null class");
            episodeString.Append("'");

            return episodeString.ToString();
        }

        public string Key => "ReTrakSyncFromReTrakTask";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new List<TaskTriggerInfo>();


        public string Name => "Import playstates from ReTrak.tv";

        public string Description => "Sync Watched/Unwatched status from ReTrak.tv for each Emby user that has a configured ReTrak account";

        public string Category => "ReTrak";
    }
}