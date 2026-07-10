// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    public partial class FluidCursorTrail : CursorTrail
    {
        public const float VISIBLE_ALPHA = 0.95f;
        public const float CURSOR_DIAMETER = 4f;

        private readonly BindableFloat thickness = new BindableFloat(1);
        private readonly BindableFloat length = new BindableFloat(1);
        private readonly Bindable<Colour4> colour = new BindableColour4(Colour4.White);
        private readonly TrailLayer[] layers = new TrailLayer[2];
        private float lastAppliedThickness = -1;

        private static readonly TrailLayer[] base_layers =
        {
            new TrailLayer(1.65f, 0.12f, 0.32f),
            new TrailLayer(0.95f, 0.04f, 0.9f),
        };

        protected override double FadeDuration => 650;

        protected override float LifetimeMultiplier => length.Value;

        protected override float FadeExponent => 1.1f;

        protected override float IntervalMultiplier => 0.18f;

        protected override TrailLayer[] CreateTrailLayers()
        {
            if (lastAppliedThickness != thickness.Value)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    TrailLayer layer = base_layers[i];
                    layers[i] = layer with
                    {
                        HeadScale = layer.HeadScale * thickness.Value,
                        TailScale = layer.TailScale * thickness.Value,
                    };
                }

                lastAppliedThickness = thickness.Value;
            }

            return layers;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, OsuConfigManager config)
        {
            config.BindWith(OsuSetting.FluidCursorTrailThickness, thickness);
            config.BindWith(OsuSetting.FluidCursorTrailLength, length);
            config.BindWith(OsuSetting.FluidCursorTrailColour, colour);
            colour.BindValueChanged(e => Colour = e.NewValue, true);

            Texture texture = textures.Get(@"Cursor/cursortrail");

            if (texture != null)
            {
                Texture = texture;
                Scale = new Vector2(1 / texture.ScaleAdjust);
            }

            Blending = BlendingParameters.Additive;
            Alpha = VISIBLE_ALPHA;
        }
    }
}
