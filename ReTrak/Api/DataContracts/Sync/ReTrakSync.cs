using System.Collections.Generic;
using ReTrak.Api.DataContracts.BaseModel;
using ReTrak.Api.DataContracts.Sync.Collection;
using ReTrak.Api.DataContracts.Sync.Ratings;
using ReTrak.Api.DataContracts.Sync.Watched;

namespace ReTrak.Api.DataContracts.Sync
{
    public class ReTrakSync<TMovie, TShow, TEpisode>
    {
        public List<TMovie> movies { get; set; }

        public List<TShow> shows { get; set; }

        public List<TEpisode> episodes { get; set; }
    }

    public class ReTrakSyncRated : ReTrakSync<ReTrakMovieRated, ReTrakShowRated, ReTrakEpisodeRated>
    {
    }

    public class ReTrakSyncWatched : ReTrakSync<ReTrakMovieWatched, ReTrakShowWatched, ReTrakEpisodeWatched>
    {
    }

    public class ReTrakSyncCollected : ReTrakSync<ReTrakMovieCollected, ReTrakShowCollected, ReTrakEpisodeCollected>
    {
    }

    public class ReTrakSyncUncollected : ReTrakSync<ReTrakMovie, ReTrakShowCollected, ReTrakEpisodeCollected>
    {
    }
}