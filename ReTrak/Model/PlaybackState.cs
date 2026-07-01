using System;

namespace ReTrak.Model
{
    internal sealed class PlaybackState
    {
        public bool IsPaused { get; set; }

        public long PlaybackPositionTicks { get; set; }

        public DateTime PlaybackTime { get; set; } = DateTime.UtcNow;
    }
}
