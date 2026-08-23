// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.LeaguePlay;
using osu.Game.Overlays;
using osu.Game.Screens.Backgrounds;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.LeaguePlay
{
    /// <summary>
    /// Confirms that a package has resolved to playable local difficulties.
    /// </summary>
    public partial class LeaguePlayMappoolPreviewScreen : OsuScreen
    {
        public override string Title => prepared.Source.Name;

        public override bool ShowFooter => true;

        private readonly LeaguePlayPreparedMappool prepared;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        protected override BackgroundScreen CreateBackground() => new BackgroundScreenDefault();

        public LeaguePlayMappoolPreviewScreen(LeaguePlayPreparedMappool prepared)
        {
            this.prepared = prepared;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            foreach (LeaguePlayPreparedMappoolItem item in prepared.Items)
            {
                rows.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 42,
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4.Opacity(0.92f),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 14,
                            Text = item.Source.Slot,
                            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                            Colour = colourProvider.Highlight1,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 85,
                            Text = $"{item.Beatmap.Metadata.Artist} - {item.Beatmap.Metadata.Title} [{item.Beatmap.DifficultyName}]",
                            Font = OsuFont.Default.With(size: 16),
                        },
                    }
                });
            }

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.72f),
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 80, Top = 55, Bottom = 45 },
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = prepared.Source.Name,
                            Font = OsuFont.TorusAlternate.With(size: 38, weight: FontWeight.Bold),
                        },
                        new OsuSpriteText
                        {
                            Y = 52,
                            Text = $"FT{prepared.Source.FirstTo} · {prepared.Source.BansPerSide} bans each · {prepared.Items.Count} resolved beatmaps",
                            Font = OsuFont.Default.With(size: 18),
                            Colour = colourProvider.Content2,
                        },
                        new OsuSpriteText
                        {
                            Y = 82,
                            Text = "Package ready. Start the match to enter the local Quick Play flow.",
                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                            Colour = colourProvider.Content1,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 120, Bottom = 55 },
                            Child = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = rows,
                            }
                        },
                        new RoundedButton
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Size = new Vector2(220, 42),
                            Text = "Start match",
                            BackgroundColour = colourProvider.Colour2,
                            Action = () => this.Push(new LeaguePlayMatchScreen(prepared)),
                        }
                    }
                }
            };
        }
    }
}
