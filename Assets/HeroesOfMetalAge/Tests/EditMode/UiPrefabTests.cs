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
        private static readonly string[] WidgetPrefabs = { "Button", "ArmySlot", "RecruitmentCard", "ArtefactCard", "WorldTile", "CombatHex", "RouteSegment", "WorldEntity", "CombatUnit" };

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
            Assert.AreEqual(6, root.GetComponentsInChildren<CanvasScreenView>(true).Length);
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
        public void Adventure_KeepsTerrainAndEntitiesInSeparateLayers()
        {
            GameObject adventure = AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.UI_FOLDER + "/Screens/Adventure.prefab");
            string[] layers = { "Environment/Road Tiles", "Environment/Forest Tiles", "Environment/Grass Tiles", "Environment/Mountain Tiles",
                "Environment/Water Tiles", "Heroes", "Enemies", "Friendly Units", "Towns" };
            foreach (string layer in layers)
            {
                Assert.IsNotNull(adventure.transform.Find(layer), layer);
            }

            Assert.Greater(adventure.transform.Find("Heroes").GetComponentsInChildren<UiWidget>(true).Length, 0);
            Assert.Greater(adventure.transform.Find("Enemies").GetComponentsInChildren<UiWidget>(true).Length, 0);
            GameObject combat = AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.UI_FOLDER + "/Screens/Combat.prefab");
            Assert.Greater(combat.transform.Find("Units/Player Units").GetComponentsInChildren<UiWidget>(true).Length, 0);
            Assert.Greater(combat.transform.Find("Units/Enemy Units").GetComponentsInChildren<UiWidget>(true).Length, 0);
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
                int expected = Enum.GetValues(typeof(MusicLayer)).Length + Enum.GetValues(typeof(SoundCue)).Length + session.Catalog.Units.Count;

                panel.Initialize(session, audio);

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
            foreach (string file in Directory.GetFiles("Assets/HeroesOfMetalAge/Runtime/Presentation", "*.cs"))
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
    }
}
