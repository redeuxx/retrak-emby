using MediaBrowser.Model.Plugins;
using ReTrak.Model;

namespace ReTrak.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            ReTrakUsers = new ReTrakUser[] {};
            ReTrakUrl = "https://retrak.tv";
        }

        public ReTrakUser[] ReTrakUsers { get; set; }
        public string ReTrakUrl { get; set; }
    }
}
