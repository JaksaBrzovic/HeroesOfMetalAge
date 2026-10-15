using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Bootstrap;
using HeroesOfMetalAge.Dialogue;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Infrastructure.Audio;
using MLin.Audio;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>The dialogue database, typing, conversation flow, voice-overs, placement and the authored dialogue prefabs.</summary>
    public class DialogueTests
    {
        private const float CHARACTERS_PER_SECOND = 100f;

        private DialogueDatabaseScriptableObject database;
        private SoundDefinition voiceOver;
        private DialogueDirector director;
        private FakeDialogueVoice voice;

        [SetUp]
        public void SetUp()
        {
            voiceOver = ScriptableObject.CreateInstance<SoundDefinition>();
            voiceOver.Clips = new[] { AudioClip.Create("line", 4410, 1, 44100, false) };
            database = NewDatabase(
                Conversation("greeting", Line("greeting-01", "kael", "Hello there.", voiceOver), Line("greeting-02", "varg", "Leave.")),
                Conversation("short", Line("short-01", "kael", "Hi.")),
                Conversation("balloon", DialogueStyle.SpeechBalloon, Line("balloon-01", "varg", "Leave now.")));
            voice = new FakeDialogueVoice();
        }

        [TearDown]
        public void TearDown()
        {
            if (director != null)
            {
                Object.DestroyImmediate(director.gameObject);
            }

            Object.DestroyImmediate(voiceOver.Clips[0]);
            Object.DestroyImmediate(voiceOver);
            Object.DestroyImmediate(database);
        }

        [Test]
        public void Typewriter_RevealsAtItsRateAndCompletesAtOnce()
        {
            DialogueTypewriter typewriter = new DialogueTypewriter(50f);
            typewriter.Begin(10);

            Assert.AreEqual(0, typewriter.VisibleCharacters);
            Assert.IsTrue(typewriter.Advance(0.1f));
            Assert.AreEqual(5, typewriter.VisibleCharacters);
            Assert.IsFalse(typewriter.Advance(0f), "no time, no change");
            typewriter.Advance(10f);
            Assert.AreEqual(10, typewriter.VisibleCharacters, "never past the line");
            Assert.IsTrue(typewriter.IsComplete);

            typewriter.Begin(8);
            typewriter.Complete();
            Assert.AreEqual(8, typewriter.VisibleCharacters);
        }

        [Test]
        public void AuthoredDatabase_IsValidAndVoiceOversUseTheVoiceChannel()
        {
            DialogueDatabaseScriptableObject authored = ContentAssets.Dialogue;

            Assert.IsNotNull(authored, "run Tools > Heroes of Metal Age > Create missing dialogue assets");
            CollectionAssert.IsEmpty(authored.Validate());
            foreach (DialogueLineEntry line in authored.Conversations.SelectMany(conversation => conversation.Lines).Where(line => line.VoiceOver != null))
            {
                Assert.IsTrue(line.VoiceOver.OutputGroup != null && line.VoiceOver.OutputGroup.name == GameAudio.VOICE_CHANNEL, line.Id + " voice channel");
            }
        }

        [Test]
        public void Validation_NamesDuplicateIdsUnknownSpeakersAndEmptyText()
        {
            DialogueDatabaseScriptableObject broken = NewDatabase(
                Conversation("a", Line("line", "kael", "One."), Line("line", "nobody", " ")),
                Conversation("a"));
            try
            {
                string errors = string.Join("\n", broken.Validate());

                StringAssert.Contains("Duplicate line ID 'line'", errors);
                StringAssert.Contains("unknown speaker 'nobody'", errors);
                StringAssert.Contains("has no text", errors);
                StringAssert.Contains("Duplicate conversation ID 'a'", errors);
                StringAssert.Contains("Conversation 'a' has no lines", errors);
                Assert.Throws<System.InvalidOperationException>(() => NewDirector().Initialize(broken, voice));
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void Conversation_TypesSkipsAdvancesAndCompletes()
        {
            NewDirector().Initialize(database, voice);
            string completed = null;
            director.Completed += id => completed = id;

            director.Play("greeting");
            DialogueBoxView box = director.PortraitBox;
            Assert.IsTrue(box.IsVisible);
            Assert.AreEqual("greeting-01", director.CurrentLine.Id);
            Assert.AreEqual(0, box.VisibleCharacters);
            Assert.AreSame(voiceOver, voice.Playing, "the line's voice-over starts with it");

            director.Tick(0.05f);
            Assert.AreEqual(5, box.VisibleCharacters);
            Assert.IsFalse(box.IsContinueVisible);

            director.Advance();
            Assert.AreEqual("Hello there.".Length, box.VisibleCharacters, "a click shows the rest of the line");
            Assert.IsTrue(box.IsContinueVisible);
            Assert.AreEqual("greeting-01", director.CurrentLine.Id);

            director.Advance();
            Assert.AreEqual("greeting-02", director.CurrentLine.Id);
            Assert.IsNull(voice.Playing, "a line without voice-over silences the previous one");

            director.Tick(1f);
            director.Advance();
            Assert.IsFalse(director.IsPlaying);
            Assert.IsFalse(box.IsVisible);
            Assert.AreEqual("greeting", completed);
        }

        [Test]
        public void PlayingAgain_ReplacesTheConversationAndStopDoesNotComplete()
        {
            NewDirector().Initialize(database, voice);
            int completions = 0;
            director.Completed += id => completions++;

            director.Play("greeting");
            director.Play("short");
            Assert.AreEqual("short-01", director.CurrentLine.Id);

            director.Stop();
            Assert.IsFalse(director.IsPlaying);
            Assert.IsFalse(director.PortraitBox.IsVisible);
            Assert.IsNull(voice.Playing);
            Assert.AreEqual(0, completions);
            Assert.Throws<System.ArgumentException>(() => director.Play("missing"));
        }

        [Test]
        public void Placement_KeepsTheBoxInsideAndReturnsToTheAuthoredSpot()
        {
            NewDirector().Initialize(database, voice);
            RectTransform rect = director.PortraitBox.Rect;
            Vector2 authoredPivot = rect.pivot;
            Vector2 authoredPosition = rect.anchoredPosition;

            director.Play("short", new Vector2(1f, 0f));
            Assert.AreEqual(new Vector2(1f, 0f), rect.anchorMin);
            Assert.AreEqual(new Vector2(1f, 0f), rect.pivot, "the bottom-right corner sits in the bottom-right corner");
            Assert.AreEqual(Vector2.zero, rect.anchoredPosition);

            director.Play("short", new Vector2(-3f, 7f));
            Assert.AreEqual(new Vector2(0f, 1f), rect.pivot, "positions outside the screen are clamped");

            director.Play("short");
            Assert.AreEqual(authoredPivot, rect.pivot);
            Assert.AreEqual(authoredPosition, rect.anchoredPosition);
        }

        [Test]
        public void SpeechBalloon_PopsInBeforeTypingAndReplacesTheBox()
        {
            NewDirector().Initialize(database, voice);
            DialogueBalloonView balloon = director.SpeechBalloon;

            director.Play("greeting");
            director.Play("balloon", new Vector2(0.5f, 0.5f));
            Assert.IsFalse(director.PortraitBox.IsVisible, "one presentation at a time");
            Assert.IsTrue(balloon.IsVisible);
            Assert.IsFalse(balloon.IsEntranceComplete);

            director.Tick(0.01f);
            Assert.AreEqual(0, balloon.VisibleCharacters, "no typing while the bubbles pop in");

            director.Tick(10f);
            Assert.IsTrue(balloon.IsEntranceComplete);
            director.Tick(0.02f);
            Assert.AreEqual(2, balloon.VisibleCharacters);

            director.Advance();
            Assert.IsTrue(balloon.IsContinueVisible);
            director.Advance();
            Assert.IsFalse(balloon.IsVisible);
        }

        [Test]
        public void SpeechBalloon_ClickDuringTheEntranceShowsEverything()
        {
            NewDirector().Initialize(database, voice);

            director.Play("balloon");
            director.Advance();

            Assert.IsTrue(director.SpeechBalloon.IsEntranceComplete);
            Assert.AreEqual("Leave now.".Length, director.SpeechBalloon.VisibleCharacters);
            Assert.IsTrue(director.SpeechBalloon.IsContinueVisible);
        }

        [Test]
        public void SpeechBalloon_StaysOnScreenWhereverTheSpeakerIs()
        {
            NewDirector().Initialize(database, voice);
            DialogueBalloonView balloon = director.SpeechBalloon;
            RectTransform cloud = (RectTransform)balloon.Rect.Find("Cloud");
            Rect frame = balloon.Rect.rect;
            Vector2 half = cloud.rect.size * 0.5f;

            foreach (Vector2 speaker in new[] { Vector2.zero, Vector2.one, new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f) })
            {
                director.Play("balloon", speaker);
                Vector2 centre = balloon.BalloonPosition;
                Assert.LessOrEqual(Mathf.Abs(centre.x), frame.width * 0.5f - half.x + 0.01f, "cloud inside horizontally for " + speaker);
                Assert.LessOrEqual(Mathf.Abs(centre.y), frame.height * 0.5f - half.y + 0.01f, "cloud inside vertically for " + speaker);
            }
        }

        [Test]
        public void MissionSites_NameConversationsTheDialogueDatabaseHas()
        {
            IReadOnlyDictionary<string, string> sites = GameBootstrap.SiteConversations(ContentAssets.Mission);
            DialogueDatabaseScriptableObject authored = ContentAssets.Dialogue;

            Assert.IsNotEmpty(sites);
            foreach (KeyValuePair<string, string> site in sites)
            {
                Assert.IsTrue(authored.Conversations.Any(conversation => conversation.Id == site.Value), site.Key + " -> " + site.Value);
            }

            foreach (string balloonSite in new[] { "town", "boss" })
            {
                DialogueConversationEntry conversation = authored.Conversations.First(entry => entry.Id == sites[balloonSite]);
                Assert.AreEqual(DialogueStyle.SpeechBalloon, conversation.Style, balloonSite + " speaks in a balloon");
            }
        }

        [Test]
        public void OverlayPrefab_IsItsOwnCanvasAboveTheGameUiWithANestedBox()
        {
            DialogueDirector prefab = ContentAssets.DialogueOverlayPrefab;

            Assert.IsNotNull(prefab, "run Tools > Heroes of Metal Age > Create missing dialogue assets");
            Canvas canvas = prefab.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, ContentAssets.RootPrefab.GetComponent<Canvas>().sortingOrder);
            CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
            Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>());

            DialogueBoxView box = prefab.GetComponentInChildren<DialogueBoxView>(true);
            Assert.IsNotNull(box);
            Assert.AreEqual(ContentAssets.DIALOGUE_BOX_PATH, AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(box)),
                "the box stays a nested prefab");
            Assert.IsTrue(box.GetComponent<Image>().raycastTarget, "the box background takes the clicks");
            TMP_Text[] labels = prefab.GetComponentsInChildren<TMP_Text>(true);
            Assert.IsTrue(labels.All(label => label.font != null && !label.raycastTarget), "labels have fonts and ignore clicks");
            Assert.IsTrue(box.GetComponentsInChildren<Image>(true).Where(image => image.gameObject != box.gameObject).All(image => !image.raycastTarget),
                "only the background is a raycast target");

            DialogueBalloonView balloon = prefab.GetComponentInChildren<DialogueBalloonView>(true);
            Assert.IsNotNull(balloon);
            Assert.AreSame(balloon, prefab.SpeechBalloon);
            Assert.AreEqual(ContentAssets.DIALOGUE_BALLOON_PATH, AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(balloon)),
                "the balloon stays a nested prefab");
            Assert.IsNull(balloon.GetComponent<Graphic>(), "the full-frame root draws nothing and takes no clicks");
            Image[] raycastImages = balloon.GetComponentsInChildren<Image>(true).Where(image => image.raycastTarget).ToArray();
            Assert.AreEqual(1, raycastImages.Length, "only the cloud takes clicks");
            Assert.AreEqual("Cloud", raycastImages[0].name);
        }

        [Test]
        public void ClickAway_ClosesBothStylesAsCompletedOnlyWhereTheConditionHolds()
        {
            NewDirector().Initialize(database, voice);
            List<string> completed = new List<string>();
            director.Completed += completed.Add;
            bool isOnMap = false;
            director.SetClickAwayCondition(() => isOnMap);

            director.Play("greeting");
            Assert.IsFalse(director.DismissArea.gameObject.activeSelf, "off the map a click away does nothing");

            isOnMap = true;
            director.Play("balloon");
            Assert.IsTrue(director.DismissArea.gameObject.activeSelf, "on the map the full-screen dismiss area waits behind the dialogue");
            director.DismissArea.onClick.Invoke();
            Assert.IsFalse(director.IsPlaying);
            Assert.IsFalse(director.SpeechBalloon.IsVisible);
            Assert.IsFalse(director.DismissArea.gameObject.activeSelf, "nothing to dismiss once closed");
            CollectionAssert.AreEqual(new[] { "balloon" }, completed, "a click away counts as read, so a held encounter dialog appears");

            director.Play("greeting");
            director.DismissArea.onClick.Invoke();
            Assert.IsFalse(director.PortraitBox.IsVisible, "the portrait box closes the same way");
            Assert.IsNull(voice.Playing, "and its voice-over stops");
            CollectionAssert.AreEqual(new[] { "balloon", "greeting" }, completed);
        }

        [Test]
        public void OverlayPrefab_DismissAreaCoversTheScreenBehindTheDialogue()
        {
            DialogueDirector prefab = ContentAssets.DialogueOverlayPrefab;
            Button area = prefab.DismissArea;

            Assert.IsNotNull(area, "run Tools > Heroes of Metal Age > Create missing dialogue assets");
            Assert.IsFalse(area.gameObject.activeSelf, "inactive until a dialogue is open where a click away closes it");
            Assert.AreSame(prefab.transform, area.transform.parent);
            Assert.AreEqual(0, area.transform.GetSiblingIndex(), "drawn first, so the box and the balloon take their own clicks");
            RectTransform rect = (RectTransform)area.transform;
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Image image = area.GetComponent<Image>();
            Assert.IsTrue(image.raycastTarget);
            Assert.AreEqual(0f, image.color.a, "invisible");
        }

        private DialogueDirector NewDirector()
        {
            director = (DialogueDirector)PrefabUtility.InstantiatePrefab(ContentAssets.DialogueOverlayPrefab);
            return director;
        }

        private static DialogueDatabaseScriptableObject NewDatabase(params DialogueConversationEntry[] conversations)
        {
            DialogueDatabaseScriptableObject created = ScriptableObject.CreateInstance<DialogueDatabaseScriptableObject>();
            created.CharactersPerSecond = CHARACTERS_PER_SECOND;
            created.SetContent(new[]
            {
                new DialogueSpeakerEntry { Id = "kael", DisplayName = "KAEL" },
                new DialogueSpeakerEntry { Id = "varg", DisplayName = "VARG" }
            }, conversations);
            return created;
        }

        private static DialogueConversationEntry Conversation(string id, params DialogueLineEntry[] lines)
        {
            return Conversation(id, DialogueStyle.PortraitBox, lines);
        }

        private static DialogueConversationEntry Conversation(string id, DialogueStyle style, params DialogueLineEntry[] lines)
        {
            DialogueConversationEntry conversation = new DialogueConversationEntry { Id = id, Style = style };
            conversation.SetLines(lines);
            return conversation;
        }

        private static DialogueLineEntry Line(string id, string speakerId, string text, SoundDefinition sound = null)
        {
            return new DialogueLineEntry { Id = id, SpeakerId = speakerId, Text = text, VoiceOver = sound };
        }

        /// <summary>Remembers the voice-over that would be playing; plays nothing.</summary>
        private sealed class FakeDialogueVoice : IDialogueVoice
        {
            public SoundDefinition Playing { get; private set; }

            public void Play(SoundDefinition sound)
            {
                Playing = sound;
            }

            public void Stop()
            {
                Playing = null;
            }
        }
    }
}
