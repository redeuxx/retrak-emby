using System.Collections.Generic;

using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Collection
{
    
    public class ReTrakShowCollected
    {
        public string last_collected_at { get; set; }

        public ReTrakShow show { get; set; }

        public List<ReTrakSeasonCollected> seasons { get; set; }

        
        public class ReTrakSeasonCollected
        {
            public int number { get; set; }

            public List<ReTrakEpisodeCollected> episodes { get; set; }

            
            public class ReTrakEpisodeCollected
            {
                public int number { get; set; }

                public string collected_at { get; set; }

                public ReTrakMetadata metadata { get; set; }
            }
        }
    }
}