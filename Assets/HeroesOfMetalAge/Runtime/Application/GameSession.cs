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
        private readonly List<WorldPosition> route = new List<WorldPosition>();
        private readonly List<string> battleLog = new List<string>();
        private int routeIndex;
        private float timer;
        private bool isTraveling;
        private WorldPosition? plannedDestination;

        /// <param name="reportError">Receives unexpected failures (listener exceptions, IO); gameplay continues.</param>
        public GameSession(DefinitionCatalog catalog, ISaveStore save, IRandomSource random, float travelStepSeconds, float enemyTurnSeconds,
            Action<Exception> reportError)
        {
            Catalog = catalog;
            this.save = save;
            this.random = random;
            this.reportError = reportError;
            this.travelStepSeconds = travelStepSeconds;
            this.enemyTurnSeconds = enemyTurnSeconds;
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
        public bool HasSave => save.Exists;
        public string SaveLocation => save.Location;

        /// <summary>True while the hero travels or the enemy is taking its turn; player commands wait.</summary>
        public bool IsBusy => isTraveling || IsEnemyTurn;

        private bool IsEnemyTurn => Screen == GameScreen.Battle && Dialog == DialogKind.None && Battle != null && Battle.Active != null && !Battle.Active.IsPlayer;
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

            timer -= Math.Max(0, deltaTime);
            if (timer > 0)
            {
                return;
            }

            if (isTraveling)
            {
                TravelStep();
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
            PublishPlayback(true, false);
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
                Cue(encounter.Kind == EncounterKind.Friendly ? SoundCue.Friendly : SoundCue.Enemy);
                FinishTravel();
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
            }
        }

        private void FinishTravel()
        {
            isTraveling = false;
            route.Clear();
            plannedDestination = null;
            PublishPlayback(false, false);
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
            timer = enemyTurnSeconds;
            PublishPlayback(false, true);
            Cue(SoundCue.BattleIntro);
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

        public void ContinueVictory()
        {
            Revision++;
            if (Dialog != DialogKind.Victory)
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
                string verb = hit.IsRetaliation ? " retaliates" : " strikes";
                AddLog(Catalog.Unit(hit.AttackerId).Name + verb + ": " + hit.Damage + " damage / " + hit.Casualties + " lost.");
                LastHit = hit;
                DamageRevision++;
                Publish(DamageResolved, hit);
            }

            if (Battle.Outcome == BattleOutcome.Defeat)
            {
                Dialog = DialogKind.Defeat;
                PublishPlayback(false, false);
                Cue(SoundCue.Defeat);
                return;
            }

            if (Battle.Outcome == BattleOutcome.Victory)
            {
                ApplyVictory();
                return;
            }

            timer = enemyTurnSeconds;
            CueActiveVoice();
        }

        private void ApplyVictory()
        {
            int oldLevel = Rules.Level(Campaign);
            if (!Accept(Rules.ApplyVictory(Campaign, Encounter, Battle.Survivors())))
            {
                return;
            }

            Victory = new VictorySummary(Encounter, Battle.Losses, Rules.Level(Campaign) > oldLevel);
            Screen = Encounter.Kind == EncounterKind.Town ? GameScreen.Town : GameScreen.Adventure;
            Dialog = DialogKind.Victory;
            PublishPlayback(false, false);
            Cue(SoundCue.Victory);
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
                PublishPlayback(false, false);
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
            PublishPlayback(false, false);
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

        private void PublishPlayback(bool isRiding, bool isFighting)
        {
            Revision++;
            Publish(PlaybackChanged, new PlaybackState(isRiding, isFighting));
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
