// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModEZHelperTest
    {
        [Test]
        public void TestRegisteredAsDifficultyReduction()
            => Assert.That(new OsuRuleset().GetModsFor(ModType.DifficultyReduction).OfType<OsuModEZHelper>(), Has.Exactly(1).Items);

        [Test]
        public void TestIncompatibleWithSynesthesia()
            => Assert.That(ModUtils.CheckCompatibleSet([new OsuModEZHelper(), new OsuModSynesthesia()]), Is.False);
    }
}
