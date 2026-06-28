
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakEpisode
    {
        public int? season { get; set; }

        public int? number { get; set; }

        public string title { get; set; }

        public ReTrakEpisodeId ids { get; set; }
    }
}