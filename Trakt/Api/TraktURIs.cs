using System;

namespace Trakt.Api
{
    public static class TraktUris
    {
        public const string Id = "c44548028dcd8f31e9bee55318562e6e5deb8524f5ca3e77e167fd3b1c9ce380";
        public const string Secret = "d453bc07bcf42f72e3915715a5275d99de8381ff007c84d20e89ed1070310c89";

        private static string BaseUrl
        {
            get
            {
                var configUrl = Plugin.Instance?.PluginConfiguration?.ReTrakUrl;
                if (string.IsNullOrWhiteSpace(configUrl))
                {
                    return "https://retrak.tv/api";
                }
                configUrl = configUrl.Trim().TrimEnd('/');
                if (!configUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    configUrl += "/api";
                }
                return configUrl;
            }
        }

        #region POST URI's

        public static string Token => $"{BaseUrl}/oauth/token";

        public static string SyncCollectionAdd => $"{BaseUrl}/sync/collection";
        public static string SyncCollectionRemove => $"{BaseUrl}/sync/collection/remove";
        public static string SyncWatchedHistoryAdd => $"{BaseUrl}/sync/history";
        public static string SyncWatchedHistoryRemove => $"{BaseUrl}/sync/history/remove";
        public static string SyncRatingsAdd => $"{BaseUrl}/sync/ratings";

        public static string ScrobbleStart => $"{BaseUrl}/scrobble/start";
        public static string ScrobblePause => $"{BaseUrl}/scrobble/pause";
        public static string ScrobbleStop => $"{BaseUrl}/scrobble/stop";
        #endregion

        #region GET URI's

        public static string WatchedMovies => $"{BaseUrl}/sync/watched/movies";
        public static string WatchedShows => $"{BaseUrl}/sync/watched/shows";
        public static string CollectedMovies => $"{BaseUrl}/sync/collection/movies?extended=metadata";
        public static string CollectedShows => $"{BaseUrl}/sync/collection/shows?extended=metadata";
        public static string PlaybackMovies => $"{BaseUrl}/sync/playback/movies";
        public static string PlaybackShows => $"{BaseUrl}/sync/playback/episodes";

        // Recommendations
        public static string RecommendationsMovies => $"{BaseUrl}/recommendations/movies";
        public static string RecommendationsShows => $"{BaseUrl}/recommendations/shows";

        #endregion

        #region DELETE 

        // Recommendations
        public static string RecommendationsMoviesDismiss => $"{BaseUrl}/recommendations/movies/{{0}}";
        public static string RecommendationsShowsDismiss => $"{BaseUrl}/recommendations/shows/{{0}}";

        #endregion
    }
}
