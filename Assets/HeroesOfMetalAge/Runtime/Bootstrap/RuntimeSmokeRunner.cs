#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeroesOfMetalAge.Bootstrap
{
    /// <summary>
    /// Built-player scenario (--smoke-test): plays the whole mission through the real UI and session, checks the outcome of
    /// every step, captures screenshots into Reports/ and quits with exit code 0 or 1. It uses its own save file only.
    /// </summary>
    public sealed class RuntimeSmokeRunner : MonoBehaviour
    {
        private const string REPORT_FILE = "runtime-smoke.txt";
        private const int MAX_MOVEMENT_TICKS = 100;
        private const int MAX_BATTLE_ACTIONS = 1000;
        private const int MAX_TRAVEL_DAYS = 10;
        private const float FRAME_TOLERANCE = 0.5f;
        private const float EDGE_TOLERANCE = 0.01f;
        private const float FOLLOW_WAIT_SECONDS = 1.5f;
        private const float END_DAY_PEEK_SECONDS = 1.2f;
        private const float BRIEFING_TIMEOUT_SECONDS = 20f;

        private readonly List<string> results = new List<string>();
        private GameSession session;
        private ISaveStore store;
        private GameHud hud;
        private GameWorld world;
        private MissionBriefingView briefing;
        private string reportFolder;

        public void Initialize(GameSession gameSession, ISaveStore saveStore, GameHud gameHud, GameWorld gameWorld, MissionBriefingView missionBriefing)
        {
            session = gameSession;
            store = saveStore;
            hud = gameHud;
            world = gameWorld;
            briefing = missionBriefing;
            reportFolder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "..", "Reports"));
            Directory.CreateDirectory(reportFolder);
            StartCoroutine(Drive());
        }

        /// <summary>
        /// Steps the scenario, including its nested steps, itself rather than through Unity, so any failed check ends in a
        /// failed report and exit code instead of a stuck player.
        /// </summary>
        private IEnumerator Drive()
        {
            Stack<IEnumerator> steps = new Stack<IEnumerator>();
            steps.Push(Scenario());
            while (steps.Count > 0)
            {
                IEnumerator step = steps.Peek();
                bool hasNext;
                try
                {
                    hasNext = step.MoveNext();
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    results.Add("FAIL " + error.Message);
                    Finish(1);
                    yield break;
                }

                if (!hasNext)
                {
                    steps.Pop();
                    continue;
                }

                if (step.Current is IEnumerator nested)
                {
                    steps.Push(nested);
                    continue;
                }

                yield return step.Current;
            }

            Finish(0);
        }

        private void Finish(int exitCode)
        {
            DeleteScenarioSave();
            File.WriteAllLines(Path.Combine(reportFolder, REPORT_FILE), results);
            UnityEngine.Application.Quit(exitCode);
        }

        // only the dedicated scenario save is ever deleted; the player's campaign.json is never touched
        private void DeleteScenarioSave()
        {
            if (Path.GetFileName(store.Location) != "refactor-smoke-campaign.json")
            {
                return;
            }

            foreach (string suffix in new[] { string.Empty, ".bak", ".tmp" })
            {
                string path = store.Location + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }

            results.Add("PASS " + message);
        }

        /// <summary>The click handler a real pointer at the widget's centre would reach.</summary>
        private GameObject PointerTarget(UiWidget widget)
        {
            Vector3 center = widget.Rect.TransformPoint(widget.Rect.rect.center);
            PointerEventData pointer = new PointerEventData(hud.Events)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, center),
                button = PointerEventData.InputButton.Left
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            hud.Events.RaycastAll(pointer, hits);
            return hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        }

        private void Click(UiWidget widget)
        {
            GameObject target = PointerTarget(widget);
            Check(target == widget.gameObject, "Pointer raycast reaches intended prefab control");
            PointerEventData click = new PointerEventData(hud.Events) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, click, ExecuteEvents.pointerClickHandler);
        }

        private void CheckFrameInsideScreen()
        {
            Vector3[] corners = new Vector3[4];
            hud.DesignFrame.GetWorldCorners(corners);
            bool isInside = corners.All(corner => corner.x >= -FRAME_TOLERANCE && corner.y >= -FRAME_TOLERANCE
                && corner.x <= Screen.width + FRAME_TOLERANCE && corner.y <= Screen.height + FRAME_TOLERANCE);
            Check(isInside, "Canvas design frame fits " + Screen.width + "x" + Screen.height);
        }

        /// <summary>The world camera covers exactly the screen area of the UI viewport it draws into.</summary>
        private void CheckViewport(RectTransform viewport, string message)
        {
            Vector3[] corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            Rect pixels = world.Camera.Camera.pixelRect;
            bool isAligned = world.Camera.Camera.enabled
                && Mathf.Abs(pixels.xMin - corners[0].x) <= 1f && Mathf.Abs(pixels.yMin - corners[0].y) <= 1f
                && Mathf.Abs(pixels.xMax - corners[2].x) <= 1f && Mathf.Abs(pixels.yMax - corners[2].y) <= 1f;
            Check(isAligned, message);
        }

        /// <summary>Points the camera at a map cell and lets a frame apply it, so the cell can be clicked like a player would.</summary>
        private IEnumerator ShowCell(WorldPosition position)
        {
            world.CenterOn(position);
            hud.RefreshNow();
            yield return null;
        }

        private IEnumerator Screenshot(string file)
        {
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(reportFolder, file));
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private IEnumerator ChangeResolution(int width, int height)
        {
            Screen.SetResolution(width, height, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Canvas.ForceUpdateCanvases();
        }

        private void FinishMovement()
        {
            int ticks = 0;
            while (session.IsBusy && ticks++ < MAX_MOVEMENT_TICKS)
            {
                session.Tick(1);
            }

            Check(!session.IsBusy, "Movement terminates");
        }

        /// <summary>
        /// Starts riding towards the sentinel, clicks the start cell after the first step and checks that the hero stops in the
        /// next cell with the new route planned and the entered cells gone from the route.
        /// </summary>
        private IEnumerator RedirectDuringRide(EncounterDefinition sentinel)
        {
            WorldPosition start = session.Campaign.Position;
            session.Plan(sentinel.Position);
            List<WorldPosition> planned = session.Route.ToList();
            session.Travel();
            session.Tick(0f);
            Check(session.Campaign.Position == planned[0] && session.Route.SequenceEqual(planned.Skip(1)), "The ride drops the cells the hero entered from the route");
            yield return ShowCell(start);
            Check(world.HandleClick(world.ScreenPoint(start)) && session.IsTraveling, "A map click during the ride picks a new destination");
            FinishMovement();
            Check(session.Campaign.Position == planned[0] && session.Route.Count > 0 && session.Route.Last() == start,
                "The hero stops in the next cell with the new route planned");
        }

        /// <summary>Rides to a cell over as many days as the route needs; stops at an encounter, a dialog or the town screen.</summary>
        private void TravelTo(WorldPosition target)
        {
            for (int day = 0; day < MAX_TRAVEL_DAYS; day++)
            {
                session.Plan(target);
                session.Travel();
                FinishMovement();
                if (session.Campaign.Position == target || session.Dialog != DialogKind.None || session.Screen != GameScreen.Adventure)
                {
                    return;
                }

                EndDay();
            }
        }

        /// <summary>Ends the day and lets the end-of-day sequence run to the new day.</summary>
        private void EndDay()
        {
            session.EndDay();
            FinishMovement();
        }

        /// <summary>Plays the player's side: attack the weakest reachable enemy, otherwise advance towards the nearest.</summary>
        private void FinishBattle()
        {
            int actions = 0;
            bool hasPausedOnResult = false;
            while (session.Screen == GameScreen.Battle && session.Dialog == DialogKind.None && actions++ < MAX_BATTLE_ACTIONS)
            {
                hasPausedOnResult |= session.Battle.Outcome != BattleOutcome.InProgress;
                if (session.IsBusy)
                {
                    session.Tick(1);
                    continue;
                }

                session.BattleAction(ChoosePlayerAction(session.Battle));
            }

            Check(actions < MAX_BATTLE_ACTIONS, "Battle terminates");
            Check(hasPausedOnResult, "Decided battle stays on the battlefield before the result screen");
        }

        private static BattleAction ChoosePlayerAction(BattleEngine battle)
        {
            Combatant active = battle.Active;
            List<Combatant> enemies = battle.Combatants
                .Where(combatant => combatant.IsAlive && !combatant.IsPlayer)
                .OrderBy(combatant => combatant.TotalHealth)
                .ThenBy(combatant => combatant.Id)
                .ToList();
            Combatant target = enemies.FirstOrDefault(battle.CanAttack);
            if (target != null)
            {
                return new BattleAction(BattleActionKind.MoveOrAttack, target.Position);
            }

            Combatant nearest = enemies.OrderBy(combatant => HexGrid.Distance(active.Position, combatant.Position)).First();
            HexPosition cell = battle.ReachableCells()
                .OrderBy(position => HexGrid.Distance(position, nearest.Position))
                .ThenBy(position => position.Y)
                .ThenBy(position => position.X)
                .First();
            return battle.At(cell) == null ? new BattleAction(BattleActionKind.MoveOrAttack, cell) : new BattleAction(BattleActionKind.Pass);
        }

        private IEnumerator Scenario()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            MissionDefinition mission = session.Catalog.Mission;
            EncounterDefinition friendly = mission.Encounter(EncounterKind.Friendly);
            EncounterDefinition sentinel = mission.Encounter(EncounterKind.Sentinel);
            EncounterDefinition town = mission.Encounter(EncounterKind.Town);
            EncounterDefinition boss = mission.Encounter(EncounterKind.Boss);

            yield return IntroAndWorld(mission, friendly, sentinel, town);
            yield return FirstBattleAndHero(mission, sentinel);
            yield return TownAndConquest(mission, town);
            yield return BossAndDefeat(boss);
        }

        private IEnumerator IntroAndWorld(MissionDefinition mission, EncounterDefinition friendly, EncounterDefinition sentinel, EncounterDefinition town)
        {
            Check(hud.GetComponent<Canvas>() != null && hud.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0, "Canvas and TMP prefab UI bootstrap");
            Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One active EventSystem");
            DebugPanelView debugPanel = hud.GetComponentInChildren<DebugPanelView>(true);
            Check(debugPanel != null && debugPanel.CueWidgets.Count > 0 && !debugPanel.gameObject.activeSelf,
                "Development panel is a hidden uGUI prefab with a button per audio cue");
            hud.RefreshNow();
            Check(!hud.ContentGroup.interactable && !hud.ContentGroup.blocksRaycasts, "Intro modal blocks underlying content");
            yield return ShowCell(friendly.Position);
            Check(!world.HandleClick(world.ScreenPoint(friendly.Position)) && session.Route.Count == 0, "Modal blocks clicks through to the world map");
            yield return Screenshot("00-intro.png");

            // the briefing scroll: every page read with a click, then the scroll is thrown away and the mission starts
            float waited = 0f;
            while (session.Dialog == DialogKind.Intro && waited < BRIEFING_TIMEOUT_SECONDS)
            {
                if (briefing.Phase == BriefingPhase.Reading && briefing.Page == briefing.PageCount - 1)
                {
                    yield return Screenshot("00-briefing.png");
                }

                briefing.Advance();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Check(session.Dialog == DialogKind.None, "Briefing scroll pages through its story and starts the mission");
            hud.RefreshNow();
            yield return null;
            CheckViewport(hud.WorldView.MapViewport, "World camera draws the map inside the adventure screen's viewport");
            Check(world.Map.HeroMarker.HasVisibleIcon, "World hero marker has visible sprite");
            Check(mission.Encounters.Where(encounter => encounter.Kind != EncounterKind.Town).All(encounter => world.Map.EncounterMarker(encounter.Id).HasVisibleIcon),
                "World friendly and enemy markers have visible sprites");
            Check(session.Campaign.Army.Count > 0, "Runtime bootstrap from authored hero");
            Check(session.World.FindRoute(session.Campaign, sentinel.Position).Count > 0, "World route reaches chokepoint");
            Check(session.World.FindRoute(session.Campaign, town.Position).Count == 0, "Sentinel blocks town route");

            world.Camera.Pan(new Vector2(-1000f, 1000f));
            yield return null;
            Rect visible = world.Camera.VisibleArea;
            Rect bounds = world.Map.Bounds;
            Check(visible.xMin >= bounds.xMin - EDGE_TOLERANCE && visible.yMax <= bounds.yMax + EDGE_TOLERANCE, "Panning stops at the map edge");

            yield return ShowCell(friendly.Position);
            Check(world.HandleClick(world.ScreenPoint(friendly.Position)) && session.Route.Count > 0, "World map click plans route");
            yield return Screenshot("01-world.png");

            world.CenterOn(session.Campaign.Position);
            session.Travel();
            FinishMovement();
            Check(session.Dialog == DialogKind.Friendly, "Travel stops for friendly encounter");
            yield return new WaitForSecondsRealtime(FOLLOW_WAIT_SECONDS);
            Check(world.Camera.VisibleArea.Contains(world.Map.HeroMarker.transform.position), "Camera follows the hero along the route");
            hud.RefreshNow();
            hud.DialogView.PrimaryButton.Button.onClick.Invoke();
            Check(session.Campaign.IsCompleted(friendly.Id), "Friendly army joins through command");
        }

        private IEnumerator FirstBattleAndHero(MissionDefinition mission, EncounterDefinition sentinel)
        {
            yield return RedirectDuringRide(sentinel);
            TravelTo(sentinel.Position);
            Check(session.Dialog == DialogKind.Hostile, "Travel stops before hostile stack");
            hud.RefreshNow();
            hud.DialogView.PrimaryButton.Button.onClick.Invoke();
            Check(session.Battle.Combatants.Count == session.Campaign.Army.Count + sentinel.Army.Count, "Battle bootstrap uses unit definitions");
            hud.RefreshNow();
            yield return null;
            CheckViewport(hud.CombatView.BattleViewport, "World camera draws the battlefield inside the battle screen's viewport");
            Check(session.Battle.Combatants.All(combatant => world.Battlefield.UnitMarker(combatant.Id).HasVisibleIcon),
                "Player and enemy battle units have visible sprites");
            Check(session.Battle.Combatants.All(combatant => world.Battlefield.UnitMarker(combatant.Id).IsSelected == (combatant == session.Battle.Active)),
                "Only the acting stack is highlighted");
            yield return Screenshot("02-battle.png");

            int ticks = 0;
            while ((session.IsBusy || !session.Battle.Active.IsPlayer) && ticks++ < MAX_MOVEMENT_TICKS)
            {
                session.Tick(1);
            }

            BattleAction firstAction = ChoosePlayerAction(session.Battle);
            Combatant actor = session.Battle.Active;
            Check(firstAction.Kind == BattleActionKind.MoveOrAttack && world.HandleClick(world.ScreenPoint(firstAction.Target))
                && session.Battle.Active != actor, "Battlefield hex click sends the battle action");
            Combatant next = session.Battle.Active;
            session.BattleAction(new BattleAction(BattleActionKind.Pass));
            Check(session.IsBusy && session.Battle.Active == next, "A battle action pauses the battle before the next one");
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Screenshot("02b-battle-action.png");

            FinishBattle();
            VictorySummary rewards = session.Victory;
            bool isRewarded = session.Dialog == DialogKind.Victory
                && session.Campaign.IsCompleted(sentinel.Id)
                && rewards.Gold > 0
                && session.Campaign.Gold == mission.StartingGold + rewards.Gold
                && session.Campaign.Experience == sentinel.RewardExperience
                && session.Campaign.Inventory.Count == rewards.ArtefactIds.Count
                && rewards.ArtefactIds.Count <= 1;
            Check(isRewarded, "Sentinel victory applies the rolled gold and at most one artefact");
            int gold = session.Campaign.Gold;
            session.BattleAction(new BattleAction(BattleActionKind.Pass));
            Check(session.Campaign.Gold == gold, "Repeated terminal action grants no extra rewards");
            hud.RefreshNow();
            Check(hud.VictoryView.gameObject.activeInHierarchy && !hud.ContentGroup.interactable, "Victory screen opens over the battle and blocks it");
            yield return Screenshot("05a-victory-screen.png");

            Click(hud.VictoryView.PrimaryButton);
            Check(session.Dialog == DialogKind.Rewards, "Victory screen leads to the rewards screen");
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Check(hud.RewardsView.gameObject.activeInHierarchy
                && hud.RewardsView.ArtefactCards.Count(card => card.gameObject.activeSelf) == rewards.ArtefactIds.Count,
                "Rewards screen shows one card per rolled artefact");
            yield return Screenshot("05b-rewards-screen.png");
            Click(hud.RewardsView.PrimaryButton);
            Check(session.Dialog == DialogKind.None, "Rewards screen closes to the map");

            session.ShowHero();
            if (rewards.ArtefactIds.Count > 0)
            {
                session.Equip(0);
                Check(session.Campaign.WeaponId == rewards.ArtefactIds[0] || session.Campaign.CharmId == rewards.ArtefactIds[0], "Reward equipment command");
            }

            yield return Screenshot("03-hero.png");

            Check(session.Campaign.Inventory.Count == 0, "Empty inventory is displayed safely");
            session.DebugArtefacts();
            session.DebugArtefacts();
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Check(session.Campaign.Inventory.Count == session.Catalog.Artefacts.Count * 2 && hud.HeroView.GetComponentsInChildren<UiWidget>(true).Length >= 15,
                "Populated inventory instantiates reusable artefact prefabs");
            yield return Screenshot("10-inventory.png");

            yield return ChangeResolution(1280, 1024);
            CheckFrameInsideScreen();
            yield return Screenshot("08-hero-4x3.png");
            yield return ChangeResolution(1920, 800);
            CheckFrameInsideScreen();
            yield return Screenshot("09-hero-ultrawide.png");
            session.Back();
            hud.RefreshNow();
            yield return null;
            CheckViewport(hud.WorldView.MapViewport, "World viewport follows the UI at 1920x800");
            yield return Screenshot("09b-world-ultrawide.png");
            yield return ChangeResolution(1920, 1080);

            world.ToggleTerrainOverlay();
            yield return null;
            Check(world.Map.IsTerrainOverlayVisible, "Development terrain overlay shows the logic layer");
            yield return Screenshot("12-terrain-overlay.png");
            world.ToggleTerrainOverlay();
        }

        private IEnumerator TownAndConquest(MissionDefinition mission, EncounterDefinition town)
        {
            TravelTo(town.Position);
            Check(session.Screen == GameScreen.Town && session.Campaign.Position == town.Position, "Town reached through cleared pass");

            hud.OpenSettings();
            hud.RefreshNow();
            Check(!hud.ContentGroup.interactable, "Options modal blocks gameplay UI");
            GameObject selected = hud.Events.currentSelectedGameObject;
            Check(selected != null && selected.name == "back", "Options opening selects a keyboard control");
            yield return Screenshot("07-options.png");
            hud.CloseSettings();
            hud.SetDebugVisible(true);
            hud.RefreshNow();
            DebugPanelView debugPanel = hud.GetComponentInChildren<DebugPanelView>(true);
            Check(debugPanel.gameObject.activeInHierarchy && !hud.ContentGroup.interactable, "F3 panel opens over the game and blocks gameplay UI");
            yield return Screenshot("11-debug.png");
            hud.SetDebugVisible(false);
            hud.RefreshNow();

            Check(session.Buy("ironbound", 10) && session.Buy("ash-archer", 5) && session.Buy("doom-rider", 3), "Town recruitment commands");
            yield return Screenshot("04-town.png");

            int day = session.Campaign.Day;
            session.EndDay();
            session.Tick(END_DAY_PEEK_SECONDS);
            yield return null;
            Check(session.IsEndingDay && session.Campaign.Day == day && hud.EndDayView.gameObject.activeInHierarchy,
                "Ending the day shows the hourglass before the day changes");
            yield return Screenshot("04b-end-day.png");
            FinishMovement();
            yield return null;
            Check(session.Campaign.Day == day + 1 && !hud.EndDayView.gameObject.activeInHierarchy, "The new day begins when the hourglass ends");

            int stock = session.Campaign.Stock("ironbound");
            for (int i = 0; i < mission.DaysPerWeek; i++)
            {
                EndDay();
            }

            Check(session.Campaign.Stock("ironbound") == stock + session.Catalog.Unit("ironbound").WeeklyGrowth, "Weekly refresh in runtime");
            session.ClaimTown();
            session.FightEncounter();
            FinishBattle();
            Check(session.Dialog == DialogKind.Victory && session.Campaign.IsTownOwned, "Town conquest");
            session.ContinueVictory();
            session.ContinueRewards();
            session.StationHero();
            Check(session.Campaign.IsHeroHome && session.Campaign.Army.Count == 0, "Hero home slot and army");
            session.Save(false);
            session.Load();
            Check(session.Campaign.IsHeroHome, "Home hero survives save and load");
            yield return Screenshot("04b-owned-town.png");

            session.StationHero();
            Check(!session.Campaign.IsHeroHome && session.Campaign.Army.Count > 0, "Hero returns to visiting slot");
            session.Save(false);
            session.Load();
            Check(session.Campaign.Army.Count == 3, "Stable-ID save round trip");
        }

        private IEnumerator BossAndDefeat(EncounterDefinition boss)
        {
            session.LeaveTown();
            TravelTo(boss.Position);
            Check(session.Dialog == DialogKind.Hostile, "Final enemy reached");
            session.FightEncounter();
            FinishBattle();
            Check(session.Dialog == DialogKind.Victory && session.Campaign.IsCompleted(boss.Id), "Recruited army defeats boss");
            yield return Screenshot("05-victory.png");

            session.Restart();
            session.StartMission();
            session.DebugTown();
            session.LeaveTown();
            TravelTo(boss.Position);
            session.FightEncounter();
            FinishBattle();
            Check(session.Dialog == DialogKind.Defeat, "Weak army defeat panel");
            hud.RefreshNow();
            Check(hud.DefeatView.gameObject.activeInHierarchy && hud.Events.currentSelectedGameObject == hud.DefeatView.PrimaryButton.gameObject,
                "Defeat screen opens with retry focused");
            yield return Screenshot("06-defeat.png");

            session.FightEncounter();
            Check(session.Screen == GameScreen.Battle && session.Dialog == DialogKind.None, "Defeat retry restores pre-battle army");
        }
    }
}
#endif
