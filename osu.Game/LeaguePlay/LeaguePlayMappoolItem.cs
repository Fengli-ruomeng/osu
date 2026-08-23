// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayMappoolItem
    {
        public required string Slot { get; init; }

        public required LeaguePlayModPool ModPool { get; init; }

        public required int BeatmapSetId { get; init; }

        public int? BeatmapId { get; init; }

        /// <summary>
        /// The path of the matching .osz file, relative to the mappool's storage.
        /// </summary>
        public required string OszPath { get; init; }
    }
}
