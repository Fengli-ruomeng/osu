// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LeaguePlay;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match.BeatmapSelect;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.LeaguePlay
{
    /// <summary>
    /// A local-beatmap variant of the original Quick Play selection panel.
    /// </summary>
    public partial class LeaguePlayBeatmapPanel : MatchmakingSelectPanel
    {
        public readonly LeaguePlayPreparedMappoolItem PreparedItem;

        public Action? PreviewAction;

        private readonly WorkingBeatmap workingBeatmap;
        private readonly Mod[] mods;
        private readonly Color4 accentColour;

        private CardContent.AvatarOverlay selectionOverlay = null!;

        public LeaguePlayBeatmapPanel(long itemId, LeaguePlayPreparedMappoolItem item, WorkingBeatmap workingBeatmap, Mod[] mods, Color4 accentColour)
            : base(new MultiplayerPlaylistItem
            {
                ID = itemId,
                BeatmapID = item.Beatmap.OnlineID,
                RulesetID = item.Beatmap.Ruleset.OnlineID,
                StarRating = item.Beatmap.StarRating,
                RequiredMods = mods.Select(mod => new APIMod(mod)).ToArray(),
            })
        {
            PreparedItem = item;
            this.workingBeatmap = workingBeatmap;
            this.mods = mods;
            this.accentColour = accentColour;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = BeatmapCard.CORNER_RADIUS,
                CornerExponent = 10,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 5,
                    Colour = Color4.Black.Opacity(0.45f),
                },
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = workingBeatmap.GetBackground(),
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.62f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 6,
                        Colour = accentColour,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Text = PreparedItem.Source.Slot,
                        Colour = accentColour,
                        Font = OsuFont.TorusAlternate.With(size: 23, weight: FontWeight.Bold),
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 78,
                        Width = WIDTH - 90,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = PreparedItem.Beatmap.Metadata.Title,
                                Font = OsuFont.Default.With(size: 17, weight: FontWeight.SemiBold),
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = $"{PreparedItem.Beatmap.Metadata.Artist}  [{PreparedItem.Beatmap.DifficultyName}]",
                                Font = OsuFont.Default.With(size: 12),
                                Alpha = 0.8f,
                            },
                            new OsuSpriteText
                            {
                                Text = $"★ {PreparedItem.Beatmap.StarRating:0.00}   {string.Join(' ', mods.Select(mod => mod.Acronym))}",
                                Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold),
                                Colour = accentColour,
                            },
                        }
                    },
                    selectionOverlay = new CardContent.AvatarOverlay
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding { Right = 5 },
                    },
                }
            });
        }

        public override void AddUser(APIUser user) => selectionOverlay.AddUser(user);

        public override void RemoveUser(APIUser user) => selectionOverlay.RemoveUser(user.Id);

        public override void PresentAsChosenBeatmap(MatchmakingPlaylistItem playlistItem)
        {
            ShowChosenBorder();
            this.MoveTo(Vector2.Zero, 1000, Easing.OutExpo);
            ScaleContainer.ScaleTo(1.5f, 1000, Easing.OutExpo);
        }

        protected override bool OnHover(HoverEvent e)
        {
            PreviewAction?.Invoke();
            return base.OnHover(e);
        }
    }
}
