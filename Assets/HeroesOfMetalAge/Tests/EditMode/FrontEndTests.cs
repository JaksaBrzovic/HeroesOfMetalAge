using System;
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
        public void Flow_FadesOutWaitsOnBlackPlaysTheVersesAndTitleAndBurns()
        {
            FrontEndFlow flow = new FrontEndFlow(1f, 2f, TwoStanzas(), 3f, 10f, true);
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
            Assert.AreEqual(FrontEndPhase.Verses, flow.Phase);
            flow.Tick(8f);
            Assert.AreEqual(FrontEndPhase.Verses, flow.Phase, "the verses take as long as the script");
            Assert.AreEqual(8f, flow.Elapsed, 1e-4f);
            flow.Tick(1.3f);
            Assert.AreEqual(FrontEndPhase.Title, flow.Phase);
            Assert.AreEqual(1f, flow.Elapsed, 1e-3f, "the rest of a long frame carries into the next phase");
            Assert.AreEqual(1f, flow.Blackness, "the intro plays on black");

            flow.Tick(2f);
            Assert.AreEqual(FrontEndPhase.Burning, flow.Phase);
            flow.Tick(1.5f);
            Assert.AreEqual(0.5f, flow.Blackness, 1e-3f, "the black burns away");
            Assert.IsFalse(flow.IsPlaying, "no input while the screen burns");

            flow.Tick(1.5f);
            Assert.IsTrue(flow.IsPlaying);
            Assert.AreEqual(0f, flow.Blackness);
            CollectionAssert.AreEqual(new[]
                {
                    FrontEndPhase.FadingOut, FrontEndPhase.Black, FrontEndPhase.Verses, FrontEndPhase.Title, FrontEndPhase.Burning,
                    FrontEndPhase.Playing
                },
                phases);
            Assert.IsFalse(flow.StartNewGame());
        }

        [Test]
        public void Flow_ZeroDurationsFinishAtOnceAndAutomatedRunsSkipTheMenu()
        {
            FrontEndFlow flow = new FrontEndFlow(0f, 0f, IntroScript.None, 0f, 0f, true);
            flow.StartNewGame();
            flow.Tick(0f);
            Assert.IsTrue(flow.IsPlaying);

            Assert.IsTrue(new FrontEndFlow(1f, 1f, TwoStanzas(), 1f, 1f, false).IsPlaying);
        }

        [Test]
        public void IntroScript_TypesLinesLetterByLetterHoldsFadesAndPausesBetweenStanzas()
        {
            IntroScript script = TwoStanzas();

            Assert.AreEqual(1, script.VisibleCharacters(0, 0, 0f), "the first letter shows as the line starts");
            Assert.AreEqual(3, script.VisibleCharacters(0, 0, 0.25f), "ten letters per second");
            Assert.AreEqual(5, script.VisibleCharacters(0, 0, 0.6f));
            Assert.AreEqual(0, script.VisibleCharacters(0, 1, 1.4f), "the next line waits for the pause after the first");
            Assert.AreEqual(1, script.VisibleCharacters(0, 1, 1.5f));
            Assert.AreEqual(5, script.VisibleCharacters(0, 1, 3.9f));

            Assert.AreEqual(0, script.StanzaAt(3.9f));
            Assert.AreEqual(1f, script.StanzaAlpha(0, 3.9f), "the typed stanza holds");
            Assert.AreEqual(0.5f, script.StanzaAlpha(0, 4.25f), 1e-4f, "then fades out");
            Assert.AreEqual(-1, script.StanzaAt(5f), "black between the stanzas");
            Assert.AreEqual(1, script.StanzaAt(5.5f));
            Assert.AreEqual(1, script.VisibleCharacters(1, 0, 5.5f));
            Assert.AreEqual(-1, script.StanzaAt(8f), "black before the title");
            Assert.AreEqual(8.3f, script.VersesSeconds, 1e-4f);

            Assert.AreEqual(3f, script.TitleSeconds, 1e-4f);
            Assert.AreEqual(0.5f, script.TitleAlpha(1f), 1e-4f, "the title fades in slowly");
            Assert.AreEqual(1f, script.TitleAlpha(2.5f));
            Assert.AreEqual(2, script.MaxLines);
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntroScript(new IntroStanza[0], "", 0f, 0f, 0f, 0f, 0f));
        }

        [Test]
        public void IntroContent_HasTheThreeStanzasAndTheTitle()
        {
            IntroScript script = ContentAssets.FrontEnd.CreateIntroScript();

            Assert.AreEqual(3, script.Stanzas.Count);
            Assert.AreEqual("As the day breaks, he is already awake", script.Stanzas[0].Lines[0].Text);
            Assert.AreEqual("A stream of life", script.Stanzas[1].Lines[2].Text);
            IntroStanza last = script.Stanzas[2];
            Assert.AreEqual("But first", last.Lines[0].Text);
            Assert.AreEqual(3f, last.Lines[0].PauseAfterSeconds, "a long pause before He must uncover");
            Assert.AreEqual("He must uncover", last.Lines[1].Text);
            Assert.AreEqual(5f, script.Stanzas[0].HoldSeconds);
            Assert.AreEqual(5f, script.Stanzas[1].HoldSeconds);
            Assert.AreEqual(2f, last.HoldSeconds);
            Assert.AreEqual("The Secret of Metal", script.Title);
            Assert.LessOrEqual(script.MaxLines, ContentAssets.FrontEndPrefab.Intro.LineTexts.Count, "every stanza fits the prefab's line texts");
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
            FrontEndFlow flow = new FrontEndFlow(1f, 1f, TwoStanzas(), 3f, 1f, true);
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
            Assert.IsFalse(frontEnd.Intro.gameObject.activeSelf);
        }

        [Test]
        public void Intro_TypesTheStanzasShowsTheTitleAndBurnsTheCurtain()
        {
            FrontEndView frontEnd = NewFrontEnd();
            FrontEndFlow flow = new FrontEndFlow(0f, 0f, TwoStanzas(), 3f, 1f, true);
            frontEnd.Initialize(flow, new FakeAudioControls());
            IntroView intro = frontEnd.Intro;

            flow.StartNewGame();
            flow.Tick(0.25f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.Verses, flow.Phase);
            Assert.IsTrue(intro.gameObject.activeSelf);
            Assert.IsFalse(frontEnd.Fade.gameObject.activeSelf, "the curtain takes over from the fade");
            Assert.IsTrue(intro.Curtain.raycastTarget, "the curtain swallows clicks");
            Assert.AreEqual(1f, intro.Curtain.color.a, "the curtain is whole");
            Assert.AreEqual("abcde", intro.LineTexts[0].text, "the whole stanza is laid out at once");
            Assert.AreEqual(3, intro.LineTexts[0].maxVisibleCharacters);
            Assert.AreEqual("fghij", intro.LineTexts[1].text);
            Assert.AreEqual(0, intro.LineTexts[1].maxVisibleCharacters);
            for (int i = 2; i < intro.LineTexts.Count; i++)
            {
                Assert.IsFalse(intro.LineTexts[i].gameObject.activeSelf, "a shorter stanza hides the unused lines");
            }

            Assert.IsFalse(intro.TitleText.gameObject.activeSelf);

            flow.Tick(4f);
            frontEnd.Refresh();
            Assert.AreEqual(0.5f, intro.Verses.alpha, 1e-4f, "the stanza fades out");
            flow.Tick(1f);
            frontEnd.Refresh();
            Assert.IsFalse(intro.Verses.gameObject.activeSelf, "black between the stanzas");
            flow.Tick(0.5f);
            frontEnd.Refresh();
            Assert.AreEqual("klm", intro.LineTexts[0].text);
            Assert.IsFalse(intro.LineTexts[1].gameObject.activeSelf);

            flow.Tick(3.55f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.Title, flow.Phase);
            Assert.IsFalse(intro.Verses.gameObject.activeSelf);
            Assert.IsTrue(intro.TitleText.gameObject.activeSelf);
            Assert.AreEqual("TITLE", intro.TitleText.text);
            Assert.AreEqual(0.5f, intro.TitleText.alpha, 1e-3f, "the title fades in");

            flow.Tick(2.5f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.Burning, flow.Phase);
            Assert.AreEqual(1f, intro.TitleText.alpha, "the title burns with the curtain");
            Assert.AreEqual(5f / 6f, intro.Curtain.color.a, 1e-3f, "the curtain's alpha tells the burn material how much is left");

            flow.Tick(3f);
            frontEnd.Refresh();
            Assert.IsTrue(flow.IsPlaying);
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
        public void IntroPrefab_BurnsTheCurtainWithTheTextsCutOut()
        {
            IntroView intro = ContentAssets.FrontEndPrefab.Intro;

            Assert.IsNotNull(intro, "run Tools > Heroes of Metal Age > Create missing main menu assets");
            Assert.AreSame(ContentAssets.IntroBurnMaterial, intro.Curtain.material);
            Assert.AreEqual(ContentAssets.INTRO_BURN_SHADER, intro.Curtain.material.shader.name);
            Mask mask = intro.Curtain.GetComponent<Mask>();
            Assert.IsNotNull(mask, "the Mask cuts the title out of the burnt holes");
            Assert.IsTrue(mask.showMaskGraphic);
            Assert.IsTrue(intro.TitleText.transform.IsChildOf(intro.Curtain.transform));
            RectTransform curtain = intro.Curtain.rectTransform;
            Assert.AreEqual(Vector2.zero, curtain.anchorMin, "the curtain covers the whole screen");
            Assert.AreEqual(Vector2.one, curtain.anchorMax);
            Assert.Greater(curtain.GetSiblingIndex(), ContentAssets.FrontEndPrefab.Fade.transform.GetSiblingIndex(), "the curtain draws over the fade");
        }

        [Test]
        public void Audio_FadesOutAfterNewGameAndReturnsWithTheTitle()
        {
            GameObject host = new GameObject("front end audio test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
                FrontEndFlow flow = new FrontEndFlow(1f, 1f, TwoStanzas(), 1f, 10f, true);
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(ContentAssets.Audio, ContentAssets.Units, ContentAssets.Mission, session, flow);
                MusicTrack? map = ContentAssets.Mission.MapMusic.HasClips ? MusicTrack.Map : (MusicTrack?)null;
                Assert.AreEqual(map, adapter.CurrentTrack, "the main menu has the map music");

                flow.StartNewGame();
                Assert.IsTrue(adapter.IsSilenced);
                Assert.IsNull(adapter.CurrentTrack, "the music fades out with the screen");
                flow.Tick(10f);
                Assert.AreEqual(FrontEndPhase.Verses, flow.Phase);
                Assert.IsNull(adapter.CurrentTrack, "silence on black and during the verses");

                flow.Tick(0.5f);
                Assert.AreEqual(FrontEndPhase.Title, flow.Phase);
                Assert.IsFalse(adapter.IsSilenced);
                Assert.AreEqual(map, adapter.CurrentTrack, "the map music fades in with the title");
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

        // ten letters per second: "abcde" is typed in half a second
        private static IntroScript TwoStanzas()
        {
            return new IntroScript(new[]
                {
                    new IntroStanza(new[] { new IntroLine("abcde", 1f), new IntroLine("fghij", 1f) }, 2f),
                    new IntroStanza(new[] { new IntroLine("klm", 0f) }, 1f)
                },
                "TITLE", 10f, 0.5f, 1f, 2f, 1f);
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
