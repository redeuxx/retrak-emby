using System;
using System.Linq;
using MediaBrowser.Controller.Entities;
using ReTrak.Model;

namespace ReTrak.Helpers
{
    internal static class UserHelper
    {
        public static ReTrakUser GetReTrakUser(User user)
        {
            return GetReTrakUser(user.Id);
        }

        public static ReTrakUser GetReTrakUser(string userId)
        {
            return GetReTrakUser(new Guid(userId));
        }

        public static ReTrakUser GetReTrakUser(Guid userGuid)
        {
            if (Plugin.Instance.PluginConfiguration.ReTrakUsers == null)
            {
                return null;
            }

            return Plugin.Instance.PluginConfiguration.ReTrakUsers.FirstOrDefault(tUser =>
            {
                if (string.IsNullOrWhiteSpace(tUser.LinkedMbUserId))
                {
                    return false;
                }
                if (string.IsNullOrWhiteSpace(tUser.AccessToken) && string.IsNullOrWhiteSpace(tUser.RefreshToken) && string.IsNullOrWhiteSpace(tUser.PIN))
                {
                    return false;
                }

                Guid retrakUserGuid;
                if (Guid.TryParse(tUser.LinkedMbUserId, out retrakUserGuid) && retrakUserGuid.Equals(userGuid))
                {
                    return true;
                }

                return false;
            });
        }
    }
}
