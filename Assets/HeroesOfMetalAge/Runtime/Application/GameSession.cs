using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Application
{
    /// <summary>
    /// Single authority for campaign transitions and commands; the rules themselves live in the Domain. Views call these
    /// commands and redraw when <see cref="Revision"/> changes; audio listens to the typed events.
    /// </summary>
    public sealed class GameSession
    {
        private const int MAX_BATTLE_LOG_LINES = 8;
        private const int DEBUG_GOLD_AMOUNT = 2000;

        private readonly ISaveStore save;
        private readonly IRandomSource random;
        private readonly Action<Exception> reportError;
        private readonly BattleAi ai = new BattleAi();
        private readonly float travelStepSeconds;
        private readonly float enemyTurnSeconds;
        private readonly float battleActionSeconds;
        private readonly float battleResultSeconds;
        private readonly List<WorldPosition> route = new List<WorldPosition>();
        private readonly List<string> battleLog = new List<string>();
        private int routeIndex;
        // a session-wide streak (kept across new missions and loads, never saved) so the legendary guarantee is reachable
        // within the short first mission while it is being tested
        private int victoriesWithoutLegendary;
        private float timer;
        private bool isTraveling;
        private WorldPosition? plannedDestination;

        /// <param name="battleActionSeconds">Pause after every battle action before the next one (either army) can act.</param>
        /// <param name="battleResultSeconds">Pause on the battlefield after the last blow before the victory or defeat screen opens.</param>
        /// <param name="reportError">Receives unexpected failures (listener exceptions, IO); gameplay continues.</param>
        public GameSession(DefinitionCatalog catalog, ISaveStore save, IRandomSource random, float travelStepSeconds, float enemyTurnSeconds,
            float battleActionSeconds, float battleResultSeconds, Action<Exception> reportError)
        {
            Catalog = catalog;
            this.save = save;
            this.random = random;
            this.reportError = reportError;
            this.travelStepSeconds = travelStepSeconds;
            this.enemyTurnSeconds = enemyTurnSeconds;
            this.battleActionSeconds = battleActionSeconds;
            this.battleResultSeconds = battleResultSeconds;
            Rules = new CampaignRules(catalog);
            World = new WorldMap(catalog.Mission);
            Campaign = Rules.NewCampaign();
            Route = route.AsReadOnly();
            BattleLog = battleLog.AsReadOnly();
        }

        public event Action<SoundCue> CueRequested;
        public event Action<string> UnitVoiceRequested;
        public event Action<PlaybackState> PlaybackChanged;
        public event Action<DamageEvent> DamageResolved;
        /// <summary>
        /// The hero reached a site from the map: a friendly or hostile encounter (raised after its dialog opened) or the
        /// town (after its screen opened). Not raised for development shortcuts.
        /// </summary>
        public event Action<EncounterDefinition> SiteReached;

        public DefinitionCatalog Catalog { get; }
        public CampaignRules Rules { get; }
        public WorldMap World { get; }
        public CampaignState Campaign { get; private set; }
        public BattleEngine Battle { get; private set; }
        public EncounterDefinition Encounter { get; private set; }
        public VictorySummary Victory { get; private set; }
        public GameScreen Screen { get; private set; } = GameScreen.Adventure;
        public DialogKind Dialog { get; private set; } = DialogKind.Intro;
        public IReadOnlyList<WorldPosition> Route { get; }
        public IReadOnlyList<string> BattleLog { get; }
        public string Notice { get; private set; } = string.Empty;
        public int NoticeRevision { get; private set; }
        /// <summary>Increases whenever anything visible may have changed.</summary>
        public long Revision { get; private set; }
        public int DamageRevision { get; private set; }
        public DamageEvent LastHit { get; private set; }
        /// <summary>Seconds the hero takes for one map step while riding.</summary>
        public float TravelStepSeconds => travelStepSeconds;
        /// <summary>What the music should follow now; <see cref="PlaybackChanged"/> reports every change.</summary>
        public PlaybackState Playback { get; private set; }
        public bool HasSave => save.Exists;
        public string SaveLocation => save.Location;

        /// <summary>
        /// True while the hero travels, the enemy is taking its turn, the pause after a battle action runs or a decided battle
        /// waits for its result screen; player commands wait.
        /// </summary>
        public bool IsBusy => isTraveling || IsEnemyTurn || IsBattlePaused || IsBattleResultPending;

        private bool IsEnemyTurn => Screen == GameScreen.Battle && Dialog == DialogKind.None && Battle != null && Battle.Active != null && !Battle.Active.IsPlayer;
        private bool IsBattlePaused => Screen == GameScreen.Battle && Dialog == DialogKind.None && timer > 0;
        // the battle is decided (and a victory already applied) but the battlefield stays on screen for the result delay
        private bool IsBattleResultPending => Screen == GameScreen.Battle && Dialog == DialogKind.None && Battle != null && Battle.Outcome != BattleOutcome.InProgress;
        private bool CanRunCampaignCommand => !IsBusy && Screen != GameScreen.Battle && Dialog == DialogKind.None;

        public void Notify(string message)
        {
            Revision++;
            Notice = message;
            NoticeRevision++;
        }

        /// <summary>Advances travel and enemy turns; driven by the presentation with frame time.</summary>
        public void Tick(float deltaTime)
        {
            if (Dialog != DialogKind.None)
            {
                return;
            }

            float previous = timer;
            timer -= Math.Max(0, deltaTime);
            if (timer > 0)
            {
                return;
            }

            // the pause after a battle action ended: the player's stack can act again, so views must redraw
            if (previous > 0 && Screen == GameScreen.Battle)
            {
                Revision++;
            }

            if (isTraveling)
            {
                TravelStep();
            }
            else if (IsBattleResultPending)
            {
                ShowBattleResult();
            }
            else if (Screen == GameScreen.Battle && Battle != null && Battle.Outcome == BattleOutcome.InProgress && !Battle.Active.IsPlayer)
            {
                ResolveBattleAction(ai.Choose(Battle), false);
            }
        }

        #region NAVIGATION
        public void StartMission()
        {
            Revision++;
            if (Dialog == DialogKind.Intro)
            {
                Dialog = DialogKind.None;
                Cue(SoundCue.Friendly);
            }
        }

        public void ShowHero()
        {
            Revision++;
            if (!CanRunCampaignCommand)
            {
                return;
            }

            Screen = Screen == GameScreen.Hero ? HomeScreen() : GameScreen.Hero;
            RefreshPlayback();
        }

        /// <summary>Closes the current dialog or overlay screen, one level at a time.</summary>
        public void Back()
        {
            Revision++;
            if (IsBusy)
            {
                return;
            }

            if (Dialog == DialogKind.Friendly || Dialog == DialogKind.Hostile || Dialog == DialogKind.Claim)
            {
                Dialog = DialogKind.None;
            }
            else if (Screen == GameScreen.Hero)
            {
                Screen = HomeScreen();
                RefreshPlayback();
            }
            else if (Screen == GameScreen.Town)
            {
                LeaveTown();
            }
        }

        public void LeaveTown()
        {
            Revision++;
            if (!CanRunCampaignCommand)
            {
                return;
            }

            if (Campaign.IsHeroHome)
            {
                Notify("Move the home hero to visiting before riding.");
                return;
            }

            Screen = GameScreen.Adventure;
            RefreshPlayback();
        }
        #endregion

        #region TRAVEL
        /// <summary>First click plans a route; clicking the same destination again starts the ride.</summary>
        public void Plan(WorldPosition target)
        {
            Revision++;
            if (!CanRunCampaignCommand || Screen != GameScreen.Adventure)
            {
                return;
            }

            if (target == Campaign.Position)
            {
                EncounterDefinition site = World.Site(Campaign, target);
                if (site != null && site.Kind == EncounterKind.Town)
                {
                    OpenTown();
                    Publish(SiteReached, site);
                }

                return;
            }

            if (plannedDestination.HasValue && plannedDestination.Value == target && route.Count > 0)
            {
                Travel();
                return;
            }

            plannedDestination = target;
            route.Clear();
            route.AddRange(World.FindRoute(Campaign, target));
            Cue(SoundCue.Click);
            if (route.Count == 0)
            {
                Notify("No route. Mountains, water and unresolved encounters block travel.");
            }
        }

        public void Travel()
        {
            Revision++;
            if (!CanRunCampaignCommand || Screen != GameScreen.Adventure || route.Count == 0)
            {
                return;
            }

            if (Campaign.IsHeroHome || Campaign.Army.Count == 0)
            {
                Notify("Your hero needs a visiting army before riding.");
                return;
            }

            if (Campaign.Movement < World.StepCost(Campaign.Position, route[0]))
            {
                Notify("Not enough movement. End the day [E].");
                return;
            }

            isTraveling = true;
            routeIndex = 0;
            timer = 0;
            RefreshPlayback();
        }

        public void EndDay()
        {
            Revision++;
            if (!CanRunCampaignCommand || !Accept(Rules.EndDay(Campaign, out bool isNewWeek)))
            {
                return;
            }

            route.Clear();
            plannedDestination = null;
            Notify(isNewWeek ? "A new week begins. Town recruitment stocks have grown." : "Day " + Campaign.Day + ". Your movement is restored.");
            Cue(isNewWeek ? SoundCue.Friendly : SoundCue.Click);
            Save(false);
        }

        private void TravelStep()
        {
            if (routeIndex >= route.Count)
            {
                FinishTravel();
                return;
            }

            WorldPosition step = route[routeIndex];
            EncounterDefinition encounter = World.Site(Campaign, step);
            if (Campaign.Movement < World.StepCost(Campaign.Position, step))
            {
                Notify("Movement spent. End the day to continue.");
                FinishTravel();
                return;
            }

            // the ride stops in front of an unresolved site and opens its dialog
            if (encounter != null && encounter.Kind != EncounterKind.Town)
            {
                Encounter = encounter;
                Dialog = encounter.Kind == EncounterKind.Friendly ? DialogKind.Friendly : DialogKind.Hostile;
                Cue(encounter.Kind == EncounterKind.Friendly ? SoundCue.FriendlyEncounter : SoundCue.EnemyEncounter);
                FinishTravel();
                Publish(SiteReached, encounter);
                return;
            }

            if (!Accept(Rules.Move(Campaign, step, World)))
            {
                FinishTravel();
                return;
            }

            routeIndex++;
            timer = travelStepSeconds;
            if (encounter != null)
            {
                FinishTravel();
                OpenTown();
                Publish(SiteReached, encounter);
            }
        }

        private void FinishTravel()
        {
            isTraveling = false;
            route.Clear();
            plannedDestination = null;
            RefreshPlayback();
            Save(false);
        }
        #endregion

        #region TOWN AND HERO
        public bool Buy(string unitId, int count)
        {
            Revision++;
            if (!CanRunCampaignCommand || Screen != GameScreen.Town || !Accept(Rules.Buy(Campaign, unitId, count)))
            {
                return false;
            }

            UnitDefinition unit = Catalog.Unit(unitId);
            Cue(SoundCue.Gold);
            Voice(unit.Id);
            Notify(count + " " + unit.Name + " join your warhost.");
            Save(false);
            return true;
        }

        public void Transfer(bool isFromGarrison, int index)
        {
            Revision++;
            if (CanRunCampaignCommand && Screen == GameScreen.Town && Accept(Rules.Transfer(Campaign, isFromGarrison, index)))
            {
                Cue(SoundCue.Click);
                Save(false);
            }
        }

        public void StationHero()
        {
            Revision++;
            if (CanRunCampaignCommand && Screen == GameScreen.Town && Accept(Rules.StationHero(Campaign)))
            {
                Cue(SoundCue.Click);
                Save(false);
            }
        }

        public void Equip(int inventoryIndex)
        {
            Revision++;
            if (CanRunCampaignCommand && Accept(Rules.Equip(Campaign, inventoryIndex)))
            {
                Cue(SoundCue.Equip);
                Save(false);
            }
        }

        public void Unequip(EquipmentSlot slot)
        {
            Revision++;
            if (CanRunCampaignCommand && Accept(Rules.Unequip(Campaign, slot)))
            {
                Cue(SoundCue.Equip);
                Save(false);
            }
        }

        public void ClaimTown()
        {
            Revision++;
            if (!CanRunCampaignCommand || Screen != GameScreen.Town || Campaign.IsTownOwned)
            {
                return;
            }

            Encounter = Catalog.Mission.Encounter(EncounterKind.Town);
            Dialog = DialogKind.Claim;
        }
        #endregion

        #region ENCOUNTERS AND BATTLE
        public void AcceptFriendly()
        {
            Revision++;
            if (Dialog != DialogKind.Friendly || Encounter == null || !Accept(Rules.Join(Campaign, Encounter)))
            {
                return;
            }

            Dialog = DialogKind.None;
            foreach (Troop troop in Encounter.Army)
            {
                Voice(troop.UnitId);
            }

            Save(false);
        }

        /// <summary>Starts (or after a defeat, retries) the battle with the current pre-battle army.</summary>
        public void FightEncounter()
        {
            Revision++;
            if (Dialog != DialogKind.Hostile && Dialog != DialogKind.Claim && Dialog != DialogKind.Defeat)
            {
                return;
            }

            if (Encounter == null || Campaign.Army.Count == 0 || Campaign.IsHeroHome || Campaign.IsCompleted(Encounter.Id))
            {
                Notify("This army cannot begin that battle.");
                return;
            }

            Battle = new BattleEngine(Catalog, random, Campaign, Encounter);
            Victory = null;
            battleLog.Clear();
            Screen = GameScreen.Battle;
            Dialog = DialogKind.None;
            // the player may act at once; an enemy that starts waits for its turn delay
            timer = Battle.Active.IsPlayer ? 0 : enemyTurnSeconds;
            RefreshPlayback();
            CueActiveVoice();
        }

        public void BattleAction(BattleAction action)
        {
            Revision++;
            if (Screen == GameScreen.Battle && Dialog == DialogKind.None && !IsBusy)
            {
                ResolveBattleAction(action, true);
            }
        }

        /// <summary>Moves from the victory screen to the rewards screen.</summary>
        public void ContinueVictory()
        {
            Revision++;
            if (Dialog != DialogKind.Victory)
            {
                return;
            }

            Dialog = DialogKind.Rewards;
            Cue(SoundCue.Gold);
            if (Victory.HasLegendary)
            {
                Cue(SoundCue.LegendaryItem);
            }
        }

        /// <summary>Closes the rewards screen; the rewards were already applied when the battle was won.</summary>
        public void ContinueRewards()
        {
            Revision++;
            if (Dialog != DialogKind.Rewards)
            {
                return;
            }

            Dialog = DialogKind.None;
            if (Victory.HasLeveledUp)
            {
                Cue(SoundCue.LevelUp);
            }
        }

        public void PrepareAfterDefeat()
        {
            Revision++;
            if (Dialog != DialogKind.Defeat)
            {
                return;
            }

            Dialog = DialogKind.None;
            Screen = Encounter.Kind == EncounterKind.Town ? GameScreen.Town : GameScreen.Adventure;
            Battle = null;
            RefreshPlayback();
        }

        private void ResolveBattleAction(BattleAction action, bool isPlayer)
        {
            BattleResolution resolution = Battle.Execute(action, isPlayer);
            if (!Accept(resolution.Result))
            {
                return;
            }

            if (!string.IsNullOrEmpty(resolution.Message))
            {
                AddLog(resolution.Message);
            }

            foreach (DamageEvent hit in resolution.Hits)
            {
                string verb = hit.IsRetaliation ? " retaliates" : hit.IsRanged ? " shoots" : " strikes";
                string result = hit.IsMiss ? "miss." : hit.Damage + " damage / " + hit.Casualties + " lost.";
                AddLog(Catalog.Unit(hit.AttackerId).Name + verb + ": " + result);
                LastHit = hit;
                DamageRevision++;
                Publish(DamageResolved, hit);
            }

            if (Battle.Outcome == BattleOutcome.Victory)
            {
                ApplyVictory();
            }

            // a decided battle opens its result screen after the delay (see Tick)
            timer = Battle.Outcome == BattleOutcome.InProgress
                ? Math.Max(battleActionSeconds, Battle.Active.IsPlayer ? 0 : enemyTurnSeconds)
                : battleResultSeconds;
            if (Battle.Outcome != BattleOutcome.InProgress)
            {
                return;
            }

            CueActiveVoice();
        }

        private void ApplyVictory()
        {
            int oldLevel = Rules.Level(Campaign);
            VictoryRewards rewards = Rules.RollRewards(Campaign, Encounter, random, victoriesWithoutLegendary);
            if (!Accept(Rules.ApplyVictory(Campaign, Encounter, Battle.Survivors(), rewards)))
            {
                return;
            }

            victoriesWithoutLegendary = rewards.HasLegendary ? 0 : victoriesWithoutLegendary + 1;
            Victory = new VictorySummary(Encounter, rewards, Battle.Losses, Rules.Level(Campaign) > oldLevel);
        }

        /// <summary>Leaves the decided battlefield for the victory or defeat screen.</summary>
        private void ShowBattleResult()
        {
            Revision++;
            if (Battle.Outcome == BattleOutcome.Defeat)
            {
                Dialog = DialogKind.Defeat;
                RefreshPlayback();
                return;
            }

            Screen = Encounter.Kind == EncounterKind.Town ? GameScreen.Town : GameScreen.Adventure;
            // a rejected victory was already reported by Accept; leave the battle without a result screen
            if (Victory == null)
            {
                RefreshPlayback();
                return;
            }

            Dialog = DialogKind.Victory;
            // the stinger comes first so the audio knows the victory music follows instead of the map or town music
            Cue(SoundCue.Victory);
            RefreshPlayback();
            Save(false);
        }

        private void CueActiveVoice()
        {
            if (Battle != null && Battle.Active != null)
            {
                Voice(Battle.Active.UnitId);
            }
        }

        private void AddLog(string message)
        {
            battleLog.Add(message);
            if (battleLog.Count > MAX_BATTLE_LOG_LINES)
            {
                battleLog.RemoveAt(0);
            }
        }
        #endregion

        #region PERSISTENCE
        /// <param name="isNotifying">Show a notice about the outcome (manual saves); autosaves stay silent.</param>
        public void Save(bool isNotifying = true)
        {
            Revision++;
            if (isTraveling || Screen == GameScreen.Battle || Dialog == DialogKind.Defeat)
            {
                if (isNotifying)
                {
                    Notify("Save is available outside battles and movement.");
                }

                return;
            }

            try
            {
                save.Write(Campaign.Snapshot());
                if (isNotifying)
                {
                    Notify("Campaign saved.");
                }
            }
            catch (Exception error)
            {
                reportError(error);
                Notify("Save failed. The last valid save is preserved.");
            }
        }

        /// <summary>Replaces the campaign only after the save validated completely.</summary>
        public void Load()
        {
            Revision++;
            if (IsBusy || Screen == GameScreen.Battle)
            {
                return;
            }

            try
            {
                CampaignState loaded = Rules.Restore(save.Read());
                Campaign = loaded;
                ResetTransientState();
                Screen = HomeScreen();
                Dialog = DialogKind.None;
                RefreshPlayback();
                Notify("Campaign loaded.");
            }
            catch (Exception error)
            {
                reportError(error);
                Notify("No valid compatible saved campaign found.");
            }
        }

        public void Restart()
        {
            Revision++;
            isTraveling = false;
            timer = 0;
            ResetTransientState();
            Campaign = Rules.NewCampaign();
            Screen = GameScreen.Adventure;
            Dialog = DialogKind.Intro;
            RefreshPlayback();
        }
        #endregion

        #region DEVELOPMENT
        public void DebugTown()
        {
            Revision++;
            if (IsBusy || Screen == GameScreen.Battle)
            {
                return;
            }

            Rules.DebugPrepareTown(Campaign);
            route.Clear();
            plannedDestination = null;
            Dialog = DialogKind.None;
            OpenTown();
        }

        public void DebugGold()
        {
            Revision++;
            if (CanRunCampaignCommand)
            {
                Rules.DebugGold(Campaign, DEBUG_GOLD_AMOUNT);
            }
        }

        public void DebugArtefacts()
        {
            Revision++;
            if (CanRunCampaignCommand)
            {
                Rules.DebugArtefacts(Campaign);
            }
        }
        #endregion

        private GameScreen HomeScreen()
        {
            return Campaign.IsHeroHome ? GameScreen.Town : GameScreen.Adventure;
        }

        private void OpenTown()
        {
            Screen = GameScreen.Town;
            Dialog = DialogKind.None;
            RefreshPlayback();
            Cue(SoundCue.Friendly);
        }

        private void ResetTransientState()
        {
            route.Clear();
            plannedDestination = null;
            Battle = null;
            Victory = null;
            Encounter = null;
        }

        private bool Accept(CommandResult result)
        {
            if (!result.IsSuccess)
            {
                Notify(result.Error);
                return false;
            }

            Revision++;
            return true;
        }

        private void Cue(SoundCue cue)
        {
            Publish(CueRequested, cue);
        }

        private void Voice(string unitId)
        {
            Publish(UnitVoiceRequested, unitId);
        }

        /// <summary>Publishes the playback state derived from travel, screen and dialog when it changed.</summary>
        private void RefreshPlayback()
        {
            Revision++;
            // the hero screen belongs to where the hero stands, so opening it keeps the town or map music
            bool isInTown = Screen == GameScreen.Town || (Screen == GameScreen.Hero && HomeScreen() == GameScreen.Town);
            PlaybackState state = new PlaybackState(isTraveling, Screen == GameScreen.Battle && Dialog == DialogKind.None, Dialog == DialogKind.Defeat,
                isInTown);
            if (state.Equals(Playback))
            {
                return;
            }

            Playback = state;
            Publish(PlaybackChanged, state);
        }

        // a failing listener must not stop the others or undo an already committed command
        private void Publish<T>(Action<T> signal, T value)
        {
            if (signal == null)
            {
                return;
            }

            foreach (Delegate subscriber in signal.GetInvocationList())
            {
                try
                {
                    ((Action<T>)subscriber)(value);
                }
                catch (Exception error)
                {
                    reportError(error);
                }
            }
        }
    }
}
