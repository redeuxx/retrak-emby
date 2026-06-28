using System.Collections.Generic;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Collection
{
    public class ReTrakShowCollected : ReTrakShow
    {
        public List<ReTrakSeasonCollected> seasons { get; set; }

        public class ReTrakSeasonCollected
        {
            public int number { get; set; }

            public List<ReTrakEpisodeCollected> episodes { get; set; }
        }
    }
}