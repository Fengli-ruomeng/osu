// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online.API;
using osu.Game.Online.Spectator;

namespace osu.Game.LeaguePlay
{
    /// <summary>
    /// Loops local gameplay frames back through the normal spectator events used by multiplayer leaderboards.
    /// </summary>
    public partial class LeaguePlaySpectatorClient : SpectatorClient
    {
        public override IBindable<bool> IsConnected => isConnected;

        private readonly BindableBool isConnected = new BindableBool(true);
        private readonly Dictionary<int, SpectatorState> userStates = new Dictionary<int, SpectatorState>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        public async Task BeginUser(int userId, SpectatorState state)
        {
            userStates[userId] = state;
            await ((ISpectatorClient)this).UserBeganPlaying(userId, state).ConfigureAwait(false);
        }

        public Task SendFrames(int userId, FrameDataBundle bundle)
            => ((ISpectatorClient)this).UserSentFrames(userId, bundle);

        public async Task EndUser(int userId, SpectatedUserState finalState)
        {
            if (!userStates.TryGetValue(userId, out SpectatorState? state))
                return;

            state.State = finalState;
            await ((ISpectatorClient)this).UserFinishedPlaying(userId, state).ConfigureAwait(false);
        }

        protected override async Task<bool> BeginPlayingInternal(long? scoreToken, SpectatorState state)
        {
            await BeginUser(api.LocalUser.Value.Id, state).ConfigureAwait(false);
            return true;
        }

        protected override Task SendFramesInternal(long? scoreToken, FrameDataBundle bundle)
            => SendFrames(api.LocalUser.Value.Id, bundle);

        protected override Task EndPlayingInternal(long? scoreToken, SpectatedUserState finalState)
            => EndUser(api.LocalUser.Value.Id, finalState);

        protected override Task WatchUserInternal(int userId)
        {
            if (userStates.TryGetValue(userId, out SpectatorState? state))
                return ((ISpectatorClient)this).UserBeganPlaying(userId, state);

            return Task.CompletedTask;
        }

        protected override Task StopWatchingUserInternal(int userId) => Task.CompletedTask;

        protected override Task DisconnectInternal()
        {
            isConnected.Value = false;
            return Task.CompletedTask;
        }

        public override Task Reconnect()
        {
            isConnected.Value = true;
            return Task.CompletedTask;
        }
    }
}
