// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Select
{
    public partial class FooterButtonPerformanceCalculator : ScreenFooterButton
    {
        private readonly PerformanceCalculatorOverlay overlay;

        public FooterButtonPerformanceCalculator(PerformanceCalculatorOverlay overlay)
            : base(overlay)
        {
            this.overlay = overlay;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Text = "PP calc";
            Icon = FontAwesome.Solid.ChartLine;
            AccentColour = colours.Purple1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindTo(overlay.IsAvailable);
        }
    }
}
