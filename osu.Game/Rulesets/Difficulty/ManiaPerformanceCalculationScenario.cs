// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// Creates a hypothetical osu!mania score which can be passed to a ruleset's performance calculator.
    /// </summary>
    public static class ManiaPerformanceCalculationScenario
    {
        public static int GetTotalJudgements(IReadOnlyDictionary<HitResult, int> maximumStatistics)
            => maximumStatistics.GetValueOrDefault(HitResult.Perfect);

        public static ManiaPerformanceCalculationInputError Validate(ManiaPerformanceCalculationInput input,
                                                                      IReadOnlyDictionary<HitResult, int> maximumStatistics)
        {
            if (input.CountPerfect < 0 || input.CountGreat < 0 || input.CountGood < 0 || input.CountOk < 0 || input.CountMeh < 0 || input.CountMiss < 0)
                return ManiaPerformanceCalculationInputError.NegativeJudgementCount;

            long enteredJudgements = (long)input.CountPerfect + input.CountGreat + input.CountGood + input.CountOk + input.CountMeh + input.CountMiss;

            return enteredJudgements == GetTotalJudgements(maximumStatistics)
                ? ManiaPerformanceCalculationInputError.None
                : ManiaPerformanceCalculationInputError.JudgementCountMismatch;
        }

        public static ScoreInfo CreateScore(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IReadOnlyList<Mod> mods, DifficultyAttributes attributes,
                                            IReadOnlyDictionary<HitResult, int> maximumStatistics, ScoreProcessor scoreProcessor,
                                            ManiaPerformanceCalculationInput input)
        {
            var error = Validate(input, maximumStatistics);
            if (error != ManiaPerformanceCalculationInputError.None)
                throw new ArgumentException($"The performance calculation input is invalid: {error}.", nameof(input));

            var statistics = new Dictionary<HitResult, int>(maximumStatistics)
            {
                [HitResult.Perfect] = input.CountPerfect,
                [HitResult.Great] = input.CountGreat,
                [HitResult.Good] = input.CountGood,
                [HitResult.Ok] = input.CountOk,
                [HitResult.Meh] = input.CountMeh,
                [HitResult.Miss] = input.CountMiss,
            };

            int achievedBaseScore = statistics.Where(kvp => kvp.Key.AffectsAccuracy())
                                              .Sum(kvp => kvp.Value * scoreProcessor.GetBaseScoreForResult(kvp.Key));
            int maximumBaseScore = maximumStatistics.Where(kvp => kvp.Key.AffectsAccuracy())
                                                     .Sum(kvp => kvp.Value * scoreProcessor.GetBaseScoreForResult(kvp.Key));
            double accuracy = maximumBaseScore == 0 ? 1 : achievedBaseScore / (double)maximumBaseScore;

            return new ScoreInfo(beatmapInfo, rulesetInfo)
            {
                Passed = true,
                Accuracy = accuracy,
                Mods = mods.Select(m => m.DeepClone()).ToArray(),
                Combo = attributes.MaxCombo,
                MaxCombo = attributes.MaxCombo,
                Statistics = statistics,
                MaximumStatistics = new Dictionary<HitResult, int>(maximumStatistics),
            };
        }
    }

    public readonly record struct ManiaPerformanceCalculationInput(int CountPerfect, int CountGreat, int CountGood, int CountOk, int CountMeh, int CountMiss);

    public enum ManiaPerformanceCalculationInputError
    {
        None,
        NegativeJudgementCount,
        JudgementCountMismatch,
    }
}
