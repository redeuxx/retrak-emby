using System.Collections.Generic;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings
{
    public class ReTrakShowRated : ReTrakRated
    {
        public string title { get; set; }

        public int? year { get; set; }

        public ReTrakShowId ids { get; set; }

        public List<ReTrakSeasonRated> seasons { get; set; }

        public class ReTrakSeasonRated : ReTrakRated
        {
            public int? number { get; set; }

            public List<ReTrakEpisodeRated> episodes { get; set; }
        }
    }
}