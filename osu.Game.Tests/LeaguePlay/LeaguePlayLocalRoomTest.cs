// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.LeaguePlay;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.LeaguePlay
{
    [TestFixture]
    public class LeaguePlayLocalRoomTest
    {
        [Test]
        public void TestCreatesBluePlayerAndRedOpponent()
        {
            var room = new LeaguePlayLocalRoom(new APIUser { Id = 42, Username = "Player" }, "Test League");

            Assert.Multiple(() =>
            {
                Assert.That(room.Room.Settings.MatchType, Is.EqualTo(MatchType.TeamVersus));
                Assert.That(room.Room.Users, Has.Count.EqualTo(2));
                Assert.That(((TeamVersusUserState)room.Player.MatchState!).TeamID, Is.EqualTo(1));
                Assert.That(((TeamVersusUserState)room.Opponent.MatchState!).TeamID, Is.Zero);
                Assert.That(room.Opponent.User, Is.SameAs(room.OpponentUser));
            });
        }

        [Test]
        public void TestMissingLocalUsernameFallsBack()
        {
            var room = new LeaguePlayLocalRoom(new APIUser { Id = APIUser.SYSTEM_USER_ID, Username = " " }, "Test League");
            Assert.That(room.Player.User!.Username, Is.EqualTo("Player"));
        }

        [Test]
        public void TestRoundContainsRealtimeLeagueAutoplay()
        {
            var ruleset = new OsuRuleset();
            var beatmap = new BeatmapInfo { Ruleset = ruleset.RulesetInfo };
            var room = new LeaguePlayLocalRoom(new APIUser { Id = 42, Username = "Player" }, "Test League");
            var leagueAutoplay = LeaguePlayModRules.CreateLeagueAutoplay(ruleset.RulesetInfo);
            var osuLeagueAutoplay = (OsuModLeagueAutoplay)leagueAutoplay;

            room.PrepareRound(beatmap, LeaguePlayModRules.CreateRequiredMods(ruleset.RulesetInfo, LeaguePlayModPool.NoMod), leagueAutoplay);

            APIMod apiMod = room.Opponent.Mods.Single();

            Assert.Multiple(() =>
            {
                Assert.That(room.Room.State, Is.EqualTo(MultiplayerRoomState.WaitingForLoad));
                Assert.That(room.PlayingUserIds, Is.EquivalentTo(new[] { 42, LeaguePlayLocalRoom.OPPONENT_USER_ID }));
                Assert.That(apiMod.Acronym, Is.EqualTo("LAT"));
                Assert.That(apiMod.Settings["real_time"], Is.EqualTo(true));
                Assert.That(osuLeagueAutoplay.MinimumUnstableRate.Value, Is.EqualTo(110d));
                Assert.That(osuLeagueAutoplay.MaximumUnstableRate.Value, Is.EqualTo(160d));
                Assert.That(osuLeagueAutoplay.MissChance.Value, Is.EqualTo(0.015d));
            });
        }

        [Test]
        public void TestRejectsNonStandardRuleset()
        {
            var ruleset = new ManiaRuleset();
            Assert.Throws<NotSupportedException>(() => LeaguePlayModRules.CreateLeagueAutoplay(ruleset.RulesetInfo));
        }
    }
}
