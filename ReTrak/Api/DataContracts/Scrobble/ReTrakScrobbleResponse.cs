using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Scrobble
{
    public class ReTrakScrobbleResponse
    {
        public string action { get; set; }

        public float progress { get; set; }

        public SocialMedia sharing { get; set; }

        public class SocialMedia
        {
            public bool facebook { get; set; }

            public bool twitter { get; set; }

            public bool tumblr { get; set; }
        }

        public ReTrakMovie movie { get; set; }

        public ReTrakEpisode episode { get; set; }

        public ReTrakShow show { get; set; }
    }
}