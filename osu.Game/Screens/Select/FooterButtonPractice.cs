// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Threading;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class FooterButtonPractice : ScreenFooterButton, IHasPopover
    {
        private readonly PracticeModeState practiceMode;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        public FooterButtonPractice(PracticeModeState practiceMode)
        {
            this.practiceMode = practiceMode;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            Text = "practice";
            Icon = FontAwesome.Solid.Crosshairs;
            AccentColour = colour.Green;

            Action = () =>
            {
                if (this.FindClosestParent<PopoverContainer>()?.CurrentTarget == this)
                    this.HidePopover();
                else
                    this.ShowPopover();
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            workingBeatmap.BindValueChanged(_ => updateDuration(), true);
            practiceMode.Enabled.BindValueChanged(enabled => OverlayState.Value = enabled.NewValue ? Visibility.Visible : Visibility.Hidden, true);
        }

        private void updateDuration()
        {
            Enabled.Value = !workingBeatmap.IsDefault;

            if (workingBeatmap.IsDefault)
                return;

            double maximumPracticeStartTime = 0;

            try
            {
                IBeatmap beatmap = workingBeatmap.Value.Beatmap;

                maximumPracticeStartTime = PracticeBeatmapDifficulty.GetMaximumStartTime(beatmap);
            }
            catch
            {
                maximumPracticeStartTime = Math.Max(0, workingBeatmap.Value.BeatmapInfo.Length - PracticeBeatmapDifficulty.MinimumRemainingGameplayTime);
            }

            practiceMode.StartTime.MaxValue = maximumPracticeStartTime;
            practiceMode.StartTime.Value = Math.Clamp(practiceMode.StartTime.Value, practiceMode.StartTime.MinValue, practiceMode.StartTime.MaxValue);
        }

        public Framework.Graphics.UserInterface.Popover GetPopover() => new Popover(this, practiceMode)
        {
            ColourProvider = colourProvider,
        };

        private partial class Popover : OsuPopover
        {
            private readonly FooterButtonPractice footerButton;
            private readonly PracticeModeState practiceMode;

            private OsuSpriteText timeText = null!;
            private TimeSlider slider = null!;
            private StarRatingDisplay starRatingDisplay = null!;
            private ScheduledDelegate? scheduledDifficultyUpdate;
            private CancellationTokenSource? difficultyCancellationSource;
            private ModSettingChangeTracker? modSettingChangeTracker;

            [Resolved]
            private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            [Resolved]
            private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

            public required OverlayColourProvider ColourProvider { get; init; }

            public Popover(FooterButtonPractice footerButton, PracticeModeState practiceMode)
            {
                this.footerButton = footerButton;
                this.practiceMode = practiceMode;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Content.Padding = new MarginPadding(10);

                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    Width = 320,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        new OsuCheckbox
                        {
                            LabelText = "Enable",
                            Current = { BindTarget = practiceMode.Enabled },
                        },
                        timeText = new OsuSpriteText
                        {
                            Font = OsuFont.Default.With(size: 18, weight: FontWeight.SemiBold),
                        },
                        slider = new TimeSlider
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = practiceMode.StartTime,
                            AccentColour = colours.Green,
                            BackgroundColour = ColourProvider.Background4.Opacity(0.8f),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = "segment difficulty",
                                    Font = OsuFont.Default.With(size: 13),
                                    Colour = ColourProvider.Content2,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                starRatingDisplay = new StarRatingDisplay(new StarDifficulty(-1, 0), StarRatingDisplaySize.Small, animated: true)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                            }
                        },
                        new OsuSpriteText
                        {
                            Text = "unranked, no replay, no submission",
                            Font = OsuFont.Default.With(size: 13),
                            Colour = ColourProvider.Content2,
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                practiceMode.Enabled.BindValueChanged(_ => updateDisplay(), true);
                practiceMode.StartTime.BindValueChanged(_ => updateDisplay(), true);
                footerButton.workingBeatmap.BindValueChanged(_ => queueDifficultyUpdate(), true);
                ruleset.BindValueChanged(_ => queueDifficultyUpdate(), true);
                mods.BindValueChanged(mods =>
                {
                    modSettingChangeTracker?.Dispose();
                    modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
                    modSettingChangeTracker.SettingChanged += _ => queueDifficultyUpdate();

                    queueDifficultyUpdate();
                }, true);
            }

            protected override void UpdateState(ValueChangedEvent<Visibility> state)
            {
                base.UpdateState(state);
                footerButton.OverlayState.Value = practiceMode.Enabled.Value ? Visibility.Visible : state.NewValue;
            }

            private void updateDisplay()
            {
                slider.Alpha = practiceMode.Enabled.Value ? 1 : 0.4f;
                starRatingDisplay.Alpha = practiceMode.Enabled.Value ? 1 : 0.4f;
                timeText.Text = practiceMode.Enabled.Value ? $"start at {formatTime(practiceMode.StartTime.Value)}" : $"selected {formatTime(practiceMode.StartTime.Value)}";
                queueDifficultyUpdate();
            }

            private void queueDifficultyUpdate()
            {
                scheduledDifficultyUpdate?.Cancel();
                scheduledDifficultyUpdate = Scheduler.AddDelayed(updateDifficulty, 150);
            }

            private void updateDifficulty()
            {
                difficultyCancellationSource?.Cancel();
                difficultyCancellationSource?.Dispose();

                var cancellationSource = difficultyCancellationSource = new CancellationTokenSource();

                if (footerButton.workingBeatmap.IsDefault)
                {
                    starRatingDisplay.Current.Value = new StarDifficulty(-1, 0);
                    return;
                }

                difficultyCache.GetPracticeDifficultyAsync(footerButton.workingBeatmap.Value, ruleset.Value, mods.Value, practiceMode.StartTime.Value, cancellationSource.Token)
                               .ContinueWith(task => Schedule(() =>
                               {
                                   if (cancellationSource.IsCancellationRequested)
                                       return;

                                   starRatingDisplay.Current.Value = task.GetResultSafely() ?? new StarDifficulty(-1, 0);
                               }), TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            private static LocalisableString formatTime(double time)
            {
                var span = TimeSpan.FromMilliseconds(Math.Round(time));
                return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
            }

            protected override void Dispose(bool isDisposing)
            {
                scheduledDifficultyUpdate?.Cancel();
                difficultyCancellationSource?.Cancel();
                difficultyCancellationSource?.Dispose();
                modSettingChangeTracker?.Dispose();

                base.Dispose(isDisposing);
            }
        }
    }
}
