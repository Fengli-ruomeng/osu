// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Models;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.LeaguePlay;

namespace osu.Game.Tests.LeaguePlay
{
    [TestFixture]
    public partial class LeaguePlayRoundResultsScreenTest
    {
        [Test]
        public async Task TestFetchOnlyReturnsAdditionalOpponentScore()
        {
            ScoreInfo playerScore = createPlayerScore();
            ScoreInfo opponentScore = createOpponentScore();
            var screen = new TestableRoundResultsScreen(playerScore, opponentScore);

            ScoreInfo[] additionalScores = await screen.FetchAdditionalScores();

            Assert.Multiple(() =>
            {
                Assert.That(additionalScores, Has.Length.EqualTo(1));
                Assert.That(additionalScores[0], Is.SameAs(opponentScore));
                Assert.That(additionalScores[0].ID, Is.Not.EqualTo(playerScore.ID));
                Assert.That(additionalScores[0].User.Username, Is.EqualTo("League AI"));
                Assert.That(playerScore.User.Username, Is.EqualTo("Guest"));
            });
        }

        [Test]
        public void TestRealOpponentStatisticsArePreserved()
        {
            ScoreInfo opponent = createOpponentScore();
            var screen = new LeaguePlayRoundResultsScreen(createPlayerScore(), opponent);

            Assert.Multiple(() =>
            {
                Assert.That(screen.OpponentScore, Is.SameAs(opponent));
                Assert.That(opponent.User.Username, Is.EqualTo("League AI"));
                Assert.That(opponent.UserID, Is.EqualTo(-1));
                Assert.That(opponent.TotalScore, Is.EqualTo(300_000));
                Assert.That(opponent.MaxCombo, Is.EqualTo(500));
            });
        }

        private static ScoreInfo createPlayerScore() => new ScoreInfo(
            new BeatmapInfo(),
            new OsuRuleset().RulesetInfo,
            new RealmUser { OnlineID = 0, Username = "Guest" })
        {
            ID = System.Guid.NewGuid(),
            TotalScore = 250_000,
            Accuracy = 0.9,
        };

        private static ScoreInfo createOpponentScore() => new ScoreInfo(
            new BeatmapInfo(),
            new OsuRuleset().RulesetInfo,
            new RealmUser { OnlineID = -1, Username = "League AI" })
        {
            ID = System.Guid.NewGuid(),
            TotalScore = 300_000,
            Accuracy = 0.96,
            MaxCombo = 500,
        };

        private partial class TestableRoundResultsScreen : LeaguePlayRoundResultsScreen
        {
            public TestableRoundResultsScreen(ScoreInfo playerScore, ScoreInfo opponentScore)
                : base(playerScore, opponentScore)
            {
            }

            public Task<ScoreInfo[]> FetchAdditionalScores() => base.FetchScores();
        }
    }
}
