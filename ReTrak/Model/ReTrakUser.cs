using System;

namespace ReTrak.Model
{
    public class ReTrakUser
    {
        public String PIN { get; set; }
        
        public String AccessToken { get; set; }

        public String RefreshToken { get; set; }

        public String LinkedMbUserId { get; set; }

        public bool UsesAdvancedRating { get; set; }

        public bool  SkipUnwatchedImportFromReTrak { get; set; }

        public bool PostWatchedHistory { get; set; }

        public bool SyncCollection { get; set; }

        public bool ExtraLogging { get; set; }

        public bool ExportMediaInfo { get; set; }

        public String[] LocationsExcluded { get; set; }
        public DateTimeOffset AccessTokenExpiration { get; set; }

        public ReTrakUser()
        {
            SkipUnwatchedImportFromReTrak = true;
            PostWatchedHistory = true;
        }
    }
}
