// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.LeaguePlay;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.LeaguePlay;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.Visual.LeaguePlay
{
    public partial class TestSceneLeaguePlayPlayer : ScreenTestScene
    {
        [Test]
        public void TestRealtimeOpponentAndMultiplayerHud()
        {
            LeaguePlayPlayer player = null!;
            ScoreInfo? opponentResult = null;
            double lastObjectTime = 0;

            AddStep("prepare local room", () =>
            {
                var rulesetInfo = new OsuRuleset().RulesetInfo;
                WorkingBeatmap working = CreateWorkingBeatmap(rulesetInfo);
                var beatmapInfo = (BeatmapInfo)working.BeatmapInfo;
                Beatmap.Value = working;
                Ruleset.Value = rulesetInfo;

                Mod[] requiredMods = LeaguePlayModRules.CreateRequiredMods(rulesetInfo, LeaguePlayModPool.NoMod);
                Mod leagueAutoplay = LeaguePlayModRules.CreateLeagueAutoplay(rulesetInfo);
                SelectedMods.Value = requiredMods;

                var room = new LeaguePlayLocalRoom(API.LocalUser.Value, "Realtime Test");
                room.PrepareRound(beatmapInfo, requiredMods, leagueAutoplay);

                lastObjectTime = working.GetPlayableBeatmap(rulesetInfo, requiredMods).GetLastObjectTime();
                Stack.Push(player = new LeaguePlayPlayer(room, working, requiredMods, leagueAutoplay, (_, opponent) => opponentResult = opponent));
            });

            AddUntilStep("gameplay started", () => player.LocalUserPlaying.Value);
            AddAssert("hidden AI ignores positional input", () => player.ChildrenOfType<LeaguePlayAIOpponent>().Single().PropagatePositionalInputSubTree, () => Is.False);
            AddAssert("hidden AI ignores keyboard input", () => player.ChildrenOfType<LeaguePlayAIOpponent>().Single().PropagateNonPositionalInputSubTree, () => Is.False);
            AddUntilStep("multiplayer team score visible", () => player.ChildrenOfType<GameplayMatchScoreDisplay>().Single().Alpha, () => Is.EqualTo(1));
            AddUntilStep("two realtime leaderboard users", () => player.ChildrenOfType<DrawableGameplayLeaderboardScore>().Count(), () => Is.EqualTo(2));
            AddStep("seek near completion", () => player.Seek(lastObjectTime + 1000));
            AddUntilStep("real opponent result received", () => opponentResult != null);
            AddAssert("opponent has real judgements", () => opponentResult!.Statistics.Values.Sum(), () => Is.GreaterThan(0));
            AddAssert("opponent has real combo", () => opponentResult!.MaxCombo, () => Is.GreaterThan(0));
            AddAssert("opponent has real score", () => opponentResult!.TotalScore, () => Is.GreaterThan(0));
        }
    }
}
