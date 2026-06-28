
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings
{
    
    public class ReTrakMovieRated : ReTrakRated
    {
        public ReTrakMovie movie { get; set; }
    }
}