using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings
{
    public class ReTrakEpisodeRated : ReTrakRated
    {
        public int? number { get; set; }

        public ReTrakEpisodeId ids { get; set; }
    }
}