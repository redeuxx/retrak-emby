
namespace ReTrak.Api.DataContracts.BaseModel
{
    public abstract class ReTrakRated
    {
        public int? rating { get; set; }

        public string rated_at { get; set; }
    }
}