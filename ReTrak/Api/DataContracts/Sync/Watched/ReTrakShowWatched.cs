using System.Collections.Generic;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Watched
{
    public class ReTrakShowWatched : ReTrakShow
    {
        public string watched_at { get; set; }

        public List<ReTrakSeasonWatched> seasons { get; set; }
    }
}