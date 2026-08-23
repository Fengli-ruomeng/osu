// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayPreparedMappoolItem
    {
        public required LeaguePlayMappoolItem Source { get; init; }

        public required BeatmapInfo Beatmap { get; init; }
    }
}
