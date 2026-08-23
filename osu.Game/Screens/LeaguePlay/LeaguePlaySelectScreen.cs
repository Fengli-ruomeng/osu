// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
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
    /// Lists local League Play packages and prepares the selected package for offline play.
    /// </summary>
    public partial class LeaguePlaySelectScreen : OsuScreen
    {
        public override string Title => "League Play";

        public override bool ShowFooter => true;

        private readonly LeaguePlayMappoolCatalog catalog = new LeaguePlayMappoolCatalog();

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private FillFlowContainer poolList = null!;
        private OsuSpriteText statusText = null!;
        private RoundedButton refreshButton = null!;
        private bool preparing;

        protected override BackgroundScreen CreateBackground() => new BackgroundScreenDefault();

        [BackgroundDependencyLoader]
        private void load()
        {
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
                    Padding = new MarginPadding { Horizontal = 80, Top = 55, Bottom = 40 },
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "LEAGUE PLAY",
                            Font = OsuFont.TorusAlternate.With(size: 42, weight: FontWeight.Bold),
                        },
                        new OsuSpriteText
                        {
                            Y = 55,
                            Text = $"Local 1v1 tournament packages from {LeaguePlayMappoolCatalog.ROOT_DIRECTORY}/",
                            Font = OsuFont.Default.With(size: 18),
                            Colour = colourProvider.Content2,
                        },
                        statusText = new OsuSpriteText
                        {
                            Y = 88,
                            Text = "Scanning local mappools...",
                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                            Colour = colourProvider.Content1,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 125, Bottom = 55 },
                            Child = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = poolList = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 10),
                                }
                            }
                        },
                        refreshButton = new RoundedButton
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Size = new Vector2(190, 42),
                            Text = "Rescan packages",
                            Action = refreshPools,
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshPools();
        }

        private void refreshPools()
        {
            if (preparing)
                return;

            poolList.Clear();

            try
            {
                LeaguePlayMappoolPackage[] packages = catalog.Load(storage).ToArray();

                if (packages.Length == 0)
                {
                    statusText.Text = $"No packages found. Create {LeaguePlayMappoolCatalog.ROOT_DIRECTORY}/<package>/config.ini in the osu! data directory.";
                    return;
                }

                statusText.Text = $"Found {packages.Length} package{(packages.Length == 1 ? string.Empty : "s")}. Select one to validate and import its beatmaps.";

                foreach (LeaguePlayMappoolPackage package in packages)
                {
                    LeaguePlayMappool mappool = package.Mappool;
                    int regularCount = mappool.Items.Count(item => item.ModPool != LeaguePlayModPool.Tiebreaker);

                    poolList.Add(new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 66,
                        Text = $"{mappool.Name}    ·    FT{mappool.FirstTo}    ·    {mappool.BansPerSide} bans each    ·    {regularCount} maps + TB",
                        BackgroundColour = colourProvider.Background3,
                        Action = () => preparePackage(package),
                    });
                }
            }
            catch (Exception exception)
            {
                statusText.Text = $"Could not load League Play packages: {exception.Message}";
                statusText.Colour = Color4.OrangeRed;
            }
        }

        private async void preparePackage(LeaguePlayMappoolPackage package)
        {
            if (preparing)
                return;

            preparing = true;
            refreshButton.Enabled.Value = false;
            poolList.Alpha = 0.5f;
            statusText.Colour = colourProvider.Content1;
            statusText.Text = $"Preparing {package.Mappool.Name}... Missing beatmaps will be imported without removing the package files.";

            try
            {
                LeaguePlayPreparedMappool prepared = await new LeaguePlayMappoolPreparer(beatmapManager).Prepare(storage, package).ConfigureAwait(false);

                Schedule(() =>
                {
                    preparing = false;
                    refreshButton.Enabled.Value = true;
                    poolList.Alpha = 1;
                    statusText.Text = $"{package.Mappool.Name} is ready.";

                    if (this.IsCurrentScreen())
                        this.Push(new LeaguePlayMappoolPreviewScreen(prepared));
                });
            }
            catch (Exception exception)
            {
                Schedule(() =>
                {
                    preparing = false;
                    refreshButton.Enabled.Value = true;
                    poolList.Alpha = 1;
                    statusText.Colour = Color4.OrangeRed;
                    statusText.Text = $"Could not prepare {package.Mappool.Name}: {exception.Message}";
                });
            }
        }
    }
}
