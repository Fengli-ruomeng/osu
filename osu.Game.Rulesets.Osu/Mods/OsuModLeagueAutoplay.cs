// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Replays;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Test-only replay generator intended to drive a local league opponent.
    /// </summary>
    public class OsuModLeagueAutoplay : Mod, ICreateReplayData
    {
        public override string Name => "League Autoplay";
        public override string Acronym => "LAT";
        public override IconUsage? Icon => OsuIcon.ModAutoplay;
        public override ModType Type => ModType.Automation;
        public override LocalisableString Description => "Watch an automated player with configurable timing variance and misses.";

        public override bool HasImplementation => true;

        // A multiplayer AI runner must consume the generated replay. A normal MultiplayerPlayer does not do so.
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay),
            typeof(ModCinema),
            typeof(ModRelax),
            typeof(OsuModAutopilot),
        };

        [SettingSource("Real time", "Run as a live League Play participant instead of only presenting a replay.", 0)]
        public BindableBool RealTime { get; } = new BindableBool();

        [SettingSource("Minimum UR", "Minimum unstable rate used when choosing each hit's timing variance.", 1)]
        public BindableNumber<double> MinimumUnstableRate { get; } = new BindableDouble(110)
        {
            MinValue = 0,
            MaxValue = 500,
            Precision = 1,
        };

        [SettingSource("Maximum UR", "Maximum unstable rate used when choosing each hit's timing variance.", 2)]
        public BindableNumber<double> MaximumUnstableRate { get; } = new BindableDouble(160)
        {
            MinValue = 0,
            MaxValue = 500,
            Precision = 1,
        };

        [SettingSource("Miss chance", "Additional chance to deliberately skip each primary hit object.", 3, SettingControlType = typeof(SettingsPercentageSlider<double>))]
        public BindableNumber<double> MissChance { get; } = new BindableDouble(0.015)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.001,
        };

        /// <summary>
        /// An optional seed for a deterministic AI replay. This is set by League Play rather than exposed as a mod setting.
        /// </summary>
        public int? ReplaySeed { get; set; }

        public ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            int seed = ReplaySeed ?? RNG.Next();
            var replay = new OsuLeagueAutoGenerator(
                beatmap,
                mods,
                MinimumUnstableRate.Value,
                MaximumUnstableRate.Value,
                MissChance.Value,
                seed).Generate();

            return new ModReplayData(replay, new ModCreatedUser { Username = "League AI" });
        }
    }
}
