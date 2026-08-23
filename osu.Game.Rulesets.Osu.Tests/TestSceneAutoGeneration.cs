// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    [HeadlessTest]
    public partial class TestSceneAutoGeneration : OsuTestScene
    {
        [TestCase(-1, true)]
        [TestCase(0, false)]
        [TestCase(1, false)]
        public void TestAlternating(double offset, bool shouldAlternate)
        {
            const double first_object_time = 1000;
            double secondObjectTime = first_object_time + AutoGenerator.KEY_UP_DELAY + OsuAutoGenerator.MIN_FRAME_SEPARATION_FOR_ALTERNATING + offset;

            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = first_object_time });
            beatmap.HitObjects.Add(new HitCircle { StartTime = secondObjectTime });

            var generated = new OsuAutoGenerator(beatmap, []).Generate();
            var frames = generated.Frames.OfType<OsuReplayFrame>().ToList();

            Assert.That(frames.Exists(f => f.Time == first_object_time && f.Actions.SingleOrDefault() == OsuAction.LeftButton));
            Assert.That(frames.Exists(f => f.Time == first_object_time + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));

            Assert.That(frames.Exists(f => f.Time == secondObjectTime && f.Actions.SingleOrDefault() == (shouldAlternate ? OsuAction.RightButton : OsuAction.LeftButton)));
            Assert.That(frames.Exists(f => f.Time == secondObjectTime + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));
        }

        [TestCase(300)]
        [TestCase(600)]
        [TestCase(1200)]
        public void TestAlternatingSpecificBPM(double bpm)
        {
            const double first_object_time = 1000;
            double secondObjectTime = first_object_time + 60000 / bpm;

            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = first_object_time });
            beatmap.HitObjects.Add(new HitCircle { StartTime = secondObjectTime });

            var generated = new OsuAutoGenerator(beatmap, []).Generate();
            var frames = generated.Frames.OfType<OsuReplayFrame>().ToList();

            Assert.That(frames.Exists(f => f.Time == first_object_time && f.Actions.SingleOrDefault() == OsuAction.LeftButton));
            Assert.That(frames.Exists(f => f.Time == first_object_time + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));

            Assert.That(frames.Exists(f => f.Time == secondObjectTime && f.Actions.SingleOrDefault() == OsuAction.RightButton));
            Assert.That(frames.Exists(f => f.Time == secondObjectTime + AutoGenerator.KEY_UP_DELAY && !f.Actions.Any()));
        }

        [Test]
        public void TestLeagueGeneratorTimingVarianceProducesConfiguredUr()
        {
            var beatmap = createSpacedCircleBeatmap(1000);
            var generated = new OsuLeagueAutoGenerator(beatmap, [], 100, 150, 0, 12345).Generate();
            var frames = generated.Frames.OfType<OsuReplayFrame>().Where(frame => frame.Actions.Any()).ToArray();

            double[] hitErrors = beatmap.HitObjects.Select(hitObject => frames.MinBy(frame => Math.Abs(frame.Time - hitObject.StartTime))!.Time - hitObject.StartTime).ToArray();
            double mean = hitErrors.Average();
            double actualUnstableRate = Math.Sqrt(hitErrors.Average(error => Math.Pow(error - mean, 2))) * 10;

            Assert.That(actualUnstableRate, Is.InRange(115, 140));
            Assert.That(hitErrors, Has.Some.Not.Zero);
        }

        [Test]
        public void TestLeagueGeneratorCanDeliberatelyMissEveryObject()
        {
            var beatmap = createSpacedCircleBeatmap(20);
            var generated = new OsuLeagueAutoGenerator(beatmap, [], 100, 150, 1, 12345).Generate();

            Assert.That(generated.Frames.OfType<OsuReplayFrame>().All(frame => !frame.Actions.Any()), Is.True);
        }

        [Test]
        public void TestLeagueGeneratorIsDeterministicAndNormalisesUrBounds()
        {
            var beatmap = createSpacedCircleBeatmap(20);
            var first = new OsuLeagueAutoGenerator(beatmap, [], 100, 150, 0.1, 9876).Generate().Frames.OfType<OsuReplayFrame>().ToArray();
            var second = new OsuLeagueAutoGenerator(beatmap, [], 150, 100, 0.1, 9876).Generate().Frames.OfType<OsuReplayFrame>().ToArray();

            Assert.That(second.Select(frame => frame.Time), Is.EqualTo(first.Select(frame => frame.Time)));
            Assert.That(second.Select(frame => frame.Actions), Is.EqualTo(first.Select(frame => frame.Actions)));
        }

        private static OsuBeatmap createSpacedCircleBeatmap(int count)
        {
            var beatmap = new OsuBeatmap();

            for (int i = 0; i < count; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 + i * 500 });

            return beatmap;
        }
    }
}
