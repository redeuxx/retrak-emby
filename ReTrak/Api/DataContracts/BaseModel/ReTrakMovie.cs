
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakMovie
    {
        public string title { get; set; }

        public int? year { get; set; }

        public ReTrakMovieId ids { get; set; }
    }
}