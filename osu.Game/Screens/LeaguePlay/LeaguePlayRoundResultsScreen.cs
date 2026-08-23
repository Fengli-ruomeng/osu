// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Screens.LeaguePlay
{
    /// <summary>
    /// Displays both real scores produced by the local multiplayer round.
    /// </summary>
    public partial class LeaguePlayRoundResultsScreen : ResultsScreen
    {
        internal ScoreInfo OpponentScore { get; }

        public LeaguePlayRoundResultsScreen(ScoreInfo playerScore, ScoreInfo opponentScore)
            : base(playerScore)
        {
            IsLocalPlay = true;
            AllowRetry = false;
            AllowWatchingReplay = true;

            OpponentScore = opponentScore;

            ScoreInfo[] orderedScores = new[] { playerScore, opponentScore }.OrderByDescending(score => score.TotalScore).ToArray();

            for (int i = 0; i < orderedScores.Length; i++)
                orderedScores[i].Position = i + 1;
        }

        // ResultsScreen adds the primary player score before calling FetchScores().
        protected override Task<ScoreInfo[]> FetchScores() => Task.FromResult(new[] { OpponentScore });
    }
}
