using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched
{
    public class ReTrakMovieWatched : ReTrakMovie
    {
        public string watched_at { get; set; }
    }
}