// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.LeaguePlay;

namespace osu.Game.Tests.LeaguePlay
{
    [TestFixture]
    public class LeaguePlayMappoolLoaderTest
    {
        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage(nameof(LeaguePlayMappoolLoaderTest));
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
        }

        [Test]
        public void TestLoadsMappoolByBeatmapSetIdPrefix()
        {
            writeConfig("""
                        [General]
                        Name = Test Cup
                        FirstTo = 2
                        BansPerSide = 0

                        [Beatmaps]
                        NM1 = 175241
                        HD1 = 200000:200001
                        TB = 300000
                        """);

            createOsz("175241 airportexpress feat.Itsuneko - BIRTH.osz");
            createOsz("200000 another beatmap.osz");
            createOsz("300000 tiebreaker.osz");

            LeaguePlayMappool result = new LeaguePlayMappoolLoader().Load(storage);

            Assert.That(result.Name, Is.EqualTo("Test Cup"));
            Assert.That(result.FirstTo, Is.EqualTo(2));
            Assert.That(result.BansPerSide, Is.Zero);
            Assert.That(result.Items, Has.Count.EqualTo(3));

            LeaguePlayMappoolItem noMod = result.Items.Single(item => item.Slot == "NM1");
            Assert.That(noMod.ModPool, Is.EqualTo(LeaguePlayModPool.NoMod));
            Assert.That(noMod.BeatmapSetId, Is.EqualTo(175241));
            Assert.That(noMod.BeatmapId, Is.Null);
            Assert.That(noMod.OszPath, Is.EqualTo("175241 airportexpress feat.Itsuneko - BIRTH.osz"));

            LeaguePlayMappoolItem hidden = result.Items.Single(item => item.Slot == "HD1");
            Assert.That(hidden.ModPool, Is.EqualTo(LeaguePlayModPool.Hidden));
            Assert.That(hidden.BeatmapSetId, Is.EqualTo(200000));
            Assert.That(hidden.BeatmapId, Is.EqualTo(200001));
            Assert.That(result.Tiebreaker.BeatmapSetId, Is.EqualTo(300000));
        }

        [Test]
        public void TestRejectsMissingConfig()
        {
            var exception = Assert.Throws<LeaguePlayMappoolLoadException>(() => new LeaguePlayMappoolLoader().Load(storage));
            Assert.That(exception!.Message, Does.Contain(LeaguePlayMappoolLoader.CONFIG_FILENAME));
        }

        [Test]
        public void TestRejectsInvalidOszFilename()
        {
            writeMinimalConfig();
            createOsz("airportexpress feat.Itsuneko - BIRTH.osz");
            createOsz("200000 hidden.osz");
            createOsz("300000 tiebreaker.osz");

            var exception = Assert.Throws<LeaguePlayMappoolLoadException>(() => new LeaguePlayMappoolLoader().Load(storage));
            Assert.That(exception!.Message, Does.Contain("must start with a positive beatmap set ID"));
        }

        [Test]
        public void TestRejectsDuplicateBeatmapSetFiles()
        {
            writeMinimalConfig();
            createOsz("175241 first.osz");
            createOsz("175241 duplicate.osz");
            createOsz("200000 hidden.osz");
            createOsz("300000 tiebreaker.osz");

            var exception = Assert.Throws<LeaguePlayMappoolLoadException>(() => new LeaguePlayMappoolLoader().Load(storage));
            Assert.That(exception!.Message, Does.Contain("Multiple .osz files"));
        }

        [Test]
        public void TestRejectsMissingReferencedOsz()
        {
            writeMinimalConfig();
            createOsz("175241 no-mod.osz");
            createOsz("300000 tiebreaker.osz");

            var exception = Assert.Throws<LeaguePlayMappoolLoadException>(() => new LeaguePlayMappoolLoader().Load(storage));
            Assert.That(exception!.Message, Does.Contain("beatmap set 200000"));
        }

        [Test]
        public void TestRejectsInvalidSlot()
        {
            writeConfig("""
                        [General]
                        Name = Test Cup
                        FirstTo = 1
                        BansPerSide = 0

                        [Beatmaps]
                        RX1 = 175241
                        TB = 300000
                        """);

            createOsz("175241 invalid.osz");
            createOsz("300000 tiebreaker.osz");

            var exception = Assert.Throws<LeaguePlayMappoolLoadException>(() => new LeaguePlayMappoolLoader().Load(storage));
            Assert.That(exception!.Message, Does.Contain("not a valid beatmap slot"));
        }

        [Test]
        public void TestRejectsPoolTooSmallForRules()
        {
            writeConfig("""
                        [General]
                        Name = Test Cup
                        FirstTo = 5
                        BansPerSide = 1

                        [Beatmaps]
                        NM1 = 175241
                        HD1 = 200000
                        TB = 300000
                        """);

            createOsz("175241 no-mod.osz");
            createOsz("200000 hidden.osz");
            createOsz("300000 tiebreaker.osz");

            var exception = Assert.Throws<LeaguePlayMappoolLoadException>(() => new LeaguePlayMappoolLoader().Load(storage));
            Assert.That(exception!.Message, Does.Contain("at least 10 non-TB beatmaps"));
        }

        [Test]
        public void TestCatalogLoadsPoolsFromFixedRootDirectory()
        {
            Storage poolStorage = storage.GetStorageForDirectory(LeaguePlayMappoolCatalog.ROOT_DIRECTORY)
                                         .GetStorageForDirectory("test-cup");

            writeMinimalConfig(poolStorage);
            createOsz(poolStorage, "175241 no-mod.osz");
            createOsz(poolStorage, "200000 hidden.osz");
            createOsz(poolStorage, "300000 tiebreaker.osz");

            LeaguePlayMappoolPackage package = new LeaguePlayMappoolCatalog().Load(storage).Single();

            Assert.That(package.Directory, Is.EqualTo("test-cup"));
            Assert.That(package.Mappool.Name, Is.EqualTo("Test Cup"));
        }

        private void writeMinimalConfig()
        {
            writeMinimalConfig(storage);
        }

        private static void writeMinimalConfig(Storage targetStorage)
        {
            writeConfig(targetStorage, """
                        [General]
                        Name = Test Cup
                        FirstTo = 2
                        BansPerSide = 0

                        [Beatmaps]
                        NM1 = 175241
                        HD1 = 200000
                        TB = 300000
                        """);
        }

        private void writeConfig(string contents)
        {
            writeConfig(storage, contents);
        }

        private static void writeConfig(Storage targetStorage, string contents)
        {
            using Stream stream = targetStorage.CreateFileSafely(LeaguePlayMappoolLoader.CONFIG_FILENAME);
            using var writer = new StreamWriter(stream);
            writer.Write(contents);
        }

        private void createOsz(string filename)
        {
            createOsz(storage, filename);
        }

        private static void createOsz(Storage targetStorage, string filename)
        {
            using Stream stream = targetStorage.CreateFileSafely(filename);
            stream.WriteByte(0);
        }
    }
}
