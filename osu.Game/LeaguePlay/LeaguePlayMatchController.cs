// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.LeaguePlay
{
    /// <summary>
    /// Owns the deterministic rules and progression of a single offline 1v1 match.
    /// UI, gameplay and opponent strategy are intentionally kept outside this class.
    /// </summary>
    public class LeaguePlayMatchController
    {
        private readonly LeaguePlayMappool mappool;
        private readonly Dictionary<string, LeaguePlaySide> bans = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LeaguePlaySide> picks = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<LeaguePlaySide> banOrder = Array.Empty<LeaguePlaySide>();
        private int nextBanIndex;
        private LeaguePlaySide? nextPicker;

        public LeaguePlayMatchController(LeaguePlayMappool mappool)
        {
            this.mappool = mappool;
        }

        public LeaguePlayMatchPhase Phase { get; private set; } = LeaguePlayMatchPhase.Rolling;

        public int? PlayerRoll { get; private set; }

        public int? OpponentRoll { get; private set; }

        public LeaguePlaySide? RollWinner { get; private set; }

        public LeaguePlaySide? FirstPicker { get; private set; }

        public LeaguePlaySide? FirstBanner { get; private set; }

        public LeaguePlaySide? ExpectedBanner => Phase == LeaguePlayMatchPhase.Banning ? banOrder[nextBanIndex] : null;

        public LeaguePlaySide? ExpectedPicker => Phase == LeaguePlayMatchPhase.Picking ? nextPicker : null;

        public LeaguePlayMappoolItem? CurrentItem { get; private set; }

        public LeaguePlaySide? CurrentPicker { get; private set; }

        public int PlayerPoints { get; private set; }

        public int OpponentPoints { get; private set; }

        public LeaguePlaySide? Winner { get; private set; }

        public IReadOnlyDictionary<string, LeaguePlaySide> Bans => bans;

        public IReadOnlyDictionary<string, LeaguePlaySide> Picks => picks;

        public void SubmitRolls(int playerRoll, int opponentRoll)
        {
            requirePhase(LeaguePlayMatchPhase.Rolling);
            validateRoll(playerRoll, nameof(playerRoll));
            validateRoll(opponentRoll, nameof(opponentRoll));

            PlayerRoll = playerRoll;
            OpponentRoll = opponentRoll;

            if (playerRoll == opponentRoll)
            {
                RollWinner = null;
                return;
            }

            RollWinner = playerRoll > opponentRoll ? LeaguePlaySide.Player : LeaguePlaySide.Opponent;
            Phase = LeaguePlayMatchPhase.ChoosingPickOrder;
        }

        /// <summary>
        /// Records the first picker selected by the roll winner.
        /// </summary>
        public void ChooseFirstPicker(LeaguePlaySide side)
        {
            requirePhase(LeaguePlayMatchPhase.ChoosingPickOrder);
            validateSide(side);

            FirstPicker = side;
            nextPicker = side;
            Phase = mappool.BansPerSide == 0 ? LeaguePlayMatchPhase.Picking : LeaguePlayMatchPhase.ChoosingBanOrder;
        }

        /// <summary>
        /// Records the first banner selected by the roll loser and creates a snake-style ban order.
        /// Two bans per side therefore produce A-B-B-A.
        /// </summary>
        public void ChooseFirstBanner(LeaguePlaySide side)
        {
            requirePhase(LeaguePlayMatchPhase.ChoosingBanOrder);
            validateSide(side);

            FirstBanner = side;

            var order = new List<LeaguePlaySide>(mappool.BansPerSide * 2);
            LeaguePlaySide other = opposite(side);

            for (int round = 0; round < mappool.BansPerSide; round++)
            {
                if (round % 2 == 0)
                {
                    order.Add(side);
                    order.Add(other);
                }
                else
                {
                    order.Add(other);
                    order.Add(side);
                }
            }

            banOrder = order;
            Phase = LeaguePlayMatchPhase.Banning;
        }

        public void Ban(LeaguePlaySide side, string slot)
        {
            requirePhase(LeaguePlayMatchPhase.Banning);
            validateSide(side);

            if (ExpectedBanner != side)
                throw new InvalidOperationException($"It is {ExpectedBanner}'s ban turn.");

            LeaguePlayMappoolItem item = getItem(slot);

            if (item.ModPool == LeaguePlayModPool.Tiebreaker)
                throw new InvalidOperationException("The tiebreaker cannot be banned.");

            ensureAvailable(item);
            bans.Add(item.Slot, side);
            nextBanIndex++;

            if (nextBanIndex == banOrder.Count)
                Phase = LeaguePlayMatchPhase.Picking;
        }

        public void Pick(LeaguePlaySide side, string slot)
        {
            requirePhase(LeaguePlayMatchPhase.Picking);
            validateSide(side);

            if (ExpectedPicker != side)
                throw new InvalidOperationException($"It is {ExpectedPicker}'s pick turn.");

            LeaguePlayMappoolItem item = getItem(slot);

            if (item.ModPool == LeaguePlayModPool.Tiebreaker)
                throw new InvalidOperationException("The tiebreaker is selected automatically at match point.");

            ensureAvailable(item);
            picks.Add(item.Slot, side);
            CurrentItem = item;
            CurrentPicker = side;
            Phase = LeaguePlayMatchPhase.AwaitingRoundResult;
        }

        /// <summary>
        /// Applies one completed round. Equal scores leave the same map active for a replay.
        /// </summary>
        public void SubmitRoundScores(long playerScore, long opponentScore)
        {
            requirePhase(LeaguePlayMatchPhase.AwaitingRoundResult);

            ArgumentOutOfRangeException.ThrowIfNegative(playerScore);
            ArgumentOutOfRangeException.ThrowIfNegative(opponentScore);

            if (playerScore == opponentScore)
                return;

            LeaguePlaySide roundWinner = playerScore > opponentScore ? LeaguePlaySide.Player : LeaguePlaySide.Opponent;

            if (roundWinner == LeaguePlaySide.Player)
                PlayerPoints++;
            else
                OpponentPoints++;

            if (PlayerPoints == mappool.FirstTo || OpponentPoints == mappool.FirstTo)
            {
                Winner = roundWinner;
                Phase = LeaguePlayMatchPhase.Completed;
                return;
            }

            if (PlayerPoints == mappool.FirstTo - 1 && OpponentPoints == mappool.FirstTo - 1)
            {
                CurrentItem = mappool.Tiebreaker;
                CurrentPicker = null;
                return;
            }

            CurrentItem = null;
            CurrentPicker = null;
            nextPicker = opposite(nextPicker!.Value);
            Phase = LeaguePlayMatchPhase.Picking;
        }

        private LeaguePlayMappoolItem getItem(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException("A mappool slot is required.", nameof(slot));

            return mappool.Items.SingleOrDefault(item => string.Equals(item.Slot, slot, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ArgumentException($"Mappool slot '{slot}' does not exist.", nameof(slot));
        }

        private void ensureAvailable(LeaguePlayMappoolItem item)
        {
            if (bans.ContainsKey(item.Slot))
                throw new InvalidOperationException($"{item.Slot} has already been banned.");

            if (picks.ContainsKey(item.Slot))
                throw new InvalidOperationException($"{item.Slot} has already been picked.");
        }

        private void requirePhase(LeaguePlayMatchPhase expected)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"This action requires phase {expected}, but the match is in phase {Phase}.");
        }

        private static void validateRoll(int roll, string parameterName)
        {
            if (roll is < 0 or > 100)
                throw new ArgumentOutOfRangeException(parameterName, "Rolls must be between 0 and 100.");
        }

        private static void validateSide(LeaguePlaySide side)
        {
            if (!Enum.IsDefined(side))
                throw new ArgumentOutOfRangeException(nameof(side));
        }

        private static LeaguePlaySide opposite(LeaguePlaySide side) => side == LeaguePlaySide.Player ? LeaguePlaySide.Opponent : LeaguePlaySide.Player;
    }
}
