using System.Collections.Generic;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched
{
    public class ReTrakSeasonWatched : ReTrakSeason
    {
        public string watched_at { get; set; }

        public List<ReTrakEpisodeWatched> episodes { get; set; }
    }
}
