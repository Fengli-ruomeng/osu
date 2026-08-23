// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.LeaguePlay;
using osu.Game.Screens.LeaguePlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match;

namespace osu.Game.Tests.Visual.LeaguePlay
{
    public partial class TestSceneLeaguePlayMatchScreen : ScreenTestScene
    {
        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Test]
        public void TestLoadOfflineMatchThroughIntro()
        {
            LeaguePlayMatchScreen screen = null!;

            AddStep("load match", () =>
            {
                var beatmap = (BeatmapInfo)beatmapManager.DefaultBeatmap.BeatmapInfo;
                var nm = new LeaguePlayMappoolItem
                {
                    Slot = "NM1",
                    ModPool = LeaguePlayModPool.NoMod,
                    BeatmapSetId = 1,
                    OszPath = "1 test.osz",
                };
                var tb = new LeaguePlayMappoolItem
                {
                    Slot = "TB",
                    ModPool = LeaguePlayModPool.Tiebreaker,
                    BeatmapSetId = 1,
                    OszPath = "1 test.osz",
                };
                var source = new LeaguePlayMappool
                {
                    Name = "Offline Test Match",
                    FirstTo = 1,
                    BansPerSide = 0,
                    Items = new[] { nm, tb },
                };

                LoadScreen(screen = new LeaguePlayMatchScreen(new LeaguePlayPreparedMappool
                {
                    Source = source,
                    Items = new[]
                    {
                        new LeaguePlayPreparedMappoolItem { Source = nm, Beatmap = beatmap },
                        new LeaguePlayPreparedMappoolItem { Source = tb, Beatmap = beatmap },
                    },
                }));
            });

            AddUntilStep("quick play stage loaded", () => screen.ChildrenOfType<StageDisplay>().Count(), () => Is.EqualTo(1));
            AddAssert("footer back is hidden", () => screen.ShowFooter, () => Is.False);
            AddUntilStep("quick play player cards loaded", () => screen.ChildrenOfType<PlayerPanel>().Count(), () => Is.EqualTo(2));
            AddAssert("ranked play shell absent", () => screen.ChildrenOfType<osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Components.RankedPlayCornerPiece>().Any(), () => Is.False);
            AddUntilStep("roll reaches choice or map grid", () => screen.ChildrenOfType<RoundedButton>().Any() || screen.ChildrenOfType<LeaguePlayBeatmapPanel>().Any());
            AddStep("resolve choice when required", () => screen.ChildrenOfType<RoundedButton>().FirstOrDefault()?.TriggerClick());
            AddUntilStep("quick play beatmap cards loaded", () => screen.ChildrenOfType<LeaguePlayBeatmapPanel>().Any());
        }
    }
}
