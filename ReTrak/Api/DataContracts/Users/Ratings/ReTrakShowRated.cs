
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Ratings
{
    
    public class ReTrakShowRated : ReTrakRated
    {
        public ReTrakShow show { get; set; }
    }
}