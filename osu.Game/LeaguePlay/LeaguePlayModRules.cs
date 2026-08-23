// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.LeaguePlay
{
    public static class LeaguePlayModRules
    {
        public static string[] GetRequiredAcronyms(LeaguePlayModPool pool) => pool switch
        {
            LeaguePlayModPool.Hidden => new[] { "NF", "HD" },
            LeaguePlayModPool.HardRock => new[] { "NF", "HR" },
            LeaguePlayModPool.DoubleTime => new[] { "NF", "DT" },
            _ => new[] { "NF" },
        };

        public static Mod[] CreateRequiredMods(RulesetInfo rulesetInfo, LeaguePlayModPool pool)
        {
            Ruleset ruleset = rulesetInfo.CreateInstance();

            return GetRequiredAcronyms(pool)
                   .Select(ruleset.CreateModFromAcronym)
                   .OfType<Mod>()
                   .ToArray();
        }

        public static Mod CreateLeagueAutoplay(RulesetInfo rulesetInfo)
        {
            if (rulesetInfo.OnlineID != 0)
                throw new NotSupportedException("League Autoplay currently supports osu!standard only.");

            Ruleset ruleset = rulesetInfo.CreateInstance();
            Mod mod = new APIMod
            {
                Acronym = "LAT",
                Settings =
                {
                    ["real_time"] = true,
                    ["minimum_unstable_rate"] = 110d,
                    ["maximum_unstable_rate"] = 160d,
                    ["miss_chance"] = 0.015d,
                },
            }.ToMod(ruleset);

            if (mod is UnknownMod)
                throw new InvalidOperationException("The osu!standard ruleset does not provide the League Autoplay mod.");

            return mod;
        }
    }
}
