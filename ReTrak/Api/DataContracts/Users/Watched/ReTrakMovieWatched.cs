
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Watched
{
    
    public class ReTrakMovieWatched
    {
        public int plays { get; set; }

        public string last_watched_at { get; set; }

        public ReTrakMovie movie { get; set; }
    }
}