// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.SongSelect
{
    [TestFixture]
    public class PerformanceCalculatorOverlayTest
    {
        [Test]
        public void TestProvidesFooterContentForLifecycle()
        {
            using var overlay = new PerformanceCalculatorOverlay();
            Assert.That(overlay.CreateFooterContent(), Is.Not.Null);
        }
    }
}
