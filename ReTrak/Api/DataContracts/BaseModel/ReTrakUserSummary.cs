
namespace ReTrak.Api.DataContracts.BaseModel
{
    public class ReTrakUserSummary
    {
        public string username { get; set; }

        public string name { get; set; }

        public bool vip { get; set; }

        public bool @private { get; set; }
    }
}