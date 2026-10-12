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
        private const float FRAME_TOLERANCE = 0.5f;

        private readonly List<string> results = new List<string>();
        private GameSession session;
        private ISaveStore store;
        private GameHud hud;
        private string reportFolder;

        public void Initialize(GameSession gameSession, ISaveStore saveStore, GameHud gameHud)
        {
            session = gameSession;
            store = saveStore;
            hud = gameHud;
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

        /// <summary>Plays the player's side: attack the weakest reachable enemy, otherwise advance towards the nearest.</summary>
        private void FinishBattle()
        {
            int actions = 0;
            while (session.Screen == GameScreen.Battle && session.Dialog == DialogKind.None && actions++ < MAX_BATTLE_ACTIONS)
            {
                if (session.IsBusy)
                {
                    session.Tick(1);
                    continue;
                }

                session.BattleAction(ChoosePlayerAction(session.Battle));
            }

            Check(actions < MAX_BATTLE_ACTIONS, "Battle terminates");
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
            Canvas.ForceUpdateCanvases();
            UiWidget friendlyTile = hud.WorldView.Tile(friendly.Position);
            Check(PointerTarget(friendlyTile) != friendlyTile.gameObject, "Modal raycast prevents click-through to map");
            yield return Screenshot("00-intro.png");

            Click(hud.DialogView.PrimaryButton);
            Check(session.Dialog == DialogKind.None, "Intro prefab button starts mission");
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Check(hud.WorldView.HeroMarker.HasVisibleIcon, "World hero marker has visible sprite");
            Check(mission.Encounters.All(encounter => hud.WorldView.EncounterMarker(encounter.Id).HasVisibleIcon),
                "World friendly/enemy/town markers have visible sprites");
            Check(session.Campaign.Army.Count > 0, "Runtime bootstrap from authored hero");
            Check(session.World.FindRoute(session.Campaign, sentinel.Position).Count > 0, "World route reaches chokepoint");
            Check(session.World.FindRoute(session.Campaign, town.Position).Count == 0, "Sentinel blocks town route");

            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Click(hud.WorldView.Tile(friendly.Position));
            Check(session.Route.Count > 0, "World tile prefab click plans route");
            yield return Screenshot("01-world.png");

            session.Travel();
            FinishMovement();
            Check(session.Dialog == DialogKind.Friendly, "Travel stops for friendly encounter");
            hud.RefreshNow();
            hud.DialogView.PrimaryButton.Button.onClick.Invoke();
            Check(session.Campaign.IsCompleted(friendly.Id), "Friendly army joins through command");
        }

        private IEnumerator FirstBattleAndHero(MissionDefinition mission, EncounterDefinition sentinel)
        {
            session.Plan(sentinel.Position);
            session.Travel();
            FinishMovement();
            Check(session.Dialog == DialogKind.Hostile, "Travel stops before hostile stack");
            hud.RefreshNow();
            hud.DialogView.PrimaryButton.Button.onClick.Invoke();
            Check(session.Battle.Combatants.Count == session.Campaign.Army.Count + sentinel.Army.Count, "Battle bootstrap uses unit definitions");
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Check(session.Battle.Combatants.All(combatant => hud.CombatView.UnitMarker(combatant.Id).HasVisibleIcon),
                "Player and enemy battle units have visible sprites");
            yield return Screenshot("02-battle.png");

            FinishBattle();
            bool isRewarded = session.Dialog == DialogKind.Victory
                && session.Campaign.IsCompleted(sentinel.Id)
                && session.Campaign.Gold == mission.StartingGold + sentinel.RewardGold
                && session.Campaign.Experience == sentinel.RewardExperience;
            Check(isRewarded, "Sentinel victory and authored rewards");
            int gold = session.Campaign.Gold;
            session.BattleAction(new BattleAction(BattleActionKind.Pass));
            Check(session.Campaign.Gold == gold, "Repeated terminal action grants no extra rewards");

            session.ContinueVictory();
            session.ShowHero();
            session.Equip(0);
            Check(session.Campaign.WeaponId == sentinel.RewardArtefactId, "Reward equipment command");
            yield return Screenshot("03-hero.png");

            Check(session.Campaign.Inventory.Count == 0, "Empty inventory is displayed safely");
            session.DebugArtefacts();
            session.DebugArtefacts();
            hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            Check(session.Campaign.Inventory.Count == 6 && hud.HeroView.GetComponentsInChildren<UiWidget>(true).Length >= 15,
                "Populated inventory instantiates reusable artefact prefabs");
            yield return Screenshot("10-inventory.png");

            yield return ChangeResolution(1280, 1024);
            CheckFrameInsideScreen();
            yield return Screenshot("08-hero-4x3.png");
            yield return ChangeResolution(1920, 800);
            CheckFrameInsideScreen();
            yield return Screenshot("09-hero-ultrawide.png");
            yield return ChangeResolution(1600, 900);
            session.Back();
        }

        private IEnumerator TownAndConquest(MissionDefinition mission, EncounterDefinition town)
        {
            session.Plan(town.Position);
            session.Travel();
            FinishMovement();
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

            int stock = session.Campaign.Stock("ironbound");
            for (int i = 0; i < mission.DaysPerWeek; i++)
            {
                session.EndDay();
            }

            Check(session.Campaign.Stock("ironbound") == stock + session.Catalog.Unit("ironbound").WeeklyGrowth, "Weekly refresh in runtime");
            session.ClaimTown();
            session.FightEncounter();
            FinishBattle();
            Check(session.Dialog == DialogKind.Victory && session.Campaign.IsTownOwned, "Town conquest");
            session.ContinueVictory();
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
            session.Plan(boss.Position);
            session.Travel();
            FinishMovement();
            Check(session.Dialog == DialogKind.Hostile, "Final enemy reached");
            session.FightEncounter();
            FinishBattle();
            Check(session.Dialog == DialogKind.Victory && session.Campaign.IsCompleted(boss.Id), "Recruited army defeats boss");
            yield return Screenshot("05-victory.png");

            session.Restart();
            session.StartMission();
            session.DebugTown();
            session.LeaveTown();
            session.Plan(boss.Position);
            session.Travel();
            FinishMovement();
            session.FightEncounter();
            FinishBattle();
            Check(session.Dialog == DialogKind.Defeat, "Weak army defeat panel");
            yield return Screenshot("06-defeat.png");

            session.FightEncounter();
            Check(session.Screen == GameScreen.Battle && session.Dialog == DialogKind.None, "Defeat retry restores pre-battle army");
        }
    }
}
#endif
