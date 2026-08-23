// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Platform;

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayMappoolCatalog
    {
        public const string ROOT_DIRECTORY = "league-pools";

        private readonly LeaguePlayMappoolLoader loader = new LeaguePlayMappoolLoader();

        /// <summary>
        /// Finds and loads every mappool package in the fixed League Play directory.
        /// </summary>
        /// <param name="storage">The game's user data storage.</param>
        public IReadOnlyList<LeaguePlayMappoolPackage> Load(Storage storage)
        {
            if (!storage.ExistsDirectory(ROOT_DIRECTORY))
                return Array.Empty<LeaguePlayMappoolPackage>();

            Storage root = storage.GetStorageForDirectory(ROOT_DIRECTORY);

            return root.GetDirectories(string.Empty)
                       .Order(StringComparer.OrdinalIgnoreCase)
                       .Select(directory => new LeaguePlayMappoolPackage
                       {
                           Directory = directory,
                           Mappool = loader.Load(root.GetStorageForDirectory(directory))
                       })
                       .ToArray();
        }
    }
}
