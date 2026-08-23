// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Replays
{
    /// <summary>
    /// Generates an autoplay replay with human-like timing variance and optional deliberate misses.
    /// All resulting judgements and score values are produced by normal gameplay processing.
    /// </summary>
    public class OsuLeagueAutoGenerator : OsuAutoGenerator
    {
        private readonly Random random;
        private readonly double minimumUnstableRate;
        private readonly double maximumUnstableRate;
        private readonly double missChance;

        public OsuLeagueAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods, double minimumUnstableRate, double maximumUnstableRate, double missChance, int seed)
            : base(beatmap, mods)
        {
            this.minimumUnstableRate = Math.Max(0, Math.Min(minimumUnstableRate, maximumUnstableRate));
            this.maximumUnstableRate = Math.Max(0, Math.Max(minimumUnstableRate, maximumUnstableRate));
            this.missChance = Math.Clamp(missChance, 0, 1);
            random = new Random(seed);
        }

        protected override HitAttempt CreateHitAttempt(OsuHitObject hitObject)
        {
            bool miss = random.NextDouble() < missChance;
            double unstableRate = minimumUnstableRate + random.NextDouble() * (maximumUnstableRate - minimumUnstableRate);

            // Unstable rate is ten times the standard deviation of hit errors in milliseconds.
            double timeOffset = nextGaussian() * unstableRate / 10;
            return new HitAttempt(miss, timeOffset);
        }

        private double nextGaussian()
        {
            double first = 1 - random.NextDouble();
            double second = 1 - random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(first)) * Math.Cos(2 * Math.PI * second);
        }
    }
}
