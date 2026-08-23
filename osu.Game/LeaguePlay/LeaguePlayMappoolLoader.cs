// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Platform;

namespace osu.Game.LeaguePlay
{
    public class LeaguePlayMappoolLoader
    {
        public const string CONFIG_FILENAME = "config.ini";

        private static readonly Regex osz_filename_regex = new Regex(@"^(?<setId>[1-9]\d*)(?:\s+.+)?\.osz$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex slot_regex = new Regex(@"^(?<pool>NM|HD|HR|DT|FM)(?<index>[1-9]\d*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public LeaguePlayMappool Load(Storage storage)
        {
            if (!storage.Exists(CONFIG_FILENAME))
                throw new LeaguePlayMappoolLoadException($"The mappool does not contain {CONFIG_FILENAME}.");

            IniDocument config;

            using (Stream stream = storage.GetStream(CONFIG_FILENAME))
            using (var reader = new StreamReader(stream))
                config = parseIni(reader);

            IniSection general = config.GetRequiredSection("General");
            IniSection beatmaps = config.GetRequiredSection("Beatmaps");

            general.EnsureOnlyContains("Name", "FirstTo", "BansPerSide");

            string name = general.GetRequired("Name");
            int firstTo = parseInteger(general.GetRequired("FirstTo"), "General.FirstTo", minimum: 1);
            int bansPerSide = parseInteger(general.GetRequired("BansPerSide"), "General.BansPerSide", minimum: 0);

            Dictionary<int, string> oszFiles = findOszFiles(storage);
            var items = new List<LeaguePlayMappoolItem>();

            foreach ((string slot, IniValue value) in beatmaps.Values)
            {
                LeaguePlayModPool modPool = parseSlot(slot);
                (int beatmapSetId, int? beatmapId) = parseBeatmapReference(value.Value, slot);

                if (!oszFiles.TryGetValue(beatmapSetId, out string? oszPath))
                    throw new LeaguePlayMappoolLoadException($"Beatmaps.{slot} references beatmap set {beatmapSetId}, but no matching .osz file was found.");

                items.Add(new LeaguePlayMappoolItem
                {
                    Slot = slot.ToUpperInvariant(),
                    ModPool = modPool,
                    BeatmapSetId = beatmapSetId,
                    BeatmapId = beatmapId,
                    OszPath = oszPath
                });
            }

            if (items.Count == 0)
                throw new LeaguePlayMappoolLoadException("The Beatmaps section must contain at least one beatmap.");

            int tiebreakerCount = items.Count(item => item.ModPool == LeaguePlayModPool.Tiebreaker);

            if (tiebreakerCount != 1)
                throw new LeaguePlayMappoolLoadException("The mappool must contain exactly one TB slot.");

            int requiredRegularBeatmaps = Math.Max(1, 2 * (firstTo - 1)) + 2 * bansPerSide;
            int regularBeatmapCount = items.Count - 1;

            if (regularBeatmapCount < requiredRegularBeatmaps)
            {
                throw new LeaguePlayMappoolLoadException(
                    $"The mappool needs at least {requiredRegularBeatmaps} non-TB beatmaps for FirstTo={firstTo} and BansPerSide={bansPerSide}, but only {regularBeatmapCount} were configured.");
            }

            return new LeaguePlayMappool
            {
                Name = name,
                FirstTo = firstTo,
                BansPerSide = bansPerSide,
                Items = items
            };
        }

        private static Dictionary<int, string> findOszFiles(Storage storage)
        {
            var files = new Dictionary<int, string>();

            foreach (string path in storage.GetFiles(string.Empty)
                                           .Where(path => Path.GetExtension(path).Equals(".osz", StringComparison.OrdinalIgnoreCase))
                                           .Order(StringComparer.OrdinalIgnoreCase))
            {
                string filename = Path.GetFileName(path);
                Match match = osz_filename_regex.Match(filename);

                if (!match.Success)
                    throw new LeaguePlayMappoolLoadException($"The .osz filename '{filename}' must start with a positive beatmap set ID.");

                int beatmapSetId = int.Parse(match.Groups["setId"].Value, CultureInfo.InvariantCulture);

                if (!files.TryAdd(beatmapSetId, path))
                    throw new LeaguePlayMappoolLoadException($"Multiple .osz files start with beatmap set ID {beatmapSetId}.");
            }

            return files;
        }

        private static LeaguePlayModPool parseSlot(string slot)
        {
            if (slot.Equals("TB", StringComparison.OrdinalIgnoreCase))
                return LeaguePlayModPool.Tiebreaker;

            Match match = slot_regex.Match(slot);

            if (!match.Success)
                throw new LeaguePlayMappoolLoadException($"'{slot}' is not a valid beatmap slot. Expected NM1, HD1, HR1, DT1, FM1, or TB.");

            return match.Groups["pool"].Value.ToUpperInvariant() switch
            {
                "NM" => LeaguePlayModPool.NoMod,
                "HD" => LeaguePlayModPool.Hidden,
                "HR" => LeaguePlayModPool.HardRock,
                "DT" => LeaguePlayModPool.DoubleTime,
                "FM" => LeaguePlayModPool.FreeMod,
                _ => throw new InvalidOperationException()
            };
        }

        private static (int BeatmapSetId, int? BeatmapId) parseBeatmapReference(string reference, string slot)
        {
            string[] components = reference.Split(':', StringSplitOptions.TrimEntries);

            if (components.Length is < 1 or > 2
                || !int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out int beatmapSetId)
                || beatmapSetId <= 0)
            {
                throw new LeaguePlayMappoolLoadException($"Beatmaps.{slot} must contain a positive beatmap set ID, optionally followed by ':beatmapId'.");
            }

            if (components.Length == 1)
                return (beatmapSetId, null);

            if (!int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out int beatmapId) || beatmapId <= 0)
                throw new LeaguePlayMappoolLoadException($"Beatmaps.{slot} contains an invalid beatmap ID.");

            return (beatmapSetId, beatmapId);
        }

        private static int parseInteger(string value, string key, int minimum)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) || result < minimum)
                throw new LeaguePlayMappoolLoadException($"{key} must be an integer greater than or equal to {minimum}.");

            return result;
        }

        private static IniDocument parseIni(TextReader reader)
        {
            var document = new IniDocument();
            IniSection? currentSection = null;
            int lineNumber = 0;

            while (reader.ReadLine() is string rawLine)
            {
                lineNumber++;
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                    continue;

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    string sectionName = line[1..^1].Trim();

                    if (sectionName.Length == 0)
                        throw new LeaguePlayMappoolLoadException($"config.ini line {lineNumber} contains an empty section name.");

                    currentSection = document.AddSection(sectionName, lineNumber);
                    continue;
                }

                if (currentSection == null)
                    throw new LeaguePlayMappoolLoadException($"config.ini line {lineNumber} appears before any section.");

                int separatorIndex = line.IndexOf('=');

                if (separatorIndex <= 0)
                    throw new LeaguePlayMappoolLoadException($"config.ini line {lineNumber} must use 'Key = Value' syntax.");

                string key = line[..separatorIndex].Trim();
                string value = line[(separatorIndex + 1)..].Trim();

                if (key.Length == 0 || value.Length == 0)
                    throw new LeaguePlayMappoolLoadException($"config.ini line {lineNumber} must contain a non-empty key and value.");

                currentSection.Add(key, value, lineNumber);
            }

            return document;
        }

        private sealed class IniDocument
        {
            private readonly Dictionary<string, IniSection> sections = new Dictionary<string, IniSection>(StringComparer.OrdinalIgnoreCase);

            public IniSection AddSection(string name, int lineNumber)
            {
                if (!sections.TryAdd(name, new IniSection(name)))
                    throw new LeaguePlayMappoolLoadException($"config.ini line {lineNumber} duplicates the [{name}] section.");

                return sections[name];
            }

            public IniSection GetRequiredSection(string name)
            {
                if (!sections.TryGetValue(name, out IniSection? section))
                    throw new LeaguePlayMappoolLoadException($"config.ini does not contain a [{name}] section.");

                return section;
            }
        }

        private sealed class IniSection
        {
            public IReadOnlyDictionary<string, IniValue> Values => values;

            private readonly string name;
            private readonly Dictionary<string, IniValue> values = new Dictionary<string, IniValue>(StringComparer.OrdinalIgnoreCase);

            public IniSection(string name)
            {
                this.name = name;
            }

            public void Add(string key, string value, int lineNumber)
            {
                if (!values.TryAdd(key, new IniValue(value)))
                    throw new LeaguePlayMappoolLoadException($"config.ini line {lineNumber} duplicates {name}.{key}.");
            }

            public string GetRequired(string key)
            {
                if (!values.TryGetValue(key, out IniValue? value))
                    throw new LeaguePlayMappoolLoadException($"config.ini does not contain {name}.{key}.");

                return value.Value;
            }

            public void EnsureOnlyContains(params string[] allowedKeys)
            {
                var allowed = new HashSet<string>(allowedKeys, StringComparer.OrdinalIgnoreCase);
                string? unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key));

                if (unknown != null)
                    throw new LeaguePlayMappoolLoadException($"config.ini contains the unsupported setting {name}.{unknown}.");
            }
        }

        private sealed record IniValue(string Value);
    }
}
