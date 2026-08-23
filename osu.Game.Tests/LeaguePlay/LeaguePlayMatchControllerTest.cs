// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.LeaguePlay;

namespace osu.Game.Tests.LeaguePlay
{
    [TestFixture]
    public class LeaguePlayMatchControllerTest
    {
        [Test]
        public void TestTiedRollRequiresReroll()
        {
            var controller = new LeaguePlayMatchController(createMappool());

            controller.SubmitRolls(50, 50);

            Assert.That(controller.Phase, Is.EqualTo(LeaguePlayMatchPhase.Rolling));
            Assert.That(controller.RollWinner, Is.Null);

            controller.SubmitRolls(80, 20);

            Assert.That(controller.Phase, Is.EqualTo(LeaguePlayMatchPhase.ChoosingPickOrder));
            Assert.That(controller.RollWinner, Is.EqualTo(LeaguePlaySide.Player));
        }

        [Test]
        public void TestTwoBanSnakeOrderAndAlternatingPicks()
        {
            LeaguePlayMatchController controller = startMatch();

            Assert.That(controller.ExpectedBanner, Is.EqualTo(LeaguePlaySide.Opponent));
            controller.Ban(LeaguePlaySide.Opponent, "NM1");
            Assert.That(controller.ExpectedBanner, Is.EqualTo(LeaguePlaySide.Player));
            controller.Ban(LeaguePlaySide.Player, "NM2");
            Assert.That(controller.ExpectedBanner, Is.EqualTo(LeaguePlaySide.Player));
            controller.Ban(LeaguePlaySide.Player, "HD1");
            Assert.That(controller.ExpectedBanner, Is.EqualTo(LeaguePlaySide.Opponent));
            controller.Ban(LeaguePlaySide.Opponent, "HD2");

            Assert.That(controller.Phase, Is.EqualTo(LeaguePlayMatchPhase.Picking));
            Assert.That(controller.ExpectedPicker, Is.EqualTo(LeaguePlaySide.Player));

            controller.Pick(LeaguePlaySide.Player, "HR1");
            controller.SubmitRoundScores(900_000, 800_000);

            Assert.That(controller.PlayerPoints, Is.EqualTo(1));
            Assert.That(controller.ExpectedPicker, Is.EqualTo(LeaguePlaySide.Opponent));
            controller.Pick(LeaguePlaySide.Opponent, "HR2");
        }

        [Test]
        public void TestTieReplaysSameMapWithoutAdvancingPickOrder()
        {
            LeaguePlayMatchController controller = startMatchWithoutBans();
            controller.Pick(LeaguePlaySide.Player, "NM1");

            controller.SubmitRoundScores(500_000, 500_000);

            Assert.That(controller.Phase, Is.EqualTo(LeaguePlayMatchPhase.AwaitingRoundResult));
            Assert.That(controller.CurrentItem!.Slot, Is.EqualTo("NM1"));
            Assert.That(controller.PlayerPoints, Is.Zero);
            Assert.That(controller.OpponentPoints, Is.Zero);
        }

        [Test]
        public void TestTiebreakerIsAutomaticAtDoubleMatchPoint()
        {
            LeaguePlayMatchController controller = startMatchWithoutBans();

            controller.Pick(LeaguePlaySide.Player, "NM1");
            controller.SubmitRoundScores(900_000, 800_000);
            controller.Pick(LeaguePlaySide.Opponent, "NM2");
            controller.SubmitRoundScores(700_000, 800_000);
            controller.Pick(LeaguePlaySide.Player, "HD1");
            controller.SubmitRoundScores(900_000, 800_000);
            controller.Pick(LeaguePlaySide.Opponent, "HD2");
            controller.SubmitRoundScores(700_000, 800_000);

            Assert.That(controller.PlayerPoints, Is.EqualTo(2));
            Assert.That(controller.OpponentPoints, Is.EqualTo(2));
            Assert.That(controller.Phase, Is.EqualTo(LeaguePlayMatchPhase.AwaitingRoundResult));
            Assert.That(controller.CurrentItem!.Slot, Is.EqualTo("TB"));
            Assert.That(controller.CurrentPicker, Is.Null);

            controller.SubmitRoundScores(950_000, 900_000);

            Assert.That(controller.Phase, Is.EqualTo(LeaguePlayMatchPhase.Completed));
            Assert.That(controller.Winner, Is.EqualTo(LeaguePlaySide.Player));
            Assert.That(controller.PlayerPoints, Is.EqualTo(3));
        }

        [Test]
        public void TestRejectsUnavailableAndOutOfTurnMaps()
        {
            LeaguePlayMatchController controller = startMatch();

            Assert.Throws<InvalidOperationException>(() => controller.Ban(LeaguePlaySide.Player, "NM1"));
            Assert.Throws<InvalidOperationException>(() => controller.Ban(LeaguePlaySide.Opponent, "TB"));

            controller.Ban(LeaguePlaySide.Opponent, "NM1");
            Assert.Throws<InvalidOperationException>(() => controller.Ban(LeaguePlaySide.Player, "NM1"));
        }

        private static LeaguePlayMatchController startMatch()
        {
            var controller = new LeaguePlayMatchController(createMappool());
            controller.SubmitRolls(80, 20);
            controller.ChooseFirstPicker(LeaguePlaySide.Player);
            controller.ChooseFirstBanner(LeaguePlaySide.Opponent);
            return controller;
        }

        private static LeaguePlayMatchController startMatchWithoutBans()
        {
            var controller = new LeaguePlayMatchController(createMappool(0));
            controller.SubmitRolls(80, 20);
            controller.ChooseFirstPicker(LeaguePlaySide.Player);
            return controller;
        }

        private static LeaguePlayMappool createMappool(int bansPerSide = 2)
        {
            var items = new List<LeaguePlayMappoolItem>();

            add("NM1", LeaguePlayModPool.NoMod);
            add("NM2", LeaguePlayModPool.NoMod);
            add("HD1", LeaguePlayModPool.Hidden);
            add("HD2", LeaguePlayModPool.Hidden);
            add("HR1", LeaguePlayModPool.HardRock);
            add("HR2", LeaguePlayModPool.HardRock);
            add("DT1", LeaguePlayModPool.DoubleTime);
            add("FM1", LeaguePlayModPool.FreeMod);
            add("TB", LeaguePlayModPool.Tiebreaker);

            return new LeaguePlayMappool
            {
                Name = "Test Cup",
                FirstTo = 3,
                BansPerSide = bansPerSide,
                Items = items
            };

            void add(string slot, LeaguePlayModPool modPool)
            {
                items.Add(new LeaguePlayMappoolItem
                {
                    Slot = slot,
                    ModPool = modPool,
                    BeatmapSetId = items.Count + 1,
                    OszPath = $"{items.Count + 1} test.osz"
                });
            }
        }
    }
}
