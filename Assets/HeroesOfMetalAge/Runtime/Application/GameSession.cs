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
        // speed mode: the hero rides a cell in a fiftieth of a second, so he is at his destination almost at once
        private const float SPEED_MODE_STEP_SECONDS = 0.02f;

        private readonly ISaveStore save;
        private readonly IRandomSource random;
        private readonly Action<Exception> reportError;
        private readonly BattleAi ai = new BattleAi();
        private readonly float travelStepSeconds;
        private readonly float enemyTurnSeconds;
        private readonly float battleActionSeconds;
        private readonly float battleResultSeconds;
        private readonly float endDaySeconds;
        // the steps still ahead of the hero; a step leaves the list when he enters its cell
        private readonly List<WorldPosition> route = new List<WorldPosition>();
        private readonly List<string> battleLog = new List<string>();
        // a session-wide streak (kept across new missions and loads, never saved) so the legendary guarantee is reachable
        // within the short first mission while it is being tested
        private int victoriesWithoutLegendary;
        private float timer;
        private float endDayTimer;
        private bool isTraveling;
        // the player picked a new destination during the ride: the hero stops when he has entered the cell he is riding into
        private bool isStopRequested;
        private bool isEndingDay;
        private WorldPosition? plannedDestination;

        /// <param name="battleActionSeconds">Pause after every battle action before the next one (either army) can act.</param>
        /// <param name="battleResultSeconds">Pause on the battlefield after the last blow before the victory or defeat screen opens.</param>
        /// <param name="endDaySeconds">Length of the end-of-day sequence; the new day begins when it ends.</param>
        /// <param name="reportError">Receives unexpected failures (listener exceptions, IO); gameplay continues.</param>
        public GameSession(DefinitionCatalog catalog, ISaveStore save, IRandomSource random, float travelStepSeconds, float enemyTurnSeconds,
            float battleActionSeconds, float battleResultSeconds, float endDaySeconds, Action<Exception> reportError)
        {
            Catalog = catalog;
            this.save = save;
            this.random = random;
            this.reportError = reportError;
            this.travelStepSeconds = travelStepSeconds;
            this.enemyTurnSeconds = enemyTurnSeconds;
            this.battleActionSeconds = battleActionSeconds;
            this.battleResultSeconds = battleResultSeconds;
            this.endDaySeconds = endDaySeconds;
            Rules = new CampaignRules(catalog);
            Objects = new MapObjectRules(catalog, Rules);
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
        /// <summary>The hero entered (or, for a closed gate, reached) a map object, after its effect or dialog.</summary>
        public event Action<MapObjectDefinition> ObjectReached;
        /// <summary>The player finished the mission at the exit portal.</summary>
        public event Action MissionFinished;

        public DefinitionCatalog Catalog { get; }
        public CampaignRules Rules { get; }
        public MapObjectRules Objects { get; }
        public WorldMap World { get; }
        public CampaignState Campaign { get; private set; }
        public BattleEngine Battle { get; private set; }
        public EncounterDefinition Encounter { get; private set; }
        /// <summary>The map object whose dialog (chest, dwelling, exit) is open, or the last one visited.</summary>
        public MapObjectDefinition MapObject { get; private set; }
        public VictorySummary Victory { get; private set; }
        public GameScreen Screen { get; private set; } = GameScreen.Adventure;
        public DialogKind Dialog { get; private set; } = DialogKind.Intro;
        /// <summary>The planned or ridden route without the cells the hero has already entered.</summary>
        public IReadOnlyList<WorldPosition> Route { get; }
        public IReadOnlyList<string> BattleLog { get; }
        public string Notice { get; private set; } = string.Empty;
        public int NoticeRevision { get; private set; }
        /// <summary>Increases whenever anything visible may have changed.</summary>
        public long Revision { get; private set; }
        public int DamageRevision { get; private set; }
        public DamageEvent LastHit { get; private set; }
        /// <summary>Seconds the hero takes for one map step while riding.</summary>
        public float TravelStepSeconds => IsSpeedMode ? Math.Min(travelStepSeconds, SPEED_MODE_STEP_SECONDS) : travelStepSeconds;
        /// <summary>Development aid (debug panel): the hero rides almost instantly. Gameplay rules are unchanged.</summary>
        public bool IsSpeedMode { get; set; }
        public bool IsTraveling => isTraveling;
        /// <summary>True while the end-of-day sequence runs; the day changes when it ends.</summary>
        public bool IsEndingDay => isEndingDay;
        /// <summary>0 when the end-of-day sequence starts, 1 when the new day begins.</summary>
        public float EndDayProgress => isEndingDay && endDaySeconds > 0f ? 1f - Math.Max(0f, endDayTimer) / endDaySeconds : 1f;
        /// <summary>What the music should follow now; <see cref="PlaybackChanged"/> reports every change.</summary>
        public PlaybackState Playback { get; private set; }
        public bool HasSave => save.Exists;
        public string SaveLocation => save.Location;

        /// <summary>
        /// True while the hero travels, the day is ending, the enemy is taking its turn, the pause after a battle action runs
        /// or a decided battle waits for its result screen; player commands wait.
        /// </summary>
        public bool IsBusy => isTraveling || isEndingDay || IsEnemyTurn || IsBattlePaused || IsBattleResultPending;

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

            if (isEndingDay)
            {
                endDayTimer -= Math.Max(0, deltaTime);
                if (endDayTimer <= 0)
                {
                    BeginNewDay();
                }

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

            if (Dialog == DialogKind.Friendly || Dialog == DialogKind.Hostile || Dialog == DialogKind.Claim || Dialog == DialogKind.Chest
                || Dialog == DialogKind.Dwelling || Dialog == DialogKind.Exit)
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
        /// <summary>
        /// First click plans a route; clicking the same destination again starts the ride. During the ride a click picks a
        /// new destination: the hero stops in the cell he is riding into with the new route planned, and clicking the new
        /// destination again before he stops lets him ride on without stopping.
        /// </summary>
        public void Plan(WorldPosition target)
        {
            Revision++;
            if (isTraveling && Screen == GameScreen.Adventure && Dialog == DialogKind.None)
            {
                Redirect(target);
                return;
            }

            if (!CanRunCampaignCommand || Screen != GameScreen.Adventure)
            {
                return;
            }

            if (target == Campaign.Position)
            {
                EncounterDefinition site = World.Site(Campaign, target);
                MapObjectDefinition standingOn = World.ObjectAt(Campaign, target);
                if (site != null && site.Kind == EncounterKind.Town)
                {
                    OpenTown();
                    Publish(SiteReached, site);
                }
                else if (standingOn != null)
                {
                    // the object the hero stands on is visited again: buy at the dwelling, ask the mill, step into the portal
                    VisitObject(standingOn);
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
                NotifyNoRoute(target);
            }
        }

        /// <summary>Takes the open chest's gold (true) or its experience (false).</summary>
        public void OpenChest(bool isTakingGold)
        {
            Revision++;
            if (Dialog != DialogKind.Chest || MapObject == null)
            {
                return;
            }

            int oldLevel = Rules.Level(Campaign);
            if (!Accept(Objects.OpenChest(Campaign, MapObject, isTakingGold, out VisitOutcome outcome)))
            {
                return;
            }

            Dialog = DialogKind.None;
            Cue(SoundCue.Gold);
            Notify(isTakingGold ? "+" + outcome.Gold + " gold from the chest." : "+" + outcome.Experience + " experience from the chest.");
            CueLevelUp(oldLevel);
            Save(false);
        }

        /// <summary>Buys creatures at the open dwelling.</summary>
        public bool RecruitAtDwelling(int count)
        {
            Revision++;
            if (Dialog != DialogKind.Dwelling || MapObject == null || !Accept(Objects.Recruit(Campaign, MapObject, count)))
            {
                return false;
            }

            UnitDefinition unit = Catalog.Unit(MapObject.UnitId);
            Cue(SoundCue.Gold);
            Voice(unit.Id);
            Notify(count + " " + unit.Name + " join your warhost.");
            Save(false);
            return true;
        }

        /// <summary>Leaves the mission through the exit portal; only once every required goal is reached.</summary>
        public void FinishMission()
        {
            Revision++;
            if (Dialog != DialogKind.Exit)
            {
                return;
            }

            if (!Objects.CanFinish(Campaign))
            {
                Notify("The portal will not open before the required goals are reached.");
                return;
            }

            Dialog = DialogKind.None;
            Save(false);
            Publish(MissionFinished);
        }

        public void Travel()
        {
            Revision++;
            // confirms a destination picked during the ride before the hero stopped
            if (isTraveling && isStopRequested && route.Count > 0)
            {
                isStopRequested = false;
                return;
            }

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
            isStopRequested = false;
            timer = 0;
            RefreshPlayback();
        }

        /// <summary>Starts the end-of-day sequence; the day changes when it ends (see <see cref="Tick"/>).</summary>
        public void EndDay()
        {
            Revision++;
            if (!CanRunCampaignCommand)
            {
                return;
            }

            isEndingDay = true;
            endDayTimer = endDaySeconds;
            Cue(SoundCue.EndDay);
            if (endDayTimer <= 0)
            {
                BeginNewDay();
            }
        }

        private void BeginNewDay()
        {
            Revision++;
            isEndingDay = false;
            endDayTimer = 0;
            if (!Accept(Rules.EndDay(Campaign, out bool isNewWeek)))
            {
                return;
            }

            route.Clear();
            plannedDestination = null;
            Notify(isNewWeek ? "A new week begins. Town recruitment stocks have grown." : "Day " + Campaign.Day + ". Your movement is restored.");
            if (isNewWeek)
            {
                Cue(SoundCue.Friendly);
            }

            Save(false);
        }

        // the cell being ridden into is already the hero's position, so the new route starts there
        private void Redirect(WorldPosition target)
        {
            if (isStopRequested && plannedDestination.HasValue && plannedDestination.Value == target && route.Count > 0)
            {
                isStopRequested = false;
                return;
            }

            isStopRequested = true;
            plannedDestination = target;
            route.Clear();
            if (target == Campaign.Position)
            {
                return;
            }

            route.AddRange(World.FindRoute(Campaign, target));
            Cue(SoundCue.Click);
            if (route.Count == 0)
            {
                NotifyNoRoute(target);
            }
        }

        private void TravelStep()
        {
            if (isStopRequested)
            {
                StopTravel();
                return;
            }

            if (route.Count == 0)
            {
                FinishTravel();
                return;
            }

            WorldPosition step = route[0];
            EncounterDefinition encounter = World.Site(Campaign, step);
            MapObjectDefinition mapObject = World.ObjectAt(Campaign, step);
            // a lasting place on the way is ridden over; it is visited when it is where the hero rides to
            if (mapObject != null && !mapObject.StopsPassage && route.Count > 1)
            {
                mapObject = null;
            }

            if (Campaign.Movement < World.StepCost(Campaign.Position, step))
            {
                Notify("Movement spent. End the day to continue.");
                FinishTravel();
                return;
            }

            // a closed gate stops the ride in front of it; with its key it opens and the hero rides in
            if (mapObject != null && mapObject.Kind == MapObjectKind.BorderGate)
            {
                if (!Accept(Objects.Visit(Campaign, mapObject, out VisitOutcome opened)))
                {
                    FinishTravel();
                    Publish(ObjectReached, mapObject);
                    return;
                }

                Cue(SoundCue.GateOpened);
                Notify(mapObject.Name + " opens for your key.");
                mapObject = null;
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

            route.RemoveAt(0);
            timer = TravelStepSeconds;
            if (encounter != null)
            {
                FinishTravel();
                OpenTown();
                Publish(SiteReached, encounter);
            }
            else if (mapObject != null)
            {
                FinishTravel();
                VisitObject(mapObject);
            }
        }

        /// <summary>The hero stands on a map object: its dialog opens or its effect applies.</summary>
        private void VisitObject(MapObjectDefinition mapObject)
        {
            MapObject = mapObject;
            switch (mapObject.Kind)
            {
                case MapObjectKind.TreasureChest:
                    Dialog = DialogKind.Chest;
                    Cue(SoundCue.FriendlyEncounter);
                    break;
                case MapObjectKind.Dwelling:
                    Dialog = DialogKind.Dwelling;
                    Cue(SoundCue.FriendlyEncounter);
                    break;
                case MapObjectKind.ExitPortal:
                    Dialog = DialogKind.Exit;
                    Cue(SoundCue.FriendlyEncounter);
                    break;
                default:
                    ApplyVisit(mapObject);
                    break;
            }

            Publish(ObjectReached, mapObject);
        }

        private void ApplyVisit(MapObjectDefinition mapObject)
        {
            int oldLevel = Rules.Level(Campaign);
            if (!Accept(Objects.Visit(Campaign, mapObject, out VisitOutcome outcome)))
            {
                return;
            }

            Notify(VisitNotice(outcome));
            if (outcome.IsRewarding)
            {
                Cue(VisitCue(outcome));
            }

            CueLevelUp(oldLevel);
            Save(false);
        }

        private string VisitNotice(VisitOutcome outcome)
        {
            MapObjectDefinition mapObject = outcome.Object;
            if (!outcome.IsRewarding)
            {
                return mapObject.Kind == MapObjectKind.Mill ? mapObject.Name + " has paid out this week. Come back next week."
                    : mapObject.Kind == MapObjectKind.MoraleShrine ? "Your warhost is already blessed."
                    : mapObject.Kind == MapObjectKind.AttackShrine || mapObject.Kind == MapObjectKind.DefenseShrine ? mapObject.Name + " has taught you all it can."
                    : mapObject.Description;
            }

            switch (mapObject.Kind)
            {
                case MapObjectKind.GoldPile:
                case MapObjectKind.Mill:
                    return "+" + outcome.Gold + " gold. " + mapObject.Description;
                case MapObjectKind.AttackShrine:
                    return mapObject.Name + ": +" + outcome.AttackBonus + " attack for good.";
                case MapObjectKind.DefenseShrine:
                    return mapObject.Name + ": +" + outcome.DefenseBonus + " defense for good.";
                case MapObjectKind.MoraleShrine:
                    return mapObject.Name + ": +" + outcome.MoraleBonus + " morale until your next victory.";
                case MapObjectKind.Artefact:
                case MapObjectKind.Prison:
                    return outcome.ArtefactId != null ? "You received " + Catalog.Artefact(outcome.ArtefactId).Name + "." : mapObject.Description;
                default:
                    return mapObject.Description;
            }
        }

        private static SoundCue VisitCue(VisitOutcome outcome)
        {
            switch (outcome.Object.Kind)
            {
                case MapObjectKind.GoldPile:
                case MapObjectKind.Mill:
                    return SoundCue.Gold;
                case MapObjectKind.Artefact:
                case MapObjectKind.Prison:
                    return SoundCue.LegendaryItem;
                case MapObjectKind.QuestGiver:
                    return SoundCue.Equip;
                default:
                    return SoundCue.Blessing;
            }
        }

        private void CueLevelUp(int oldLevel)
        {
            if (Rules.Level(Campaign) > oldLevel)
            {
                Cue(SoundCue.LevelUp);
                Notify("Level " + Rules.Level(Campaign) + "! Your hero grows stronger.");
            }
        }

        private void FinishTravel()
        {
            route.Clear();
            plannedDestination = null;
            StopTravel();
        }

        // keeps the route and destination, so a destination picked during the ride stays planned
        private void StopTravel()
        {
            isTraveling = false;
            isStopRequested = false;
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

        /// <summary>Wears an inventory artefact in a chosen place it fits.</summary>
        public void EquipAt(int inventoryIndex, EquipmentPlace place)
        {
            Revision++;
            if (CanRunCampaignCommand && Accept(Rules.EquipAt(Campaign, inventoryIndex, place)))
            {
                Cue(SoundCue.Equip);
                Save(false);
            }
        }

        public void Unequip(EquipmentPlace place)
        {
            Revision++;
            if (CanRunCampaignCommand && Accept(Rules.Unequip(Campaign, place)))
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

            Encounter = Catalog.Mission.Town;
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
            if (Battle.PlayerMorale != 0)
            {
                AddLog("Your warhost fights with " + (Battle.PlayerMorale > 0 ? "good" : "bad") + " morale (" + MoraleText(Battle.PlayerMorale) + ").");
            }

            ReportMorale();
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
            // the victory music, looping while the victory and rewards screens are open, fades out now
            RefreshPlayback();
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

            ReportMorale();
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

        /// <summary>"+2" or "-1".</summary>
        public static string MoraleText(int morale)
        {
            return morale > 0 ? "+" + morale : morale.ToString();
        }

        private void ReportMorale()
        {
            foreach (MoraleEvent moraleEvent in Battle.TakeMoraleEvents())
            {
                string unit = Catalog.Unit(moraleEvent.UnitId).Name;
                AddLog(moraleEvent.IsGood ? "High morale! " + unit + " acts again." : "Low morale! " + unit + " freezes.");
                Cue(moraleEvent.IsGood ? SoundCue.GoodMorale : SoundCue.BadMorale);
            }
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

        public void DebugRevealMap()
        {
            Revision++;
            Rules.DebugRevealMap(Campaign);
        }
        #endregion

        private GameScreen HomeScreen()
        {
            return Campaign.IsHeroHome ? GameScreen.Town : GameScreen.Adventure;
        }

        private void NotifyNoRoute(WorldPosition target)
        {
            Notify(Campaign.Explored.IsExplored(target)
                ? "No route. Mountains, water, unexplored land and unresolved encounters block travel."
                : "That land is unexplored. Ride closer to see what lies there.");
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
            isStopRequested = false;
            isEndingDay = false;
            endDayTimer = 0;
            route.Clear();
            plannedDestination = null;
            Battle = null;
            Victory = null;
            Encounter = null;
            MapObject = null;
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
                isInTown, Dialog == DialogKind.Victory || Dialog == DialogKind.Rewards);
            if (state.Equals(Playback))
            {
                return;
            }

            Playback = state;
            Publish(PlaybackChanged, state);
        }

        private void Publish(Action signal)
        {
            if (signal == null)
            {
                return;
            }

            foreach (Delegate subscriber in signal.GetInvocationList())
            {
                try
                {
                    ((Action)subscriber)();
                }
                catch (Exception error)
                {
                    reportError(error);
                }
            }
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
