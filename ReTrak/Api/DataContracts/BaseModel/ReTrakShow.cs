
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakShow
    {
        public string title { get; set; }

        public int? year { get; set; }

        public ReTrakShowId ids { get; set; }
    }
}