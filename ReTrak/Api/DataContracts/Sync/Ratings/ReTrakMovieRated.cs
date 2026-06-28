using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Sync.Ratings
{
    public class ReTrakMovieRated : ReTrakRated
    {
        public string title { get; set; }

        public int? year { get; set; }

        public ReTrakMovieId ids { get; set; }
    }
}