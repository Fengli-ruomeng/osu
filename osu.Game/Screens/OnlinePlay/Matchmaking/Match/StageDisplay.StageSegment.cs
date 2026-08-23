// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.Matchmaking;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Match
{
    public partial class StageDisplay
    {
        internal partial class StageSegment : CompositeDrawable
        {
            [Resolved]
            private MultiplayerClient client { get; set; } = null!;

            public readonly int? Round;

            private readonly MatchmakingStage stage;
            private readonly MatchmakingStageState? localState;

            private readonly LocalisableString displayText;
            private Drawable progressBar = null!;

            private DateTimeOffset countdownStartTime;
            private DateTimeOffset countdownEndTime;
            private SpriteIcon arrow = null!;

            private Sample? segmentStartedSample;

            private Container mainContent = null!;

            public bool Active { get; private set; }

            public float Progress => progressBar.Width;

            public StageSegment(int? round, MatchmakingStage stage, LocalisableString displayText, MatchmakingStageState? localState = null)
            {
                Round = round;

                this.stage = stage;
                this.displayText = displayText;
                this.localState = localState;

                AutoSizeAxes = Axes.Both;

                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, OverlayColourProvider colourProvider)
            {
                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        arrow = new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Alpha = 0.5f,
                            Size = new Vector2(16),
                            Icon = FontAwesome.Solid.ArrowRight,
                            Margin = new MarginPadding { Horizontal = 10 }
                        },
                        mainContent = new Container
                        {
                            Masking = true,
                            CornerRadius = 5,
                            CornerExponent = 10,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Children = new[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Dark3,
                                },
                                progressBar = new Box
                                {
                                    Blending = BlendingParameters.Additive,
                                    EdgeSmoothness = new Vector2(1),
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0,
                                    Colour = colourProvider.Colour3,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = displayText,
                                    Padding = new MarginPadding(10)
                                }
                            }
                        }
                    }
                };

                Alpha = 0.5f;
                segmentStartedSample = audio.Samples.Get(@"Multiplayer/Matchmaking/stage-segment");
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (localState != null)
                {
                    localState.Stage.BindValueChanged(onLocalStageChanged, true);
                    localState.CurrentRound.BindValueChanged(onLocalRoundChanged);
                    localState.CountdownEnd.BindValueChanged(onLocalCountdownChanged, true);
                    return;
                }

                client.MatchRoomStateChanged += onMatchRoomStateChanged;
                client.CountdownStarted += onCountdownStarted;
                client.CountdownStopped += onCountdownStopped;

                if (client.Room != null)
                {
                    onMatchRoomStateChanged(client.Room.MatchState);
                    foreach (var countdown in client.Room.ActiveCountdowns)
                        onCountdownStarted(countdown);
                }
            }

            protected override void Update()
            {
                base.Update();

                if (!Active)
                    return;

                TimeSpan total = countdownEndTime - countdownStartTime;
                TimeSpan elapsed = DateTimeOffset.Now - countdownStartTime;

                if (total.TotalMilliseconds <= 0)
                {
                    progressBar.Width = 0;
                    return;
                }

                progressBar.Width = (float)Math.Clamp(elapsed.TotalMilliseconds / total.TotalMilliseconds, 0, 1);
            }

            private void onMatchRoomStateChanged(MatchRoomState? state) => Scheduler.Add(() =>
            {
                if (state is not MatchmakingRoomState roomState)
                    return;

                updateActive(roomState.Stage, roomState.CurrentRound);
            });

            private void onLocalStageChanged(ValueChangedEvent<MatchmakingStage> _) => Scheduler.Add(updateFromLocalState);

            private void onLocalRoundChanged(ValueChangedEvent<int> _) => Scheduler.Add(updateFromLocalState);

            private void updateFromLocalState()
            {
                if (localState == null)
                    return;

                updateActive(localState.Stage.Value, localState.CurrentRound.Value);
            }

            private void updateActive(MatchmakingStage currentStage, int currentRound)
            {
                bool wasActive = Active;
                Active = (Round == null || currentRound == Round) && stage == currentStage;

                if (wasActive)
                    progressBar.Width = 1;

                mainContent.ScaleTo(Active ? 1.3f : 1, 500, Easing.OutQuint);

                bool isPreparing =
                    (stage == MatchmakingStage.RoundWarmupTime && currentStage == MatchmakingStage.WaitingForClientsJoin) ||
                    (stage == MatchmakingStage.GameplayWarmupTime && currentStage == MatchmakingStage.WaitingForClientsBeatmapDownload) ||
                    (stage == MatchmakingStage.ResultsDisplaying && currentStage == MatchmakingStage.Gameplay);

                if (isPreparing)
                {
                    arrow.FadeTo(1, 500)
                         .Then()
                         .FadeTo(0.5f, 500)
                         .Loop();
                }
                else
                {
                    arrow.ClearTransforms();
                    arrow.FadeTo(Active ? 1 : 0.5f, 200);
                }
            }

            private void onLocalCountdownChanged(ValueChangedEvent<DateTimeOffset?> e) => Scheduler.Add(() =>
            {
                countdownStartTime = DateTimeOffset.Now;
                countdownEndTime = e.NewValue ?? countdownStartTime;

                if (Active && e.NewValue != null)
                {
                    this.FadeIn(200);
                    segmentStartedSample?.Play();
                }
            });

            private void onCountdownStarted(MultiplayerCountdown countdown) => Scheduler.Add(() =>
            {
                if (!Active)
                    return;

                if (countdown is not MatchmakingStageCountdown)
                    return;

                countdownStartTime = DateTimeOffset.Now;
                countdownEndTime = countdownStartTime + countdown.TimeRemaining;
                arrow.FadeIn(500, Easing.OutQuint);

                this.FadeIn(200);

                segmentStartedSample?.Play();
            });

            private void onCountdownStopped(MultiplayerCountdown countdown) => Scheduler.Add(() =>
            {
                if (!Active)
                    return;

                if (countdown is not MatchmakingStageCountdown)
                    return;

                countdownEndTime = DateTimeOffset.Now;
            });

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (localState != null)
                {
                    localState.Stage.ValueChanged -= onLocalStageChanged;
                    localState.CurrentRound.ValueChanged -= onLocalRoundChanged;
                    localState.CountdownEnd.ValueChanged -= onLocalCountdownChanged;
                    return;
                }

                if (client.IsNotNull())
                {
                    client.MatchRoomStateChanged -= onMatchRoomStateChanged;
                    client.CountdownStarted -= onCountdownStarted;
                    client.CountdownStopped -= onCountdownStopped;
                }
            }
        }
    }
}
