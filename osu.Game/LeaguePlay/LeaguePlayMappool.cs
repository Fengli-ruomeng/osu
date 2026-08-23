// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayMappool
    {
        public required string Name { get; init; }

        public required int FirstTo { get; init; }

        public required int BansPerSide { get; init; }

        public required IReadOnlyList<LeaguePlayMappoolItem> Items { get; init; }

        public LeaguePlayMappoolItem Tiebreaker => Items.Single(item => item.ModPool == LeaguePlayModPool.Tiebreaker);
    }
}
