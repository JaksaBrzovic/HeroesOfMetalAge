using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Dialogue;
using HeroesOfMetalAge.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off bootstrap tool: it created the initial dialogue database and the DialogueBox, DialogueBalloon and
    /// DialogueOverlay prefabs, which are now committed, authored assets edited by hand. It only creates assets that are
    /// missing (and adds the balloon or the click-away dismiss area to an overlay that has none); it never changes anything that exists, so it has nothing
    /// to do while they exist. Safe to delete together with its call in
    /// <see cref="PrototypePipeline.Setup"/> and the mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class DialogueAssetSeeder
    {
        private const float BOX_WIDTH = 880f;
        private const float BOX_HEIGHT = 216f;
        private const float PADDING = 20f;
        private const float PORTRAIT_SIZE = 176f;
        private const float PORTRAIT_INSET = 4f;
        private const float NAME_HEIGHT = 34f;
        private const float CONTINUE_HEIGHT = 26f;
        private const float AUTHORED_BOTTOM = 120f;
        private const int OVERLAY_SORTING_ORDER = 10;
        private const string CLOUD_SPRITE_PATH = "Assets/HeroesOfMetalAge/Content/UI/Sprites/BalloonCloud.png";
        private const string BUBBLE_SPRITE_PATH = "Assets/HeroesOfMetalAge/Content/UI/Sprites/BalloonBubble.png";
        private static readonly Vector2 CloudSize = new Vector2(520f, 260f);
        private static readonly float[] BubbleSizes = { 16f, 26f, 38f };
        private static readonly Color Ink = new Color(0.08f, 0.07f, 0.06f);
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);

        [MenuItem("Tools/Heroes of Metal Age/Create missing dialogue assets")]
        public static void CreateMissingAssets()
        {
            UiTheme theme = ContentAssets.Theme;
            if (theme == null || theme.Font == null)
            {
                throw new InvalidOperationException("The dialogue prefabs take their font and colours from " + ContentAssets.THEME_PATH + ", which is missing or has no font.");
            }

            if (ContentAssets.Dialogue == null)
            {
                CreateDatabase();
            }

            if (ContentAssets.DialogueBoxPrefab == null)
            {
                CreateBoxPrefab(theme);
            }

            if (ContentAssets.DialogueBalloonPrefab == null)
            {
                CreateBalloonPrefab(theme);
            }

            if (ContentAssets.DialogueOverlayPrefab == null)
            {
                CreateOverlayPrefab();
            }
            else
            {
                if (ContentAssets.DialogueOverlayPrefab.SpeechBalloon == null)
                {
                    AddBalloonToOverlay();
                }

                if (ContentAssets.DialogueOverlayPrefab.DismissArea == null)
                {
                    AddDismissAreaToOverlay();
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void CreateDatabase()
        {
            DialogueDatabaseScriptableObject database = ScriptableObject.CreateInstance<DialogueDatabaseScriptableObject>();
            Sprite heroPortrait = ContentAssets.Heroes.Heroes.Count > 0 ? ContentAssets.Heroes.Heroes[0].Portrait : null;
            DialogueSpeakerEntry[] speakers =
            {
                new DialogueSpeakerEntry { Id = "kael", DisplayName = "KAEL IRONSONG", Portrait = heroPortrait },
                new DialogueSpeakerEntry { Id = "lord-varg", DisplayName = "LORD VARG" }
            };
            DialogueConversationEntry arrival = new DialogueConversationEntry { Id = "ashen-pass-arrival" };
            arrival.SetLines(new[]
            {
                Line("ashen-pass-arrival-01", "kael", "The Ashen Pass. Every road east runs through this valley, and every road here has a guard."),
                Line("ashen-pass-arrival-02", "lord-varg", "Turn back, bard. The Ashen Clan holds this pass, and the clan does not sell passage."),
                Line("ashen-pass-arrival-03", "kael", "Then I will not buy it. Strings up, banners high!")
            });
            DialogueConversationEntry ready = new DialogueConversationEntry { Id = "kael-ready" };
            ready.SetLines(new[] { Line("kael-ready-01", "kael", "Ready when you are.") });
            database.SetContent(speakers, new List<DialogueConversationEntry> { arrival, ready });
            AssetDatabase.CreateAsset(database, ContentAssets.DIALOGUE_DATABASE_PATH);
        }

        private static DialogueLineEntry Line(string id, string speakerId, string text)
        {
            return new DialogueLineEntry { Id = id, SpeakerId = speakerId, Text = text };
        }

        private static void CreateBoxPrefab(UiTheme theme)
        {
            GameObject box = NewRect("DialogueBox", null);
            RectTransform boxRect = (RectTransform)box.transform;
            SetRect(boxRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, AUTHORED_BOTTOM),
                new Vector2(BOX_WIDTH, BOX_HEIGHT));
            // the background takes the clicks that advance the dialogue
            AddImage(box, WithAlpha(theme.Panel, 0.96f), true);

            GameObject border = NewRect("Border", box.transform);
            SetRect((RectTransform)border.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 4f));
            AddImage(border, theme.Gold, false);

            GameObject frame = NewRect("PortraitFrame", box.transform);
            SetRect((RectTransform)frame.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(PADDING, 0f),
                new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE));
            AddImage(frame, theme.Background, false);

            GameObject portrait = NewRect("PortraitImage", frame.transform);
            Stretch((RectTransform)portrait.transform, new Vector2(PORTRAIT_INSET, PORTRAIT_INSET), new Vector2(-PORTRAIT_INSET, -PORTRAIT_INSET));
            Image portraitImage = AddImage(portrait, Color.white, false);
            portraitImage.preserveAspect = true;
            portraitImage.sprite = theme.Hero;

            GameObject textArea = NewRect("TextArea", box.transform);
            Stretch((RectTransform)textArea.transform, new Vector2(PADDING * 2f + PORTRAIT_SIZE, PADDING), new Vector2(-PADDING - 4f, -PADDING));

            GameObject speakerName = NewRect("SpeakerNameText", textArea.transform);
            SetRect((RectTransform)speakerName.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, NAME_HEIGHT));
            TMP_Text speakerNameText = AddText(speakerName, theme, "SPEAKER", 26f, theme.Gold, TextAlignmentOptions.TopLeft);
            speakerNameText.fontStyle = FontStyles.Bold;

            GameObject line = NewRect("LineText", textArea.transform);
            Stretch((RectTransform)line.transform, new Vector2(0f, CONTINUE_HEIGHT), new Vector2(0f, -NAME_HEIGHT - 6f));
            TMP_Text lineText = AddText(line, theme, "Line text", 24f, theme.Text, TextAlignmentOptions.TopLeft);
            lineText.textWrappingMode = TextWrappingModes.Normal;
            lineText.overflowMode = TextOverflowModes.Ellipsis;

            GameObject next = NewRect("ContinueText", textArea.transform);
            SetRect((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(160f, CONTINUE_HEIGHT));
            TMP_Text continueText = AddText(next, theme, "NEXT", 18f, theme.Muted, TextAlignmentOptions.BottomRight);

            DialogueBoxView view = box.AddComponent<DialogueBoxView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("portraitImage").objectReferenceValue = portraitImage;
            serialized.FindProperty("speakerNameText").objectReferenceValue = speakerNameText;
            serialized.FindProperty("lineText").objectReferenceValue = lineText;
            serialized.FindProperty("continueText").objectReferenceValue = continueText;
            serialized.FindProperty("fallbackPortrait").objectReferenceValue = theme.Hero;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(box, ContentAssets.DIALOGUE_BOX_PATH);
            Object.DestroyImmediate(box);
        }

        private static void CreateOverlayPrefab()
        {
            GameObject overlay = NewRect("DialogueOverlay", null);
            Canvas canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // above GameUiRoot (0); its own Canvas keeps the per-frame typing out of the game UI's rebuilds
            canvas.sortingOrder = OVERLAY_SORTING_ORDER;
            CanvasScaler scaler = overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            overlay.AddComponent<GraphicRaycaster>();

            GameObject frame = NewRect("DesignFrame", overlay.transform);
            SetRect((RectTransform)frame.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, DesignSize);

            GameObject box = (GameObject)PrefabUtility.InstantiatePrefab(ContentAssets.DialogueBoxPrefab.gameObject, frame.transform);
            box.SetActive(false);
            DialogueBalloonView balloon = InstantiateBalloon(frame.transform);
            Button dismissArea = CreateDismissArea(overlay.transform);

            DialogueDirector director = overlay.AddComponent<DialogueDirector>();
            SerializedObject serialized = new SerializedObject(director);
            serialized.FindProperty("designFrame").objectReferenceValue = frame.transform;
            serialized.FindProperty("portraitBox").objectReferenceValue = box.GetComponent<DialogueBoxView>();
            serialized.FindProperty("speechBalloon").objectReferenceValue = balloon;
            serialized.FindProperty("dismissArea").objectReferenceValue = dismissArea;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(overlay, ContentAssets.DIALOGUE_OVERLAY_PATH);
            Object.DestroyImmediate(overlay);
        }

        // the overlay was authored before a click away closed the dialogue: add the invisible full-screen button behind it
        private static void AddDismissAreaToOverlay()
        {
            GameObject overlay = PrefabUtility.LoadPrefabContents(ContentAssets.DIALOGUE_OVERLAY_PATH);
            try
            {
                DialogueDirector director = overlay.GetComponent<DialogueDirector>();
                SerializedObject serialized = new SerializedObject(director);
                serialized.FindProperty("dismissArea").objectReferenceValue = CreateDismissArea(overlay.transform);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(overlay, ContentAssets.DIALOGUE_OVERLAY_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(overlay);
            }
        }

        // covers the whole screen (letterbox included) under the design frame, so the dialogue itself still takes its clicks
        private static Button CreateDismissArea(Transform overlay)
        {
            GameObject area = NewRect("DismissArea", overlay);
            Stretch((RectTransform)area.transform, Vector2.zero, Vector2.zero);
            area.transform.SetAsFirstSibling();
            Image image = area.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            Button button = area.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            area.SetActive(false);
            return button;
        }

        // the overlay was authored before the speech balloon existed: nest the balloon next to the box and wire it
        private static void AddBalloonToOverlay()
        {
            GameObject overlay = PrefabUtility.LoadPrefabContents(ContentAssets.DIALOGUE_OVERLAY_PATH);
            try
            {
                DialogueDirector director = overlay.GetComponent<DialogueDirector>();
                Transform frame = overlay.transform.Find("DesignFrame");
                DialogueBalloonView balloon = InstantiateBalloon(frame);
                SerializedObject serialized = new SerializedObject(director);
                serialized.FindProperty("designFrame").objectReferenceValue = frame;
                serialized.FindProperty("speechBalloon").objectReferenceValue = balloon;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(overlay, ContentAssets.DIALOGUE_OVERLAY_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(overlay);
            }
        }

        private static DialogueBalloonView InstantiateBalloon(Transform frame)
        {
            GameObject balloon = (GameObject)PrefabUtility.InstantiatePrefab(ContentAssets.DialogueBalloonPrefab.gameObject, frame);
            balloon.SetActive(false);
            return balloon.GetComponent<DialogueBalloonView>();
        }

        private static void CreateBalloonPrefab(UiTheme theme)
        {
            Sprite cloudSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CLOUD_SPRITE_PATH);
            Sprite bubbleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BUBBLE_SPRITE_PATH);
            if (cloudSprite == null || bubbleSprite == null)
            {
                throw new InvalidOperationException("The speech balloon needs the sprites " + CLOUD_SPRITE_PATH + " and " + BUBBLE_SPRITE_PATH + ".");
            }

            // fills the design frame and draws nothing; the trail and the cloud are placed inside it
            GameObject root = NewRect("DialogueBalloon", null);
            Stretch((RectTransform)root.transform, Vector2.zero, Vector2.zero);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            GameObject trail = NewRect("Trail", root.transform);
            Stretch((RectTransform)trail.transform, Vector2.zero, Vector2.zero);
            RectTransform[] bubbles = new RectTransform[BubbleSizes.Length];
            for (int i = 0; i < BubbleSizes.Length; i++)
            {
                GameObject bubble = NewRect("Bubble" + (i + 1), trail.transform);
                SetRect((RectTransform)bubble.transform, middle, middle, middle, new Vector2(-200f + i * 60f, -120f + i * 50f),
                    new Vector2(BubbleSizes[i], BubbleSizes[i]));
                AddImage(bubble, Color.white, false).sprite = bubbleSprite;
                bubbles[i] = (RectTransform)bubble.transform;
            }

            // after the trail, so the cloud draws over the last bubble; it takes the clicks that advance the dialogue
            GameObject cloud = NewRect("Cloud", root.transform);
            SetRect((RectTransform)cloud.transform, middle, middle, middle, Vector2.zero, CloudSize);
            AddImage(cloud, Color.white, true).sprite = cloudSprite;

            GameObject line = NewRect("LineText", cloud.transform);
            Stretch((RectTransform)line.transform, new Vector2(76f, 58f), new Vector2(-76f, -50f));
            TMP_Text lineText = AddText(line, theme, "Line text", 30f, Ink, TextAlignmentOptions.Center);
            lineText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            lineText.textWrappingMode = TextWrappingModes.Normal;
            lineText.overflowMode = TextOverflowModes.Ellipsis;

            GameObject next = NewRect("ContinueText", cloud.transform);
            SetRect((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f),
                new Vector2(160f, 26f));
            TMP_Text continueText = AddText(next, theme, "NEXT", 16f, new Color(0.35f, 0.33f, 0.3f), TextAlignmentOptions.Bottom);

            DialogueBalloonView view = root.AddComponent<DialogueBalloonView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("lineText").objectReferenceValue = lineText;
            serialized.FindProperty("continueText").objectReferenceValue = continueText;
            serialized.FindProperty("balloon").objectReferenceValue = cloud.transform;
            SerializedProperty trailProperty = serialized.FindProperty("trailBubbles");
            trailProperty.arraySize = bubbles.Length;
            for (int i = 0; i < bubbles.Length; i++)
            {
                trailProperty.GetArrayElementAtIndex(i).objectReferenceValue = bubbles[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.DIALOGUE_BALLOON_PATH);
            Object.DestroyImmediate(root);
        }

        private static GameObject NewRect(string name, Transform parent)
        {
            GameObject target = new GameObject(name, typeof(RectTransform));
            target.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
            {
                target.transform.SetParent(parent, false);
            }

            return target;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Image AddImage(GameObject target, Color color, bool isRaycastTarget)
        {
            Image image = target.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = isRaycastTarget;
            return image;
        }

        private static TMP_Text AddText(GameObject target, UiTheme theme, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = target.AddComponent<TextMeshProUGUI>();
            label.font = theme.Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
