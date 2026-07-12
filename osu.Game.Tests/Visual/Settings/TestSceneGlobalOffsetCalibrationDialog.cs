// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Sections.Audio;
using osu.Game.Screens.Edit.Timing;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneGlobalOffsetCalibrationDialog : OsuManualInputManagerTestScene
    {
        private readonly BindableDouble globalOffset = new BindableDouble
        {
            MinValue = -500,
            MaxValue = 500,
        };

        [SetUp]
        public void SetUp() => Schedule(() => Child = new GlobalOffsetCalibrationDialog(globalOffset));

        [Test]
        public void TestDisplayAndStart()
        {
            AddAssert("metronome present", () => this.ChildrenOfType<MetronomeDisplay>().Any());
            AddAssert("configured hit keys displayed", () => this.ChildrenOfType<GlobalOffsetCalibrationControl>().Single().BindingInstruction, () => Does.Contain("to tap, or click"));
            AddAssert("start button has correct height", () => getButton("Start calibration").DrawHeight, () => Is.EqualTo(45).Within(1));
            AddAssert("start button uses full width", () => getButton("Start calibration").DrawWidth, () => Is.EqualTo(getButton("Tap").DrawWidth).Within(1));
            AddStep("press same hit key twice", () =>
            {
                InputManager.Key(Key.Z);
                InputManager.Key(Key.Z);
            });
            AddAssert("both hit key presses received", () => this.ChildrenOfType<GlobalOffsetCalibrationControl>().Single().ReceivedHitKeyPresses, () => Is.EqualTo(2));
            AddStep("start calibration", () => this.ChildrenOfType<RoundedButton>().Single(button => button.Text.ToString() == "Start calibration").TriggerClick());
            AddAssert("tap button disabled during warmup", () => this.ChildrenOfType<RoundedButton>().Single(button => button.Text.ToString() == "Tap").Enabled.Value, () => Is.False);
        }

        private RoundedButton getButton(string text) => this.ChildrenOfType<RoundedButton>().Single(button => button.Text.ToString() == text);
    }
}
