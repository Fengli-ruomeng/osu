// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.Rulesets.Difficulty
{
    [TestFixture]
    public class OsuPerformanceCalculationScenarioTest
    {
        private readonly DifficultyAttributes attributes = new DifficultyAttributes
        {
            MaxCombo = 100,
        };

        private readonly Dictionary<HitResult, int> maximumStatistics = new Dictionary<HitResult, int>
        {
            [HitResult.Great] = 10,
            [HitResult.LargeTickHit] = 7,
            [HitResult.SliderTailHit] = 4,
        };

        [Test]
        public void TestCreateScorePreservesSuccessfulNestedJudgements()
        {
            var ruleset = new OsuRuleset();
            var input = new OsuPerformanceCalculationInput(8, 1, 0, 1, 80);
            var scoreProcessor = ruleset.CreateScoreProcessor();

            var score = OsuPerformanceCalculationScenario.CreateScore(new BeatmapInfo(), ruleset.RulesetInfo, [], attributes, maximumStatistics, scoreProcessor, input);

            Assert.Multiple(() =>
            {
                Assert.That(score.Accuracy, Is.EqualTo(3310 / 3810.0));
                Assert.That(score.MaxCombo, Is.EqualTo(80));
                Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(8));
                Assert.That(score.Statistics[HitResult.Ok], Is.EqualTo(1));
                Assert.That(score.Statistics[HitResult.Miss], Is.EqualTo(1));
                Assert.That(score.Statistics[HitResult.LargeTickHit], Is.EqualTo(7));
                Assert.That(score.Statistics[HitResult.LargeTickMiss], Is.Zero);
                Assert.That(score.Statistics[HitResult.SliderTailHit], Is.EqualTo(4));
            });
        }

        [Test]
        public void TestCreateScoreAppliesDroppedSliderParts()
        {
            var ruleset = new OsuRuleset();
            var input = new OsuPerformanceCalculationInput(10, 0, 0, 0, 100, 3, 2);

            var score = OsuPerformanceCalculationScenario.CreateScore(new BeatmapInfo(), ruleset.RulesetInfo, [], attributes, maximumStatistics,
                                                                       ruleset.CreateScoreProcessor(), input);

            Assert.Multiple(() =>
            {
                Assert.That(score.Statistics[HitResult.LargeTickHit], Is.EqualTo(4));
                Assert.That(score.Statistics[HitResult.LargeTickMiss], Is.EqualTo(3));
                Assert.That(score.Statistics[HitResult.SliderTailHit], Is.EqualTo(2));
                Assert.That(score.Accuracy, Is.EqualTo(3420 / 3810.0));
            });
        }

        [TestCase(8, 0, OsuPerformanceCalculationInputError.SliderTickMissOutOfRange)]
        [TestCase(0, 5, OsuPerformanceCalculationInputError.SliderTailDroppedOutOfRange)]
        [TestCase(7, 4, OsuPerformanceCalculationInputError.None)]
        public void TestSliderValidation(int tickMisses, int tailDropped, OsuPerformanceCalculationInputError expected)
        {
            var input = new OsuPerformanceCalculationInput(10, 0, 0, 0, 100, tickMisses, tailDropped);
            Assert.That(OsuPerformanceCalculationScenario.Validate(input, attributes, maximumStatistics), Is.EqualTo(expected));
        }

        [TestCase(9, 0, 0, 0, 100, OsuPerformanceCalculationInputError.JudgementCountMismatch)]
        [TestCase(10, 0, 0, 0, 101, OsuPerformanceCalculationInputError.ComboOutOfRange)]
        [TestCase(-1, 10, 0, 1, 100, OsuPerformanceCalculationInputError.NegativeJudgementCount)]
        [TestCase(10, 0, 0, 0, 100, OsuPerformanceCalculationInputError.None)]
        public void TestValidation(int great, int ok, int meh, int miss, int combo, OsuPerformanceCalculationInputError expected)
        {
            var input = new OsuPerformanceCalculationInput(great, ok, meh, miss, combo);
            Assert.That(OsuPerformanceCalculationScenario.Validate(input, attributes, maximumStatistics), Is.EqualTo(expected));
        }
    }
}
