// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LeaguePlay;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osuTK;

namespace osu.Game.Screens.LeaguePlay
{
    /// <summary>
    /// A local multiplayer player backed by a real TeamVersus room and the standard multiplayer leaderboard.
    /// </summary>
    public partial class LeaguePlayPlayer : Player
    {
        protected override bool PauseOnFocusLost => false;

        private readonly LeaguePlayLocalRoom room;
        private readonly IWorkingBeatmap workingBeatmap;
        private readonly Mod[] requiredMods;
        private readonly Mod leagueAutoplay;
        private readonly Action<ScoreInfo, ScoreInfo> completion;

        private readonly LeaguePlaySpectatorClient spectatorClient = new LeaguePlaySpectatorClient();

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly MultiplayerLeaderboardProvider leaderboardProvider;

        private LeaguePlayAIOpponent aiOpponent = null!;
        private GameplayMatchScoreDisplay teamScoreDisplay = null!;
        private ScoreInfo opponentScore;
        private bool gameplayStarted;

        private ScoringMode previousScoringMode;
        private bool scoringModeOverridden;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public LeaguePlayPlayer(LeaguePlayLocalRoom room, IWorkingBeatmap workingBeatmap, IReadOnlyList<Mod> requiredMods, Mod leagueAutoplay,
                                Action<ScoreInfo, ScoreInfo> completion)
            : base(new PlayerConfiguration
            {
                AllowPause = false,
                AllowRestart = false,
                AutomaticallySkipIntro = true,
                ShowLeaderboard = true,
                ShowResults = true,
            })
        {
            this.room = room;
            this.workingBeatmap = workingBeatmap;
            this.requiredMods = requiredMods.Select(mod => mod.DeepClone()).ToArray();
            this.leagueAutoplay = leagueAutoplay.DeepClone();
            this.completion = completion;

            leaderboardProvider = new MultiplayerLeaderboardProvider(room.Room.Users.ToArray(), room.PlayingUserIds);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<SpectatorClient>(spectatorClient);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (!LoadedBeatmapSuccessfully)
                return;

            previousScoringMode = config.Get<ScoringMode>(OsuSetting.ScoreDisplayMode);
            config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised);
            scoringModeOverridden = true;

            AddInternal(spectatorClient);

            ScoreProcessor.ApplyNewJudgementsWhenFailed = true;

            Ruleset ruleset = Ruleset.Value.CreateInstance();
            Mod[] opponentMods = requiredMods.Append(leagueAutoplay).ToArray();

            GameplayClockContainer.Add(aiOpponent = new LeaguePlayAIOpponent(
                workingBeatmap,
                ruleset,
                opponentMods,
                leagueAutoplay,
                room.OpponentUser,
                spectatorClient));

            LoadComponentAsync(new FillFlowContainer
            {
                Width = 260,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Child = teamScoreDisplay = new GameplayMatchScoreDisplay
                {
                    Expanded = { BindTarget = HUDOverlay.ShowHud },
                    Alpha = 0,
                },
            }, HUDOverlay.TopLeftElements.Add);

            LoadComponentAsync(new MultiplayerPositionDisplay
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
            }, drawable => HUDOverlay.BottomRightElements.Insert(-1, drawable));

            LoadComponentAsync(leaderboardProvider, loaded =>
            {
                AddInternal(loaded);

                if (!loaded.HasTeams)
                    return;

                teamScoreDisplay.Alpha = 1;
                teamScoreDisplay.Team1Score.BindTarget = loaded.TeamScores.First().Value;
                teamScoreDisplay.Team2Score.BindTarget = loaded.TeamScores.Last().Value;
            });
        }

        protected override void StartGameplay()
        {
            room.StartGameplay();
            spectatorClient.BeginPlaying(null, GameplayState, Score);
            aiOpponent.Start();
            gameplayStarted = true;

            base.StartGameplay();
        }

        protected override void PerformFail()
        {
            // Multiplayer scores continue receiving judgements after health reaches zero.
            ScoreProcessor.FailScore(Score.ScoreInfo);
        }

        protected override Score CreateScore(IBeatmap beatmap) => new Score
        {
            ScoreInfo = new ScoreInfo
            {
                User = room.Player.User!,
            },
        };

        protected override void ConcludeFailedScore(Score score)
            => throw new NotSupportedException($"{nameof(LeaguePlayPlayer)} should not conclude a failed score before the multiplayer round ends.");

        protected override async Task PrepareScoreForResultsAsync(Score score)
        {
            await base.PrepareScoreForResultsAsync(score).ConfigureAwait(false);

            score.ScoreInfo.Date = DateTimeOffset.Now;
            room.FinishPlayer();
            spectatorClient.EndPlaying(null, GameplayState);

            opponentScore = await aiOpponent.WaitForCompletionAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            room.FinishOpponent();
            room.FinishRound();
        }

        protected override Task ImportScore(Score score) => Task.CompletedTask;

        protected override ResultsScreen CreateResults(ScoreInfo score)
        {
            Debug.Assert(opponentScore != null);
            completion(score, opponentScore);
            return new LeaguePlayRoundResultsScreen(score, opponentScore);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (gameplayStarted)
                spectatorClient.EndPlaying(null, GameplayState);

            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && scoringModeOverridden)
                config.SetValue(OsuSetting.ScoreDisplayMode, previousScoringMode);

            base.Dispose(isDisposing);
        }
    }
}
