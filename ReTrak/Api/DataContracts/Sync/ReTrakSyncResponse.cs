using System.Collections.Generic;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync
{
    public class ReTrakSyncResponse
    {
        public Items added { get; set; }

        public Items deleted { get; set; }

        public Items existing { get; set; }

        public class Items
        {
            public int movies { get; set; }

            public int shows { get; set; }

            public int seasons { get; set; }

            public int episodes { get; set; }

            public int people { get; set; }
        }

        public NotFoundObjects not_found { get; set; }

        public class NotFoundObjects
        {
            public List<ReTrakMovie> movies { get; set; }

            public List<ReTrakShow> shows { get; set; }

            public List<ReTrakEpisode> episodes { get; set; }

            public List<ReTrakSeason> seasons { get; set; }

            public List<ReTrakPerson> people { get; set; }
        }
    }
}