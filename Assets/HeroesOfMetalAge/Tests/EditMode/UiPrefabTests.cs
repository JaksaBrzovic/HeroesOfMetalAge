using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Bootstrap;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>The authored uGUI prefabs are complete, wired and initialize against the real content.</summary>
    public class UiPrefabTests
    {
        private static readonly string[] WidgetPrefabs = { "Button", "ArmySlot", "RecruitmentCard", "ArtefactCard" };

        private GameHud root;

        [SetUp]
        public void SetUp()
        {
            root = ContentAssets.RootPrefab;
        }

        [Test]
        public void Root_IsASavedCanvasPrefabWithOneEventSystem()
        {
            Assert.IsNotNull(root);
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabAsset(root));
            Assert.IsNotNull(root.GetComponent<Canvas>());
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
            Assert.AreEqual(1, root.GetComponentsInChildren<EventSystem>(true).Length);
            Assert.IsNotNull(root.GetComponentInChildren<StandaloneInputModule>(true));
            Assert.AreEqual(9, root.GetComponentsInChildren<CanvasScreenView>(true).Length);
        }

        [Test]
        public void Root_InitializesAndRefreshesEveryScreenWithAuthoredContent()
        {
            GameHud hud = (GameHud)PrefabUtility.InstantiatePrefab(root);
            try
            {
                DefinitionCatalog catalog = ContentAssets.LoadCatalog();
                GameSession session = CampaignTestUtility.NewSession(catalog, new MemorySaveStore());
                PresentationAssets media = GameBootstrap.CreatePresentationAssets(ContentAssets.Units, ContentAssets.Artefacts, ContentAssets.Heroes,
                    ContentAssets.Mission);

                Assert.DoesNotThrow(() => hud.Initialize(session, new FakeAudioControls(), media));
                session.StartMission();
                session.DebugTown();
                Assert.DoesNotThrow(hud.RefreshNow, "town screen");
                session.ShowHero();
                Assert.DoesNotThrow(hud.RefreshNow, "hero screen");
                hud.OpenSettings();
                Assert.DoesNotThrow(hud.RefreshNow, "options overlay");
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void HeldEncounterDialog_StaysHiddenUntilReleased()
        {
            GameHud hud = (GameHud)PrefabUtility.InstantiatePrefab(root);
            try
            {
                GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
                PresentationAssets media = GameBootstrap.CreatePresentationAssets(ContentAssets.Units, ContentAssets.Artefacts, ContentAssets.Heroes,
                    ContentAssets.Mission);
                hud.Initialize(session, new FakeAudioControls(), media);
                Assert.IsFalse(hud.DialogView.gameObject.activeSelf, "the mission's intro is the briefing scroll, not this dialog");
                Assert.IsFalse(hud.IsGameplayInputEnabled, "but gameplay waits for it");
                session.StartMission();
                RideToFriendlyEncounter(session);
                hud.RefreshNow();
                Assert.IsTrue(hud.DialogView.gameObject.activeSelf, "the encounter dialog shows");

                hud.SetDialogHeld(true);
                hud.RefreshNow();
                Assert.IsFalse(hud.DialogView.gameObject.activeSelf, "held while something is said first");
                Assert.IsFalse(hud.IsGameplayInputEnabled, "gameplay stays blocked under the held dialog");

                hud.SetDialogHeld(false);
                hud.RefreshNow();
                Assert.IsTrue(hud.DialogView.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void WorldViewports_LetTheWorldShowThroughAndTakeNoClicks()
        {
            GameObject adventure = AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.UI_FOLDER + "/Screens/Adventure.prefab");
            GameObject combat = AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.UI_FOLDER + "/Screens/Combat.prefab");

            Assert.AreSame(adventure.transform.Find("MapViewport"), adventure.GetComponent<WorldCanvasView>().MapViewport);
            Assert.AreSame(combat.transform.Find("BattleViewport"), combat.GetComponent<CombatCanvasView>().BattleViewport);
            Assert.IsEmpty(adventure.transform.Find("MapViewport").GetComponentsInChildren<Graphic>(true), "nothing drawn over the map");
            Assert.IsEmpty(combat.transform.Find("BattleViewport").GetComponentsInChildren<Graphic>(true), "nothing drawn over the battlefield");
            Assert.IsNull(root.transform.Find("Backdrop"), "no full-screen backdrop hides the world");
        }

        [Test]
        public void Labels_HaveFontsAndIgnoreRaycasts()
        {
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);

            Assert.Greater(labels.Length, 20);
            Assert.IsTrue(labels.All(label => label.font != null), "every label has a font");
            Assert.IsTrue(labels.All(label => !label.raycastTarget), "decorative text does not block clicks");
        }

        [Test]
        public void Sliders_ConstrainDrivenGeometry()
        {
            foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
            {
                Assert.AreNotEqual(slider.transform, slider.fillRect.parent, slider.name);
                Assert.AreNotEqual(slider.transform, slider.handleRect.parent, slider.name);
                Assert.AreEqual(Vector2.zero, slider.fillRect.sizeDelta, slider.name);
                Assert.AreEqual(0f, slider.handleRect.sizeDelta.y, slider.name);
            }
        }

        [Test]
        public void Widgets_AreReusablePrefabsAndStayLinked()
        {
            foreach (string widget in WidgetPrefabs)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.UI_FOLDER + "/Widgets/" + widget + ".prefab"), widget);
            }

            foreach (UiWidget widget in root.GetComponentsInChildren<UiWidget>(true))
            {
                Assert.IsNotNull(PrefabUtility.GetCorrespondingObjectFromSource(widget), "nested widget keeps its prefab link: " + widget.name);
            }
        }

        [Test]
        public void DebugPanel_IsAUguiPrefabWithAButtonPerLayerCueAndVoice()
        {
            DebugPanelView prefab = ContentAssets.DebugPanelPrefab;
            Assert.IsNotNull(prefab);
            DebugPanelView panel = Object.Instantiate(prefab);
            try
            {
                FakeAudioControls audio = new FakeAudioControls();
                GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
                // layers, music tracks, the riding loop, cues and unit voices
                int expected = Enum.GetValues(typeof(MusicLayer)).Length + Enum.GetValues(typeof(MusicTrack)).Length + 1
                    + Enum.GetValues(typeof(SoundCue)).Length + session.Catalog.Units.Count;

                panel.Initialize(session, audio, () => { });

                // clicks need OnEnable listeners, which only run in play mode; the built-player scenario covers them
                Assert.AreEqual(expected, panel.CueWidgets.Count);
                Assert.IsTrue(panel.CueWidgets.All(widget => widget.Button != null), "every cue button is clickable");
                Assert.AreEqual("Ambient Layer", panel.CueWidgets[0].name);
                Assert.AreEqual(session.Catalog.Units[0].Name + " Voice", panel.CueWidgets[expected - session.Catalog.Units.Count].name);
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void PresentationCode_NeverBuildsOrDrawsUi()
        {
            string[] forbidden = { "OnGUI(", "GUI.DrawTexture", "GUILayout.", "new GameObject", "AddComponent<" };
            string[] files = Directory.GetFiles("Assets/HeroesOfMetalAge/Runtime/Presentation", "*.cs")
                .Concat(Directory.GetFiles("Assets/HeroesOfMetalAge/Runtime/Dialogue", "*.cs"))
                .ToArray();
            foreach (string file in files)
            {
                string code = File.ReadAllText(file);
                foreach (string pattern in forbidden)
                {
                    StringAssert.DoesNotContain(pattern, code, Path.GetFileName(file));
                }
            }
        }

        [Test]
        public void MissingOrDestroyedSprites_FallBackToVisiblePlaceholders()
        {
            UiTheme theme = ContentAssets.Theme;
            DefinitionCatalog catalog = ContentAssets.LoadCatalog();
            GameSession session = CampaignTestUtility.NewSession(catalog, new MemorySaveStore());
            Texture2D texture = new Texture2D(8, 8);
            Sprite destroyed = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
            Sprite live = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
            Object.DestroyImmediate(destroyed);
            string unitId = catalog.Units[0].Id;
            string heroId = session.Campaign.HeroId;
            string artefactId = catalog.Artefacts[0].Id;
            try
            {
                Assert.IsTrue(destroyed == null && !ReferenceEquals(destroyed, null), "Unity fake-null sprite");
                CanvasViewContext broken = Context(session, theme, unitId, heroId, artefactId, destroyed);
                Assert.AreSame(theme.Hero, broken.HeroIcon);
                Assert.AreSame(theme.Unit(catalog.Unit(unitId).IconKey), broken.UnitIcon(unitId));
                Assert.AreSame(theme.Town, broken.TownIcon);
                Assert.AreSame(theme.Axe, broken.ArtefactIcon(artefactId));

                CanvasViewContext authored = Context(session, theme, unitId, heroId, artefactId, live);
                Assert.AreSame(live, authored.HeroIcon);
                Assert.AreSame(live, authored.UnitIcon(unitId));
                Assert.AreSame(live, authored.TownIcon);
                Assert.AreSame(live, authored.ArtefactIcon(artefactId));
            }
            finally
            {
                Object.DestroyImmediate(live);
                Object.DestroyImmediate(texture);
            }
        }

        private static CanvasViewContext Context(GameSession session, UiTheme theme, string unitId, string heroId, string artefactId, Sprite sprite)
        {
            PresentationAssets media = new PresentationAssets(
                new Dictionary<string, Sprite> { [unitId] = sprite },
                new Dictionary<string, Sprite> { [heroId] = sprite },
                new Dictionary<string, Sprite> { [artefactId] = sprite },
                sprite);
            return new CanvasViewContext(session, null, media, theme);
        }

        // rides day after day towards the first friendly encounter until it stops the hero
        private static void RideToFriendlyEncounter(GameSession session)
        {
            EncounterDefinition friendly = session.Catalog.Mission.Encounters.First(encounter => encounter.Kind == EncounterKind.Friendly);
            for (int day = 0; day < 10 && session.Dialog == DialogKind.None; day++)
            {
                session.Plan(friendly.Position);
                session.Travel();
                for (int tick = 0; tick < 200 && session.IsBusy; tick++)
                {
                    session.Tick(1f);
                }

                if (session.Dialog == DialogKind.None)
                {
                    session.EndDay();
                    for (int tick = 0; tick < 200 && session.IsBusy; tick++)
                    {
                        session.Tick(1f);
                    }
                }
            }

            Assert.AreEqual(DialogKind.Friendly, session.Dialog, "the ride stops at the friendly encounter");
        }
    }
}
