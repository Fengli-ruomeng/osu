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
    /// Creates a hypothetical osu!standard score which can be passed to a ruleset's performance calculator.
    /// </summary>
    public static class OsuPerformanceCalculationScenario
    {
        public static int GetTotalJudgements(IReadOnlyDictionary<HitResult, int> maximumStatistics)
            => maximumStatistics.GetValueOrDefault(HitResult.Great);

        public static OsuPerformanceCalculationInputError Validate(OsuPerformanceCalculationInput input, DifficultyAttributes attributes,
                                                                    IReadOnlyDictionary<HitResult, int> maximumStatistics)
        {
            if (input.CountGreat < 0 || input.CountOk < 0 || input.CountMeh < 0 || input.CountMiss < 0)
                return OsuPerformanceCalculationInputError.NegativeJudgementCount;

            long enteredJudgements = (long)input.CountGreat + input.CountOk + input.CountMeh + input.CountMiss;

            if (enteredJudgements != GetTotalJudgements(maximumStatistics))
                return OsuPerformanceCalculationInputError.JudgementCountMismatch;

            if (input.MaxCombo < 0 || input.MaxCombo > attributes.MaxCombo)
                return OsuPerformanceCalculationInputError.ComboOutOfRange;

            int maximumSliderTicks = maximumStatistics.GetValueOrDefault(HitResult.LargeTickHit);
            if (input.CountSliderTickMiss < 0 || input.CountSliderTickMiss > maximumSliderTicks)
                return OsuPerformanceCalculationInputError.SliderTickMissOutOfRange;

            int maximumSliderTails = maximumStatistics.GetValueOrDefault(HitResult.SliderTailHit);
            if (input.CountSliderTailDropped < 0 || input.CountSliderTailDropped > maximumSliderTails)
                return OsuPerformanceCalculationInputError.SliderTailDroppedOutOfRange;

            return OsuPerformanceCalculationInputError.None;
        }

        public static ScoreInfo CreateScore(BeatmapInfo beatmapInfo, RulesetInfo rulesetInfo, IReadOnlyList<Mod> mods, DifficultyAttributes attributes,
                                            IReadOnlyDictionary<HitResult, int> maximumStatistics, ScoreProcessor scoreProcessor,
                                            OsuPerformanceCalculationInput input)
        {
            var error = Validate(input, attributes, maximumStatistics);
            if (error != OsuPerformanceCalculationInputError.None)
                throw new ArgumentException($"The performance calculation input is invalid: {error}.", nameof(input));

            int totalJudgements = GetTotalJudgements(maximumStatistics);

            // Begin with the maximum statistics so nested slider judgements remain successful in the simple scenario.
            var statistics = new Dictionary<HitResult, int>(maximumStatistics)
            {
                [HitResult.Great] = input.CountGreat,
                [HitResult.Ok] = input.CountOk,
                [HitResult.Meh] = input.CountMeh,
                [HitResult.Miss] = input.CountMiss,
                [HitResult.LargeTickHit] = maximumStatistics.GetValueOrDefault(HitResult.LargeTickHit) - input.CountSliderTickMiss,
                [HitResult.LargeTickMiss] = input.CountSliderTickMiss,
                [HitResult.SliderTailHit] = maximumStatistics.GetValueOrDefault(HitResult.SliderTailHit) - input.CountSliderTailDropped,
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
                Combo = input.MaxCombo,
                MaxCombo = input.MaxCombo,
                Statistics = statistics,
                MaximumStatistics = new Dictionary<HitResult, int>(maximumStatistics),
            };
        }
    }

    public readonly record struct OsuPerformanceCalculationInput(int CountGreat, int CountOk, int CountMeh, int CountMiss, int MaxCombo,
                                                                 int CountSliderTickMiss = 0, int CountSliderTailDropped = 0);

    public enum OsuPerformanceCalculationInputError
    {
        None,
        NegativeJudgementCount,
        JudgementCountMismatch,
        ComboOutOfRange,
        SliderTickMissOutOfRange,
        SliderTailDroppedOutOfRange,
    }
}
