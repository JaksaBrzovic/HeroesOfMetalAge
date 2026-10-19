using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>The story scroll that starts a new mission: its pages, the explosion and the throw.</summary>
    public class BriefingTests
    {
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
        public void Briefing_WaitsOpensExplodesOnceTurnsThePageAndIsThrownAwayBeforeTheMissionStarts()
        {
            MissionBriefingView briefing = NewBriefing();
            float open = Seconds(briefing, "openSeconds");
            float close = Seconds(briefing, "closeSeconds");
            float delay = Seconds(briefing, "explosionDelay");
            float throwing = Seconds(briefing, "throwSeconds");
            GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
            FakeAudioControls audio = new FakeAudioControls();
            bool canStart = false;
            int shakes = 0;
            briefing.Initialize(session, audio, new[] { "one", "two" }, () => canStart, (seconds, amplitude) => shakes++);
            Assert.AreEqual(DialogKind.Intro, session.Dialog);

            briefing.Tick(1f);
            Assert.AreEqual(BriefingPhase.Hidden, briefing.Phase, "it waits for the front end");
            Assert.IsFalse(briefing.Content.gameObject.activeSelf);

            canStart = true;
            briefing.Tick(0.01f);
            Assert.AreEqual(BriefingPhase.Opening, briefing.Phase);
            Assert.IsTrue(briefing.Content.gameObject.activeSelf);
            Assert.AreEqual("one", briefing.PageText.text);
            briefing.Advance();
            Assert.AreEqual(BriefingPhase.Opening, briefing.Phase, "a click while it unrolls is ignored");

            briefing.Tick(open);
            Assert.AreEqual(BriefingPhase.Reading, briefing.Phase);
            briefing.Tick(delay * 0.5f);
            Assert.AreEqual(0f, briefing.ExplosionProgress, "the bomb waits a moment after the scroll has opened");
            briefing.Tick(delay);
            briefing.Tick(0.05f);
            Assert.Greater(briefing.ExplosionProgress, 0f, "then it goes off");
            Assert.IsTrue(briefing.Explosion.gameObject.activeSelf);
            Assert.IsTrue(briefing.Flash.gameObject.activeSelf, "the screen flashes white");
            Assert.AreEqual(1, shakes, "the whole screen shakes");
            CollectionAssert.AreEqual(new[] { SoundCue.BriefingExplosion }, audio.Played, "with a big boom");

            briefing.Advance();
            Assert.AreEqual(BriefingPhase.Closing, briefing.Phase);
            briefing.Tick(close);
            Assert.AreEqual(BriefingPhase.Opening, briefing.Phase, "it opens again");
            Assert.AreEqual(1, briefing.Page);
            Assert.AreEqual("two", briefing.PageText.text, "with the next page");
            briefing.Tick(open);
            briefing.Tick(delay * 2f);
            Assert.AreEqual(1, audio.Played.Count(cue => cue == SoundCue.BriefingExplosion), "one explosion only");

            briefing.Advance();
            briefing.Tick(close);
            Assert.AreEqual(BriefingPhase.Throwing, briefing.Phase, "after the last page it is thrown away");
            Assert.AreEqual(DialogKind.Intro, session.Dialog, "the mission waits for the throw");
            briefing.Tick(throwing * 0.5f);
            Assert.Less(briefing.Content.localScale.x, 1f, "flying off into the distance");
            Assert.AreNotEqual(0f, briefing.Content.localEulerAngles.z, "spinning");
            briefing.Tick(throwing);
            Assert.AreEqual(BriefingPhase.Hidden, briefing.Phase);
            Assert.AreEqual(DialogKind.None, session.Dialog, "the mission starts");
            Assert.IsFalse(briefing.Content.gameObject.activeSelf);
        }

        [Test]
        public void Briefing_StepsAsideWhenTheIntroEndsAnotherWayAndSkipsWithoutPages()
        {
            MissionBriefingView briefing = NewBriefing();
            GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
            briefing.Initialize(session, new FakeAudioControls(), new[] { "one" }, () => true, null);
            briefing.Tick(0.01f);
            Assert.AreEqual(BriefingPhase.Opening, briefing.Phase);

            session.StartMission();
            briefing.Tick(0.01f);
            Assert.AreEqual(BriefingPhase.Hidden, briefing.Phase, "a campaign loaded another way ends the briefing");
            Assert.IsFalse(briefing.Content.gameObject.activeSelf);

            session.Restart();
            Object.DestroyImmediate(instance);
            briefing = NewBriefing();
            briefing.Initialize(session, new FakeAudioControls(), new string[0], () => true, null);
            briefing.Tick(0.01f);
            Assert.AreEqual(DialogKind.None, session.Dialog, "no story starts the mission at once");
        }

        [Test]
        public void BriefingPrefab_DrawsBetweenTheGameAndTheFrontEndWithTheScrollAndTheExplosion()
        {
            MissionBriefingView prefab = ContentAssets.MissionBriefingPrefab;

            Assert.IsNotNull(prefab, "run Tools > Heroes of Metal Age > Create missing mission briefing assets");
            int order = prefab.GetComponent<Canvas>().sortingOrder;
            Assert.Greater(order, ContentAssets.DialogueOverlayPrefab.GetComponent<Canvas>().sortingOrder, "above the dialogue");
            Assert.Less(order, ContentAssets.FrontEndPrefab.GetComponent<Canvas>().sortingOrder, "below the front end");
            Assert.AreEqual(6, prefab.ScrollFrameCount);
            Assert.AreSame(ContentAssets.NukeMaterial, prefab.Explosion.material);
            Assert.AreEqual(ContentAssets.NUKE_SHADER, prefab.Explosion.material.shader.name);
            Assert.IsFalse(prefab.Explosion.raycastTarget);
            Assert.IsFalse(prefab.Flash.raycastTarget);
            Assert.IsTrue(prefab.PageText.transform.IsChildOf(prefab.Content), "the text flies away with the scroll");
            Assert.Greater(prefab.Explosion.transform.GetSiblingIndex(), prefab.Content.GetSiblingIndex(), "the explosion goes off over the scroll");
            Assert.Greater(prefab.Flash.transform.GetSiblingIndex(), prefab.Explosion.transform.GetSiblingIndex());
        }

        [Test]
        public void BriefingContent_TellsTheMissionsStoryOnTwoPages()
        {
            System.Collections.Generic.IReadOnlyList<string> pages = ContentAssets.Mission.BriefingPages;

            Assert.AreEqual(2, pages.Count);
            StringAssert.StartsWith("On the first day, our hero collects his friends", pages[0]);
            StringAssert.Contains("\n", pages[0], "the second sentence starts a new line");
            StringAssert.Contains("passage of Psur", pages[1]);
            Assert.IsTrue(ContentAssets.Audio.Sound(SoundCue.BriefingExplosion).HasClips, "the boom has a clip");
        }

        private MissionBriefingView NewBriefing()
        {
            MissionBriefingView view = (MissionBriefingView)PrefabUtility.InstantiatePrefab(ContentAssets.MissionBriefingPrefab);
            instance = view.gameObject;
            return view;
        }

        private static float Seconds(MissionBriefingView view, string field)
        {
            return new SerializedObject(view).FindProperty(field).floatValue;
        }
    }
}
