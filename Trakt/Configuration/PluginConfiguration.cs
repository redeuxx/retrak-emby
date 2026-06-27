using MediaBrowser.Model.Plugins;
using Trakt.Model;

namespace Trakt.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            TraktUsers = new TraktUser[] {};
            ReTrakUrl = "https://retrak.tv";
        }

        public TraktUser[] TraktUsers { get; set; }
        public string ReTrakUrl { get; set; }
    }
}
