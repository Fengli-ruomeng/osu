// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Online.Multiplayer.MatchTypes.Matchmaking;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Match
{
    /// <summary>
    /// A local state source for driving the matchmaking stage display without a multiplayer server.
    /// </summary>
    public class MatchmakingStageState
    {
        public readonly Bindable<MatchmakingStage> Stage = new Bindable<MatchmakingStage>(MatchmakingStage.WaitingForClientsJoin);
        public readonly BindableInt CurrentRound = new BindableInt(1) { MinValue = 1 };
        public readonly Bindable<DateTimeOffset?> CountdownEnd = new Bindable<DateTimeOffset?>();

        public void Set(MatchmakingStage stage, int round, TimeSpan? duration = null)
        {
            CurrentRound.Value = round;
            Stage.Value = stage;
            CountdownEnd.Value = duration == null ? null : DateTimeOffset.Now + duration.Value;
        }
    }
}
