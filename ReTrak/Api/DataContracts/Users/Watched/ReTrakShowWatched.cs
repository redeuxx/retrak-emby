using System.Collections.Generic;

using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Watched
{
    
    public class ReTrakShowWatched
    {
        public int plays { get; set; }

        public string last_watched_at { get; set; }

        public ReTrakShow show { get; set; }

        public List<Season> seasons { get; set; }

        
        public class Season
        {
            public int number { get; set; }

            public List<Episode> episodes { get; set; }

            
            public class Episode
            {
                public int number { get; set; }

                public int plays { get; set; }
            }
        }
    }
}