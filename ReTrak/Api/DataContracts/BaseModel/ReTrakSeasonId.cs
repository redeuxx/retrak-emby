
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakSeasonId : ReTrakId
    {
        public int? tmdb { get; set; }

        public int? tvdb { get; set; }

        public int? tvrage { get; set; }
    }
}