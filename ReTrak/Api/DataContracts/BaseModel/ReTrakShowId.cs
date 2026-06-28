
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakShowId : ReTrakId
    {
        public string imdb { get; set; }

        public int? tmdb { get; set; }

        public int? tvdb { get; set; }

        public int? tvrage { get; set; }
    }
}