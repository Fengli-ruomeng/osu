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
using osu.Framework.Graphics.Sprites;
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

        private readonly LabelledNumberBox countPerfect = new LabelledNumberBox { Label = "MAX / 320" };
        private readonly LabelledNumberBox countGreat = new LabelledNumberBox { Label = "300" };
        private readonly LabelledNumberBox countGood = new LabelledNumberBox { Label = "200" };
        private readonly LabelledNumberBox countOk = new LabelledNumberBox { Label = "100" };
        private readonly LabelledNumberBox countMeh = new LabelledNumberBox { Label = "50" };
        private readonly LabelledNumberBox countMiss = new LabelledNumberBox { Label = "Miss" };
        private readonly LabelledNumberBox maxCombo = new LabelledNumberBox { Label = "Highest combo" };
        private readonly LabelledNumberBox countSliderTickMiss = new LabelledNumberBox { Label = "Slider tick misses" };
        private readonly LabelledNumberBox countSliderTailDropped = new LabelledNumberBox { Label = "Slider tails dropped" };

        private OsuSpriteText beatmapText = null!;
        private OsuTextFlowContainer inputDescriptionText = null!;
        private OsuSpriteText resultText = null!;
        private OsuTextFlowContainer breakdownText = null!;
        private OsuTextFlowContainer statusText = null!;

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

            MainAreaContent.Child = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    Width = 0.72f,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Top = 30, Bottom = 30 },
                    Spacing = new Vector2(0, 12),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        beatmapText = new OsuSpriteText
                        {
                            Font = OsuFont.TorusAlternate.With(size: 22, weight: FontWeight.SemiBold),
                        },
                        inputDescriptionText = new OsuTextFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        countPerfect,
                        countGreat,
                        countGood,
                        countOk,
                        countMeh,
                        countMiss,
                        maxCombo,
                        countSliderTickMiss,
                        countSliderTailDropped,
                        statusText = new OsuTextFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        resultText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.Numeric.With(size: 44),
                            Text = "-- pp",
                        },
                        breakdownText = new OsuTextFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                        },
                    }
                }
            };

            foreach (var input in inputs)
                input.Current.BindValueChanged(_ => updateCalculation());

            beatmap.BindValueChanged(_ => selectionChanged(), true);
            ruleset.BindValueChanged(_ => selectionChanged(), true);
            mods.BindValueChanged(_ => selectionChanged(), true);
        }

        private IEnumerable<LabelledNumberBox> inputs
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
                difficultyAttributes = null;
                resultText.Text = "-- pp";
                breakdownText.Text = string.Empty;
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

                if (!cancellationToken.IsCancellationRequested)
                    Schedule(() => setStatus("Could not calculate difficulty for this beatmap.", colours.Red1));
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

            difficultyAttributes = selectedDifficulty;
            maximumStatistics = selectedMaximumStatistics;
            calculationBeatmap = selectedBeatmap;
            calculationRuleset = selectedRuleset;
            calculationMods = selectedMods;
            calculationScoreProcessor = scoreProcessor;
            calculationMode = selectedRuleset.OnlineID == 3 ? SupportedRuleset.Mania : SupportedRuleset.Osu;

            int totalJudgements = calculationMode == SupportedRuleset.Mania
                ? ManiaPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics)
                : OsuPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics);
            string displayedMods = selectedMods.Length == 0 ? "No Mod" : string.Join(", ", selectedMods.Select(m => m.Acronym));
            string comboDisplay = calculationMode == SupportedRuleset.Mania ? string.Empty : $"  •  {selectedDifficulty.MaxCombo:N0}x max";

            beatmapText.Text = $"{selectedBeatmap}  •  {starDifficulty.Value.Stars:0.00}★  •  {totalJudgements:N0} judgements{comboDisplay}  •  {displayedMods}";

            resettingInputs = true;
            countPerfect.Text = calculationMode == SupportedRuleset.Mania ? totalJudgements.ToString(CultureInfo.InvariantCulture) : "0";
            countGreat.Text = calculationMode == SupportedRuleset.Osu ? totalJudgements.ToString(CultureInfo.InvariantCulture) : "0";
            countGood.Text = "0";
            countOk.Text = "0";
            countMeh.Text = "0";
            countMiss.Text = "0";
            maxCombo.Text = selectedDifficulty.MaxCombo.ToString(CultureInfo.InvariantCulture);
            countSliderTickMiss.Text = "0";
            countSliderTailDropped.Text = "0";
            countSliderTickMiss.Label = $"Slider tick misses (max {maximumStatistics.GetValueOrDefault(HitResult.LargeTickHit):N0})";
            countSliderTailDropped.Label = $"Slider tails dropped (max {maximumStatistics.GetValueOrDefault(HitResult.SliderTailHit):N0})";

            countPerfect.Alpha = calculationMode == SupportedRuleset.Mania ? 1 : 0;
            countGood.Alpha = calculationMode == SupportedRuleset.Mania ? 1 : 0;
            maxCombo.Alpha = calculationMode == SupportedRuleset.Osu ? 1 : 0;
            countSliderTickMiss.Alpha = calculationMode == SupportedRuleset.Osu ? 1 : 0;
            countSliderTailDropped.Alpha = calculationMode == SupportedRuleset.Osu ? 1 : 0;
            inputDescriptionText.Text = calculationMode == SupportedRuleset.Mania
                ? "Enter all six judgement totals. Their sum must match the map's maximum judgement count; combo does not affect mania PP."
                : "Enter the judgement totals, highest combo and any dropped slider parts. The four main judgement counts must add up to the map's object count.";
            resettingInputs = false;

            updateCalculation();
        }

        private void updateCalculation()
        {
            if (resettingInputs || difficultyAttributes == null || calculationBeatmap == null || calculationRuleset == null || calculationScoreProcessor == null)
                return;

            if (!tryParse(countGreat, out int great) || !tryParse(countOk, out int ok) || !tryParse(countMeh, out int meh) || !tryParse(countMiss, out int miss))
            {
                clearResult("All fields must contain non-negative whole numbers.");
                return;
            }

            ScoreInfo score;
            string calculationStatus;

            if (calculationMode == SupportedRuleset.Mania)
            {
                if (!tryParse(countPerfect, out int perfect) || !tryParse(countGood, out int good))
                {
                    clearResult("All fields must contain non-negative whole numbers.");
                    return;
                }

                var input = new ManiaPerformanceCalculationInput(perfect, great, good, ok, meh, miss);
                var validationError = ManiaPerformanceCalculationScenario.Validate(input, maximumStatistics);

                if (validationError == ManiaPerformanceCalculationInputError.NegativeJudgementCount)
                {
                    clearResult("Judgement counts cannot be negative.");
                    return;
                }

                if (validationError == ManiaPerformanceCalculationInputError.JudgementCountMismatch)
                {
                    showJudgementCountMismatch((long)perfect + great + good + ok + meh + miss,
                                               ManiaPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics));
                    return;
                }

                score = ManiaPerformanceCalculationScenario.CreateScore(calculationBeatmap, calculationRuleset, calculationMods, difficultyAttributes,
                                                                          maximumStatistics, calculationScoreProcessor, input);
                calculationStatus = "Calculated using osu!mania's built-in performance calculator.";
            }
            else
            {
                if (!tryParse(maxCombo, out int combo) || !tryParse(countSliderTickMiss, out int sliderTickMiss)
                    || !tryParse(countSliderTailDropped, out int sliderTailDropped))
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
                    showJudgementCountMismatch((long)great + ok + meh + miss,
                                               OsuPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics));
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.ComboOutOfRange)
                {
                    clearResult($"Highest combo must be between 0 and {difficultyAttributes.MaxCombo:N0}.");
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.SliderTickMissOutOfRange)
                {
                    clearResult($"Slider tick misses must be between 0 and {maximumStatistics.GetValueOrDefault(HitResult.LargeTickHit):N0}.");
                    return;
                }

                if (validationError == OsuPerformanceCalculationInputError.SliderTailDroppedOutOfRange)
                {
                    clearResult($"Slider tails dropped must be between 0 and {maximumStatistics.GetValueOrDefault(HitResult.SliderTailHit):N0}.");
                    return;
                }

                score = OsuPerformanceCalculationScenario.CreateScore(calculationBeatmap, calculationRuleset, calculationMods, difficultyAttributes,
                                                                       maximumStatistics, calculationScoreProcessor, input);
                calculationStatus = combo == difficultyAttributes.MaxCombo && (miss > 0 || sliderTickMiss > 0)
                    ? "Warning: a full maximum combo cannot normally contain misses or slider tick misses; PP is shown for the supplied values."
                    : "Calculated using osu!standard's built-in performance calculator and the supplied slider statistics.";
            }

            var calculator = calculationRuleset.CreateInstance().CreatePerformanceCalculator();
            var result = calculator?.Calculate(score, difficultyAttributes);

            if (result == null)
            {
                clearResult("A performance calculator is not available for this ruleset.");
                return;
            }

            resultText.Text = $"{result.Total:N0} pp";
            string breakdown = string.Join("   •   ", result.GetAttributesForDisplay()
                                                           .Where(a => a.PropertyName != nameof(PerformanceAttributes.Total))
                                                           .Select(a => $"{a.DisplayName} {a.Value:N1}"));
            breakdownText.Text = string.IsNullOrEmpty(breakdown) ? $"{score.Accuracy:P2} accuracy" : $"{score.Accuracy:P2} accuracy   •   {breakdown}";
            setStatus(calculationStatus, colours.Yellow);
        }

        private void showJudgementCountMismatch(long entered, int expected)
        {
            long difference = expected - entered;
            clearResult(difference > 0
                ? $"Entered {entered:N0} / {expected:N0} judgements ({difference:N0} remaining)."
                : $"Entered {entered:N0} / {expected:N0} judgements ({-difference:N0} too many)." );
        }

        private static bool tryParse(LabelledNumberBox input, out int value)
            => int.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;

        private void clearResult(string status)
        {
            resultText.Text = "-- pp";
            breakdownText.Text = string.Empty;
            setStatus(status, colours.Red1);
        }

        private void setStatus(string text, osuTK.Graphics.Color4 colour)
        {
            statusText.Text = text;
            statusText.Colour = colour;
        }

        private void resetToPerfect()
        {
            if (difficultyAttributes == null)
                return;

            int totalJudgements = calculationMode == SupportedRuleset.Mania
                ? ManiaPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics)
                : OsuPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics);

            resettingInputs = true;
            countPerfect.Text = calculationMode == SupportedRuleset.Mania ? totalJudgements.ToString(CultureInfo.InvariantCulture) : "0";
            countGreat.Text = calculationMode == SupportedRuleset.Osu ? totalJudgements.ToString(CultureInfo.InvariantCulture) : "0";
            countGood.Text = "0";
            countOk.Text = "0";
            countMeh.Text = "0";
            countMiss.Text = "0";
            maxCombo.Text = difficultyAttributes.MaxCombo.ToString(CultureInfo.InvariantCulture);
            countSliderTickMiss.Text = "0";
            countSliderTailDropped.Text = "0";
            resettingInputs = false;
            updateCalculation();
        }

        private void fillRemainingWithBestJudgement()
        {
            int totalJudgements = calculationMode == SupportedRuleset.Mania
                ? ManiaPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics)
                : OsuPerformanceCalculationScenario.GetTotalJudgements(maximumStatistics);

            if (!tryParse(countGreat, out int great) || !tryParse(countGood, out int good) || !tryParse(countOk, out int ok)
                || !tryParse(countMeh, out int meh) || !tryParse(countMiss, out int miss))
                return;

            if (calculationMode == SupportedRuleset.Mania)
                countPerfect.Text = Math.Max(0, totalJudgements - great - good - ok - meh - miss).ToString(CultureInfo.InvariantCulture);
            else
                countGreat.Text = Math.Max(0, totalJudgements - ok - meh - miss).ToString(CultureInfo.InvariantCulture);
        }

        private enum SupportedRuleset
        {
            Osu,
            Mania,
        }

        public override VisibilityContainer CreateFooterContent() => new CalculatorFooterContent(resetToPerfect, fillRemainingWithBestJudgement);

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
