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
            FrontEndFlow flow = new FrontEndFlow(Timing(black: 2f, burn: 3f, music: 10f), TwoStanzas(), true);
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
            FrontEndFlow flow = new FrontEndFlow(FrontEndTiming.Instant, IntroScript.None, true);
            flow.StartNewGame();
            flow.Tick(0f);
            Assert.IsTrue(flow.IsPlaying);

            Assert.IsTrue(new FrontEndFlow(Timing(), TwoStanzas(), false).IsPlaying);
        }

        [Test]
        public void Flow_LoadGameGoesToBlackAndBurnsWithoutTheIntro()
        {
            FrontEndFlow flow = new FrontEndFlow(Timing(fadeOut: 1f, loadOut: 3f, black: 2f, burn: 1f), TwoStanzas(), true);
            List<FrontEndPhase> phases = new List<FrontEndPhase>();
            flow.PhaseChanged += phases.Add;

            Assert.IsTrue(flow.StartLoadGame());
            Assert.IsFalse(flow.StartNewGame(), "one choice at a time");
            Assert.AreEqual(3f, flow.MusicFadeOutSeconds, "the music fades with Load Game's own way to black");
            flow.Tick(1.5f);
            Assert.AreEqual(0.5f, flow.Blackness, 1e-4f);
            flow.Tick(1.5f);
            Assert.AreEqual(FrontEndPhase.Black, flow.Phase);
            flow.Tick(2f);
            Assert.AreEqual(FrontEndPhase.Burning, flow.Phase);
            flow.Tick(1f);
            Assert.IsTrue(flow.IsPlaying);
            CollectionAssert.AreEqual(new[] { FrontEndPhase.FadingOut, FrontEndPhase.Black, FrontEndPhase.Burning, FrontEndPhase.Playing }, phases);
        }

        [Test]
        public void Flow_CreditsRollUntilAClickAndCloseBackToTheMenu()
        {
            FrontEndFlow flow = new FrontEndFlow(Timing(creditsOpen: 1f, creditsClose: 0.5f), TwoStanzas(), true);

            Assert.IsFalse(flow.CloseCredits(), "nothing to close in the menu");
            Assert.IsTrue(flow.OpenCredits());
            Assert.AreEqual(FrontEndChoice.Credits, flow.Choice);
            Assert.IsFalse(flow.CloseCredits(), "not while they open");
            flow.Tick(1f);
            Assert.AreEqual(FrontEndPhase.Credits, flow.Phase);
            flow.Tick(100f);
            Assert.AreEqual(FrontEndPhase.Credits, flow.Phase, "the credits wait for a click");
            Assert.AreEqual(100f, flow.Elapsed, 1e-3f, "and keep counting while they roll");
            Assert.AreEqual(0f, flow.Blackness);

            Assert.IsTrue(flow.CloseCredits());
            Assert.IsFalse(flow.CloseCredits());
            flow.Tick(0.5f);
            Assert.AreEqual(FrontEndPhase.MainMenu, flow.Phase);
            Assert.AreEqual(FrontEndChoice.None, flow.Choice);
            Assert.IsTrue(flow.StartNewGame(), "the menu takes a choice again");
        }

        [Test]
        public void Flow_QuitLetsTheRiderLeaveThenStops()
        {
            FrontEndFlow flow = new FrontEndFlow(Timing(quit: 2f), TwoStanzas(), true);
            List<FrontEndPhase> phases = new List<FrontEndPhase>();
            flow.PhaseChanged += phases.Add;

            Assert.IsTrue(flow.Quit());
            Assert.AreEqual(2f, flow.MusicFadeOutSeconds, "the music fades out with him");
            flow.Tick(1f);
            Assert.AreEqual(0.5f, flow.Progress, 1e-4f);
            flow.Tick(5f);
            Assert.AreEqual(FrontEndPhase.Quit, flow.Phase);
            Assert.IsFalse(flow.StartNewGame());
            Assert.IsFalse(flow.IsPlaying);
            CollectionAssert.AreEqual(new[] { FrontEndPhase.Quitting, FrontEndPhase.Quit }, phases);
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
            Assert.AreEqual(2f, last.Lines[0].PauseAfterSeconds, "a long pause before He must uncover");
            Assert.AreEqual("He must uncover", last.Lines[1].Text);
            Assert.AreEqual(3f, script.Stanzas[0].HoldSeconds);
            Assert.AreEqual(3f, script.Stanzas[1].HoldSeconds);
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
            Assert.AreEqual(1f, button.GildProgress, "the gold has poured over the title with the horns");
        }

        [Test]
        public void NewGameButton_PoursGoldOverItsOwnCopyOfTheTitleMaterialWithTheHorns()
        {
            NewGameButtonView button = NewFrontEnd().MainMenu.NewGameButton;
            Image title = (Image)new SerializedObject(button).FindProperty("titleImage").objectReferenceValue;
            button.Initialize();
            Assert.AreNotSame(ContentAssets.MenuGildMaterial, title.material, "the shared material asset never changes");
            Assert.AreEqual(ContentAssets.MENU_GILD_SHADER, title.material.shader.name);

            button.SetHovered(true);
            button.Tick(FRAME * 5f);
            Assert.AreEqual(0f, button.GildProgress, "hovering only flexes");
            button.Click();
            button.Tick(FRAME * 2f);
            Assert.Greater(button.GildProgress, 0f, "the gold starts with the horns");
            Assert.Less(button.GildProgress, 1f);
            Assert.AreEqual(button.GildProgress, title.material.GetFloat("_GildProgress"), 1e-4f);
            button.Tick(2f);
            Assert.AreEqual(1f, title.material.GetFloat("_GildProgress"));
        }

        [Test]
        public void MainMenu_NewGameStartsTheFlowWithItsSoundsAndHidesTheMenu()
        {
            FrontEndView frontEnd = NewFrontEnd();
            FrontEndFlow flow = new FrontEndFlow(Timing(burn: 3f), TwoStanzas(), true);
            FakeAudioControls audio = new FakeAudioControls();
            frontEnd.Initialize(flow, audio, "credits");
            MainMenuView menu = frontEnd.MainMenu;

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
            FrontEndFlow flow = new FrontEndFlow(Timing(fadeOut: 0f, black: 0f, burn: 3f), TwoStanzas(), true);
            frontEnd.Initialize(flow, new FakeAudioControls(), "credits");
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
            Assert.IsNotNull(menu.LoadGameButton, "run Tools > Heroes of Metal Age > Create missing main menu assets");
            Assert.IsNotNull(menu.CreditsButton);
            Assert.IsNotNull(menu.QuitButton);
            Assert.IsNotNull(prefab.Credits);
            Assert.IsNull(prefab.transform.Find("Overlays/Darkness"), "the camera falls into the hole instead of a spreading blot");

            // the buttons stand on the right side of the screen, New Game at the top
            RectTransform newGame = (RectTransform)menu.NewGameButton.transform;
            RectTransform buttons = (RectTransform)newGame.parent;
            Assert.AreEqual(Vector2.one, buttons.anchorMin);
            Assert.AreEqual(0, newGame.GetSiblingIndex());
            Assert.AreEqual(6, Frames(menu.NewGameButton, "flexFrames").Length);
            Assert.AreEqual(6, Frames(menu.NewGameButton, "hornsFrames").Length);
            RectTransform[] entries =
            {
                (RectTransform)menu.LoadGameButton.transform, (RectTransform)menu.CreditsButton.transform, (RectTransform)menu.QuitButton.transform
            };
            float bottom = newGame.rect.height;
            foreach (RectTransform entry in entries)
            {
                Assert.AreSame(buttons, entry.parent, entry.name + " stands in the button column");
                Assert.GreaterOrEqual(-entry.anchoredPosition.y, bottom, entry.name + " is below the entry above it");
                bottom = -entry.anchoredPosition.y + entry.rect.height;
            }

            Assert.LessOrEqual(bottom + 40f, 1080f, "the column fits the screen");
        }

        [Test]
        public void MenuPrefab_TheArmRisesOverTheGildedTitleAndEachEntryHasItsEffect()
        {
            MainMenuView menu = ContentAssets.FrontEndPrefab.MainMenu;
            SerializedObject newGame = new SerializedObject(menu.NewGameButton);
            Image title = (Image)newGame.FindProperty("titleImage").objectReferenceValue;
            Image arm = (Image)newGame.FindProperty("armImage").objectReferenceValue;
            Assert.AreSame(ContentAssets.MenuGildMaterial, title.material);
            Assert.Greater(arm.transform.GetSiblingIndex(), title.transform.GetSiblingIndex(), "the fist passes over the title");
            Assert.Greater(arm.rectTransform.offsetMin.x, 0f, "the arm moved right");
            Assert.Greater(arm.rectTransform.offsetMin.y, 0f, "and up, just under the title");

            Assert.IsTrue(menu.LoadGameCrumble.transform.IsChildOf(menu.LoadGameButton.transform));
            Assert.IsTrue(menu.LoadGameHole.IsChildOf(menu.LoadGameButton.transform));
            Assert.Less(menu.LoadGameHole.GetSiblingIndex(), menu.LoadGameCrumble.transform.GetSiblingIndex(), "the title lies in the hole");
            Assert.IsTrue(menu.CreditsScroll.transform.IsChildOf(menu.CreditsButton.transform));
            Assert.IsTrue(menu.QuitChopper.Chopper.transform.IsChildOf(menu.QuitButton.transform));
            Assert.IsNotNull(ContentAssets.FrontEndPrefab.Credits.GetComponentInChildren<RectMask2D>(true), "the credits roll in a clipped viewport");
            Assert.IsTrue(menu.LoadGameHole.IsChildOf(menu.DesignFrame), "the camera falls with everything the menu shows");
            Assert.IsTrue(menu.Parallax.transform.IsChildOf(menu.DesignFrame));
            Assert.AreEqual(6, menu.CreditsScroll.FrameCount);
            Assert.AreEqual(6, menu.QuitChopper.HoverFrameCount);
            Assert.AreEqual(7, menu.QuitChopper.DepartureFrameCount);
            Assert.Greater(menu.CreditsButton.transform.GetSiblingIndex(), menu.QuitButton.transform.GetSiblingIndex(), "the opened scroll lies over Quit");
        }

        [Test]
        public void Parallax_TheRestingCameraSwaysSidewaysBothWaysWithoutJumps()
        {
            float min = 0f;
            float max = 0f;
            float previous = ParallaxView.Drift(0f, 100f);
            for (float time = 0.1f; time < 240f; time += 0.1f)
            {
                float drift = ParallaxView.Drift(time, 100f);
                Assert.LessOrEqual(Mathf.Abs(drift), 100f, "never further than the sway");
                Assert.Less(Mathf.Abs(drift - previous), 6f, "a smooth drift, no jumps");
                min = Mathf.Min(min, drift);
                max = Mathf.Max(max, drift);
                previous = drift;
            }

            Assert.Less(min, -60f, "it drifts left");
            Assert.Greater(max, 60f, "and right");
        }

        [Test]
        public void Parallax_EachLayerMovesAgainstTheCameraByItsFactor()
        {
            ParallaxView parallax = NewFrontEnd().MainMenu.Parallax;
            parallax.Initialize();
            int last = parallax.LayerCount - 1;
            Vector2 sky = parallax.LayerRect(0).anchoredPosition;
            Vector2 rocks = parallax.LayerRect(last).anchoredPosition + parallax.CameraOffset * parallax.LayerFactor(last);
            Vector2 middle = parallax.LayerRect(last / 2).anchoredPosition + parallax.CameraOffset * parallax.LayerFactor(last / 2);

            Glide(parallax, 1f, new Vector2(0.9f, 0.8f));
            Vector2 offset = parallax.CameraOffset;
            Assert.AreNotEqual(Vector2.zero, offset);
            Assert.AreEqual(sky, parallax.LayerRect(0).anchoredPosition, "the sky stays");
            Assert.Less((rocks - offset * parallax.LayerFactor(last) - parallax.LayerRect(last).anchoredPosition).magnitude, 1e-3f, "the rocks move the most");
            Assert.Less((middle - offset * parallax.LayerFactor(last / 2) - parallax.LayerRect(last / 2).anchoredPosition).magnitude, 1e-3f);
        }

        [Test]
        public void Parallax_FollowsThePointerSwaysWhenItRestsAndHandsBackWhenItMoves()
        {
            ParallaxView parallax = NewFrontEnd().MainMenu.Parallax;
            parallax.Initialize();
            Vector2 amplitude = parallax.PointerAmplitude;

            Glide(parallax, 0.9f, new Vector2(1f, 1f));
            Assert.Greater(parallax.CameraOffset.x, amplitude.x * 0.8f, "the camera goes the way the pointer is");
            Assert.Greater(parallax.CameraOffset.y, amplitude.y * 0.8f, "up and down too");
            Glide(parallax, 0.9f, new Vector2(0f, 0.5f));
            Assert.Less(parallax.CameraOffset.x, -amplitude.x * 0.8f, "and follows it back");
            Assert.AreEqual(0f, parallax.SwayWeight, "the pointer leads while it moves");

            // resting longer than any idle delay, then longer than the blend
            Glide(parallax, 4.2f, new Vector2(0f, 0.5f));
            Assert.Greater(parallax.SwayWeight, 0f, "the sway starts once the pointer has rested");
            Glide(parallax, 3f, new Vector2(0f, 0.5f));
            Assert.AreEqual(1f, parallax.SwayWeight);
            Assert.AreEqual(0f, parallax.CameraOffset.y, 1e-4f, "the sway is sideways only");
            Assert.LessOrEqual(Mathf.Abs(parallax.CameraOffset.x), parallax.SwayAmplitude);

            // the pointer keeps moving, so it keeps the camera
            for (float time = 0f; time < 2f; time += 1f / 60f)
            {
                parallax.Tick(1f / 60f, new Vector2(0.6f + time * 0.1f, 0.5f));
                if (time < 0.2f)
                {
                    Assert.Less(parallax.SwayWeight, 1f, "a moving pointer takes the camera back");
                }
            }

            Assert.AreEqual(0f, parallax.SwayWeight);
            Assert.Greater(parallax.CameraOffset.x, 0f, "towards the pointer on the right");
        }

        [Test]
        public void ParallaxPrefab_LayersBackToFrontCoverTheScreenAtEveryMove()
        {
            ParallaxView parallax = ContentAssets.FrontEndPrefab.MainMenu.Parallax;

            Assert.IsNotNull(parallax, "run Tools > Heroes of Metal Age > Create missing main menu assets");
            Assert.AreEqual(11, parallax.LayerCount);
            Assert.AreEqual(0f, parallax.LayerFactor(0), "the sky is still");
            Assert.AreEqual(0, parallax.transform.GetSiblingIndex(), "behind the buttons");
            Assert.IsNotNull(parallax.GetComponent<Canvas>(), "its own canvas, so the moving layers do not rebuild the buttons");
            Assert.IsNull(parallax.transform.parent.Find("Background"), "the single background image is gone");
            float sideways = Mathf.Max(parallax.SwayAmplitude, parallax.PointerAmplitude.x);
            for (int i = 0; i < parallax.LayerCount; i++)
            {
                RectTransform layer = parallax.LayerRect(i);
                Image image = layer.GetComponent<Image>();
                Assert.IsNotNull(image.sprite, layer.name);
                Assert.IsFalse(image.raycastTarget, layer.name + " takes no clicks");
                Assert.AreEqual(i, layer.GetSiblingIndex(), "drawn back to front");
                Assert.AreEqual(1080f, layer.rect.height, 0.5f, layer.name);
                // a 21:9 screen is 2520 wide at this height
                Assert.GreaterOrEqual(layer.rect.width * parallax.Overscan * 0.5f - sideways * parallax.LayerFactor(i), 1260f,
                    layer.name + " covers a wide screen at the furthest move");
                Assert.GreaterOrEqual(1080f * (parallax.Overscan - 1f) * 0.5f, parallax.PointerAmplitude.y * parallax.LayerFactor(i),
                    layer.name + " never shows its top or bottom edge");
                if (i > 0)
                {
                    Assert.GreaterOrEqual(parallax.LayerFactor(i), parallax.LayerFactor(i - 1), "nearer layers move more");
                }
            }
        }

        [Test]
        public void LoadGame_TitleFallsIntoTheHoleTheCameraFallsInAfterItAndTheBlackBurnsWithoutTheIntro()
        {
            FrontEndView frontEnd = NewFrontEnd();
            FrontEndFlow flow = new FrontEndFlow(Timing(loadOut: 2f, black: 1f, burn: 2f), TwoStanzas(), true);
            FakeAudioControls audio = new FakeAudioControls();
            frontEnd.Initialize(flow, audio, "credits");
            MainMenuView menu = frontEnd.MainMenu;
            RectTransform frame = menu.DesignFrame;
            RectTransform hole = menu.LoadGameHole;
            RectTransform screen = (RectTransform)menu.transform;

            menu.LoadGameButton.Click();
            menu.LoadGameButton.Click();
            Assert.AreEqual(FrontEndPhase.FadingOut, flow.Phase);
            Assert.AreEqual(FrontEndChoice.LoadGame, flow.Choice);
            CollectionAssert.AreEqual(new[] { SoundCue.Click }, audio.Played, "one click sound");

            flow.Tick(0.25f);
            frontEnd.Refresh();
            Assert.Greater(menu.LoadGameCrumble.Progress, 0f, "the title crumbles");
            Assert.AreEqual(0f, frontEnd.Fade.alpha, "no black yet");
            float early = frame.localScale.x;
            Assert.GreaterOrEqual(early, 1f, "the camera starts to fall slowly");
            Assert.Less(early, 1.2f);

            flow.Tick(0.75f);
            frontEnd.Refresh();
            Assert.AreEqual(1f, menu.LoadGameCrumble.Progress, "the title has fallen into the hole");
            float middle = frame.localScale.x;
            Assert.Greater(middle, early);

            flow.Tick(0.9f);
            frontEnd.Refresh();
            Assert.Greater(frame.localScale.x - middle, middle - early, "faster and faster");
            Vector2 holeOnScreen = screen.InverseTransformPoint(hole.TransformPoint(hole.rect.center));
            Assert.Less((holeOnScreen - screen.rect.center).magnitude, 1f, "the hole is in the middle of the screen");
            Assert.AreNotEqual(Quaternion.identity, frame.localRotation, "sucked into a whirl");
            Assert.Greater(frontEnd.Fade.alpha, 0f, "the black closes in at the end of the fall");

            flow.Tick(0.2f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.Black, flow.Phase);
            Assert.IsTrue(frontEnd.Fade.gameObject.activeSelf);
            Assert.AreEqual(1f, frontEnd.Fade.alpha);

            flow.Tick(1.5f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.Burning, flow.Phase, "no verses and no title for Load Game");
            Assert.IsTrue(frontEnd.Intro.gameObject.activeSelf);
            Assert.IsFalse(frontEnd.Intro.TitleText.gameObject.activeSelf, "the plain black burns");
            flow.Tick(2f);
            frontEnd.Refresh();
            Assert.IsTrue(flow.IsPlaying);
        }

        [Test]
        public void Credits_ScrollUnrollsTheCreditsRollAndAnyClickGoesBackToTheMenu()
        {
            FrontEndView frontEnd = NewFrontEnd();
            FrontEndFlow flow = new FrontEndFlow(Timing(creditsOpen: 2f, creditsClose: 1f), TwoStanzas(), true);
            FakeAudioControls audio = new FakeAudioControls();
            frontEnd.Initialize(flow, audio, "Heroes of Metal Age\n\nA\nB\nC");
            MainMenuView menu = frontEnd.MainMenu;
            Sprite[] frames = Frames(menu.CreditsScroll, "frames");

            Assert.AreSame(frames[0], menu.CreditsScroll.CurrentFrame, "the scroll is rolled and sealed");
            menu.CreditsButton.Click();
            Assert.AreEqual(FrontEndPhase.OpeningCredits, flow.Phase);
            flow.Tick(0.5f);
            frontEnd.Refresh();
            Assert.AreNotSame(frames[0], menu.CreditsScroll.CurrentFrame, "the scroll unrolls");
            Assert.IsFalse(frontEnd.Credits.gameObject.activeSelf && frontEnd.Credits.GetComponent<CanvasGroup>().alpha > 0f,
                "the credits screen waits for the scroll");
            flow.Tick(1f);
            frontEnd.Refresh();
            Assert.AreSame(frames[frames.Length - 1], menu.CreditsScroll.CurrentFrame, "unrolled");
            Assert.Greater(frontEnd.Credits.GetComponent<CanvasGroup>().alpha, 0f, "the credits screen fades in");

            frontEnd.Credits.ClickBack();
            Assert.AreEqual(FrontEndPhase.OpeningCredits, flow.Phase, "no way back while it opens");
            flow.Tick(1f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.Credits, flow.Phase);
            Assert.AreEqual("Heroes of Metal Age\n\nA\nB\nC", frontEnd.Credits.CreditsText.text);
            Assert.IsFalse(menu.gameObject.activeSelf, "the menu hides under the credits");
            float offset = frontEnd.Credits.ScrollOffset;
            flow.Tick(1f);
            frontEnd.Refresh();
            Assert.Greater(frontEnd.Credits.ScrollOffset, offset, "the credits roll up");

            frontEnd.Credits.ClickBack();
            Assert.AreEqual(FrontEndPhase.ClosingCredits, flow.Phase);
            flow.Tick(1f);
            frontEnd.Refresh();
            Assert.AreEqual(FrontEndPhase.MainMenu, flow.Phase);
            Assert.AreEqual(FrontEndChoice.None, flow.Choice);
            Assert.IsFalse(frontEnd.Credits.gameObject.activeSelf);
            Assert.IsTrue(menu.gameObject.activeSelf);
            Assert.AreSame(frames[0], menu.CreditsScroll.CurrentFrame, "the scroll is rolled up again");
            Assert.IsTrue(menu.GetComponent<CanvasGroup>().interactable);
            CollectionAssert.AreEqual(new[] { SoundCue.Click, SoundCue.Click }, audio.Played);

            menu.CreditsButton.Click();
            Assert.AreEqual(FrontEndPhase.OpeningCredits, flow.Phase, "the entry takes clicks again");
        }

        [Test]
        public void Quit_HoverLiftsTheFootAClickFinishesItThenTheRiderLeavesAndTheFlowAsksToClose()
        {
            FrontEndView frontEnd = NewFrontEnd();
            FrontEndFlow flow = new FrontEndFlow(Timing(quit: 2f), TwoStanzas(), true);
            frontEnd.Initialize(flow, new FakeAudioControls(), "credits");
            MainMenuView menu = frontEnd.MainMenu;
            ChopperView chopper = menu.QuitChopper;
            Sprite[] hover = Frames(chopper, "hoverFrames");
            Sprite[] departure = Frames(chopper, "departureFrames");
            Assert.AreSame(hover[0], chopper.CurrentFrame, "the rider at rest");

            menu.QuitButton.SetHovered(true);
            frontEnd.Refresh();
            chopper.Tick(2.5f / 12f);
            Assert.AreSame(hover[2], chopper.CurrentFrame, "hovering lifts his foot, twelve frames a second");
            menu.QuitButton.SetHovered(false);
            frontEnd.Refresh();
            chopper.Tick(1f / 12f);
            Assert.AreSame(hover[1], chopper.CurrentFrame, "leaving puts it back down");
            menu.QuitButton.SetHovered(true);
            frontEnd.Refresh();
            chopper.Tick(1f / 12f);

            menu.QuitButton.Click();
            Assert.AreEqual(FrontEndPhase.Quitting, flow.Phase);
            frontEnd.Refresh();
            Assert.AreSame(hover[3], chopper.CurrentFrame, "first he finishes lifting his foot");
            flow.Tick(1.2f);
            frontEnd.Refresh();
            Assert.Contains(chopper.CurrentFrame, departure, "then he rides off");
            Assert.AreNotSame(departure[0], chopper.CurrentFrame, "the departure skips its first frame, the last hover frame");
            chopper.Tick(1f);
            Assert.Contains(chopper.CurrentFrame, departure, "the pointer no longer moves him once he leaves");
            flow.Tick(1f);
            Assert.AreEqual(FrontEndPhase.Quit, flow.Phase);
            frontEnd.Refresh();
            Assert.AreSame(departure[departure.Length - 1], chopper.CurrentFrame, "gone");
            Assert.IsTrue(menu.gameObject.activeSelf, "the menu stays while he leaves");
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
                FrontEndFlow flow = new FrontEndFlow(Timing(music: 10f), TwoStanzas(), true);
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

        [Test]
        public void Audio_ReturnsWithTheBurnAfterLoadGameAndFadesOutOnQuit()
        {
            GameObject host = new GameObject("front end audio test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
                FrontEndFlow flow = new FrontEndFlow(Timing(loadOut: 2f, black: 1f, burn: 1f), TwoStanzas(), true);
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(ContentAssets.Audio, ContentAssets.Units, ContentAssets.Mission, session, flow);
                MusicTrack? map = ContentAssets.Mission.MapMusic.HasClips ? MusicTrack.Map : (MusicTrack?)null;

                flow.StartLoadGame();
                Assert.IsTrue(adapter.IsSilenced);
                flow.Tick(2.5f);
                Assert.IsTrue(adapter.IsSilenced, "silence on black");
                flow.Tick(0.6f);
                Assert.AreEqual(FrontEndPhase.Burning, flow.Phase);
                Assert.IsFalse(adapter.IsSilenced);
                Assert.AreEqual(map, adapter.CurrentTrack, "the map music comes back as the black burns");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            host = new GameObject("front end quit audio test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
                FrontEndFlow flow = new FrontEndFlow(Timing(quit: 1f), TwoStanzas(), true);
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(ContentAssets.Audio, ContentAssets.Units, ContentAssets.Mission, session, flow);

                flow.Quit();
                Assert.IsTrue(adapter.IsSilenced, "the music leaves with the rider");
                Assert.IsNull(adapter.CurrentTrack);
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

        private static FrontEndTiming Timing(float fadeOut = 1f, float loadOut = 1f, float black = 1f, float burn = 1f, float music = 1f,
            float creditsOpen = 1f, float creditsClose = 1f, float quit = 1f)
        {
            return new FrontEndTiming(fadeOut, loadOut, black, burn, music, creditsOpen, creditsClose, quit);
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

        // advances in frames of a sixtieth of a second, as the menu would
        private static void Glide(ParallaxView parallax, float seconds, Vector2 pointer)
        {
            for (float time = 0f; time < seconds; time += 1f / 60f)
            {
                parallax.Tick(1f / 60f, pointer);
            }
        }

        private static Sprite[] Frames(Object view, string field)
        {
            SerializedProperty property = new SerializedObject(view).FindProperty(field);
            Sprite[] frames = new Sprite[property.arraySize];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = (Sprite)property.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            return frames;
        }
    }
}
