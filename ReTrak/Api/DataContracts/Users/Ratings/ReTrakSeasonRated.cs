
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings
{
    
    public class ReTrakSeasonRated : ReTrakRated
    {
        public ReTrakSeason season { get; set; }
    }
}