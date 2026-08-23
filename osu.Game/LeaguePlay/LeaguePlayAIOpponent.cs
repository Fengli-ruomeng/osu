// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;

namespace osu.Game.LeaguePlay
{
    /// <summary>
    /// Runs a replay-generating mod through a hidden, clock-synchronised gameplay ruleset.
    /// All score data is therefore produced by the same judgement and scoring code as a normal player.
    /// </summary>
    public partial class LeaguePlayAIOpponent : CompositeComponent
    {
        public override bool HandlePositionalInput => false;

        public override bool HandleNonPositionalInput => false;

        public override bool PropagatePositionalInputSubTree => false;

        public override bool PropagateNonPositionalInputSubTree => false;

        public ScoreInfo ScoreInfo => score.ScoreInfo;

        public bool HasCompleted => completion.Task.IsCompleted;

        private readonly IWorkingBeatmap workingBeatmap;
        private readonly Ruleset ruleset;
        private readonly Mod[] mods;
        private readonly ICreateReplayData replayGenerator;
        private readonly APIUser user;
        private readonly LeaguePlaySpectatorClient spectatorClient;

        private readonly TaskCompletionSource<ScoreInfo> completion = new TaskCompletionSource<ScoreInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

        private DrawableRuleset drawableRuleset = null!;
        private ScoreProcessor scoreProcessor = null!;
        private HealthProcessor healthProcessor = null!;
        private Score score = null!;

        private bool started;
        private bool ended;

        public LeaguePlayAIOpponent(IWorkingBeatmap workingBeatmap, Ruleset ruleset, IReadOnlyList<Mod> mods, Mod replayGeneratingMod, APIUser user,
                                    LeaguePlaySpectatorClient spectatorClient)
        {
            if (ruleset.RulesetInfo.OnlineID != 0)
                throw new NotSupportedException("League Autoplay currently supports osu!standard only.");

            this.workingBeatmap = workingBeatmap;
            this.ruleset = ruleset;
            this.mods = mods.Select(mod => mod.DeepClone()).ToArray();
            replayGenerator = replayGeneratingMod as ICreateReplayData
                              ?? throw new ArgumentException("The League Autoplay mod must generate replay data.", nameof(replayGeneratingMod));
            this.user = user;
            this.spectatorClient = spectatorClient;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(CancellationToken? cancellationToken)
        {
            IBeatmap playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods, cancellationToken ?? CancellationToken.None);

            drawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap, mods);
            drawableRuleset.Audio.Volume.Value = 0;

            scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyNewJudgementsWhenFailed = true;
            scoreProcessor.ApplyBeatmap(playableBeatmap);

            healthProcessor = mods.OfType<IApplicableHealthProcessor>().FirstOrDefault()?.CreateHealthProcessor(playableBeatmap.HitObjects[0].StartTime)
                              ?? ruleset.CreateHealthProcessor(playableBeatmap.HitObjects[0].StartTime);
            healthProcessor.ApplyBeatmap(playableBeatmap);

            score = replayGenerator.CreateScoreFromReplayData(playableBeatmap, mods);
            score.ScoreInfo.ID = Guid.NewGuid();
            score.ScoreInfo.User = user;
            score.ScoreInfo.BeatmapInfo = workingBeatmap.BeatmapInfo as BeatmapInfo
                                          ?? throw new InvalidOperationException("League Play requires a database-backed beatmap.");
            score.ScoreInfo.BeatmapHash = workingBeatmap.BeatmapInfo.Hash;
            score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
            score.ScoreInfo.Mods = mods;
            score.ScoreInfo.Date = DateTimeOffset.Now;
            score.ScoreInfo.Passed = true;

            drawableRuleset.NewResult += result =>
            {
                healthProcessor.ApplyResult(result);
                scoreProcessor.ApplyResult(result);
                scoreProcessor.PopulateScore(score.ScoreInfo);

                if (started)
                    Scheduler.AddOnce(sendSnapshot);
            };

            drawableRuleset.RevertResult += result =>
            {
                healthProcessor.RevertResult(result);
                scoreProcessor.RevertResult(result);
                scoreProcessor.PopulateScore(score.ScoreInfo);
            };

            scoreProcessor.HasCompleted.BindValueChanged(completed =>
            {
                if (completed.NewValue)
                    Scheduler.AddOnce(finish);
            });

            scoreProcessor.OnLoadComplete += _ =>
            {
                foreach (var mod in mods.OfType<IApplicableToScoreProcessor>())
                    mod.ApplyToScoreProcessor(scoreProcessor);
            };

            healthProcessor.OnLoadComplete += _ =>
            {
                foreach (var mod in mods.OfType<IApplicableToHealthProcessor>())
                    mod.ApplyToHealthProcessor(healthProcessor);
            };

            drawableRuleset.FrameStableComponents.AddRange(new Drawable[]
            {
                scoreProcessor,
                healthProcessor,
            });

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = drawableRuleset,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            drawableRuleset.SetReplayScore(score);
        }

        public void Start()
        {
            if (started)
                return;

            started = true;
            spectatorClient.BeginUser(user.Id, new SpectatorState
            {
                BeatmapID = workingBeatmap.BeatmapInfo.OnlineID,
                RulesetID = ruleset.RulesetInfo.OnlineID,
                Mods = mods.Select(mod => new APIMod(mod)).ToArray(),
                State = SpectatedUserState.Playing,
                MaximumStatistics = scoreProcessor.MaximumStatistics,
            }).FireAndForget();

            sendSnapshot();
        }

        public async Task<ScoreInfo> WaitForCompletionAsync(TimeSpan timeout)
        {
            await Task.WhenAny(completion.Task, Task.Delay(timeout)).ConfigureAwait(false);

            if (!completion.Task.IsCompleted)
                throw new TimeoutException("League AI did not finish processing the beatmap in time.");

            return await completion.Task.ConfigureAwait(false);
        }

        private void sendSnapshot()
        {
            if (!started || ended)
                return;

            scoreProcessor.PopulateScore(score.ScoreInfo);

            var frame = new LegacyReplayFrame(Clock.CurrentTime, 0, 0, ReplayButtonState.None);
            spectatorClient.SendFrames(user.Id, new FrameDataBundle(score.ScoreInfo, scoreProcessor, new[] { frame })).FireAndForget();
        }

        private void finish()
        {
            if (ended)
                return;

            scoreProcessor.PopulateScore(score.ScoreInfo);
            sendSnapshot();

            ended = true;
            spectatorClient.EndUser(user.Id, SpectatedUserState.Passed).FireAndForget();
            completion.TrySetResult(score.ScoreInfo.DeepClone());
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && started && !ended)
            {
                ended = true;
                spectatorClient.EndUser(user.Id, SpectatedUserState.Quit).FireAndForget();
                completion.TrySetCanceled();
            }

            base.Dispose(isDisposing);
        }
    }
}
