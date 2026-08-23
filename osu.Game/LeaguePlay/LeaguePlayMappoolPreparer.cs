// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;

namespace osu.Game.LeaguePlay
{
    /// <summary>
    /// Imports a mappool package's beatmaps when necessary and resolves each configured slot to a local difficulty.
    /// </summary>
    public class LeaguePlayMappoolPreparer
    {
        private readonly BeatmapManager beatmapManager;

        public LeaguePlayMappoolPreparer(BeatmapManager beatmapManager)
        {
            this.beatmapManager = beatmapManager;
        }

        public async Task<LeaguePlayPreparedMappool> Prepare(Storage gameStorage, LeaguePlayMappoolPackage package)
        {
            Storage packageStorage = gameStorage.GetStorageForDirectory(LeaguePlayMappoolCatalog.ROOT_DIRECTORY)
                                                .GetStorageForDirectory(package.Directory);

            LeaguePlayMappoolItem[] unresolvedItems = package.Mappool.Items.Where(item => tryResolve(item) == null).ToArray();

            ImportTask[] importTasks = unresolvedItems.Select(item => item.OszPath)
                                                      .Distinct(StringComparer.OrdinalIgnoreCase)
                                                      .Select(path => (ImportTask)new PreservedImportTask(packageStorage.GetFullPath(path)))
                                                      .ToArray();

            if (importTasks.Length > 0)
            {
                await beatmapManager.Import(importTasks, new ImportParameters
                {
                    Batch = importTasks.Length > 1
                }).ConfigureAwait(false);
            }

            var preparedItems = new List<LeaguePlayPreparedMappoolItem>();

            foreach (LeaguePlayMappoolItem item in package.Mappool.Items)
            {
                BeatmapInfo? beatmap = tryResolve(item);

                if (beatmap == null)
                {
                    throw new LeaguePlayMappoolLoadException(
                        $"{item.Slot} could not be resolved after importing '{Path.GetFileName(item.OszPath)}'. Check that its beatmap set and beatmap IDs are correct.");
                }

                preparedItems.Add(new LeaguePlayPreparedMappoolItem
                {
                    Source = item,
                    Beatmap = beatmap
                });
            }

            return new LeaguePlayPreparedMappool
            {
                Source = package.Mappool,
                Items = preparedItems
            };
        }

        private BeatmapInfo? tryResolve(LeaguePlayMappoolItem item)
        {
            Live<BeatmapSetInfo>? beatmapSet = beatmapManager.QueryBeatmapSet(set => set.OnlineID == item.BeatmapSetId);

            if (beatmapSet == null)
                return null;

            BeatmapInfo[] candidates = beatmapSet.PerformRead(set => set.Beatmaps.Where(beatmap => beatmap.Ruleset.OnlineID == 0
                                                                                                 && (item.BeatmapId == null || beatmap.OnlineID == item.BeatmapId))
                                                                          .Select(beatmap => beatmap.Detach())
                                                                          .ToArray());

            if (item.BeatmapId != null)
            {
                return candidates.Length switch
                {
                    0 => null,
                    1 => candidates[0],
                    _ => throw new LeaguePlayMappoolLoadException(
                        $"{item.Slot} resolves to more than one local copy of beatmap {item.BeatmapId} in set {item.BeatmapSetId}.")
                };
            }

            return candidates.Length switch
            {
                0 => throw new LeaguePlayMappoolLoadException($"{item.Slot} references a beatmap set with no osu!standard difficulties."),
                1 => candidates[0],
                _ => throw new LeaguePlayMappoolLoadException(
                    $"{item.Slot} references beatmap set {item.BeatmapSetId}, which contains multiple osu!standard difficulties. Use '{item.BeatmapSetId}:beatmapId' in config.ini.")
            };
        }

        /// <summary>
        /// League Play packages are persistent user data, while the standard importer deletes successfully imported .osz files.
        /// Suppress that deletion so the package remains reusable and portable.
        /// </summary>
        private sealed class PreservedImportTask : ImportTask
        {
            public PreservedImportTask(string path)
                : base(path)
            {
            }

            public override void DeleteFile()
            {
            }
        }
    }
}
