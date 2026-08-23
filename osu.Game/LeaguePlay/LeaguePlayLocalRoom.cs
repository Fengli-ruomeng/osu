// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mods;

namespace osu.Game.LeaguePlay
{
    /// <summary>
    /// An in-process two-player multiplayer room used by League Play.
    /// It owns real multiplayer room/user state while deliberately omitting any network transport.
    /// </summary>
    public class LeaguePlayLocalRoom
    {
        public const int OPPONENT_USER_ID = -1;

        public MultiplayerRoom Room { get; }

        public MultiplayerRoomUser Player { get; }

        public MultiplayerRoomUser Opponent { get; }

        public APIUser OpponentUser { get; }

        public IBindableList<int> PlayingUserIds => playingUserIds;

        private readonly BindableList<int> playingUserIds = new BindableList<int>();

        private long nextPlaylistItemId;

        public LeaguePlayLocalRoom(APIUser playerUser, string name)
        {
            ArgumentNullException.ThrowIfNull(playerUser);

            if (string.IsNullOrWhiteSpace(playerUser.Username))
            {
                playerUser = new APIUser
                {
                    Id = playerUser.Id,
                    Username = "Player",
                    AvatarUrl = playerUser.AvatarUrl,
                };
            }

            OpponentUser = new APIUser
            {
                Id = OPPONENT_USER_ID,
                Username = "League AI",
                IsBot = true,
            };

            Player = new MultiplayerRoomUser(playerUser.Id)
            {
                User = playerUser,
                MatchState = new TeamVersusUserState { TeamID = 1 },
            };

            Opponent = new MultiplayerRoomUser(OPPONENT_USER_ID)
            {
                User = OpponentUser,
                MatchState = new TeamVersusUserState { TeamID = 0 },
            };

            var matchState = TeamVersusRoomState.CreateDefault(2);
            matchState.Slots![0] = Opponent.UserID;
            matchState.Slots[1] = Player.UserID;

            Room = new MultiplayerRoom(-1)
            {
                State = MultiplayerRoomState.Open,
                Host = Player,
                MatchState = matchState,
                Users = new List<MultiplayerRoomUser> { Player, Opponent },
                Settings = new MultiplayerRoomSettings
                {
                    Name = name,
                    MatchType = MatchType.TeamVersus,
                    QueueMode = QueueMode.HostOnly,
                    MaxParticipants = 2,
                    AutoSkip = true,
                },
            };
        }

        public void PrepareRound(BeatmapInfo beatmap, IReadOnlyList<Mod> requiredMods, Mod leagueAutoplay)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            ArgumentNullException.ThrowIfNull(requiredMods);
            ArgumentNullException.ThrowIfNull(leagueAutoplay);

            if (beatmap.Ruleset.OnlineID != 0)
                throw new NotSupportedException("League Autoplay currently supports osu!standard only.");

            var item = new MultiplayerPlaylistItem
            {
                ID = ++nextPlaylistItemId,
                OwnerID = Player.UserID,
                BeatmapID = beatmap.OnlineID,
                BeatmapChecksum = beatmap.MD5Hash,
                RulesetID = beatmap.Ruleset.OnlineID,
                RequiredMods = requiredMods.Select(mod => new APIMod(mod)).ToArray(),
            };

            Room.Playlist.Clear();
            Room.Playlist.Add(item);
            Room.Settings.PlaylistItemId = item.ID;
            Room.State = MultiplayerRoomState.WaitingForLoad;

            Player.Mods = Array.Empty<APIMod>();
            Opponent.Mods = new[] { new APIMod(leagueAutoplay) };
            Player.State = MultiplayerUserState.Loaded;
            Opponent.State = MultiplayerUserState.Loaded;

            playingUserIds.Clear();
            playingUserIds.Add(Player.UserID);
            playingUserIds.Add(Opponent.UserID);
        }

        public void StartGameplay()
        {
            Room.State = MultiplayerRoomState.Playing;
            Player.State = MultiplayerUserState.Playing;
            Opponent.State = MultiplayerUserState.Playing;
        }

        public void FinishPlayer() => Player.State = MultiplayerUserState.FinishedPlay;

        public void FinishOpponent() => Opponent.State = MultiplayerUserState.FinishedPlay;

        public void FinishRound()
        {
            Player.State = MultiplayerUserState.Results;
            Opponent.State = MultiplayerUserState.Results;
            Room.State = MultiplayerRoomState.Open;
        }
    }
}
