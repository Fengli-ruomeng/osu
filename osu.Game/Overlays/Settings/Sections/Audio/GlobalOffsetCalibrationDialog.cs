// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;
using osuTK;
using osuTK.Graphics;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class GlobalOffsetCalibrationDialog : PopupDialog
    {
        public GlobalOffsetCalibrationDialog(Bindable<double> globalOffset)
        {
            Icon = OsuIcon.Metronome;
            HeaderText = AudioSettingsStrings.OffsetWizard;
            BodyText = "Follow the metronome by ear and tap in time. Two consistent rounds are required before the result can be applied to global offset.";

            MainContent.AutoSizeAxes = Axes.None;
            MainContent.Height = 410;
            MainContent.Child = new GlobalOffsetCalibrationControl(globalOffset);

            Buttons = new[]
            {
                new PopupDialogCancelButton { Text = "Close" },
            };
        }
    }

    public partial class GlobalOffsetCalibrationControl : CompositeDrawable, IBeatSyncProvider
    {
        private const int osu_left_button_action = 0;
        private const int osu_right_button_action = 1;

        private readonly Bindable<double> globalOffset;
        private readonly StopwatchClock calibrationClock = new StopwatchClock();
        private readonly ControlPointInfo controlPoints = new ControlPointInfo();
        private readonly BindableBeatDivisor beatDivisor = new BindableBeatDivisor();
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private OffsetCalibrationSession? session;
        private IDisposable? musicDuckOperation;
        private Sample? metronomeTick;
        private Sample? metronomeDownbeat;
        private int lastDispatchedBeat = -1;

        private OsuSpriteText statusText = null!;
        private OsuTextFlowContainer detailText = null!;
        private Box progressFill = null!;
        private CalibrationTapButton tapButton = null!;
        private RoundedButton startButton = null!;
        private RoundedButton applyButton = null!;
        private CalibrationKeyBindingContainer keyBindingContainer = null!;

        public string BindingInstruction { get; private set; } = string.Empty;

        internal int ReceivedHitKeyPresses { get; private set; }

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private ReadableKeyCombinationProvider readableKeyCombinationProvider { get; set; } = null!;

        public GlobalOffsetCalibrationControl(Bindable<double> globalOffset)
        {
            this.globalOffset = globalOffset;

            RelativeSizeAxes = Axes.X;
            Height = 410;

            controlPoints.Add(0, new TimingControlPoint
            {
                BeatLength = OffsetCalibrationSession.BEAT_LENGTH,
                TimeSignature = TimeSignature.SimpleQuadruple,
            });
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBeatSyncProvider>(this);
            dependencies.CacheAs(beatDivisor);
            dependencies.CacheAs(colourProvider);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audioManager, RulesetStore rulesets)
        {
            metronomeTick = audioManager.Samples.Get(@"UI/metronome-tick");
            metronomeDownbeat = audioManager.Samples.Get(@"UI/metronome-tick-downbeat");

            RulesetInfo osuRuleset = rulesets.GetRuleset(0) ?? throw new InvalidOperationException("osu!standard ruleset is unavailable.");

            InternalChild = keyBindingContainer = new CalibrationKeyBindingContainer(osuRuleset)
            {
                RelativeSizeAxes = Axes.Both,
                BindingsLoaded = updateBindingDescription,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Padding = new MarginPadding { Horizontal = 25, Top = 10 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 155,
                            Child = new MetronomeDisplay
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                EnableClicking = false,
                            }
                        },
                        statusText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Torus.With(size: 20, weight: FontWeight.SemiBold),
                            Text = "Ready to calibrate",
                        },
                        detailText = new OsuTextFlowContainer(text => text.Font = OsuFont.Torus.With(size: 15))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 8,
                            Masking = true,
                            CornerRadius = 4,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Background4,
                                },
                                progressFill = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0,
                                    Colour = colourProvider.Colour1,
                                },
                            }
                        },
                        tapButton = new CalibrationTapButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 75,
                            Action = registerTap,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(10),
                            Children = new Drawable[]
                            {
                                startButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 45,
                                    Text = "Start calibration",
                                    Action = startOrRetry,
                                },
                                applyButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 45,
                                    Text = "Apply global offset",
                                    Action = applyResult,
                                },
                            }
                        },
                    }
                }
            };

            tapButton.Enabled.Value = false;
            setApplyButtonVisible(false);
            keyBindingContainer.Add(new CalibrationInputHandler(registerTapFromHitKey));
        }

        private void updateBindingDescription()
        {
            string[] bindings = keyBindingContainer.GetHitKeyBindings()
                                                   .Select(readableKeyCombinationProvider.GetReadableString)
                                                   .Distinct()
                                                   .ToArray();

            detailText.Text = BindingInstruction = bindings.Length > 0
                ? $"Use {string.Join(" or ", bindings)} to tap, or click the tap button. Listen to the sound rather than following the animation."
                : "Click the tap button in time with the sound.";
        }

        protected override void Update()
        {
            base.Update();

            if (session?.State != OffsetCalibrationSession.CalibrationState.Running)
                return;

            dispatchMetronomeBeat();

            int warmupRemaining = session.GetWarmupBeatsRemaining(calibrationClock.CurrentTime);

            if (warmupRemaining > 0)
            {
                statusText.Text = $"Listen first... {warmupRemaining} warmup beats remaining";
                detailText.Text = $"Round {session.CompletedRounds + 1} of {OffsetCalibrationSession.REQUIRED_ROUNDS}";
                tapButton.Enabled.Value = false;
            }
            else
            {
                statusText.Text = $"Tap with the beat - {session.RecordedTaps}/{OffsetCalibrationSession.TAPS_PER_ROUND}";
                detailText.Text = $"Round {session.CompletedRounds + 1} of {OffsetCalibrationSession.REQUIRED_ROUNDS}";
                tapButton.Enabled.Value = true;
            }
        }

        private void startOrRetry()
        {
            if (session == null || session.State == OffsetCalibrationSession.CalibrationState.Completed)
                session = new OffsetCalibrationSession(globalOffset.Value, FramedBeatmapClock.GetPlatformOffset(audio.UseExperimentalWasapi.Value));

            musicDuckOperation ??= musicController.Duck(new DuckParameters
            {
                DuckVolumeTo = 0,
                DuckDuration = 150,
                RestoreDuration = 250,
            });

            calibrationClock.Stop();
            calibrationClock.Reset();
            session.StartRound(0);
            lastDispatchedBeat = -1;
            calibrationClock.Start();

            progressFill.Width = 0;
            startButton.Hide();
            setApplyButtonVisible(false);
            tapButton.Enabled.Value = false;
        }

        private void registerTap()
        {
            if (session?.RegisterTap(calibrationClock.CurrentTime) != true)
                return;

            tapButton.Flash();
            progressFill.ResizeWidthTo((float)session.RecordedTaps / OffsetCalibrationSession.TAPS_PER_ROUND, 100, Easing.OutQuint);

            if (session.State == OffsetCalibrationSession.CalibrationState.Running)
                return;

            calibrationClock.Stop();
            tapButton.Enabled.Value = false;
            updateCompletedRoundState();
        }

        private void registerTapFromHitKey()
        {
            ReceivedHitKeyPresses++;
            registerTap();
        }

        private void dispatchMetronomeBeat()
        {
            if (session == null)
                return;

            int beatIndex = (int)Math.Floor(calibrationClock.CurrentTime / OffsetCalibrationSession.BEAT_LENGTH);

            if (beatIndex == lastDispatchedBeat)
                return;

            lastDispatchedBeat = beatIndex;
            double dispatchTime = calibrationClock.CurrentTime;
            session.RegisterBeat(beatIndex, dispatchTime);

            if (beatIndex % 4 == 0)
                metronomeDownbeat?.Play();
            else
                metronomeTick?.Play();
        }

        private void updateCompletedRoundState()
        {
            if (session == null)
                return;

            if (session.State == OffsetCalibrationSession.CalibrationState.BetweenRounds)
            {
                OffsetCalibrationSession.RoundResult result = session.RoundResults[0];
                statusText.Text = $"Round 1 suggests {result.SuggestedOffset:N0} ms";
                detailText.Text = $"Timing spread: +/-{result.Spread:N1} ms. Run the second round to verify it.";
                startButton.Text = "Start round 2";
                startButton.Show();
                return;
            }

            startButton.Text = "Try again";
            startButton.Show();

            if (session.IsReliable)
            {
                statusText.Text = $"Suggested global offset: {session.SuggestedOffset:N0} ms";
                detailText.Text = "Both rounds agree. You can safely apply this result.";
                applyButton.Enabled.Value = true;
                setApplyButtonVisible(true);
            }
            else
            {
                statusText.Text = "The two rounds were not consistent enough";
                detailText.Text = "Keep a relaxed rhythm and try again. No setting has been changed.";
                setApplyButtonVisible(false);
            }
        }

        private void setApplyButtonVisible(bool visible)
        {
            startButton.Width = visible ? 0.49f : 1;
            applyButton.Width = 0.49f;

            if (visible)
                applyButton.Show();
            else
                applyButton.Hide();
        }

        private void applyResult()
        {
            if (session?.SuggestedOffset is not double suggestedOffset || !session.IsReliable)
                return;

            globalOffset.Value = Math.Clamp(suggestedOffset, -500, 500);
            statusText.Text = $"Global offset set to {globalOffset.Value:N0} ms";
            detailText.Text = "Calibration complete. You may close this window.";
            applyButton.Enabled.Value = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            calibrationClock.Stop();
            musicDuckOperation?.Dispose();
            base.Dispose(isDisposing);
        }

        ControlPointInfo IBeatSyncProvider.ControlPoints => controlPoints;
        IClock IBeatSyncProvider.Clock => calibrationClock;
        ChannelAmplitudes IHasAmplitudes.CurrentAmplitudes => ChannelAmplitudes.Empty;

        private partial class CalibrationTapButton : RoundedButton
        {
            public void Flash() => this.FlashColour(Color4.White, 120, Easing.OutQuint);

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (!Enabled.Value)
                    return false;

                Action?.Invoke();
                return true;
            }

            protected override bool OnClick(ClickEvent e) => true;

            public CalibrationTapButton()
            {
                Text = "Tap";
            }
        }

        private partial class CalibrationInputHandler : Component, IKeyBindingHandler<int>
        {
            private readonly Action action;

            public CalibrationInputHandler(Action action)
            {
                this.action = action;
            }

            public bool OnPressed(KeyBindingPressEvent<int> e)
            {
                if (e.Repeat || (e.Action != osu_left_button_action && e.Action != osu_right_button_action))
                    return false;

                action();
                return true;
            }

            public void OnReleased(KeyBindingReleaseEvent<int> e)
            {
            }
        }

        private partial class CalibrationKeyBindingContainer : DatabasedKeyBindingContainer<int>
        {
            public Action? BindingsLoaded;

            private static readonly InputKey[] mouse_inputs =
            {
                InputKey.MouseLeft,
                InputKey.MouseMiddle,
                InputKey.MouseRight,
                InputKey.MouseWheelDown,
                InputKey.MouseWheelLeft,
                InputKey.MouseWheelRight,
                InputKey.MouseWheelUp,
            };

            private readonly RulesetInfo ruleset;

            protected override bool HandleRepeats => false;

            public override IEnumerable<IKeyBinding> DefaultKeyBindings => ruleset.CreateInstance().GetDefaultKeyBindings()
                                                                                  .Where(binding => isHitAction(Convert.ToInt32(binding.Action)))
                                                                                  .Select(binding => new KeyBinding(binding.KeyCombination, Convert.ToInt32(binding.Action)));

            public CalibrationKeyBindingContainer(RulesetInfo ruleset)
                : base(ruleset, 0, SimultaneousBindingMode.Unique)
            {
                this.ruleset = ruleset;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                BindingsLoaded?.Invoke();
            }

            protected override void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
            {
                base.ReloadMappings(realmKeyBindings);

                KeyBindings = KeyBindings.Where(binding => isHitAction(Convert.ToInt32(binding.Action))
                                                            && !binding.KeyCombination.Keys.Any(mouse_inputs.Contains)).ToList();
                RealmKeyBindingStore.ClearDuplicateBindings(KeyBindings);
            }

            public IEnumerable<KeyCombination> GetHitKeyBindings() => KeyBindings.Select(binding => binding.KeyCombination);

            private static bool isHitAction(int action) => action == osu_left_button_action || action == osu_right_button_action;
        }
    }
}
