// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    /// <summary>
    /// Collects taps against a fixed metronome and produces a global audio offset suggestion.
    /// </summary>
    public class OffsetCalibrationSession
    {
        public const double BPM = 120;
        public const double BEAT_LENGTH = 60000 / BPM;
        public const int WARMUP_BEATS = 8;
        public const int TAPS_PER_ROUND = 24;
        public const int REQUIRED_ROUNDS = 2;

        private const double maximum_tap_error = 200;
        private const double maximum_round_spread = 30;
        private const double maximum_round_disagreement = 10;

        private readonly double initialGlobalOffset;
        private readonly double platformOffset;
        private readonly List<RoundResult> roundResults = new List<RoundResult>();
        private readonly List<double> currentRoundErrors = new List<double>();
        private readonly HashSet<int> registeredBeatIndices = new HashSet<int>();
        private readonly Dictionary<int, double> dispatchedBeats = new Dictionary<int, double>();

        private double roundStartTime;

        public CalibrationState State { get; private set; } = CalibrationState.NotStarted;

        public int CompletedRounds => roundResults.Count;

        public int RecordedTaps => currentRoundErrors.Count;

        public double? SuggestedOffset { get; private set; }

        public bool IsReliable { get; private set; }

        public IReadOnlyList<RoundResult> RoundResults => roundResults;

        public OffsetCalibrationSession(double initialGlobalOffset, double platformOffset)
        {
            this.initialGlobalOffset = initialGlobalOffset;
            this.platformOffset = platformOffset;
        }

        public void StartRound(double startTime)
        {
            if (State == CalibrationState.Running || State == CalibrationState.Completed)
                throw new InvalidOperationException($"Cannot start a round while calibration is {State}.");

            roundStartTime = startTime;
            currentRoundErrors.Clear();
            registeredBeatIndices.Clear();
            dispatchedBeats.Clear();
            State = CalibrationState.Running;
        }

        /// <summary>
        /// Records when a metronome sound was submitted for playback. Using the dispatch time rather than
        /// the theoretical beat boundary avoids including frame scheduling jitter in the result.
        /// </summary>
        public void RegisterBeat(int beatIndex, double dispatchTime)
        {
            if (State == CalibrationState.Running)
                dispatchedBeats[beatIndex] = dispatchTime;
        }

        public int GetWarmupBeatsRemaining(double currentTime)
        {
            if (State != CalibrationState.Running)
                return 0;

            int currentBeat = (int)Math.Floor((currentTime - roundStartTime) / BEAT_LENGTH);
            return Math.Clamp(WARMUP_BEATS - currentBeat, 0, WARMUP_BEATS);
        }

        /// <summary>
        /// Attempts to register a tap. Taps during warmup, duplicate taps on one beat and taps too far from a beat are ignored.
        /// </summary>
        public bool RegisterTap(double tapTime)
        {
            if (State != CalibrationState.Running)
                return false;

            if (dispatchedBeats.Count == 0)
                return false;

            KeyValuePair<int, double> lastDispatchedBeat = dispatchedBeats.MaxBy(beat => beat.Key);
            var nextPredictedBeat = new KeyValuePair<int, double>(lastDispatchedBeat.Key + 1, lastDispatchedBeat.Value + BEAT_LENGTH);
            KeyValuePair<int, double> closestBeat = dispatchedBeats.Append(nextPredictedBeat).MinBy(beat => Math.Abs(tapTime - beat.Value));
            int closestBeatIndex = closestBeat.Key;

            if (closestBeatIndex < WARMUP_BEATS || !registeredBeatIndices.Add(closestBeatIndex))
                return false;

            double rawError = tapTime - closestBeat.Value;

            if (Math.Abs(rawError) > maximum_tap_error)
            {
                registeredBeatIndices.Remove(closestBeatIndex);
                return false;
            }

            // This mirrors gameplay timing: the platform and current global offsets are already applied
            // to the gameplay clock when a hit error is calculated.
            currentRoundErrors.Add(rawError + platformOffset + initialGlobalOffset);

            if (currentRoundErrors.Count == TAPS_PER_ROUND)
                completeRound();

            return true;
        }

        private void completeRound()
        {
            double medianError = median(currentRoundErrors);
            double medianAbsoluteDeviation = median(currentRoundErrors.Select(error => Math.Abs(error - medianError)));
            double spread = medianAbsoluteDeviation * 1.4826;

            roundResults.Add(new RoundResult(initialGlobalOffset - medianError, spread));

            if (roundResults.Count < REQUIRED_ROUNDS)
            {
                State = CalibrationState.BetweenRounds;
                return;
            }

            // Calculate the final result from round medians. Giving both rounds equal weight prevents an
            // accidentally longer future round from dominating the result.
            SuggestedOffset = Math.Round(median(roundResults.Select(result => result.SuggestedOffset)));
            IsReliable = roundResults.All(result => result.Spread <= maximum_round_spread)
                         && roundResults.Max(result => result.SuggestedOffset) - roundResults.Min(result => result.SuggestedOffset) <= maximum_round_disagreement;
            State = CalibrationState.Completed;
        }

        private static double median(IEnumerable<double> values)
        {
            double[] ordered = values.Order().ToArray();

            if (ordered.Length == 0)
                throw new InvalidOperationException("Cannot calculate the median of an empty collection.");

            int midpoint = ordered.Length / 2;
            return ordered.Length % 2 == 0 ? (ordered[midpoint - 1] + ordered[midpoint]) / 2 : ordered[midpoint];
        }

        public readonly struct RoundResult
        {
            public double SuggestedOffset { get; }
            public double Spread { get; }

            public RoundResult(double suggestedOffset, double spread)
            {
                SuggestedOffset = suggestedOffset;
                Spread = spread;
            }
        }

        public enum CalibrationState
        {
            NotStarted,
            Running,
            BetweenRounds,
            Completed,
        }
    }
}
