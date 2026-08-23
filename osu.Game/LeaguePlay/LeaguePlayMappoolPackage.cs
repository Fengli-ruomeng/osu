// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayMappoolPackage
    {
        /// <summary>
        /// The directory containing this package, relative to the League Play mappool root.
        /// </summary>
        public required string Directory { get; init; }

        public required LeaguePlayMappool Mappool { get; init; }
    }
}
