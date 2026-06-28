
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings
{
    
    public class ReTrakEpisodeRated : ReTrakRated
    {
        public ReTrakEpisode episode { get; set; }
    }
}