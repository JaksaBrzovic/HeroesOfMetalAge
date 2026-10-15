using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Infrastructure.Audio;
using HeroesOfMetalAge.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>The main menu, the New Game button's animation and the transition into the mission with its audio.</summary>
    public class FrontEndTests
    {
        private const float FRAME = 1f / 16f;

        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Flow_FadesOutWaitsOnBlackPlaysTheIntroAndFadesIn()
        {
            FrontEndFlow flow = new FrontEndFlow(1f, 2f, 3f, 1f, true);
            List<FrontEndPhase> phases = new List<FrontEndPhase>();
            flow.PhaseChanged += phases.Add;

            Assert.AreEqual(FrontEndPhase.MainMenu, flow.Phase);
            Assert.IsFalse(flow.IsPlaying);
            Assert.AreEqual(0f, flow.Blackness);
            flow.Tick(10f);
            Assert.AreEqual(FrontEndPhase.MainMenu, flow.Phase, "the menu waits for the player");

            Assert.IsTrue(flow.StartNewGame());
            Assert.IsFalse(flow.StartNewGame(), "a second click during the fade is ignored");
            flow.Tick(0.5f);
            Assert.AreEqual(0.5f, flow.Blackness, 1e-4f);

            flow.Tick(0.5f);
            Assert.AreEqual(FrontEndPhase.Black, flow.Phase);
            Assert.AreEqual(1f, flow.Blackness);
            flow.Tick(2f);
            Assert.AreEqual(FrontEndPhase.Intro, flow.Phase);
            flow.Tick(1.5f);
            Assert.AreEqual(0.5f, flow.Progress, 1e-4f);
            Assert.AreEqual(1f, flow.Blackness, "the intro plays on black");
            flow.Tick(1.75f);
            Assert.AreEqual(FrontEndPhase.FadingIn, flow.Phase);
            Assert.AreEqual(0.75f, flow.Blackness, 1e-4f, "the rest of a long frame carries into the next phase");
            Assert.IsFalse(flow.IsPlaying, "no input while the mission fades in");

            flow.Tick(1f);
            Assert.IsTrue(flow.IsPlaying);
            Assert.AreEqual(0f, flow.Blackness);
            CollectionAssert.AreEqual(new[] { FrontEndPhase.FadingOut, FrontEndPhase.Black, FrontEndPhase.Intro, FrontEndPhase.FadingIn, FrontEndPhase.Playing },
                phases);
            Assert.IsFalse(flow.StartNewGame());
        }

        [Test]
        public void Flow_ZeroDurationsFinishAtOnceAndAutomatedRunsSkipTheMenu()
        {
            FrontEndFlow flow = new FrontEndFlow(0f, 0f, 0f, 0f, true);
            flow.StartNewGame();
            flow.Tick(0f);
            Assert.IsTrue(flow.IsPlaying);

            Assert.IsTrue(new FrontEndFlow(1f, 1f, 1f, 1f, false).IsPlaying);
        }

        [Test]
        public void NewGameButton_FlexesOnHoverAndRelaxesBackwards()
        {
            NewGameButtonView button = NewFrontEnd().MainMenu.NewGameButton;
            Sprite[] flex = Frames(button, "flexFrames");
            button.Initialize();
            Assert.AreSame(flex[0], button.CurrentFrame, "the arm rests on the first flex frame");

            button.SetHovered(true);
            button.Tick(FRAME * 2.5f);
            Assert.AreSame(flex[2], button.CurrentFrame, "one frame per sixteenth of a second");
            button.Tick(FRAME * 10f);
            Assert.AreSame(flex[flex.Length - 1], button.CurrentFrame, "the flex holds its last frame");

            button.SetHovered(false);
            button.Tick(FRAME * 2f);
            Assert.AreSame(flex[flex.Length - 3], button.CurrentFrame, "leaving plays the flex backwards");
            button.SetHovered(true);
            button.Tick(FRAME);
            Assert.AreSame(flex[flex.Length - 2], button.CurrentFrame, "hovering again turns forward from where it is");
            button.SetHovered(false);
            button.Tick(FRAME * 20f);
            Assert.AreSame(flex[0], button.CurrentFrame);
        }

        [Test]
        public void NewGameButton_ClickFinishesTheFlexThenRaisesTheHorns()
        {
            NewGameButtonView button = NewFrontEnd().MainMenu.NewGameButton;
            Sprite[] flex = Frames(button, "flexFrames");
            Sprite[] horns = Frames(button, "hornsFrames");
            button.Initialize();
            int hovers = 0;
            int clicks = 0;
            button.Hovered += () => hovers++;
            button.Clicked += () => clicks++;

            button.SetHovered(true);
            button.Tick(FRAME * 1.5f);
            button.Click();
            button.Click();
            Assert.AreEqual(1, hovers);
            Assert.AreEqual(1, clicks, "the click is reported once, at once");

            button.SetHovered(false);
            button.Tick(FRAME * (flex.Length - 2));
            Assert.AreSame(flex[flex.Length - 1], button.CurrentFrame, "the flex finishes first, even after the pointer left");
            button.Tick(FRAME);
            Assert.AreSame(horns[1], button.CurrentFrame, "the horns skip their first frame, which repeats the flex's last");
            button.Tick(FRAME * 20f);
            Assert.AreSame(horns[horns.Length - 1], button.CurrentFrame);
            Assert.IsTrue(button.IsHornsComplete);
            button.SetHovered(true);
            Assert.AreEqual(1, hovers, "no hover sound after the click");
        }

        [Test]
        public void MainMenu_NewGameStartsTheFlowWithItsSoundsAndHidesTheMenu()
        {
            FrontEndView frontEnd = NewFrontEnd();
            FrontEndFlow flow = new FrontEndFlow(1f, 1f, 1f, 1f, true);
            FakeAudioControls audio = new FakeAudioControls();
            frontEnd.Initialize(flow, audio);
            MainMenuView menu = frontEnd.MainMenu;

            // the placeholders' clicks need OnEnable listeners, which only run in play mode
            menu.NewGameButton.SetHovered(true);
            menu.NewGameButton.Click();
            Assert.AreEqual(FrontEndPhase.FadingOut, flow.Phase);
            CollectionAssert.AreEqual(new[] { SoundCue.MainMenuHover, SoundCue.MainMenuNewGame }, audio.Played);

            frontEnd.Refresh();
            Assert.IsTrue(frontEnd.Fade.gameObject.activeSelf);
            Assert.IsTrue(frontEnd.Fade.blocksRaycasts, "the fade swallows clicks");
            Assert.IsFalse(menu.GetComponent<CanvasGroup>().interactable, "nothing else in the menu can be clicked during the fade");

            flow.Tick(1.5f);
            frontEnd.Refresh();
            Assert.IsFalse(menu.gameObject.activeSelf, "the menu is gone on black");
            flow.Tick(1f);
            frontEnd.Refresh();
            Assert.IsTrue(frontEnd.Intro.gameObject.activeSelf, "the placeholder intro hourglass plays");
            flow.Tick(2f);
            frontEnd.Refresh();
            Assert.IsFalse(frontEnd.gameObject.activeSelf, "the front end is hidden while playing");
        }

        [Test]
        public void FrontEndPrefab_DrawsAboveEverythingWithTheArtAndNestedWidgets()
        {
            FrontEndView prefab = ContentAssets.FrontEndPrefab;

            Assert.IsNotNull(prefab, "run Tools > Heroes of Metal Age > Create missing main menu assets");
            Canvas canvas = prefab.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, ContentAssets.DialogueOverlayPrefab.GetComponent<Canvas>().sortingOrder);
            CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);

            Assert.AreEqual(ContentAssets.END_DAY_OVERLAY_PATH, AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(prefab.Intro)),
                "the intro is the nested end-of-day hourglass");
            MainMenuView menu = prefab.MainMenu;
            foreach (UiWidget placeholder in new[] { menu.LoadGameButton, menu.CreditsButton, menu.QuitButton })
            {
                Assert.AreEqual(ContentAssets.UI_FOLDER + "/Widgets/Button.prefab", AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(placeholder)),
                    placeholder.name + " stays a nested button widget");
            }

            // the buttons stand on the right side of the screen, New Game at the top
            RectTransform newGame = (RectTransform)menu.NewGameButton.transform;
            RectTransform buttons = (RectTransform)newGame.parent;
            Assert.AreEqual(Vector2.one, buttons.anchorMin);
            Assert.AreEqual(0, newGame.GetSiblingIndex());
            Assert.AreEqual(6, Frames(menu.NewGameButton, "flexFrames").Length);
            Assert.AreEqual(6, Frames(menu.NewGameButton, "hornsFrames").Length);
        }

        [Test]
        public void Audio_FadesOutAfterNewGameAndReturnsWithTheMission()
        {
            GameObject host = new GameObject("front end audio test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
                FrontEndFlow flow = new FrontEndFlow(1f, 1f, 1f, 1f, true);
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(ContentAssets.Audio, ContentAssets.Units, ContentAssets.Mission, session, flow);
                MusicTrack? map = ContentAssets.Mission.MapMusic.HasClips ? MusicTrack.Map : (MusicTrack?)null;
                Assert.AreEqual(map, adapter.CurrentTrack, "the main menu has the map music");

                flow.StartNewGame();
                Assert.IsTrue(adapter.IsSilenced);
                Assert.IsNull(adapter.CurrentTrack, "the music fades out with the screen");
                flow.Tick(2.5f);
                Assert.AreEqual(FrontEndPhase.Intro, flow.Phase);
                Assert.IsNull(adapter.CurrentTrack, "silence on black and during the intro");

                flow.Tick(0.5f);
                Assert.AreEqual(FrontEndPhase.FadingIn, flow.Phase);
                Assert.IsFalse(adapter.IsSilenced);
                Assert.AreEqual(map, adapter.CurrentTrack, "the music fades in with the mission");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private FrontEndView NewFrontEnd()
        {
            FrontEndView view = (FrontEndView)PrefabUtility.InstantiatePrefab(ContentAssets.FrontEndPrefab);
            instance = view.gameObject;
            return view;
        }

        private static Sprite[] Frames(NewGameButtonView button, string field)
        {
            SerializedProperty property = new SerializedObject(button).FindProperty(field);
            Sprite[] frames = new Sprite[property.arraySize];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = (Sprite)property.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            return frames;
        }
    }
}
