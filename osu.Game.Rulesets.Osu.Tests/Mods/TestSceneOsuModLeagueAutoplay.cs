// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    public partial class TestSceneOsuModLeagueAutoplay : OsuModTestScene
    {
        [Test]
        public void TestRealtimeSettingSerialises()
        {
            var mod = new OsuModLeagueAutoplay();
            Assert.Multiple(() =>
            {
                Assert.That(mod.RealTime.Value, Is.False);
                Assert.That(mod.MinimumUnstableRate.Value, Is.EqualTo(110));
                Assert.That(mod.MaximumUnstableRate.Value, Is.EqualTo(160));
                Assert.That(mod.MissChance.Value, Is.EqualTo(0.015));
            });

            mod.RealTime.Value = true;
            Assert.That(new APIMod(mod).Settings["real_time"], Is.EqualTo(true));
        }

        [Test]
        public void TestReplayIsScoredByGameplayRuleset()
        {
            var beatmap = new OsuBeatmap();

            for (int i = 0; i < 30; i++)
                beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 + i * 150 });

            var mod = new OsuModLeagueAutoplay
            {
                MinimumUnstableRate = { Value = 0 },
                MaximumUnstableRate = { Value = 0 },
                MissChance = { Value = 0.2 },
                ReplaySeed = 12345,
            };
            var replay = mod.CreateReplayData(beatmap, new[] { mod }).Replay;

            CreateModTest(new ModTestData
            {
                Autoplay = false,
                Mod = mod,
                CreateBeatmap = () => beatmap,
                ReplayFrames = replay.Frames,
                PassCondition = () => Player.ScoreProcessor.JudgedHits >= beatmap.HitObjects.Count,
            });

            AddUntilStep("all objects judged", () => Player.ScoreProcessor.JudgedHits >= beatmap.HitObjects.Count);
            AddAssert("real misses produced", () => Player.Results, () => Has.Some.Matches<JudgementResult>(result => !result.IsHit));
            AddAssert("combo broken", () => Player.ScoreProcessor.HighestCombo.Value, () => Is.LessThan(Player.ScoreProcessor.MaximumCombo));
            AddAssert("score calculated", () => Player.ScoreProcessor.TotalScore.Value, () => Is.InRange(1, 999999));
            AddAssert("accuracy calculated", () => Player.ScoreProcessor.Accuracy.Value, () => Is.LessThan(1));
        }
    }
}
