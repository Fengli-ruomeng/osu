// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Select;
using osuTK;
using CommonStrings = osu.Game.Localisation.CommonStrings;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Displays beatmap metadata inside <see cref="PlayerLoader"/>
    /// </summary>
    public partial class BeatmapMetadataDisplay : Container
    {
        private readonly IWorkingBeatmap beatmap;
        private readonly Bindable<IReadOnlyList<Mod>> mods;
        private readonly Drawable logoFacade;
        private LoadingSpinner loading;
        private Drawable blockingLoadLayer;

        public IBindable<IReadOnlyList<Mod>> Mods => mods;

        public bool Loading
        {
            set
            {
                if (value)
                    loading.Show();
                else
                    loading.Hide();
            }
        }

        private bool userBlocked;

        public bool UserBlocked
        {
            set
            {
                if (value == userBlocked)
                    return;

                userBlocked = value;

                if (userBlocked)
                {
                    using (BeginDelayedSequence(500))
                    {
                        blockingLoadLayer
                            // Slight delay to avoid this flashing briefly during multiplayer load and other scenarios where
                            // load may be blocked for a short period.
                            .FadeIn(300, Easing.Out)
                            .Then()
                            .FadeTo(0.6f, 1000, Easing.In)
                            .Loop();
                    }
                }
                else
                    blockingLoadLayer.FadeOut(500, Easing.OutQuint);
            }
        }

        public BeatmapMetadataDisplay(IWorkingBeatmap beatmap, Bindable<IReadOnlyList<Mod>> mods, Drawable logoFacade)
        {
            this.beatmap = beatmap;
            this.logoFacade = logoFacade;

            this.mods = new Bindable<IReadOnlyList<Mod>>();
            this.mods.BindTo(mods);
        }

        private FillFlowContainer versionFlow;
        private StarRatingDisplay starRatingDisplay;

        private BeatmapDifficultyCache difficultyCache;
        private CancellationTokenSource starDifficultyCancellationSource;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; }

        [Resolved(canBeNull: true)]
        private PracticeModeState practiceMode { get; set; }

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache difficultyCache, OsuColour colours)
        {
            this.difficultyCache = difficultyCache;

            var metadata = beatmap.BeatmapInfo.Metadata;

            AutoSizeAxes = Axes.Both;
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Origin = Anchor.TopCentre,
                    Anchor = Anchor.TopCentre,
                    Direction = FillDirection.Vertical,
                    Children = new[]
                    {
                        logoFacade.With(d =>
                        {
                            d.Anchor = Anchor.TopCentre;
                            d.Origin = Anchor.TopCentre;
                        }),
                        new OsuSpriteText
                        {
                            Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                            Font = OsuFont.GetFont(size: 36, italics: true),
                            Origin = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                            Margin = new MarginPadding { Top = 15 },
                        },
                        new OsuSpriteText
                        {
                            Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                            Font = OsuFont.GetFont(size: 26, italics: true),
                            Origin = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                        },
                        new Container
                        {
                            Size = new Vector2(300, 60),
                            Margin = new MarginPadding(10),
                            Origin = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                            CornerRadius = 10,
                            Masking = true,
                            Children = new[]
                            {
                                new Sprite
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Texture = beatmap.GetBackground(),
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    FillMode = FillMode.Fill,
                                },
                                loading = new LoadingLayer(dimBackground: true)
                                {
                                    BlockPositionalInput = false,
                                },
                                blockingLoadLayer = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            Colour = colours.PinkDarker,
                                            Alpha = 0.5f,
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.Style.Heading2,
                                            Text = PlayerLoaderStrings.LoadingPaused
                                        }
                                    }
                                },
                            }
                        },
                        versionFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(5f),
                            Margin = new MarginPadding { Bottom = 40 },
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = beatmap.BeatmapInfo.DifficultyName,
                                    Font = OsuFont.GetFont(size: 26, italics: true),
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                },
                                starRatingDisplay = new StarRatingDisplay(default)
                                {
                                    Alpha = 0f,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                }
                            }
                        },
                        new GridContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            AutoSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new MetadataLineLabel(BeatmapsetsStrings.ShowInfoSource),
                                    new MetadataLineInfo(metadata.Source)
                                },
                                new Drawable[]
                                {
                                    new MetadataLineLabel(CommonStrings.Mapper),
                                    new MetadataLineInfo(metadata.Author.Username)
                                }
                            }
                        },
                        new ModDisplay
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Margin = new MarginPadding { Top = 20 },
                            Current = mods
                        },
                    },
                }
            };

            Loading = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            mods.BindValueChanged(_ => updateStarRating());
            ruleset.BindValueChanged(_ => updateStarRating());
            practiceMode?.Enabled.BindValueChanged(_ => updateStarRating());
            practiceMode?.StartTime.BindValueChanged(_ => updateStarRating());

            updateStarRating();
        }

        private void updateStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            var cancellationSource = starDifficultyCancellationSource;

            Task<StarDifficulty?> starDifficultyTask = practiceMode?.Enabled.Value == true
                ? difficultyCache.GetPracticeDifficultyAsync(beatmap, ruleset.Value, mods.Value, practiceMode.StartTime.Value, cancellationSource.Token)
                : difficultyCache.GetDifficultyAsync(beatmap.BeatmapInfo, ruleset.Value, mods.Value, cancellationSource.Token);

            starDifficultyTask.ContinueWith(task => Schedule(() =>
            {
                if (cancellationSource.IsCancellationRequested)
                    return;

                starRatingDisplay.Current.Value = task.GetResultSafely() ?? new StarDifficulty(-1, 0);

                versionFlow.AutoSizeDuration = 300;
                versionFlow.AutoSizeEasing = Easing.OutQuint;

                starRatingDisplay.FadeIn(300, Easing.InQuint);
            }), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        protected override void Dispose(bool isDisposing)
        {
            starDifficultyCancellationSource?.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class MetadataLineLabel : OsuSpriteText
        {
            public MetadataLineLabel(LocalisableString text)
            {
                Anchor = Anchor.TopRight;
                Origin = Anchor.TopRight;
                Margin = new MarginPadding { Right = 5 };
                Colour = OsuColour.Gray(0.8f);
                Text = text;
            }
        }

        private partial class MetadataLineInfo : OsuSpriteText
        {
            public MetadataLineInfo(string text)
            {
                Margin = new MarginPadding { Left = 5 };
                Text = string.IsNullOrEmpty(text) ? @"-" : text;
            }
        }
    }
}
