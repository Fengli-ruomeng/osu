// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayPreparedMappool
    {
        public required LeaguePlayMappool Source { get; init; }

        public required IReadOnlyList<LeaguePlayPreparedMappoolItem> Items { get; init; }

        public LeaguePlayPreparedMappoolItem Tiebreaker => Items.Single(item => item.Source.ModPool == LeaguePlayModPool.Tiebreaker);
    }
}
