using System;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;
using ReTrak.Helpers;
using System.Threading;

namespace ReTrak.Api
{
    /// <summary>
    /// 
    /// </summary>
    [Route("/ReTrak/Users/{UserId}/Items/{Id}/Rate", "POST")]
    public class RateItem
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string Id { get; set; }

        [ApiMember(Name = "Rating", Description = "Rating between 1 - 10 (0 = unrate)", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int Rating { get; set; }
        
    }



    /// <summary>
    /// 
    /// </summary>
    [Route("/ReTrak/Users/{UserId}/Items/{Id}/Comment", "POST")]
    public class CommentItem
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public Guid Id { get; set; }

        [ApiMember(Name = "Comment", Description = "Text for the comment", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
        public string Comment { get; set; }

        [ApiMember(Name = "Spoiler", Description = "Set to true to indicate the comment contains spoilers. Defaults to false", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool Spoiler { get; set; }

        [ApiMember(Name = "Review", Description = "Set to true to indicate the comment is a 200+ word review. Defaults to false", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool Review { get; set; }
    }



    /// <summary>
    /// 
    /// </summary>
    [Route("/ReTrak/Users/{UserId}/RecommendedMovies", "POST")]
    public class RecommendedMovies
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Genre", Description = "Genre slug to filter by. (See http://retrak.tv/api-docs/genres-movies)", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public int Genre { get; set; }

        [ApiMember(Name = "StartYear", Description = "4-digit year to filter movies released this year or later", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int StartYear { get; set; }

        [ApiMember(Name = "EndYear", Description = "4-digit year to filter movies released this year or earlier", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int EndYear { get; set; }

        [ApiMember(Name = "HideCollected", Description = "Set true to hide movies in the users collection", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideCollected { get; set; }

        [ApiMember(Name = "HideWatchlisted", Description = "Set true to hide movies in the users watchlist", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideWatchlisted { get; set; }
    }



    /// <summary>
    /// 
    /// </summary>
    [Route("/ReTrak/Users/{UserId}/RecommendedShows", "POST")]
    public class RecommendedShows
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Genre", Description = "Genre slug to filter by. (See http://retrak.tv/api-docs/genres-shows)", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public int Genre { get; set; }

        [ApiMember(Name = "StartYear", Description = "4-digit year to filter shows released this year or later", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int StartYear { get; set; }

        [ApiMember(Name = "EndYear", Description = "4-digit year to filter shows released this year or earlier", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int EndYear { get; set; }

        [ApiMember(Name = "HideCollected", Description = "Set true to hide shows in the users collection", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideCollected { get; set; }

        [ApiMember(Name = "HideWatchlisted", Description = "Set true to hide shows in the users watchlist", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideWatchlisted { get; set; }
    }



    /// <summary>
    /// 
    /// </summary>
    public class ReTrakUriService : IService
    {
        private readonly ReTrakApi _retrakApi;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReTrakUriService"/> class.
        /// </summary>
        /// <param name="retrakApi">The retrak API.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryManager">The library manager.</param>
        public ReTrakUriService(ReTrakApi retrakApi, ILogger logger, ILibraryManager libraryManager)
        {
            _retrakApi = retrakApi;
            _logger = logger;
            _libraryManager = libraryManager;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(RateItem request)
        {
            _logger.Info("RateItem request received");

            var currentItem = _libraryManager.GetItemById(request.Id);

            if (currentItem == null)
            {
                _logger.Info("currentItem is null");
                return null;
            }

            return _retrakApi.SendItemRating(currentItem, request.Rating, UserHelper.GetReTrakUser(request.UserId), CancellationToken.None).Result;
            
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(CommentItem request)
        {
            _logger.Info("CommentItem request received");

            var currentItem = _libraryManager.GetItemById(request.Id);

            return _retrakApi.SendItemComment(currentItem, request.Comment, request.Spoiler,
                                             UserHelper.GetReTrakUser(request.UserId), request.Review).Result;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(RecommendedMovies request)
        {
            return _retrakApi.SendMovieRecommendationsRequest(UserHelper.GetReTrakUser(request.UserId), CancellationToken.None).Result;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(RecommendedShows request)
        {
            return _retrakApi.SendShowRecommendationsRequest(UserHelper.GetReTrakUser(request.UserId), CancellationToken.None).Result;
        }
    }
}
