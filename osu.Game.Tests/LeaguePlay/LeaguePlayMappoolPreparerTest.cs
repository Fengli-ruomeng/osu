// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.LeaguePlay;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.LeaguePlay
{
    [TestFixture]
    public class LeaguePlayMappoolPreparerTest : ImportTest
    {
        [Test]
        public async Task TestImportsAndResolvesSpecificDifficultyWithoutDeletingPackage()
        {
            using HeadlessGameHost host = new CleanRunHeadlessGameHost();

            try
            {
                TestOsuGameBase game = LoadOsuIntoHost(host);
                Storage gameStorage = game.Dependencies.Get<Storage>();
                BeatmapManager beatmapManager = game.Dependencies.Get<BeatmapManager>();
                Storage packageStorage = gameStorage.GetStorageForDirectory(LeaguePlayMappoolCatalog.ROOT_DIRECTORY)
                                                    .GetStorageForDirectory("test-pool");

                const string osz_filename = "241526 Soleily - Renatus.osz";

                using (Stream source = TestResources.GetTestBeatmapStream())
                using (Stream destination = packageStorage.CreateFileSafely(osz_filename))
                    await source.CopyToAsync(destination);

                using (Stream configStream = packageStorage.CreateFileSafely(LeaguePlayMappoolLoader.CONFIG_FILENAME))
                using (var writer = new StreamWriter(configStream))
                {
                    await writer.WriteAsync("""
                                            [General]
                                            Name = Test Pool
                                            FirstTo = 1
                                            BansPerSide = 0

                                            [Beatmaps]
                                            NM1 = 241526:557821
                                            TB = 241526:557821
                                            """);
                }

                LeaguePlayMappoolPackage package = new LeaguePlayMappoolCatalog().Load(gameStorage).Single();
                LeaguePlayPreparedMappool prepared = await new LeaguePlayMappoolPreparer(beatmapManager).Prepare(gameStorage, package);

                Assert.That(prepared.Items, Has.Count.EqualTo(2));
                Assert.That(prepared.Items.All(item => item.Beatmap.OnlineID == 557821));
                Assert.That(prepared.Items.All(item => item.Beatmap.DifficultyName == "Insane"));
                Assert.That(packageStorage.Exists(osz_filename), Is.True, "Preparing a mappool must not consume its source .osz file.");
            }
            finally
            {
                host.Exit();
            }
        }
    }
}
