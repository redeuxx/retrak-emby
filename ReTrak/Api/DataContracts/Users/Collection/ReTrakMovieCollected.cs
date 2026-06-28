
using ReTrak.Api.DataContracts.BaseModel;

namespace ReTrak.Api.DataContracts.Users.Collection
{
    
    public class ReTrakMovieCollected
    {
        public string collected_at { get; set; }

        public ReTrakMetadata metadata { get; set; }

        public ReTrakMovie movie { get; set; }
    }
}