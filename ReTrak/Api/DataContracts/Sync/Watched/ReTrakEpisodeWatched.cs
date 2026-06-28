using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched
{
    public class ReTrakEpisodeWatched : ReTrakEpisode
    {
        public string watched_at { get; set; }
    }
}