// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class PerformanceCalculatorOverlay : ShearedOverlayContainer
    {
        public readonly BindableBool IsAvailable = new BindableBool();

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        private readonly CompactNumberField countPerfect = new CompactNumberField("MAX / 320");
        private readonly CompactNumberField countGreat = new CompactNumberField("300");
        private readonly CompactNumberField countGood = new CompactNumberField("200");
        private readonly CompactNumberField countOk = new CompactNumberField("100");
        private readonly CompactNumberField countMeh = new CompactNumberField("50");
        private readonly CompactNumberField countMiss = new CompactNumberField("Miss");
        private readonly CompactNumberField maxCombo = new CompactNumberField("Highest combo");
        private readonly CompactNumberField countSliderTickMiss = new CompactNumberField("Tick misses");
        private readonly CompactNumberField countSliderTailDropped = new CompactNumberField("Tails dropped");
        private readonly OsuCheckbox autoFillBest = new OsuCheckbox { LabelText = "Auto-fill best judgement" };

        private OsuSpriteText beatmapTitleText = null!;
        private OsuSpriteText beatmapDetailsText = null!;
        private OsuTextFlowContainer inputDescriptionText = null!;
        private OsuSpriteText resultText = null!;
        private OsuSpriteText accuracyText = null!;
        private OsuTextFlowContainer breakdownText = null!;
        private OsuTextFlowContainer statusText = null!;
        private OsuTextFlowContainer judgementTotalText = null!;
        private GridContainer judgementGrid = null!;
        private GridContainer secondaryGrid = null!;
        private Container judgementTotalContainer = null!;
        private Container sliderToggleContainer = null!;
        private Container sliderDetailsContainer = null!;
        private OsuButton sliderDetailsButton = null!;

        private OsuColour colours = null!;
        private CancellationTokenSource? difficultyCancellation;
        private DifficultyAttributes? difficultyAttributes;
        private Dictionary<HitResult, int> maximumStatistics = new Dictionary<HitResult, int>();
        private BeatmapInfo? calculationBeatmap;
        private RulesetInfo? calculationRuleset;
        private Mod[] calculationMods = Array.Empty<Mod>();
        private ScoreProcessor? calculationScoreProcessor;
        private SupportedRuleset calculationMode;
        private bool resettingInputs;
        private bool updatingAutoFill;
        private bool sliderDetailsExpanded;
        private bool draftInvalidated;

        public PerformanceCalculatorOverlay()
            : base(OverlayColourScheme.Purple)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            this.colours = colours;

            Header.Title = "PP calculator";
            Header.Description = "Estimate performance from a hypothetical score.";

            judgementGrid = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
            };

            secondaryGrid = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 72,
            };

            sliderToggleContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
                Child = sliderDetailsButton = new RoundedButton
                {
                    RelativeSizeAxes = Axes.Both,
                    Text = "Slider details",
                    Action = () => setSliderDetailsExpanded(!sliderDetailsExpanded),
                }
            };

            sliderDetailsContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 0,
                Alpha = 0,
                Masking = true,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            countSliderTickMiss,
                            countSliderTailDropped,
                        }
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Relative, 0.5f),
                        new Dimension(GridSizeMode.Relative, 0.5f),
                    }
                }
            };

            judgementTotalContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(6),
                Child = judgementTotalText = new OsuTextFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    TextAnchor = Anchor.CentreLeft,
                    Text = "Judgements\n-- / --",
                    Colour = ColourProvider.Content2,
                }
            };

            var inputContent = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(20),
                Spacing = new Vector2(0, 13),
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 48,
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new OsuTextFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Text = "Score input",
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Left = 12 },
                                    Child = autoFillBest,
                                },
                            }
                        },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Relative, 0.58f),
                            new Dimension(GridSizeMode.Relative, 0.42f),
                        }
                    },
                    inputDescriptionText = new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Colour = ColourProvider.Content2,
                    },
                    judgementGrid,
                    secondaryGrid,
                    sliderToggleContainer,
                    sliderDetailsContainer,
                }
            };

            var resultContent = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(22),
                Spacing = new Vector2(0, 13),
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Estimated performance",
                        Font = OsuFont.TorusAlternate.With(size: 16, weight: FontWeight.SemiBold),
                        Colour = ColourProvider.Content2,
                    },
                    resultText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 40),
                        Text = "-- pp",
                    },
                    accuracyText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 18),
                        Text = "-- accuracy",
                    },
                    breakdownText = new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                    statusText = new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                }
            };

            MainAreaContent.Child = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.Both,
                Width = 0.84f,
                Padding = new MarginPadding { Top = 24, Bottom = 24 },
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            createCard(
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Horizontal = 20, Vertical = 14 },
                                    Spacing = new Vector2(0, 4),
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        beatmapTitleText = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.TorusAlternate.With(size: 18, weight: FontWeight.SemiBold),
                                        },
                                        beatmapDetailsText = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Torus.With(size: 14),
                                            Colour = ColourProvider.Content2,
                                        },
                                    }
                                }, ColourProvider.Background3),
                        },
                        new Drawable[] { Empty() },
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        createCard(new OsuScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Child = inputContent,
                                        }, ColourProvider.Background4),
                                        Empty(),
                                        createCard(new OsuScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Child = resultContent,
                                        }, ColourProvider.Background4),
                                    }
                                },
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Relative, 0.64f),
                                    new Dimension(GridSizeMode.Absolute, 16),
                                    new Dimension(),
                                }
                            }
                        }
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 82),
                        new Dimension(GridSizeMode.Absolute, 16),
                        new Dimension(),
                    },
                }
            };

            foreach (var input in inputs)
                input.Current.BindValueChanged(_ => onInputChanged(input));

            autoFillBest.Current.Value = true;
            configureInputLayout(SupportedRuleset.Osu);
            autoFillBest.Current.BindValueChanged(_ => autoFillChanged(), true);

            beatmap.BindValueChanged(_ => selectionChanged(), true);
            ruleset.BindValueChanged(_ => selectionChanged(), true);
            mods.BindValueChanged(_ => selectionChanged(), true);
        }

        private IEnumerable<CompactNumberField> inputs
        {
            get
            {
                yield return countPerfect;
                yield return countGreat;
                yield return countGood;
                yield return countOk;
                yield return countMeh;
                yield return countMiss;
                yield return maxCombo;
                yield return countSliderTickMiss;
                yield return countSliderTailDropped;
            }
        }

        private void selectionChanged()
        {
            if (calculationBeatmap != null
                && (beatmap.IsDefault
                    || calculationBeatmap.ID != beatmap.Value.BeatmapInfo.ID
                    || calculationRuleset?.OnlineID != ruleset.Value.OnlineID))
            {
                draftInvalidated = true;
            }

            bool supportedRulesetSelected = ruleset.Value.OnlineID is 0 or 3;
            IsAvailable.Value = supportedRulesetSelected && !beatmap.IsDefault;

            if (!IsAvailable.Value)
            {
                difficultyCancellation?.Cancel();
                difficultyAttributes = null;

                if (State.Value == Visibility.Visible)
                    Hide();

                return;
            }

            difficultyCancellation?.Cancel();
            difficultyCancellation?.Dispose();
            difficultyCancellation = new CancellationTokenSource();

            _ = refreshDifficulty(difficultyCancellation.Token);
        }

        private async Task refreshDifficulty(CancellationToken cancellationToken)
        {
            var selectedBeatmap = beatmap.Value.BeatmapInfo;
            var selectedWorkingBeatmap = beatmap.Value;
            var selectedRuleset = ruleset.Value;
            var selectedMods = mods.Value.Select(m => m.DeepClone()).ToArray();

            Schedule(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                difficultyAttributes = null;
                resultText.Text = "-- pp";
                accuracyText.Text = "-- accuracy";
                breakdownText.Text = string.Empty;
                clearInputErrors();
                setJudgementTotal(null, null);
                setStatus("Calculating difficulty...", colours.Yellow);
            });

            try
            {
                var starDifficulty = await Dependencies.Get<BeatmapDifficultyCache>()
                                                       .GetDifficultyAsync(selectedBeatmap, selectedRuleset, selectedMods, cancellationToken)
                                                       .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                var rulesetInstance = selectedRuleset.CreateInstance();
                var playableBeatmap = selectedWorkingBeatmap.GetPlayableBeatmap(rulesetInstance.RulesetInfo, selectedMods, cancellationToken);
                ScoreProcessor scoreProcessor = rulesetInstance.CreateScoreProcessor();
                scoreProcessor.Mods.Value = selectedMods;
                scoreProcessor.ApplyBeatmap(playableBeatmap);
                var selectedMaximumStatistics = new Dictionary<HitResult, int>(scoreProcessor.MaximumStatistics);

                Schedule(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        applyDifficulty(selectedBeatmap, selectedRuleset, selectedMods, selectedMaximumStatistics, scoreProcessor, starDifficulty);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to prepare the PP calculator scenario.");

                Schedule(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        setStatus("Could not calculate difficulty for this beatmap.", colours.Red1);
                });
            }
        }

        private void applyDifficulty(BeatmapInfo selectedBeatmap, RulesetInfo selectedRuleset, Mod[] selectedMods,
                                     Dictionary<HitResult, int> selectedMaximumStatistics, ScoreProcessor scoreProcessor, StarDifficulty? starDifficulty)
        {
            if (starDifficulty?.DifficultyAttributes is not DifficultyAttributes selectedDifficulty)
            {
                setStatus("Difficulty data is unavailable. Make sure the beatmap is downloaded locally.", colours.Red1);
                return;
            }

            SupportedRuleset selectedMode = selectedRuleset.OnlineID == 3 ? SupportedRuleset.Mania : SupportedRuleset.Osu;
            int selectedTotalJudgements = getTotalJudgements(selectedMode, selectedMaximumStatistics);
            int? previousTotalJudgements = calculationBeatmap == null ? null : getTotalJudgements(calculationMode, maximumStatistics);
            bool preserveInputs = !draftInvalidated
                                  && calculationBeatmap?.ID == selectedBeatmap.ID
                                  && calculationRuleset?.OnlineID == selectedRuleset.OnlineID
                                  && previousTotalJudgements == selectedTotalJudgements;

            difficultyAttributes = selectedDifficulty;
            maximumStatistics = selectedMaximumStatistics;
            calculationBeatmap = selectedBeatmap;
            calculationRuleset = selectedRuleset;
            calculationMods = selectedMods;
            calculationScoreProcessor = scoreProcessor;
            calculationMode = selectedMode;
            draftInvalidated = false;

            string displayedMods = selectedMods.Length == 0 ? "No Mod" : string.Join(", ", selectedMods.Select(m => m.Acronym));
            string comboDisplay = calculationMode == SupportedRuleset.Mania ? string.Empty : $"  •  {selectedDifficulty.MaxCombo:N0}x max combo";

            beatmapTitleText.Text = selectedBeatmap.ToString();
            beatmapDetailsText.Text = $"{starDifficulty.Value.Stars:0.00}★  •  {selectedTotalJudgements:N0} judgements{comboDisplay}  •  {displayedMods}";

            countSliderTickMiss.Label = $"Tick misses (max {maximumStatistics.GetValueOrDefault(HitResult.LargeTickHit):N0})";
            countSliderTailDropped.Label = $"Tails dropped (max {maximumStatistics.GetValueOrDefault(HitResult.SliderTailHit):N0})";
            configureInputLayout(calculationMode);

            if (!preserveInputs)
            {
                resetToPerfect();
                return;
            }

            if (autoFillBest.Current.Value)
                updateAutoFilledBestJudgement();

            updateCalculation();
        }

        private void updateCalculation()
        {
            if (resettingInputs || difficultyAttributes == null || calculationBeatmap == null || calculationRuleset == null || calculationScoreProcessor == null)
                return;

            clearInputErrors();

            int expectedJudgements = getTotalJudgements(calculationMode, maximumStatistics);

            ScoreInfo score;
            string calculationStatus;

            if (calculationMode == SupportedRuleset.Mania)
            {
                bool allFieldsValid = tryParse(countPerfect, out int perfect)
                                      & tryParse(countGreat, out int great)
                                      & tryParse(countGood, out int good)
                                      & tryParse(countOk, out int ok)
                                      & tryParse(countMeh, out int meh)
                                      & tryParse(countMiss, out int miss);

                if (!allFieldsValid)
                {
                    setJudgementTotal(null, expectedJudgements);
                    clearResult("All fields must contain non-negative whole numbers.");
                    return;
                }

                long enteredJudgements = (long)perfect + great + good + ok + meh + miss;
                setJudgementTotal(enteredJudgements, expectedJudgements);

                var input = new ManiaPerformanceCalculationInput(perfect, great, good, ok, meh, miss);
                var validationError = ManiaPerformanceCalculationScenario.Validate(input, maximumStatistics);

                if (validationError == ManiaPerformanceCalculationInputError.NegativeJudgementCount)
                {
                    clearResult("Judgement counts cannot be negative.");
                    return;
                }

                if (validationError == ManiaPerformanceCalculationInputError.JudgementCountMismatch)
                {
                    showJudgementCountMismatch(enteredJudgements, expectedJudgements);
                    return;
                }

                score = ManiaPerformanceCalculationScenario.CreateScore(calculationBeatmap, calculationRuleset, calculationMods, difficultyAttributes,
                                                                          maximumStatistics, calculationScoreProcessor, input);
                calculationStatus = string.Empty;
            }
            else
            {
                bool judgementFieldsValid = tryParse(countGreat, out int great)
                                            & tryParse(countOk, out int ok)
                                            & tryParse(countMeh, out int meh)
                                            & tryParse(countMiss, out int miss);
                bool secondaryFieldsValid = tryParse(maxCombo, out int combo)
                                            & tryParse(countSliderTickMiss, out int sliderTickMiss)
                                            & tryParse(countSliderTailDropped, out int sliderTailDropped);

                if (judgementFieldsValid)
                    setJudgementTotal((long)great + ok + meh + miss, expectedJudgements);
                else
                    setJudgementTotal(null, expectedJudgements);

                if (!judgementFieldsValid || !secondaryFieldsValid)
                {
                    clearResult("All fields must contain non-negative whole numbers.");
                    return;
                }

                var input = new OsuPerformanceCalculationInput(great, ok, meh, miss, combo, sliderTickMiss, sliderTailDropped);
                var validationError = OsuPerformanceCalculationScenario.Validate(input, difficultyAttributes, maximumStatistics);

                if (validationError == OsuPerformanceCalculationInputError.NegativeJudgementCount)
                {
                    clearResult("Judgement counts cannot be negative.");
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.JudgementCountMismatch)
                {
                    showJudgementCountMismatch((long)great + ok + meh + miss, expectedJudgements);
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.ComboOutOfRange)
                {
                    maxCombo.SetInvalid(true, colours);
                    clearResult($"Highest combo must be between 0 and {difficultyAttributes.MaxCombo:N0}.");
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.SliderTickMissOutOfRange)
                {
                    setSliderDetailsExpanded(true);
                    countSliderTickMiss.SetInvalid(true, colours);
                    clearResult($"Slider tick misses must be between 0 and {maximumStatistics.GetValueOrDefault(HitResult.LargeTickHit):N0}.");
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.SliderTailDroppedOutOfRange)
                {
                    setSliderDetailsExpanded(true);
                    countSliderTailDropped.SetInvalid(true, colours);
                    clearResult($"Slider tails dropped must be between 0 and {maximumStatistics.GetValueOrDefault(HitResult.SliderTailHit):N0}.");
                    return;
                }

                score = OsuPerformanceCalculationScenario.CreateScore(calculationBeatmap, calculationRuleset, calculationMods, difficultyAttributes,
                                                                       maximumStatistics, calculationScoreProcessor, input);
                calculationStatus = combo == difficultyAttributes.MaxCombo && (miss > 0 || sliderTickMiss > 0)
                    ? "Warning: a full maximum combo cannot normally contain misses or slider tick misses; PP is shown for the supplied values."
                    : string.Empty;
            }

            var calculator = calculationRuleset.CreateInstance().CreatePerformanceCalculator();
            var result = calculator?.Calculate(score, difficultyAttributes);

            if (result == null)
            {
                clearResult("A performance calculator is not available for this ruleset.");
                return;
            }

            resultText.Text = $"{result.Total:N1} pp";
            accuracyText.Text = $"{score.Accuracy:P2} accuracy";
            breakdownText.Text = string.Join("\n", result.GetAttributesForDisplay()
                                                          .Where(a => a.PropertyName != nameof(PerformanceAttributes.Total))
                                                          .Select(a => $"{a.DisplayName}: {a.Value:N1}"));
            setStatus(calculationStatus, calculationStatus.Length > 0 ? colours.Yellow : ColourProvider.Content2);
        }

        private void showJudgementCountMismatch(long entered, int expected)
        {
            long difference = expected - entered;
            clearResult(difference > 0
                ? $"Entered {entered:N0} / {expected:N0} judgements ({difference:N0} remaining)."
                : $"Entered {entered:N0} / {expected:N0} judgements ({-difference:N0} too many)." );
        }

        private bool tryParse(CompactNumberField input, out int value)
        {
            bool valid = int.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
            input.SetInvalid(!valid, colours);
            return valid;
        }

        private void clearInputErrors()
        {
            foreach (var input in inputs)
                input.SetInvalid(false, colours);
        }

        private void setJudgementTotal(long? entered, int? expected)
        {
            judgementTotalText.Text = entered.HasValue && expected.HasValue
                ? $"Judgements\n{entered.Value:N0} / {expected.Value:N0}"
                : expected.HasValue
                    ? $"Judgements\n-- / {expected.Value:N0}"
                    : "Judgements\n-- / --";
            judgementTotalText.Colour = entered.HasValue && expected.HasValue && entered.Value == expected.Value
                ? colours.Green1
                : entered.HasValue || expected.HasValue ? colours.Red1 : ColourProvider.Content2;
        }

        private void clearResult(string status)
        {
            resultText.Text = "-- pp";
            accuracyText.Text = "-- accuracy";
            breakdownText.Text = string.Empty;
            setStatus(status, colours.Red1);
        }

        private void setStatus(string text, Color4 colour)
        {
            statusText.Text = text;
            statusText.Colour = colour;
            statusText.Alpha = string.IsNullOrEmpty(text) ? 0 : 1;
        }

        private static Container createCard(Drawable content, Color4 colour) => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 14,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                },
                content,
            }
        };

        private void configureInputLayout(SupportedRuleset mode)
        {
            bool mania = mode == SupportedRuleset.Mania;

            judgementGrid.Content = mania
                ? new[]
                {
                    new Drawable[] { countPerfect, countGreat, countGood },
                    new Drawable[] { countOk, countMeh, countMiss },
                }
                : new[]
                {
                    new Drawable[] { countGreat, countOk, countMeh, countMiss },
                };
            judgementGrid.ColumnDimensions = mania
                ? new[]
                {
                    new Dimension(GridSizeMode.Relative, 1f / 3),
                    new Dimension(GridSizeMode.Relative, 1f / 3),
                    new Dimension(GridSizeMode.Relative, 1f / 3),
                }
                : new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.25f),
                    new Dimension(GridSizeMode.Relative, 0.25f),
                    new Dimension(GridSizeMode.Relative, 0.25f),
                    new Dimension(GridSizeMode.Relative, 0.25f),
                };
            judgementGrid.RowDimensions = mania
                ? new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.5f),
                    new Dimension(GridSizeMode.Relative, 0.5f),
                }
                : new[] { new Dimension(GridSizeMode.Relative, 1) };
            judgementGrid.Height = mania ? 148 : 74;

            secondaryGrid.Content = mania
                ? new[] { new Drawable[] { judgementTotalContainer } }
                : new[] { new Drawable[] { maxCombo, judgementTotalContainer } };
            secondaryGrid.ColumnDimensions = mania
                ? new[] { new Dimension(GridSizeMode.Relative, 1) }
                : new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.5f),
                    new Dimension(GridSizeMode.Relative, 0.5f),
                };

            updateInputDescription(mode);

            countPerfect.ReadOnly = mania && autoFillBest.Current.Value;
            countGreat.ReadOnly = !mania && autoFillBest.Current.Value;

            if (mania)
            {
                countGreat.ReadOnly = false;
                sliderToggleContainer.Height = 0;
                sliderToggleContainer.Alpha = 0;
                setSliderDetailsExpanded(false, false);
            }
            else
            {
                countPerfect.ReadOnly = false;
                sliderToggleContainer.Height = 40;
                sliderToggleContainer.Alpha = 1;
                setSliderDetailsExpanded(sliderDetailsExpanded, false);
            }
        }

        private void setSliderDetailsExpanded(bool expanded, bool animate = true)
        {
            expanded &= calculationMode == SupportedRuleset.Osu;
            sliderDetailsExpanded = expanded;
            sliderDetailsButton.Text = expanded ? "Hide slider details" : "Slider details";

            sliderDetailsContainer.ClearTransforms();

            if (!animate)
            {
                sliderDetailsContainer.Height = expanded ? 74 : 0;
                sliderDetailsContainer.Alpha = expanded ? 1 : 0;
                return;
            }

            sliderDetailsContainer.ResizeHeightTo(expanded ? 74 : 0, 250, Easing.OutQuint);
            sliderDetailsContainer.FadeTo(expanded ? 1 : 0, 180, Easing.OutQuint);
        }

        private void onInputChanged(CompactNumberField input)
        {
            if (resettingInputs || updatingAutoFill)
                return;

            bool lowerJudgementChanged = calculationMode == SupportedRuleset.Mania
                ? input == countGreat || input == countGood || input == countOk || input == countMeh || input == countMiss
                : input == countOk || input == countMeh || input == countMiss;

            if (autoFillBest.Current.Value && lowerJudgementChanged)
                updateAutoFilledBestJudgement();

            updateCalculation();
        }

        private void autoFillChanged()
        {
            bool enabled = autoFillBest.Current.Value;
            countPerfect.ReadOnly = calculationMode == SupportedRuleset.Mania && enabled;
            countGreat.ReadOnly = calculationMode == SupportedRuleset.Osu && enabled;
            updateInputDescription(calculationMode);

            if (difficultyAttributes == null)
                return;

            if (enabled)
                updateAutoFilledBestJudgement();

            updateCalculation();
        }

        private void updateInputDescription(SupportedRuleset mode)
        {
            bool automatic = autoFillBest.Current.Value;

            inputDescriptionText.Text = mode == SupportedRuleset.Mania
                ? automatic
                    ? "Enter 300, 200, 100, 50 and misses. MAX is filled from the remaining judgements."
                    : "Enter all six judgement totals. Their sum must match the map's judgement count."
                : automatic
                    ? "Enter 100, 50 and misses. 300 is filled from the remaining objects; combo updates PP immediately."
                    : "Enter all four judgement totals and highest combo. Their sum must match the map's object count.";
        }

        private void updateAutoFilledBestJudgement()
        {
            if (difficultyAttributes == null || updatingAutoFill)
                return;

            int totalJudgements = getTotalJudgements(calculationMode, maximumStatistics);
            long lowerJudgements;
            CompactNumberField target;

            if (calculationMode == SupportedRuleset.Mania)
            {
                if (!tryParseValue(countGreat, out int great)
                    || !tryParseValue(countGood, out int good)
                    || !tryParseValue(countOk, out int ok)
                    || !tryParseValue(countMeh, out int meh)
                    || !tryParseValue(countMiss, out int miss))
                    return;

                lowerJudgements = (long)great + good + ok + meh + miss;
                target = countPerfect;
            }
            else
            {
                if (!tryParseValue(countOk, out int ok)
                    || !tryParseValue(countMeh, out int meh)
                    || !tryParseValue(countMiss, out int miss))
                    return;

                lowerJudgements = (long)ok + meh + miss;
                target = countGreat;
            }

            updatingAutoFill = true;

            try
            {
                target.Text = Math.Max(0, (long)totalJudgements - lowerJudgements).ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                updatingAutoFill = false;
            }
        }

        private static bool tryParseValue(CompactNumberField input, out int value)
            => int.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;

        private static int getTotalJudgements(SupportedRuleset mode, IReadOnlyDictionary<HitResult, int> statistics)
            => mode == SupportedRuleset.Mania
                ? ManiaPerformanceCalculationScenario.GetTotalJudgements(statistics)
                : OsuPerformanceCalculationScenario.GetTotalJudgements(statistics);

        private void resetToPerfect()
        {
            if (difficultyAttributes == null)
                return;

            int totalJudgements = getTotalJudgements(calculationMode, maximumStatistics);

            resettingInputs = true;

            try
            {
                countPerfect.Text = calculationMode == SupportedRuleset.Mania ? totalJudgements.ToString(CultureInfo.InvariantCulture) : "0";
                countGreat.Text = calculationMode == SupportedRuleset.Osu ? totalJudgements.ToString(CultureInfo.InvariantCulture) : "0";
                countGood.Text = "0";
                countOk.Text = "0";
                countMeh.Text = "0";
                countMiss.Text = "0";
                maxCombo.Text = difficultyAttributes.MaxCombo.ToString(CultureInfo.InvariantCulture);
                countSliderTickMiss.Text = "0";
                countSliderTailDropped.Text = "0";
            }
            finally
            {
                resettingInputs = false;
            }

            updateCalculation();
        }

        private void fillRemainingWithBestJudgement()
        {
            updateAutoFilledBestJudgement();
            updateCalculation();
        }

        private partial class CompactNumberField : CompositeDrawable
        {
            private readonly OsuSpriteText labelText;
            private readonly OsuNumberBox numberBox;
            private readonly Container validationBorder;

            public Bindable<string> Current => numberBox.Current;

            public string Text
            {
                get => numberBox.Text;
                set => numberBox.Text = value;
            }

            public string Label
            {
                set => labelText.Text = value;
            }

            public bool ReadOnly
            {
                get => numberBox.ReadOnly;
                set
                {
                    numberBox.ReadOnly = value;
                    numberBox.Alpha = value ? 0.75f : 1;
                }
            }

            public CompactNumberField(string label)
            {
                RelativeSizeAxes = Axes.Both;
                Padding = new MarginPadding { Horizontal = 5, Vertical = 3 };

                InternalChildren = new Drawable[]
                {
                    labelText = new OsuSpriteText
                    {
                        Text = label,
                        Font = OsuFont.Torus.With(size: 13, weight: FontWeight.SemiBold),
                    },
                    validationBorder = new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Masking = true,
                        CornerRadius = 5,
                        Children = new Drawable[]
                        {
                            numberBox = new OsuNumberBox
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = Vector2.One,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                AlwaysPresent = true,
                            },
                        }
                    },
                };
            }

            public void SetInvalid(bool invalid, OsuColour colours)
            {
                validationBorder.BorderColour = colours.Red1;
                validationBorder.BorderThickness = invalid ? 2 : 0;
            }
        }

        private enum SupportedRuleset
        {
            Osu,
            Mania,
        }

        public override VisibilityContainer CreateFooterContent() => new CalculatorFooterContent(resetToPerfect, fillRemainingWithBestJudgement);

        protected override bool OnClick(ClickEvent e) => true;

        private partial class CalculatorFooterContent : VisibilityContainer
        {
            private readonly Action reset;
            private readonly Action fillRemaining;

            public CalculatorFooterContent(Action reset, Action fillRemaining)
            {
                this.reset = reset;
                this.fillRemaining = fillRemaining;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(8),
                    Children = new Drawable[]
                    {
                        new ShearedButton
                        {
                            Width = 180,
                            Text = "Reset to perfect",
                            DarkerColour = colours.Purple2,
                            LighterColour = colours.Purple1,
                            Action = reset,
                        },
                        new ShearedButton
                        {
                            Width = 220,
                            Text = "Fill remaining",
                            DarkerColour = colours.Blue2,
                            LighterColour = colours.Blue1,
                            Action = fillRemaining,
                        },
                    }
                };
            }

            protected override void PopIn() => this.FadeIn(200, Easing.OutQuint);

            protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            difficultyCancellation?.Cancel();
            difficultyCancellation?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
