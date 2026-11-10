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
    /// One-off generation tool for the revised panels; it goes with <c>Tools/UiArt/revise_panels.py</c>, which redrew the
    /// artwork. It made the dialogue box's children follow the box's size (the overlay shows the box smaller than it was
    /// authored) at the frame's proportions, kept the combat stats inside their panel, moved the casualties into the
    /// victory frame (taller now), and rebuilt the adventure screen's right panel at two thirds of its width: the map
    /// viewport grew, the warhost and the ride button were removed, the goals moved into the new <c>MissionPanel</c> opened
    /// by the mission card, and only the end day button stayed. A second round made the victory's first step show only the
    /// casualties (centred and larger; the experience moved to the rewards screen), put the defeat screen into the victory
    /// frame without the hero, with its three choices on the button row, and moved the dialogue box to the middle of the
    /// screen; it also added the development RESTART BATTLE button to the battle log and the Quick battle animation
    /// toggle to the options. Every step only runs while its
    /// result is missing, so the
    /// tool does nothing once it ran and the prefabs are edited by hand from then on. The project works without it: delete
    /// this file and its call in <see cref="PrototypePipeline.Setup"/>, and the mentions in README.md and
    /// Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class PanelRevisionSeeder
    {
        private const string SCREENS = ContentAssets.UI_FOLDER + "/Screens";
        private const string WIDGETS = ContentAssets.UI_FOLDER + "/Widgets";
        private const string SPRITES = "Assets/HeroesOfMetalAge/Content/UI/Sprites/Gameplay";
        private const float AUTO_SIZE_MINIMUM = 0.55f;

        // the dialogue box as authored and as the overlay shows it (the frame's 1920 x 548 proportions)
        private const float BOX_AUTHORED_WIDTH = 1152f;
        private const float BOX_AUTHORED_HEIGHT = 328.8f;
        private const float BOX_WIDTH = 880f;

        private const float COMBAT_PANEL_RIGHT = 1860f;

        // revise_panels.py: rows inserted into the victory frame below the losses line, in layout units
        private const float VICTORY_INSERTED = 236.34f;
        private const float VICTORY_FRAME_WIDTH = 722.3f;
        private const float CASUALTIES_SCALE = 0.9f;

        // the casualties as the victory's first step and the defeat show them, in layout units
        private const float CASUALTIES_WIDTH = 600f;
        private const float CASUALTIES_HEIGHT = 284f;
        private const float CASUALTIES_TOP = 524f;
        private const float CASUALTIES_TITLE_SIZE = 26f;
        private const float CASUALTIES_SLOT_WIDTH = 80f;
        private const float CASUALTIES_SLOT_HEIGHT = 94f;
        private const float CASUALTIES_SLOT_SPACING = 6f;

        private const float REWARDS_GOLD_TOP = 196f;
        private const float REWARDS_GOLD_HEIGHT = 56f;

        // revise_panels.py: rows of the victory frame above the defeat frame (the hero), in source pixels
        private const float DEFEAT_CROPPED_ROWS = 418f;
        private const float DEFEAT_BUTTON_WIDTH = 184f;
        private const float DEFEAT_BUTTON_HEIGHT = 52.8f;
        private const float DEFEAT_BUTTON_GAP = 10f;
        // the defeat's closing words are two lines; they end above the button row
        private const float DEFEAT_BODY_RAISE = 26f;

        // the development RESTART BATTLE button inside the battle log, in screen layout units
        private const float RESTART_LEFT = 1378f;
        private const float RESTART_TOP = 862f;
        private const float RESTART_WIDTH = 220f;
        private const float RESTART_HEIGHT = 48f;

        // the options' toggle row in frame units: Mute stays at 68.6, the other two follow with equal gaps
        private const float OPTIONS_MUTE_LEFT = 68.6f;
        private const float OPTIONS_QUICK_COMBAT_LEFT = 344.2f;
        private const float OPTIONS_QUICK_ANIMATION_LEFT = 567.2f;
        private const float OPTIONS_LABEL_OFFSET = 54.8f;
        private const float OPTIONS_QUICK_ANIMATION_LABEL_WIDTH = 250f;

        private static readonly Color TextColor = new Color(0.93f, 0.89f, 0.8f, 1f);
        private static readonly Color GoldColor = new Color(0.93f, 0.76f, 0.45f, 1f);
        private static readonly Color HintColor = new Color(0.72f, 0.66f, 0.56f, 1f);

        [MenuItem("Tools/Heroes of Metal Age/Revise panels")]
        public static void CreateMissingAssets()
        {
            TypographySettings typography = TypographyTools.Settings;
            if (typography == null)
            {
                return;
            }

            ReviseDialogueBox();
            ReviseDialogueOverlay();
            ReviseCombat();
            ReviseVictory();
            ReviseAdventure(typography);
            ReviseCasualtiesPanel();
            ReviseVictoryLosses();
            ReviseRewardsExperience();
            ReviseDefeat();
            CentreDialogueBox();
            AddRestartBattleButton();
            AddQuickAnimationToggle();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The casualties widget lives inside the victory and defeat frames: no frame or title of its own, centred titles and
        /// larger slots in rows that centre the filled slots (<see cref="CasualtiesView"/> hides the empty ones).
        /// </summary>
        private static void ReviseCasualtiesPanel()
        {
            string path = WIDGETS + "/CasualtiesPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform title = root.transform.Find("TitleText");
                if (title == null)
                {
                    return;
                }

                Object.DestroyImmediate(title.gameObject);
                Image frame = root.GetComponent<Image>();
                if (frame != null)
                {
                    Object.DestroyImmediate(frame);
                }

                RectTransform panel = (RectTransform)root.transform;
                panel.sizeDelta = new Vector2(CASUALTIES_WIDTH, CASUALTIES_HEIGHT);
                CasualtiesTitle(Child(panel, "PlayerTitle"), 0f);
                CasualtiesRow(Child(panel, "PlayerSlots"), 38f);
                CasualtiesTitle(Child(panel, "EnemyTitle"), 150f);
                CasualtiesRow(Child(panel, "EnemySlots"), 188f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CasualtiesTitle(RectTransform rect, float top)
        {
            Place(rect, 0f, top, CASUALTIES_WIDTH, top + 34f);
            TMP_Text text = rect.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = CASUALTIES_TITLE_SIZE;
            text.fontSizeMax = CASUALTIES_TITLE_SIZE;
            text.fontSizeMin = CASUALTIES_TITLE_SIZE * AUTO_SIZE_MINIMUM;
            text.enableAutoSizing = true;
        }

        private static void CasualtiesRow(RectTransform row, float top)
        {
            Place(row, 0f, top, CASUALTIES_WIDTH, top + CASUALTIES_SLOT_HEIGHT);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = CASUALTIES_SLOT_SPACING;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // the slots are laid out in the order the view fills them
            SerializedProperty slots = new SerializedObject(row.GetComponent<ArmySlotsView>()).FindProperty("slots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                RectTransform slot = ((Component)slots.GetArrayElementAtIndex(i).objectReferenceValue).GetComponent<RectTransform>();
                slot.SetSiblingIndex(i);
                slot.sizeDelta = new Vector2(CASUALTIES_SLOT_WIDTH, CASUALTIES_SLOT_HEIGHT);
            }
        }

        /// <summary>The victory's first step shows only the losses, centred below the subtitle; experience moves to the rewards.</summary>
        private static void ReviseVictoryLosses()
        {
            string path = SCREENS + "/Victory.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RectTransform frame = Child(root.transform, "Frame");
                Transform experience = frame.Find("ExperienceText");
                if (experience == null)
                {
                    return;
                }

                Object.DestroyImmediate(experience.gameObject);
                Object.DestroyImmediate(Child(frame, "LossesText").gameObject);
                RectTransform panel = (RectTransform)root.GetComponentInChildren<CasualtiesView>(true).transform;
                PlaceCasualties(panel, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <param name="shift">How much higher the frame's contents sit than in the victory frame (the defeat frame has no hero).</param>
        private static void PlaceCasualties(RectTransform panel, float shift)
        {
            panel.localScale = Vector3.one;
            float left = (VICTORY_FRAME_WIDTH - CASUALTIES_WIDTH) / 2f;
            Place(panel, left, CASUALTIES_TOP - shift, left + CASUALTIES_WIDTH, CASUALTIES_TOP - shift + CASUALTIES_HEIGHT);
        }

        /// <summary>The rewards show the experience under the gold, beside the gold pile.</summary>
        private static void ReviseRewardsExperience()
        {
            string path = SCREENS + "/Rewards.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RewardsCanvasView view = root.GetComponent<RewardsCanvasView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("experienceText").objectReferenceValue != null)
                {
                    return;
                }

                RectTransform frame = Child(root.transform, "Frame");
                RectTransform gold = Child(frame, "GoldText");
                gold.anchoredPosition = new Vector2(gold.anchoredPosition.x, -REWARDS_GOLD_TOP);
                gold.sizeDelta = new Vector2(gold.sizeDelta.x, REWARDS_GOLD_HEIGHT);
                GameObject copy = Object.Instantiate(Child(frame, "NoteText").gameObject, frame);
                copy.name = "ExperienceText";
                TMP_Text experience = copy.GetComponent<TMP_Text>();
                float left = gold.anchoredPosition.x;
                float top = REWARDS_GOLD_TOP + REWARDS_GOLD_HEIGHT + 2f;
                Place(experience.rectTransform, left, top, left + 340f, top + 34f);
                experience.alignment = TextAlignmentOptions.MidlineLeft;
                experience.color = GoldColor;
                experience.fontSize = 24f;
                experience.fontSizeMax = 24f;
                experience.fontSizeMin = 24f * AUTO_SIZE_MINIMUM;
                experience.enableAutoSizing = true;
                experience.text = "+0 EXPERIENCE";
                serialized.FindProperty("experienceText").objectReferenceValue = experience;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The defeat screen in the victory frame without the hero (revise_panels.py builds DefeatFrame.png from it): the
        /// victory's title, subtitle, losses and closing words in the same places, and the three choices on the button row.
        /// </summary>
        private static void ReviseDefeat()
        {
            string path = SCREENS + "/Defeat.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            GameObject victory = PrefabUtility.LoadPrefabContents(SCREENS + "/Victory.prefab");
            try
            {
                DefeatCanvasView view = root.GetComponent<DefeatCanvasView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("subtitleText").objectReferenceValue != null)
                {
                    return;
                }

                RectTransform victoryFrame = Child(victory.transform, "Frame");
                RectTransform frame = Child(root.transform, "Frame");
                Sprite sprite = frame.GetComponent<Image>().sprite;
                float scale = VICTORY_FRAME_WIDTH / sprite.rect.width;
                frame.sizeDelta = new Vector2(VICTORY_FRAME_WIDTH, sprite.rect.height * scale);
                float shift = DEFEAT_CROPPED_ROWS * scale;

                RectTransform title = Child(frame, "TitleText");
                CopyText(Child(victoryFrame, "TitleText"), title, shift);
                Object.DestroyImmediate(Child(frame, "BodyText").gameObject);
                RectTransform subtitle = Copy(Child(victoryFrame, "SubtitleText"), frame, shift);
                RectTransform body = Copy(Child(victoryFrame, "BodyText"), frame, shift + DEFEAT_BODY_RAISE);

                RectTransform panel = (RectTransform)root.GetComponentInChildren<CasualtiesView>(true).transform;
                panel.SetParent(frame, false);
                PlaceCasualties(panel, shift);

                // the three choices centred on the row of the victory's single button
                RectTransform continueButton = Child(victoryFrame, "ContinueButton");
                float centreY = -continueButton.anchoredPosition.y + continueButton.sizeDelta.y / 2f - shift;
                string[] buttons = { "RetryButton", "ReturnButton", "NewMissionButton" };
                float total = buttons.Length * DEFEAT_BUTTON_WIDTH + (buttons.Length - 1) * DEFEAT_BUTTON_GAP;
                float x = (VICTORY_FRAME_WIDTH - total) / 2f;
                foreach (string name in buttons)
                {
                    RectTransform button = Child(frame, name);
                    Place(button, x, centreY - DEFEAT_BUTTON_HEIGHT / 2f, x + DEFEAT_BUTTON_WIDTH, centreY + DEFEAT_BUTTON_HEIGHT / 2f);
                    button.SetAsLastSibling();
                    x += DEFEAT_BUTTON_WIDTH + DEFEAT_BUTTON_GAP;
                }

                serialized.FindProperty("subtitleText").objectReferenceValue = subtitle.GetComponent<TMP_Text>();
                serialized.FindProperty("bodyText").objectReferenceValue = body.GetComponent<TMP_Text>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(victory);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform Copy(RectTransform source, Transform parent, float shift)
        {
            GameObject copy = Object.Instantiate(source.gameObject, parent);
            copy.name = source.name;
            RectTransform rect = (RectTransform)copy.transform;
            rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, shift);
            return rect;
        }

        /// <summary>Gives <paramref name="target"/> the source text's place (moved up by <paramref name="shift"/>) and its look.</summary>
        private static void CopyText(RectTransform source, RectTransform target, float shift)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition + new Vector2(0f, shift);
            TMP_Text from = source.GetComponent<TMP_Text>();
            TMP_Text to = target.GetComponent<TMP_Text>();
            to.alignment = from.alignment;
            to.fontSize = from.fontSize;
            to.fontSizeMax = from.fontSizeMax;
            to.fontSizeMin = from.fontSizeMin;
            to.enableAutoSizing = from.enableAutoSizing;
            to.color = from.color;
        }

        /// <summary>The development RESTART BATTLE button, in the bottom left corner of the battle log.</summary>
        private static void AddRestartBattleButton()
        {
            string path = SCREENS + "/Combat.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CombatCanvasView view = root.GetComponent<CombatCanvasView>();
                SerializedObject serialized = new SerializedObject(view);
                SerializedProperty property = serialized.FindProperty("restartButton");
                if (property.objectReferenceValue != null)
                {
                    return;
                }

                UiWidget button = Nest<UiWidget>(AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab"), root.transform, "RestartBattleButton");
                Place(button.Rect, RESTART_LEFT, RESTART_TOP, RESTART_LEFT + RESTART_WIDTH, RESTART_TOP + RESTART_HEIGHT);
                property.objectReferenceValue = button;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The Quick battle animation toggle joins Mute and Quick combat in their row (the sliders' tracks are painted into
        /// the frame, so there is no room for another row): the three share the row's width.
        /// </summary>
        private static void AddQuickAnimationToggle()
        {
            string path = SCREENS + "/Options.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SerializedObject serialized = new SerializedObject(root.GetComponent<SettingsCanvasView>());
                SerializedProperty property = serialized.FindProperty("quickAnimationToggle");
                Toggle quickCombat = (Toggle)serialized.FindProperty("quickCombatToggle").objectReferenceValue;
                if (property.objectReferenceValue != null || quickCombat == null)
                {
                    return;
                }

                Transform frame = quickCombat.transform.parent;
                Transform quickLabel = frame.Find(quickCombat.name + "Label");
                Toggle animation = Object.Instantiate(quickCombat.gameObject, frame).GetComponent<Toggle>();
                animation.name = "QuickAnimationToggle";
                MoveTo(quickCombat.transform, OPTIONS_QUICK_COMBAT_LEFT);
                MoveTo(animation.transform, OPTIONS_QUICK_ANIMATION_LEFT);
                if (quickLabel != null)
                {
                    TMP_Text label = Object.Instantiate(quickLabel.gameObject, frame).GetComponent<TMP_Text>();
                    label.name = "QuickAnimationToggleLabel";
                    label.text = "Quick battle animation";
                    MoveTo(quickLabel, OPTIONS_QUICK_COMBAT_LEFT + OPTIONS_LABEL_OFFSET);
                    MoveTo(label.transform, OPTIONS_QUICK_ANIMATION_LEFT + OPTIONS_LABEL_OFFSET);
                    // each label ends before the next toggle
                    SetWidth(quickLabel, OPTIONS_QUICK_ANIMATION_LEFT - OPTIONS_QUICK_COMBAT_LEFT - OPTIONS_LABEL_OFFSET);
                    SetWidth(label.transform, OPTIONS_QUICK_ANIMATION_LABEL_WIDTH);
                }

                Transform muteLabel = frame.Find("MuteToggleLabel");
                if (muteLabel != null)
                {
                    SetWidth(muteLabel, OPTIONS_QUICK_COMBAT_LEFT - OPTIONS_MUTE_LEFT - OPTIONS_LABEL_OFFSET);
                }

                property.objectReferenceValue = animation;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetWidth(Transform child, float width)
        {
            RectTransform rect = (RectTransform)child;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private static void MoveTo(Transform child, float left)
        {
            RectTransform rect = (RectTransform)child;
            rect.anchoredPosition = new Vector2(left, rect.anchoredPosition.y);
        }

        /// <summary>The dialogue box stands in the middle of the screen instead of at its bottom.</summary>
        private static void CentreDialogueBox()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.DIALOGUE_OVERLAY_PATH);
            try
            {
                DialogueBoxView box = root.GetComponentInChildren<DialogueBoxView>(true);
                Vector2 centre = new Vector2(0.5f, 0.5f);
                if (box == null || box.Rect.anchorMin == centre)
                {
                    return;
                }

                box.Rect.anchorMin = centre;
                box.Rect.anchorMax = centre;
                box.Rect.pivot = centre;
                box.Rect.anchoredPosition = Vector2.zero;
                PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.DIALOGUE_OVERLAY_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Every child keeps its place in proportion to the box, so the box can be shown at any size.</summary>
        private static void ReviseDialogueBox()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.DIALOGUE_BOX_PATH);
            try
            {
                RectTransform box = (RectTransform)root.transform;
                RectTransform portrait = (RectTransform)root.transform.Find("PortraitImage");
                if (portrait == null || portrait.anchorMin != portrait.anchorMax)
                {
                    return;
                }

                float fontScale = BOX_WIDTH / BOX_AUTHORED_WIDTH;
                foreach (RectTransform child in box)
                {
                    float x0 = child.anchoredPosition.x;
                    float y0 = -child.anchoredPosition.y;
                    float x1 = x0 + child.sizeDelta.x;
                    float y1 = y0 + child.sizeDelta.y;
                    child.anchorMin = new Vector2(x0 / BOX_AUTHORED_WIDTH, 1f - y1 / BOX_AUTHORED_HEIGHT);
                    child.anchorMax = new Vector2(x1 / BOX_AUTHORED_WIDTH, 1f - y0 / BOX_AUTHORED_HEIGHT);
                    child.offsetMin = child.offsetMax = Vector2.zero;
                    TMP_Text text = child.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        text.fontSize *= fontScale;
                        text.fontSizeMax *= fontScale;
                        text.fontSizeMin *= fontScale;
                        text.enableAutoSizing = true;
                        text.fontSizeMin = Mathf.Min(text.fontSizeMin, text.fontSizeMax * 0.7f);
                    }
                }

                box.sizeDelta = BoxSize;
                PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.DIALOGUE_BOX_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Vector2 BoxSize => new Vector2(BOX_WIDTH, BOX_WIDTH * BOX_AUTHORED_HEIGHT / BOX_AUTHORED_WIDTH);

        /// <summary>The overlay's box keeps the frame's proportions, so the frame is not squeezed.</summary>
        private static void ReviseDialogueOverlay()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.DIALOGUE_OVERLAY_PATH);
            try
            {
                DialogueBoxView box = root.GetComponentInChildren<DialogueBoxView>(true);
                if (box == null || Vector2.Distance(box.Rect.sizeDelta, BoxSize) < 0.5f)
                {
                    return;
                }

                box.Rect.sizeDelta = BoxSize;
                PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.DIALOGUE_OVERLAY_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The acting stack's name and numbers end inside the war council's frame.</summary>
        private static void ReviseCombat()
        {
            string path = SCREENS + "/Combat.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool isChanged = false;
                foreach (string name in new[] { "ActorText", "StatsText" })
                {
                    RectTransform rect = (RectTransform)root.transform.Find(name);
                    float width = COMBAT_PANEL_RIGHT - rect.anchoredPosition.x;
                    if (rect.sizeDelta.x > width + 0.5f)
                    {
                        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                        isChanged = true;
                    }
                }

                if (isChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The casualties sit inside the taller victory frame, between the losses line and the closing words.</summary>
        private static void ReviseVictory()
        {
            string path = SCREENS + "/Victory.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RectTransform frame = (RectTransform)root.transform.Find("Frame");
                CasualtiesView casualties = root.GetComponentInChildren<CasualtiesView>(true);
                if (casualties.transform.parent == frame)
                {
                    return;
                }

                Sprite sprite = frame.GetComponent<Image>().sprite;
                frame.sizeDelta = new Vector2(VICTORY_FRAME_WIDTH, sprite.rect.height * VICTORY_FRAME_WIDTH / sprite.rect.width);
                Place((RectTransform)frame.Find("BodyText"), 69.4f, 826f, 654.4f, 870f);
                RectTransform button = (RectTransform)frame.Find("ContinueButton");
                button.anchoredPosition += new Vector2(0f, -VICTORY_INSERTED);

                RectTransform panel = (RectTransform)casualties.transform;
                panel.SetParent(frame, false);
                float width = panel.sizeDelta.x;
                float height = panel.sizeDelta.y;
                float left = (VICTORY_FRAME_WIDTH - width * CASUALTIES_SCALE) / 2f;
                // the panel's own title is hidden, so its first row starts right below the losses line
                float top = 590f - 86f * CASUALTIES_SCALE;
                Place(panel, left, top, left + width, top + height);
                panel.localScale = new Vector3(CASUALTIES_SCALE, CASUALTIES_SCALE, 1f);
                // a later step removes the widget's own frame and title
                Image panelFrame = panel.GetComponent<Image>();
                if (panelFrame != null)
                {
                    panelFrame.enabled = false;
                }

                Transform panelTitle = panel.Find("TitleText");
                if (panelTitle != null)
                {
                    panelTitle.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The right panel at two thirds of its width (revise_panels.py) and the mission panel over the map.</summary>
        private static void ReviseAdventure(TypographySettings typography)
        {
            string path = SCREENS + "/Adventure.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WorldCanvasView view = root.GetComponent<WorldCanvasView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("missionPanel").objectReferenceValue != null)
                {
                    return;
                }

                Transform screen = root.transform;
                foreach (string name in new[] { "WarhostTitle", "ArmySlots", "RideButton" })
                {
                    Transform removed = screen.Find(name);
                    if (removed != null)
                    {
                        Object.DestroyImmediate(removed.gameObject);
                    }
                }

                Place(Child(screen, "MapViewport"), 21.8f, 81.5f, 1502f, 997.4f);
                Place(Child(screen, "PortraitImage"), 1551.6f, 127.4f, 1708.9f, 332.8f);
                TMP_Text hero = Child(screen, "HeroText").GetComponent<TMP_Text>();
                Place(hero.rectTransform, 1735f, 128f, 1884f, 196f);
                Shrinkable(hero, 0.8f, TextAlignmentOptions.MidlineLeft);
                TMP_Text level = Child(screen, "LevelText").GetComponent<TMP_Text>();
                Place(level.rectTransform, 1735f, 197f, 1884f, 228f);
                Shrinkable(level, 0.85f, TextAlignmentOptions.MidlineLeft);
                TMP_Text movement = Child(screen, "MovementText").GetComponent<TMP_Text>();
                Place(movement.rectTransform, 1784f, 256f, 1884f, 292f);
                Shrinkable(movement, 1f, TextAlignmentOptions.MidlineLeft);
                Place(Child(screen, "MovementBar"), 1758f, 307.6f, 1863f, 330.5f);

                UiWidget endDay = (UiWidget)serialized.FindProperty("endDayButton").objectReferenceValue;
                Place(endDay.Rect, 1548f, 861f, 1800f, 948f);
                RectTransform endDayTitle = (RectTransform)endDay.transform.Find("Title");
                if (endDayTitle != null)
                {
                    endDayTitle.anchorMin = Vector2.zero;
                    endDayTitle.anchorMax = Vector2.one;
                    endDayTitle.offsetMin = new Vector2(84f, 0f);
                    endDayTitle.offsetMax = new Vector2(-8f, 0f);
                }

                // the mission card: the former warhost box with the mission's banner below it, one button over both
                GameObject hotspot = AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/HotspotButton.prefab");
                UiWidget missionButton = Nest<UiWidget>(hotspot, screen, "MissionButton");
                Place(missionButton.Rect, 1538f, 366f, 1888f, 848f);
                missionButton.transform.SetSiblingIndex(Child(screen, "Backdrop").GetSiblingIndex() + 1);
                RectTransform missionTitle = Child(screen, "MissionTitle");
                Place(missionTitle, 1552f, 368f, 1880f, 414f);
                missionTitle.SetAsLastSibling();
                TMP_Text missionName = PlacedText(screen, "MissionNameText", typography, TextStyle.Title, 26f, TextAlignmentOptions.Center, GoldColor,
                    1556f, 428f, 1868f, 470f);
                TMP_Text missionProgress = PlacedText(screen, "MissionProgressText", typography, TextStyle.Label, 19f, TextAlignmentOptions.Center,
                    TextColor, 1556f, 474f, 1868f, 504f);
                TMP_Text hint = PlacedText(screen, "MissionHintText", typography, TextStyle.Label, 16f, TextAlignmentOptions.Center, HintColor,
                    1556f, 512f, 1868f, 544f);
                hint.text = "CLICK TO OPEN THE MISSION";

                MissionPanelView panel = CreateMissionPanel(screen, typography);
                Child(screen, "Header").SetAsLastSibling();
                Child(screen, "Footer").SetAsLastSibling();

                serialized.FindProperty("missionButton").objectReferenceValue = missionButton;
                serialized.FindProperty("missionNameText").objectReferenceValue = missionName;
                serialized.FindProperty("missionProgressText").objectReferenceValue = missionProgress;
                serialized.FindProperty("missionPanel").objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A framed panel over the middle of the map: name, progress, story, the goals and a close button.</summary>
        private static MissionPanelView CreateMissionPanel(Transform screen, TypographySettings typography)
        {
            GameObject root = NewUi("MissionPanel", screen);
            RectTransform rect = (RectTransform)root.transform;
            Place(rect, 382f, 210f, 1142f, 870f);
            Image frame = root.AddComponent<Image>();
            frame.sprite = LoadSprite(SPRITES + "/Dialog/PanelFrame.png");
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 2.6f;
            // the frame takes the clicks on the panel, so they never reach the map below
            frame.raycastTarget = true;
            MissionPanelView view = root.AddComponent<MissionPanelView>();
            TMP_Text title = PlacedText(rect, "TitleText", typography, TextStyle.Title, 36f, TextAlignmentOptions.Center, GoldColor, 50f, 44f, 710f, 98f);
            TMP_Text progress = PlacedText(rect, "ProgressText", typography, TextStyle.Label, 20f, TextAlignmentOptions.Center, TextColor, 50f, 102f, 710f, 134f);
            TMP_Text description = PlacedText(rect, "DescriptionText", typography, TextStyle.Body, 20f, TextAlignmentOptions.TopLeft, TextColor, 60f, 146f, 700f, 296f);

            RectTransform objectives = Child(screen, "Objectives");
            objectives.SetParent(rect, false);
            Place(objectives, 60f, 306f, 700f, 556f);
            List<Object> rows = new List<Object>();
            foreach (RectTransform row in objectives)
            {
                RectTransform box = (RectTransform)row.Find("Box");
                box.anchorMin = new Vector2(0f, 0.18f);
                box.anchorMax = new Vector2(0.05f, 0.82f);
                RectTransform goal = (RectTransform)row.Find("Goal");
                goal.anchorMin = new Vector2(0.07f, 0f);
                rows.Add(row.GetComponent<UiWidget>());
            }

            UiWidget close = Nest<UiWidget>(AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab"), rect, "CloseButton");
            Place(close.Rect, 270f, 574f, 490f, 626f);

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("progressText").objectReferenceValue = progress;
            serialized.FindProperty("descriptionText").objectReferenceValue = description;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            SerializedProperty list = serialized.FindProperty("objectiveRows");
            list.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return view;
        }

        #region HELPERS
        private static RectTransform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                throw new InvalidOperationException(parent.name + " has no child " + name);
            }

            return (RectTransform)child;
        }

        private static void Shrinkable(TMP_Text text, float sizeFactor, TextAlignmentOptions alignment)
        {
            text.fontSize *= sizeFactor;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = text.fontSize * AUTO_SIZE_MINIMUM;
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = alignment;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("Missing sprite " + path);
            }

            return sprite;
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = LayerMask.NameToLayer("UI");
            return gameObject;
        }

        private static TMP_Text PlacedText(Transform parent, string name, TypographySettings typography, TextStyle style, float size, TextAlignmentOptions alignment,
            Color color, float x0, float y0, float x1, float y1)
        {
            GameObject gameObject = NewUi(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.color = color;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * AUTO_SIZE_MINIMUM;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            gameObject.AddComponent<TypographyText>().Configure(typography, style);
            Place(text.rectTransform, x0, y0, x1, y1);
            return text;
        }

        private static T Nest<T>(GameObject prefab, Transform parent, string name)
            where T : Component
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        /// <summary>Places a rect given in the parent's units from its top left: x0, y0, x1, y1.</summary>
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x0, -y0);
            rect.sizeDelta = new Vector2(x1 - x0, y1 - y0);
        }
        #endregion
    }
}
