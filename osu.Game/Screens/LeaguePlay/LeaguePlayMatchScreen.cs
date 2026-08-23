// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.LeaguePlay;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer.MatchTypes.Matchmaking;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.OnlinePlay.Matchmaking;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match.BeatmapSelect;
using osu.Game.Screens.Play;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.LeaguePlay
{
    /// <summary>
    /// Runs an offline 1v1 league match using the original Quick Play presentation layer.
    /// </summary>
    public partial class LeaguePlayMatchScreen : OsuScreen, IPreviewTrackOwner
    {
        public override string Title => prepared.Source.Name;

        public override bool ShowFooter => false;

        public override bool? ApplyModTrackAdjustments => true;

        public override bool HideOverlaysOnEnter => true;

        public override float BackgroundParallaxAmount => 0;

        private readonly LeaguePlayPreparedMappool prepared;
        private readonly LeaguePlayMatchController controller;
        private readonly Dictionary<string, long> itemIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<long, LeaguePlayPreparedMappoolItem> itemsById = new();
        private readonly MatchmakingStageState stageState = new MatchmakingStageState();
        private readonly int maximumRounds;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private Container selectionArea = null!;
        private Container playerPanelArea = null!;
        private OsuSpriteText phaseText = null!;
        private OsuSpriteText detailText = null!;
        private FillFlowContainer actionButtons = null!;
        private PlayerPanel playerPanel = null!;
        private PlayerPanel opponentPanel = null!;

        private APIUser playerUser = null!;
        private APIUser opponentUser = null!;
        private LeaguePlayLocalRoom localRoom = null!;
        private BeatmapSelectGrid? beatmapGrid;

        private Sample? confirmSample;
        private bool acceptingSelection;
        private bool completedRound;
        private string? completedRoundSlot;
        private long completedPlayerScore;
        private long completedOpponentScore;
        private int roundNumber = 1;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private PreviewTrackManager previewTrackManager { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        protected override BackgroundScreen CreateBackground() => new MatchmakingBackgroundScreen(colourProvider);

        public LeaguePlayMatchScreen(LeaguePlayPreparedMappool prepared)
        {
            this.prepared = prepared;
            controller = new LeaguePlayMatchController(prepared.Source);
            maximumRounds = Math.Max(1, prepared.Source.FirstTo * 2 - 1);

            for (int i = 0; i < prepared.Items.Count; i++)
            {
                long id = i + 1;
                itemIds.Add(prepared.Items[i].Source.Slot, id);
                itemsById.Add(id, prepared.Items[i]);
            }
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            playerUser = api.LocalUser.Value;
            localRoom = new LeaguePlayLocalRoom(playerUser, prepared.Source.Name);
            playerUser = localRoom.Player.User!;
            opponentUser = localRoom.OpponentUser;
            confirmSample = audio.Samples.Get(@"SongSelect/confirm-selection");

            InternalChild = new InverseScalingDrawSizePreservingFillContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Bottom = StageDisplay.HEIGHT },
                        Children = new Drawable[]
                        {
                            selectionArea = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Horizontal = 245, Top = 118, Bottom = 8 },
                            },
                            playerPanelArea = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    playerPanel = createPlayerPanel(localRoom.Player),
                                    opponentPanel = createPlayerPanel(localRoom.Opponent),
                                }
                            },
                            createPhaseHeader(),
                        }
                    },
                    new StageDisplay(maximumRounds, stageState)
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                    },
                }
            };
        }

        private static PlayerPanel createPlayerPanel(osu.Game.Online.Multiplayer.MultiplayerRoomUser user) => new PlayerPanel(user, false)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Scale = new Vector2(0.8f),
        };

        private Drawable createPhaseHeader() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 112,
            Padding = new MarginPadding { Horizontal = 300, Top = 14 },
            Children = new Drawable[]
            {
                phaseText = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Font = OsuFont.TorusAlternate.With(size: 28, weight: FontWeight.Bold),
                },
                detailText = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 35,
                    Font = OsuFont.Default.With(size: 14),
                    Alpha = 0.8f,
                },
                actionButtons = new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 62,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                },
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            showPlayersGrid();
            updateScoreDisplays();
            stageState.Set(MatchmakingStage.WaitingForClientsJoin, roundNumber, TimeSpan.FromSeconds(1.3));
            phaseText.Text = "LEAGUE PLAY";
            detailText.Text = $"{prepared.Source.Name}  •  FT{prepared.Source.FirstTo}";
            Scheduler.AddDelayed(performRoll, 1300);
        }

        private void performRoll()
        {
            actionButtons.Clear();
            clearSelectionArea();
            showPlayersGrid();
            stageState.Set(MatchmakingStage.RoundWarmupTime, roundNumber, TimeSpan.FromSeconds(1));

            int playerRoll = RNG.Next(0, 101);
            int opponentRoll = RNG.Next(0, 101);
            controller.SubmitRolls(playerRoll, opponentRoll);
            phaseText.Text = $"Roll: Player {playerRoll} - {opponentRoll} AI";

            if (controller.Phase == LeaguePlayMatchPhase.Rolling)
            {
                detailText.Text = "Tie. Rolling again...";
                Scheduler.AddDelayed(performRoll, 700);
                return;
            }

            detailText.Text = $"{displayName(controller.RollWinner!.Value)} won the roll and chooses the pick order.";
            Scheduler.AddDelayed(choosePickOrder, 500);
        }

        private void choosePickOrder()
        {
            LeaguePlaySide decider = controller.RollWinner!.Value;

            if (decider == LeaguePlaySide.Opponent)
            {
                LeaguePlaySide choice = RNG.NextBool() ? LeaguePlaySide.Player : LeaguePlaySide.Opponent;
                controller.ChooseFirstPicker(choice);
                detailText.Text = $"AI chose {displayName(choice)} to pick first.";
                Scheduler.AddDelayed(chooseBanOrder, 650);
                return;
            }

            phaseText.Text = "Choose pick order";
            detailText.Text = "You won the roll. Who should pick first?";
            showSideChoice("Pick first", LeaguePlaySide.Player, "AI picks first", LeaguePlaySide.Opponent, side =>
            {
                controller.ChooseFirstPicker(side);
                chooseBanOrder();
            });
        }

        private void chooseBanOrder()
        {
            actionButtons.Clear();

            if (prepared.Source.BansPerSide == 0)
            {
                advanceTurn();
                return;
            }

            LeaguePlaySide decider = opposite(controller.RollWinner!.Value);

            if (decider == LeaguePlaySide.Opponent)
            {
                LeaguePlaySide choice = RNG.NextBool() ? LeaguePlaySide.Player : LeaguePlaySide.Opponent;
                controller.ChooseFirstBanner(choice);
                detailText.Text = $"AI chose {displayName(choice)} to ban first.";
                Scheduler.AddDelayed(advanceTurn, 650);
                return;
            }

            phaseText.Text = "Choose ban order";
            detailText.Text = "You lost the roll, so you decide who bans first.";
            showSideChoice("Ban first", LeaguePlaySide.Player, "AI bans first", LeaguePlaySide.Opponent, side =>
            {
                controller.ChooseFirstBanner(side);
                advanceTurn();
            });
        }

        private void advanceTurn()
        {
            actionButtons.Clear();
            acceptingSelection = false;

            switch (controller.Phase)
            {
                case LeaguePlayMatchPhase.Banning:
                    beginMapSelection(controller.ExpectedBanner!.Value, true);
                    break;

                case LeaguePlayMatchPhase.Picking:
                    beginMapSelection(controller.ExpectedPicker!.Value, false);
                    break;

                case LeaguePlayMatchPhase.AwaitingRoundResult:
                    showGetReady();
                    break;

                case LeaguePlayMatchPhase.Completed:
                    clearSelectionArea();
                    showPlayersGrid();
                    stageState.Set(MatchmakingStage.Ended, Math.Min(roundNumber, maximumRounds));
                    phaseText.Text = controller.Winner == LeaguePlaySide.Player ? "MATCH WON" : "MATCH LOST";
                    detailText.Text = $"Final score: Player {controller.PlayerPoints} - {controller.OpponentPoints} AI";
                    break;
            }
        }

        private void beginMapSelection(LeaguePlaySide side, bool banning)
        {
            stageState.Set(MatchmakingStage.UserBeatmapSelect, Math.Min(roundNumber, maximumRounds), TimeSpan.FromSeconds(20));
            showPlayersSplit();

            phaseText.Text = $"{(banning ? "Ban" : "Pick")} phase - {displayName(side)}";
            detailText.Text = side == LeaguePlaySide.Player
                ? $"Choose an available map to {(banning ? "ban" : "play")}. Hover a card to preview it."
                : $"AI is choosing a map to {(banning ? "ban" : "play")}...";

            LeaguePlayPreparedMappoolItem[] available = prepared.Items
                                                                   .Where(item => item.Source.ModPool != LeaguePlayModPool.Tiebreaker
                                                                                  && !controller.Bans.ContainsKey(item.Source.Slot)
                                                                                  && !controller.Picks.ContainsKey(item.Source.Slot))
                                                                   .ToArray();

            createBeatmapGrid(available, side == LeaguePlaySide.Player ? choosePlayerMap : null);

            if (side == LeaguePlaySide.Player)
            {
                acceptingSelection = true;
                return;
            }

            Scheduler.AddDelayed(() =>
            {
                LeaguePlayPreparedMappoolItem selected = available[RNG.Next(available.Length)];
                resolveMapSelection(selected, LeaguePlaySide.Opponent, banning);
            }, 850);
        }

        private void choosePlayerMap(MultiplayerPlaylistItem selected)
        {
            if (!acceptingSelection || !itemsById.TryGetValue(selected.ID, out LeaguePlayPreparedMappoolItem? item))
                return;

            bool banning = controller.Phase == LeaguePlayMatchPhase.Banning;
            resolveMapSelection(item, LeaguePlaySide.Player, banning);
        }

        private void resolveMapSelection(LeaguePlayPreparedMappoolItem item, LeaguePlaySide side, bool banning)
        {
            acceptingSelection = false;
            long id = itemIds[item.Source.Slot];
            APIUser user = side == LeaguePlaySide.Player ? playerUser : opponentUser;
            beatmapGrid?.SetUserSelection(user, id, true);
            beatmapGrid?.RollAndDisplayFinalBeatmap([id], id, id);
            previewBeatmap(item);
            confirmSample?.Play();

            if (banning)
                controller.Ban(side, item.Source.Slot);
            else
                controller.Pick(side, item.Source.Slot);

            phaseText.Text = $"{displayName(side)} {(banning ? "banned" : "picked")} {item.Source.Slot}";
            detailText.Text = $"{item.Beatmap.Metadata.Artist} - {item.Beatmap.Metadata.Title} [{item.Beatmap.DifficultyName}]";
            Scheduler.AddDelayed(advanceTurn, 2750);
        }

        private void showGetReady()
        {
            LeaguePlayPreparedMappoolItem current = prepared.Items.Single(item => item.Source.Slot == controller.CurrentItem!.Slot);
            stageState.Set(MatchmakingStage.GameplayWarmupTime, Math.Min(roundNumber, maximumRounds));
            showPlayersSplit();
            createBeatmapGrid([current], null);

            long id = itemIds[current.Source.Slot];
            APIUser picker = controller.CurrentPicker == LeaguePlaySide.Opponent ? opponentUser : playerUser;
            beatmapGrid?.SetUserSelection(picker, id, true);
            beatmapGrid?.RollAndDisplayFinalBeatmap([id], id, id);
            previewBeatmap(current);

            phaseText.Text = $"Get ready - {current.Source.Slot}";
            detailText.Text = $"{current.Beatmap.Metadata.Artist} - {current.Beatmap.Metadata.Title} [{current.Beatmap.DifficultyName}]";
            actionButtons.Add(createActionButton($"Play {current.Source.Slot}", startRound, controller.CurrentPicker ?? LeaguePlaySide.Player));
        }

        private void createBeatmapGrid(IEnumerable<LeaguePlayPreparedMappoolItem> items, Action<MultiplayerPlaylistItem>? selectionAction)
        {
            clearSelectionArea();

            beatmapGrid = new BeatmapSelectGrid(item =>
            {
                LeaguePlayPreparedMappoolItem preparedItem = itemsById[item.ID];
                WorkingBeatmap workingBeatmap = beatmapManager.GetWorkingBeatmap(preparedItem.Beatmap);
                Mod[] mods = LeaguePlayModRules.CreateRequiredMods(preparedItem.Beatmap.Ruleset, preparedItem.Source.ModPool);

                return new LeaguePlayBeatmapPanel(item.ID, preparedItem, workingBeatmap, mods, getPoolColour(preparedItem.Source.ModPool))
                {
                    PreviewAction = () => previewBeatmap(preparedItem),
                };
            }, includeRandomPanel: false)
            {
                RelativeSizeAxes = Axes.Both,
            };

            if (selectionAction != null)
                beatmapGrid.ItemSelected += selectionAction;

            selectionArea.Add(beatmapGrid);
            beatmapGrid.AddItems(items.Select(createMatchmakingItem));
        }

        private MatchmakingPlaylistItem createMatchmakingItem(LeaguePlayPreparedMappoolItem item)
        {
            Mod[] mods = LeaguePlayModRules.CreateRequiredMods(item.Beatmap.Ruleset, item.Source.ModPool);
            long id = itemIds[item.Source.Slot];

            return new MatchmakingPlaylistItem(
                new MultiplayerPlaylistItem
                {
                    ID = id,
                    BeatmapID = item.Beatmap.OnlineID,
                    RulesetID = item.Beatmap.Ruleset.OnlineID,
                    StarRating = item.Beatmap.StarRating,
                    RequiredMods = mods.Select(mod => new APIMod(mod)).ToArray(),
                },
                new APIBeatmap
                {
                    OnlineID = item.Beatmap.OnlineID,
                    OnlineBeatmapSetID = item.Beatmap.BeatmapSet?.OnlineID ?? 0,
                    RulesetID = item.Beatmap.Ruleset.OnlineID,
                    StarRating = item.Beatmap.StarRating,
                    DifficultyName = item.Beatmap.DifficultyName,
                },
                mods);
        }

        private void startRound()
        {
            LeaguePlayPreparedMappoolItem current = prepared.Items.Single(item => item.Source.Slot == controller.CurrentItem!.Slot);
            WorkingBeatmap workingBeatmap = beatmapManager.GetWorkingBeatmap(current.Beatmap);
            Beatmap.Value = workingBeatmap;
            Ruleset.Value = current.Beatmap.Ruleset;
            Mods.Value = LeaguePlayModRules.CreateRequiredMods(current.Beatmap.Ruleset, current.Source.ModPool);

            Mod leagueAutoplay = LeaguePlayModRules.CreateLeagueAutoplay(current.Beatmap.Ruleset);
            localRoom.PrepareRound(current.Beatmap, Mods.Value, leagueAutoplay);
            actionButtons.Clear();
            detailText.Text = "Opening local multiplayer room...";
            stageState.Set(MatchmakingStage.Gameplay, Math.Min(roundNumber, maximumRounds));

            this.Push(new PlayerLoader(() => new LeaguePlayPlayer(localRoom, workingBeatmap, Mods.Value, leagueAutoplay, onRoundCompleted)));
        }

        private void onRoundCompleted(ScoreInfo playerScore, ScoreInfo opponentScore)
        {
            completedRoundSlot = controller.CurrentItem!.Slot;
            completedPlayerScore = playerScore.TotalScore;
            completedOpponentScore = opponentScore.TotalScore;
            controller.SubmitRoundScores(playerScore.TotalScore, opponentScore.TotalScore);
            completedRound = true;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            beginHandlingTrack();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            endHandlingTrack();
            base.OnSuspending(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            beginHandlingTrack();

            if (!completedRound)
                return;

            completedRound = false;
            clearSelectionArea();
            showPlayersGrid();
            updateScoreDisplays();
            stageState.Set(MatchmakingStage.ResultsDisplaying, Math.Min(roundNumber, maximumRounds), TimeSpan.FromSeconds(1.6));

            bool tie = completedPlayerScore == completedOpponentScore;
            string winner = tie ? "Tie - replay required" : completedPlayerScore > completedOpponentScore ? "Player wins the point" : "AI wins the point";
            phaseText.Text = $"{completedRoundSlot} - {winner}";
            detailText.Text = $"Player {completedPlayerScore:N0} - {completedOpponentScore:N0} AI";

            if (!tie)
                roundNumber = Math.Min(roundNumber + 1, maximumRounds);

            Scheduler.AddDelayed(advanceTurn, 1600);
        }

        private void showPlayersGrid()
        {
            playerPanel.DisplayMode = PlayerPanelDisplayMode.Vertical;
            opponentPanel.DisplayMode = PlayerPanelDisplayMode.Vertical;
            playerPanel.ScaleTo(0.8f, 500, Easing.OutPow10).MoveTo(new Vector2(-90, 15), 500, Easing.OutPow10).FadeIn(200);
            opponentPanel.ScaleTo(0.8f, 500, Easing.OutPow10).MoveTo(new Vector2(90, 15), 500, Easing.OutPow10).FadeIn(200);
        }

        private void showPlayersSplit()
        {
            playerPanel.DisplayMode = PlayerPanelDisplayMode.Horizontal;
            opponentPanel.DisplayMode = PlayerPanelDisplayMode.Horizontal;
            playerPanel.ScaleTo(0.68f, 500, Easing.OutPow10).MoveTo(new Vector2(-480, 10), 500, Easing.OutPow10).FadeIn(200);
            opponentPanel.ScaleTo(0.68f, 500, Easing.OutPow10).MoveTo(new Vector2(480, 10), 500, Easing.OutPow10).FadeIn(200);
        }

        private void updateScoreDisplays()
        {
            int playerPlacement = controller.PlayerPoints >= controller.OpponentPoints ? 1 : 2;
            int opponentPlacement = controller.OpponentPoints > controller.PlayerPoints ? 1 : 2;
            playerPanel.SetPlacementAndPoints(playerPlacement, controller.PlayerPoints);
            opponentPanel.SetPlacementAndPoints(opponentPlacement, controller.OpponentPoints);
        }

        private void previewBeatmap(LeaguePlayPreparedMappoolItem item)
        {
            Beatmap.Value = beatmapManager.GetWorkingBeatmap(item.Beatmap);
            Ruleset.Value = item.Beatmap.Ruleset;
            Mods.Value = LeaguePlayModRules.CreateRequiredMods(item.Beatmap.Ruleset, item.Source.ModPool);
        }

        private void beginHandlingTrack()
        {
            Beatmap.BindValueChanged(applyLoopingToTrack, true);
        }

        private void endHandlingTrack()
        {
            Beatmap.ValueChanged -= applyLoopingToTrack;
            Beatmap.Value.Track.Looping = false;
            previewTrackManager.StopAnyPlaying(this);
        }

        private void applyLoopingToTrack(ValueChangedEvent<WorkingBeatmap> beatmap)
        {
            if (!this.IsCurrentScreen())
                return;

            beatmap.NewValue.PrepareTrackForPreview(true);
            music.EnsurePlayingSomething();
        }

        private void clearSelectionArea()
        {
            beatmapGrid = null;
            selectionArea.Clear();
        }

        private void showSideChoice(string playerText, LeaguePlaySide playerChoice, string opponentText, LeaguePlaySide opponentChoice, Action<LeaguePlaySide> action)
        {
            actionButtons.Clear();
            actionButtons.AddRange(new Drawable[]
            {
                createActionButton(playerText, () => action(playerChoice), LeaguePlaySide.Player),
                createActionButton(opponentText, () => action(opponentChoice), LeaguePlaySide.Opponent),
            });
        }

        private static RoundedButton createActionButton(string text, Action action, LeaguePlaySide side) => new RoundedButton
        {
            Size = new Vector2(180, 42),
            Text = text,
            BackgroundColour = side == LeaguePlaySide.Player ? Color4Extensions.FromHex("4B8CFF") : Color4Extensions.FromHex("E65A7A"),
            Action = action,
        };

        private static Color4 getPoolColour(LeaguePlayModPool pool) => pool switch
        {
            LeaguePlayModPool.NoMod => Color4Extensions.FromHex("5EBFFF"),
            LeaguePlayModPool.Hidden => Color4Extensions.FromHex("B28DFF"),
            LeaguePlayModPool.HardRock => Color4Extensions.FromHex("FF8198"),
            LeaguePlayModPool.DoubleTime => Color4Extensions.FromHex("FFB45E"),
            LeaguePlayModPool.FreeMod => Color4Extensions.FromHex("70E1B2"),
            LeaguePlayModPool.Tiebreaker => Color4Extensions.FromHex("FFE36E"),
            _ => Color4.Gray,
        };

        private static LeaguePlaySide opposite(LeaguePlaySide side)
            => side == LeaguePlaySide.Player ? LeaguePlaySide.Opponent : LeaguePlaySide.Player;

        private static string displayName(LeaguePlaySide side)
            => side == LeaguePlaySide.Player ? "Player" : "AI";

        protected override void Dispose(bool isDisposing)
        {
            if (IsLoaded)
            {
                Beatmap.ValueChanged -= applyLoopingToTrack;
                Beatmap.Value.Track.Looping = false;
            }

            base.Dispose(isDisposing);
        }
    }
}
