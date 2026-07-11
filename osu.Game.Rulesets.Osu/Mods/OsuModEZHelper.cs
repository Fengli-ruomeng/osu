// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModEZHelper : Mod, IUpdatableByPlayfield, IApplicableToDrawableHitObject
    {
        private static readonly OsuColour colours = new OsuColour();
        private int? currentComboIndex;

        internal static Color4 CurrentComboColour => colours.Blue;
        internal static Color4 NextComboColour => colours.Red;
        internal static Color4 OtherComboColour => colours.Gray8;

        public override string Name => "EZ Helper";

        public override string Acronym => "EZH";

        public override LocalisableString Description => "Highlights the current combo group in blue and the next in red.";

        public override ModType Type => ModType.DifficultyReduction;

        public override Type[] IncompatibleMods => new[] { typeof(OsuModSynesthesia) };

        public void Update(Playfield playfield)
        {
            DrawableOsuHitObject? currentObject = null;

            foreach (var drawable in playfield.HitObjectContainer.AliveEntries.Values)
            {
                if (drawable is not DrawableOsuHitObject osuDrawable || osuDrawable.AllJudged)
                    continue;

                if (currentObject == null || osuDrawable.HitObject.StartTime < currentObject.HitObject.StartTime)
                    currentObject = osuDrawable;
            }

            currentComboIndex = currentObject?.HitObject.ComboIndex;
        }

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            drawable.OnUpdate += _ =>
            {
                if (drawable.HitObject is not OsuHitObject osuHitObject)
                    return;

                drawable.AccentColour.Value = osuHitObject.ComboIndex switch
                {
                    var index when index == currentComboIndex => CurrentComboColour,
                    var index when index == currentComboIndex + 1 => NextComboColour,
                    _ => OtherComboColour,
                };
            };
        }
    }
}
