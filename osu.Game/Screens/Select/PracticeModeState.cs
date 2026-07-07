// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Screens.Select
{
    public class PracticeModeState
    {
        public readonly BindableBool Enabled = new BindableBool();

        public readonly BindableDouble StartTime = new BindableDouble
        {
            MinValue = 0,
            MaxValue = 1000,
            Precision = 1000,
        };
    }
}
