
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakPersonId : ReTrakId
    {
        public string imdb { get; set; }

        public int? tmdb { get; set; }

        public int? tvrage { get; set; }
    }
}