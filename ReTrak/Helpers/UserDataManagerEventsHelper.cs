using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using ReTrak.Api;
using ReTrak.Model;
using System.Threading.Tasks;

namespace ReTrak.Helpers
{
    /// <summary>
    /// Helper class used to update the watched status of movies/episodes. Attempts to organise
    /// requests to lower retrak.tv api calls.
    /// </summary>
    internal class UserDataManagerEventsHelper
    {
        private List<UserDataPackage> _userDataPackages;
        private readonly ILogger _logger;
        private readonly ReTrakApi _retrakApi;
        private Timer _timer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="retrakApi"></param>
        public UserDataManagerEventsHelper(ILogger logger, ReTrakApi retrakApi)
        {
            _userDataPackages = new List<UserDataPackage>();
            _logger = logger;
            _retrakApi = retrakApi;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="userDataSaveEventArgs"></param>
        /// <param name="retrakUser"></param>
        public async Task ProcessUserDataSaveEventArgs(UserDataSaveEventArgs userDataSaveEventArgs, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            var userPackage = _userDataPackages.FirstOrDefault(e => e.ReTrakUser.Equals(retrakUser));

            if (userPackage == null)
            {
                userPackage = new UserDataPackage { ReTrakUser = retrakUser };
                _userDataPackages.Add(userPackage);
            }


            if (_timer == null)
            {
                _timer = new Timer(OnTimerCallback, null, TimeSpan.FromMilliseconds(5000),
                    Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer.Change(TimeSpan.FromMilliseconds(5000), Timeout.InfiniteTimeSpan);
            }

            var movie = userDataSaveEventArgs.Item as Movie;

            if (movie != null)
            {
                if (userDataSaveEventArgs.UserData.Played)
                {
                    userPackage.SeenMovies.Add(movie);

                    if (userPackage.SeenMovies.Count >= 100)
                    {
                        await _retrakApi.SendMoviePlaystateUpdates(userPackage.SeenMovies, userPackage.ReTrakUser, true, true,
                                                            cancellationToken).ConfigureAwait(false);
                        userPackage.SeenMovies = new List<Movie>();
                    }

                    await MovieStatusUpdate(movie, userPackage.ReTrakUser, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    userPackage.UnSeenMovies.Add(movie);

                    if (userPackage.UnSeenMovies.Count >= 100)
                    {
                        await _retrakApi.SendMoviePlaystateUpdates(userPackage.UnSeenMovies, userPackage.ReTrakUser, true, false,
                                                            cancellationToken).ConfigureAwait(false);
                        userPackage.UnSeenMovies = new List<Movie>();
                    }
                }

                return;
            }

            var episode = userDataSaveEventArgs.Item as Episode;

            if (episode == null) return;

            // If it's not the series we're currently storing, upload our episodes and reset the arrays
            if (!userPackage.CurrentSeriesId.Equals(episode.Series.Id))
            {
                if (userPackage.SeenEpisodes.Any())
                {
                    await _retrakApi.SendEpisodePlaystateUpdates(userPackage.SeenEpisodes, userPackage.ReTrakUser, true, true,
                                                          cancellationToken).ConfigureAwait(false);
                    userPackage.SeenEpisodes = new List<Episode>();
                }

                if (userPackage.UnSeenEpisodes.Any())
                {
                    await _retrakApi.SendEpisodePlaystateUpdates(userPackage.UnSeenEpisodes, userPackage.ReTrakUser, true, false,
                                                          cancellationToken).ConfigureAwait(false);
                    userPackage.UnSeenEpisodes = new List<Episode>();
                }

                userPackage.CurrentSeriesId = episode.Series.Id;
            }

            if (userDataSaveEventArgs.UserData.Played)
            {
                userPackage.SeenEpisodes.Add(episode);

                await EpisodeStatusUpdate(episode, retrakUser, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                userPackage.UnSeenEpisodes.Add(episode);
            }
        }

        private void OnTimerCallback(object state)
        {
            foreach (var package in _userDataPackages)
            {

                if (package.UnSeenMovies.Any())
                {
                    var movies = package.UnSeenMovies.ToList();
                    package.UnSeenMovies.Clear();
                    _retrakApi.SendMoviePlaystateUpdates(movies, package.ReTrakUser, true, false,
                        CancellationToken.None).ConfigureAwait(false);
                }
                if (package.SeenMovies.Any())
                {
                    var movies = package.SeenMovies.ToList();
                    package.SeenMovies.Clear();
                    _retrakApi.SendMoviePlaystateUpdates(movies, package.ReTrakUser, true, true,
                        CancellationToken.None).ConfigureAwait(false);
                }
                if (package.UnSeenEpisodes.Any())
                {
                    var episodes = package.UnSeenEpisodes.ToList();
                    package.UnSeenEpisodes.Clear();
                    _retrakApi.SendEpisodePlaystateUpdates(episodes, package.ReTrakUser, true, false,
                        CancellationToken.None).ConfigureAwait(false);
                }
                if (package.SeenEpisodes.Any())
                {
                    var episodes = package.SeenEpisodes.ToList();
                    package.SeenEpisodes.Clear();
                    _retrakApi.SendEpisodePlaystateUpdates(episodes, package.ReTrakUser, true, true,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        private async Task MovieStatusUpdate(Movie movie, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            var retrakPlaybackMovies = await _retrakApi.SendGetPlaybackMoviesRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            var playbackMovie = Match.FindMatch(movie, retrakPlaybackMovies);
            if (playbackMovie != null)
            {
                try
                {
                    await _retrakApi.SendMovieStatusUpdateAsync(movie, MediaStatus.Stop, retrakUser, 100, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Exception handled sending status update", ex);
                }
            }
        }

        private async Task EpisodeStatusUpdate(Episode episode, ReTrakUser retrakUser, CancellationToken cancellationToken)
        {
            var retrakPlaybackEpisodes = await _retrakApi.SendGetPlaybackShowsRequest(retrakUser, cancellationToken).ConfigureAwait(false);
            var playbackEpisode = Match.FindMatch(episode, retrakPlaybackEpisodes);
            if (playbackEpisode != null)
            {
                try
                {
                    await _retrakApi.SendEpisodeStatusUpdateAsync(episode, MediaStatus.Stop, retrakUser, 100, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Exception handled sending status update", ex);
                }
            }
        }
    }



    /// <summary>
    /// Class that contains all the items to be reported to retrak.tv and supporting properties. 
    /// </summary>
    internal class UserDataPackage
    {
        public ReTrakUser ReTrakUser;
        public Guid CurrentSeriesId;
        public List<Movie> SeenMovies;
        public List<Movie> UnSeenMovies;
        public List<Episode> SeenEpisodes;
        public List<Episode> UnSeenEpisodes;

        public UserDataPackage()
        {
            SeenMovies = new List<Movie>();
            UnSeenMovies = new List<Movie>();
            SeenEpisodes = new List<Episode>();
            UnSeenEpisodes = new List<Episode>();
        }
    }
}
