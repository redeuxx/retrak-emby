using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Scrobble
{
    public class ReTrakScrobbleMovie
    {
        public ReTrakMovie movie { get; set; }

        public float progress { get; set; }

        public string app_version { get; set; }

        public string app_date { get; set; }
    }
}