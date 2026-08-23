// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.LeaguePlay;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Screens.LeaguePlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match.BeatmapSelect;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.LeaguePlay
{
    public partial class TestSceneLeaguePlayBeatmapPanel : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        [Test]
        public void TestQuickPlayPanelStates()
        {
            LeaguePlayBeatmapPanel panel = null!;

            AddStep("create panel", () =>
            {
                var beatmap = new Beatmap
                {
                    BeatmapInfo =
                    {
                        StarRating = 8.4,
                        Metadata = new BeatmapMetadata
                        {
                            Artist = "airportexpress feat. Itsuneko",
                            Title = "BIRTH",
                        },
                        DifficultyName = "Insane",
                    }
                };

                var working = CreateWorkingBeatmap(beatmap);
                var source = new LeaguePlayMappoolItem
                {
                    Slot = "DT2",
                    ModPool = LeaguePlayModPool.DoubleTime,
                    BeatmapSetId = 175241,
                    BeatmapId = 422762,
                    OszPath = "175241 airportexpress feat.Itsuneko - BIRTH.osz",
                };

                Child = panel = new LeaguePlayBeatmapPanel(1, new LeaguePlayPreparedMappoolItem
                {
                    Source = source,
                    Beatmap = working.BeatmapInfo,
                }, working, [], Color4.Orange)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AllowSelection = true,
                };
            });

            AddStep("add selecting user", () => panel.AddUser(new APIUser { Id = 123, Username = "Player" }));
            AddStep("present chosen card", () => panel.PresentAsChosenBeatmap(new MatchmakingPlaylistItem(panel.Item, new APIBeatmap(), [])));
        }
    }
}
