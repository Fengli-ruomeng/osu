// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class DebugSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.DebugSettings";

        /// <summary>
        /// "Import files"
        /// </summary>
        public static LocalisableString ImportFiles => new TranslatableString(getKey(@"import_files"), @"Import files");

        /// <summary>
        /// "Import selected file"
        /// </summary>
        public static LocalisableString ImportSelectedFile => new TranslatableString(getKey(@"import_selected_file"), @"Import selected file");

        /// <summary>
        /// "Import all files from directory"
        /// </summary>
        public static LocalisableString ImportAllFilesFromDirectory => new TranslatableString(getKey(@"import_all_files_from_directory"), @"Import all files from directory");

        /// <summary>
        /// "Imports all osu! files from selected directory"
        /// </summary>
        public static LocalisableString ImportAllFilesFromDirectoryTooltip => new TranslatableString(getKey(@"import_all_files_from_directory_tooltip"), @"Imports all osu! files from selected directory");

        /// <summary>
        /// "Select a file"
        /// </summary>
        public static LocalisableString SelectFile => new TranslatableString(getKey(@"select_file"), @"Select a file");

        /// <summary>
        /// "Run latency certifier"
        /// </summary>
        public static LocalisableString RunLatencyCertifier => new TranslatableString(getKey(@"run_latency_certifier"), @"Run latency certifier");

        /// <summary>
        /// "FluidCursorTrail"
        /// </summary>
        public static LocalisableString FluidCursorTrail => new TranslatableString(getKey(@"fluid_cursor_trail"), @"FluidCursorTrail");

        /// <summary>
        /// "FluidCursorTrail thickness"
        /// </summary>
        public static LocalisableString FluidCursorTrailThickness => new TranslatableString(getKey(@"fluid_cursor_trail_thickness"), @"FluidCursorTrail thickness");

        /// <summary>
        /// "FluidCursorTrail length"
        /// </summary>
        public static LocalisableString FluidCursorTrailLength => new TranslatableString(getKey(@"fluid_cursor_trail_length"), @"FluidCursorTrail length");

        /// <summary>
        /// "FluidCursorTrail colour"
        /// </summary>
        public static LocalisableString FluidCursorTrailColour => new TranslatableString(getKey(@"fluid_cursor_trail_colour"), @"FluidCursorTrail colour");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
