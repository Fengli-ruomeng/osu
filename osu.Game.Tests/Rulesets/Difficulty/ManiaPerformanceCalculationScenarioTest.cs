// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.Rulesets.Difficulty
{
    [TestFixture]
    public class ManiaPerformanceCalculationScenarioTest
    {
        private readonly DifficultyAttributes attributes = new DifficultyAttributes
        {
            MaxCombo = 20,
        };

        private readonly Dictionary<HitResult, int> maximumStatistics = new Dictionary<HitResult, int>
        {
            [HitResult.Perfect] = 10,
        };

        [Test]
        public void TestCreateScorePopulatesAllJudgements()
        {
            var ruleset = new ManiaRuleset();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            var input = new ManiaPerformanceCalculationInput(4, 2, 1, 1, 1, 1);

            var score = ManiaPerformanceCalculationScenario.CreateScore(new BeatmapInfo(), ruleset.RulesetInfo, [], attributes,
                                                                         maximumStatistics, scoreProcessor, input);
            double expectedAccuracy = (4 * 305 + 2 * 300 + 200 + 100 + 50) / (10 * 305.0);

            Assert.Multiple(() =>
            {
                Assert.That(score.Statistics[HitResult.Perfect], Is.EqualTo(4));
                Assert.That(score.Statistics[HitResult.Great], Is.EqualTo(2));
                Assert.That(score.Statistics[HitResult.Good], Is.EqualTo(1));
                Assert.That(score.Statistics[HitResult.Ok], Is.EqualTo(1));
                Assert.That(score.Statistics[HitResult.Meh], Is.EqualTo(1));
                Assert.That(score.Statistics[HitResult.Miss], Is.EqualTo(1));
                Assert.That(score.MaxCombo, Is.EqualTo(attributes.MaxCombo));
                Assert.That(score.Accuracy, Is.EqualTo(expectedAccuracy));
            });
        }

        [TestCase(9, 0, 0, 0, 0, 0, ManiaPerformanceCalculationInputError.JudgementCountMismatch)]
        [TestCase(-1, 8, 1, 1, 1, 0, ManiaPerformanceCalculationInputError.NegativeJudgementCount)]
        [TestCase(10, 0, 0, 0, 0, 0, ManiaPerformanceCalculationInputError.None)]
        public void TestValidation(int perfect, int great, int good, int ok, int meh, int miss, ManiaPerformanceCalculationInputError expected)
        {
            var input = new ManiaPerformanceCalculationInput(perfect, great, good, ok, meh, miss);
            Assert.That(ManiaPerformanceCalculationScenario.Validate(input, maximumStatistics), Is.EqualTo(expected));
        }
    }
}
