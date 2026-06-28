using System;
using System.Collections.Generic;
using System.Text;
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Playback
{
    public class ReTrakPlaybackEpisode
    {
        public ReTrakEpisode episode { get; set; }

        public float progress { get; set; }

        public DateTime paused_at { get; set; }
    }
}
