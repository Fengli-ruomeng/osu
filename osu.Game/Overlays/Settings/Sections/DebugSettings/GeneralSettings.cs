// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class GeneralSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"General";

        [BackgroundDependencyLoader]
        private void load(FrameworkDebugConfigManager config, FrameworkConfigManager frameworkConfig, OsuConfigManager osuConfig)
        {
            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = @"Show log overlay",
                Current = frameworkConfig.GetBindable<bool>(FrameworkSetting.ShowLogOverlay)
            }));

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = @"Bypass front-to-back render pass",
                Current = config.GetBindable<bool>(DebugSetting.BypassFrontToBackPass)
            }));

            Add(new SettingsItemV2(new FormCheckBox
            {
                Caption = DebugSettingsStrings.FluidCursorTrail,
                Current = osuConfig.GetBindable<bool>(OsuSetting.FluidCursorTrail)
            }));

            Add(new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = DebugSettingsStrings.FluidCursorTrailThickness,
                Current = osuConfig.GetBindable<float>(OsuSetting.FluidCursorTrailThickness),
                KeyboardStep = 0.05f,
                LabelFormat = v => $@"{v:0.##}x",
            }));

            Add(new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = DebugSettingsStrings.FluidCursorTrailLength,
                Current = osuConfig.GetBindable<float>(OsuSetting.FluidCursorTrailLength),
                KeyboardStep = 0.05f,
                LabelFormat = v => $@"{v:0.##}x",
            }));

            Add(new SettingsColour
            {
                LabelText = DebugSettingsStrings.FluidCursorTrailColour,
                Current = osuConfig.GetBindable<Colour4>(OsuSetting.FluidCursorTrailColour),
            });
        }
    }
}
