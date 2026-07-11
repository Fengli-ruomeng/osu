// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    public partial class TestSceneOsuModEZHelper : OsuModTestScene
    {
        protected override bool AllowFail => true;

        [Test]
        public void TestComboGroupsRecolourAsPlayAdvances()
        {
            CreateModTest(new ModTestData
            {
                Mod = new OsuModEZHelper(),
                Autoplay = true,
                CreateBeatmap = createBeatmap,
                PassCondition = () => true,
            });

            AddStep("stop clock", () => Player.GameplayClockContainer.Stop());

            seekTo(900);
            AddUntilStep("first combo is blue", () => hasColour(OsuModEZHelper.CurrentComboColour, 1000, 1020));
            AddUntilStep("second combo is red", () => hasColour(OsuModEZHelper.NextComboColour, 1400, 1420));
            AddUntilStep("later combo is grey", () => hasColour(OsuModEZHelper.OtherComboColour, 1450, 1470));

            AddStep("start clock", () => Player.GameplayClockContainer.Start());
            AddUntilStep("first combo judged", () => Player.ScoreProcessor.JudgedHits >= 2);
            AddStep("stop clock", () => Player.GameplayClockContainer.Stop());
            AddUntilStep("second combo becomes blue", () => hasColour(OsuModEZHelper.CurrentComboColour, 1400, 1420));
            AddUntilStep("third combo becomes red", () => hasColour(OsuModEZHelper.NextComboColour, 1450, 1470));
            AddUntilStep("fourth combo stays grey", () => hasColour(OsuModEZHelper.OtherComboColour, 1500, 1520));
        }

        private void seekTo(double time)
        {
            AddStep($"seek to {time}", () => Player.GameplayClockContainer.Seek(time));
            AddUntilStep("wait for seek", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(time).Within(50));
        }

        private bool hasColour(Color4 colour, params double[] startTimes)
        {
            var drawables = Player.ChildrenOfType<DrawableHitCircle>().ToArray();

            return startTimes.All(time => drawables.Any(d => d.HitObject.StartTime == time && d.AccentColour.Value == colour));
        }

        private static Beatmap createBeatmap() => new Beatmap
        {
            BeatmapInfo = new BeatmapInfo
            {
                Difficulty = new BeatmapDifficulty
                {
                    ApproachRate = 0,
                    OverallDifficulty = 10,
                },
            },
            HitObjects = new List<HitObject>
            {
                new HitCircle { StartTime = 1000, NewCombo = true },
                new HitCircle { StartTime = 1020 },
                new HitCircle { StartTime = 1400, NewCombo = true },
                new HitCircle { StartTime = 1420 },
                new HitCircle { StartTime = 1450, NewCombo = true },
                new HitCircle { StartTime = 1470 },
                new HitCircle { StartTime = 1500, NewCombo = true },
                new HitCircle { StartTime = 1520 },
            },
        };
    }
}
