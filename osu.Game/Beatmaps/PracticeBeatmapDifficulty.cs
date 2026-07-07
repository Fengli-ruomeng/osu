// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Beatmaps
{
    public static class PracticeBeatmapDifficulty
    {
        public const double MinimumRemainingGameplayTime = 5000;

        public static double GetMaximumStartTime(IBeatmap beatmap)
            => beatmap.HitObjects.Count > 0 ? Math.Max(0, beatmap.GetLastObjectTime() - MinimumRemainingGameplayTime) : 0;

        public static double ClampStartTime(IBeatmap beatmap, double startTime)
            => Math.Clamp(startTime, 0, GetMaximumStartTime(beatmap));

        public static IBeatmap CreatePracticeBeatmap(IWorkingBeatmap beatmap, IRulesetInfo ruleset, IReadOnlyList<Mod> mods, double startTime, CancellationToken cancellationToken = default)
        {
            var playable = beatmap.GetPlayableBeatmap(ruleset, mods, cancellationToken);
            ApplyStart(playable, startTime);
            return playable;
        }

        public static double ApplyStart(IBeatmap beatmap, double startTime)
        {
            double effectiveStartTime = ClampStartTime(beatmap, startTime);

            if (beatmap.GetType().GetProperty(nameof(Beatmap.HitObjects))?.GetValue(beatmap) is not IList mutableHitObjects)
                return effectiveStartTime;

            for (int i = mutableHitObjects.Count - 1; i >= 0; i--)
            {
                if (mutableHitObjects[i] is HitObject hitObject && hitObject.GetEndTime() < effectiveStartTime)
                    mutableHitObjects.RemoveAt(i);
            }

            return effectiveStartTime;
        }

        public static StarDifficulty Calculate(IWorkingBeatmap beatmap, Ruleset ruleset, IReadOnlyList<Mod> mods, double startTime, CancellationToken cancellationToken = default)
        {
            var clonedMods = mods.Select(m => m.DeepClone()).ToArray();
            var practiceBeatmap = CreatePracticeBeatmap(beatmap, ruleset.RulesetInfo, clonedMods, startTime, cancellationToken);

            return Calculate(practiceBeatmap, ruleset, clonedMods, cancellationToken);
        }

        public static StarDifficulty Calculate(IBeatmap practiceBeatmap, Ruleset ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken = default)
        {
            var clonedMods = mods.Select(m => m.DeepClone()).ToArray();
            var workingBeatmap = new PracticeWorkingBeatmap(practiceBeatmap);

            var difficulty = ruleset.CreateDifficultyCalculator(workingBeatmap).Calculate(clonedMods, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var performanceCalculator = ruleset.CreatePerformanceCalculator();
            if (performanceCalculator == null)
                return new StarDifficulty(difficulty, new PerformanceAttributes());

            ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = clonedMods;
            scoreProcessor.ApplyBeatmap(practiceBeatmap);
            cancellationToken.ThrowIfCancellationRequested();

            var perfectScore = new ScoreInfo(practiceBeatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = clonedMods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics
            };

            var performance = performanceCalculator.Calculate(perfectScore, difficulty);
            cancellationToken.ThrowIfCancellationRequested();

            return new StarDifficulty(difficulty, performance);
        }

        private class PracticeWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap beatmap;

            public PracticeWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap.BeatmapInfo, null)
            {
                this.beatmap = beatmap;
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => beatmap;

            protected override IBeatmap GetBeatmap() => beatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
