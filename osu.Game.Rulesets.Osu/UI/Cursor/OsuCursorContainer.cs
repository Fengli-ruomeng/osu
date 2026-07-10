// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    public partial class OsuCursorContainer : GameplayCursorContainer, IKeyBindingHandler<OsuAction>
    {
        public new OsuCursor ActiveCursor => (OsuCursor)base.ActiveCursor;

        protected override Drawable CreateCursor() => new OsuCursor();
        protected override Container<Drawable> Content => fadeContainer;

        private readonly Container<Drawable> fadeContainer;

        private readonly Bindable<bool> showTrail = new Bindable<bool>(true);
        private readonly Bindable<bool> useFluidCursorTrail = new Bindable<bool>();

        private readonly SkinnableDrawable cursorTrail;
        private readonly FluidCursorTrail fluidCursorTrail;

        private readonly CursorRippleVisualiser rippleVisualiser;

        public OsuCursorContainer()
        {
            InternalChild = fadeContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    cursorTrail = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.CursorTrail), _ => new DefaultCursorTrail(), confineMode: ConfineMode.NoScaling),
                    fluidCursorTrail = new FluidCursorTrail(),
                    rippleVisualiser = new CursorRippleVisualiser(),
                    new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.CursorParticles), confineMode: ConfineMode.NoScaling),
                }
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuRulesetConfigManager rulesetConfig, OsuConfigManager config)
        {
            rulesetConfig?.BindWith(OsuRulesetSetting.ShowCursorTrail, showTrail);
            config.BindWith(OsuSetting.FluidCursorTrail, useFluidCursorTrail);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            showTrail.BindValueChanged(_ => updateTrailVisibility(), true);
            useFluidCursorTrail.BindValueChanged(_ => updateTrailVisibility(), true);

            ActiveCursor.CursorScale.BindValueChanged(e =>
            {
                var newScale = new Vector2(e.NewValue);

                rippleVisualiser.CursorScale = newScale;
                updateTrailScale();
            }, true);
            cursorTrail.OnSkinChanged += updateTrailScale;
        }

        private void updateTrailVisibility()
        {
            cursorTrail.FadeTo(showTrail.Value && !useFluidCursorTrail.Value ? 1 : 0, 200);
            fluidCursorTrail.FadeTo(showTrail.Value && useFluidCursorTrail.Value ? FluidCursorTrail.VISIBLE_ALPHA : 0, 200);
        }

        private void updateTrailScale()
        {
            if (cursorTrail.Drawable is CursorTrail trail) trail.CursorScale = new Vector2(ActiveCursor.CursorScale.Value);
            fluidCursorTrail.CursorScale = new Vector2(ActiveCursor.CursorScale.Value);
        }

        private int downCount;

        private void updateExpandedState()
        {
            if (downCount > 0)
                ActiveCursor.Expand();
            else
                ActiveCursor.Contract();
        }

        protected override void Update()
        {
            base.Update();

            if (cursorTrail.Drawable is CursorTrail trail)
            {
                updateTrailState(trail);
            }

            updateTrailState(fluidCursorTrail);
        }

        private void updateTrailState(CursorTrail trail)
        {
            trail.NewPartScale = ActiveCursor.CurrentExpandedScale;
            trail.PartRotation = ActiveCursor.CurrentRotation;
        }

        public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
        {
            switch (e.Action)
            {
                case OsuAction.LeftButton:
                case OsuAction.RightButton:
                    downCount++;
                    updateExpandedState();
                    break;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
        {
            switch (e.Action)
            {
                case OsuAction.LeftButton:
                case OsuAction.RightButton:
                    // Todo: Math.Max() is required as a temporary measure to address https://github.com/ppy/osu-framework/issues/2576
                    downCount = Math.Max(0, downCount - 1);

                    if (downCount == 0)
                        updateExpandedState();
                    break;
            }
        }

        public override bool HandlePositionalInput => true; // OverlayContainer will set this false when we go hidden, but we always want to receive input.

        protected override void PopIn()
        {
            fadeContainer.FadeTo(1, 300, Easing.OutQuint);
            ActiveCursor.ScaleTo(1f, 400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            fadeContainer.FadeTo(0.05f, 450, Easing.OutQuint);
            ActiveCursor.ScaleTo(0.8f, 450, Easing.OutQuint);
        }

        private partial class DefaultCursorTrail : CursorTrail
        {
            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get(@"Cursor/cursortrail");
                Scale = new Vector2(1 / Texture.ScaleAdjust);
            }
        }
    }
}
