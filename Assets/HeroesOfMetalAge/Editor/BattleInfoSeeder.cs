using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off generation tool for auto combat, quick combat and the info cards. It created the <c>CasualtiesPanel</c> widget
    /// (what each side lost) and nested it into the Victory and Defeat screens, created the <c>InfoPopup</c> overlay (the
    /// card shown while the right mouse button is held) and nested it at the top of GameUiRoot, and added the AUTO button to
    /// the Combat screen and the quick combat toggle to the Options screen. Every step only runs while what it creates is
    /// missing, so the tool does nothing once it ran and the prefabs are edited by hand from then on. The project works
    /// without it: delete this file and its call in <see cref="PrototypePipeline.Setup"/>, and the mentions in README.md and
    /// Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class BattleInfoSeeder
    {
        private const string SPRITES = "Assets/HeroesOfMetalAge/Content/UI/Sprites/Gameplay";
        private const string WIDGETS = ContentAssets.UI_FOLDER + "/Widgets";
        private const string SCREENS = ContentAssets.UI_FOLDER + "/Screens";
        private const string CASUALTIES_PATH = WIDGETS + "/CasualtiesPanel.prefab";
        private const string INFO_POPUP_PATH = SCREENS + "/InfoPopup.prefab";
        private const int SLOT_COUNT = 7;
        private const float AUTO_SIZE_MINIMUM = 0.55f;

        private static readonly Color TextColor = new Color(0.93f, 0.89f, 0.8f, 1f);
        private static readonly Color GoldColor = new Color(0.93f, 0.76f, 0.45f, 1f);
        private static readonly Color LoreColor = new Color(0.82f, 0.72f, 0.55f, 1f);

        [MenuItem("Tools/Heroes of Metal Age/Create missing battle info assets")]
        public static void CreateMissingAssets()
        {
            TypographySettings typography = TypographyTools.Settings;
            if (typography == null || ContentAssets.RootPrefab == null)
            {
                return;
            }

            GameObject casualties = CreateCasualtiesPanel(typography);
            NestCasualties(SCREENS + "/Victory.prefab", casualties);
            NestCasualties(SCREENS + "/Defeat.prefab", casualties);
            GameObject popup = CreateInfoPopup(typography);
            AddAutoButton();
            AddQuickCombatToggle();
            NestPopupIntoRoot(popup);
            AssetDatabase.SaveAssets();
        }

        /// <summary>A framed panel with the player's and the enemy's losses, seven unit slots each.</summary>
        private static GameObject CreateCasualtiesPanel(TypographySettings typography)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CASUALTIES_PATH);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = NewUi("CasualtiesPanel", null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(460f, 400f);
            Image frame = root.AddComponent<Image>();
            frame.sprite = LoadSprite(SPRITES + "/Dialog/PanelFrame.png");
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 2.6f;
            frame.raycastTarget = true;
            CasualtiesView view = root.AddComponent<CasualtiesView>();
            TMP_Text title = PlacedText(root.transform, "TitleText", typography, TextStyle.Heading, 26f, TextAlignmentOptions.Center, GoldColor, 30, 34, 430, 80);
            title.text = "CASUALTIES";
            TMP_Text playerTitle = PlacedText(root.transform, "PlayerTitle", typography, TextStyle.Label, 18f, TextAlignmentOptions.MidlineLeft, TextColor, 44, 86, 416, 116);
            ArmySlotsView playerSlots = SlotRow(root.transform, "PlayerSlots", new[] { 44f, 120f, 416f, 206f });
            TMP_Text enemyTitle = PlacedText(root.transform, "EnemyTitle", typography, TextStyle.Label, 18f, TextAlignmentOptions.MidlineLeft, TextColor, 44, 222, 416, 252);
            ArmySlotsView enemySlots = SlotRow(root.transform, "EnemySlots", new[] { 44f, 256f, 416f, 342f });
            Assign(view, "playerTitleText", playerTitle);
            Assign(view, "playerSlots", playerSlots);
            Assign(view, "enemyTitleText", enemyTitle);
            Assign(view, "enemySlots", enemySlots);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CASUALTIES_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // seven army slots without names: the unit's card and the count lost
        private static ArmySlotsView SlotRow(Transform parent, string name, float[] box)
        {
            RectTransform row = (RectTransform)NewUi(name, parent).transform;
            Place(row, box);
            ArmySlotsView view = row.gameObject.AddComponent<ArmySlotsView>();
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/ArmySlot.prefab");
            float width = (box[2] - box[0]) / SLOT_COUNT;
            List<Object> slots = new List<Object>();
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                UiWidget slot = Nest<UiWidget>(slotPrefab, row, "Slot" + (i + 1));
                Place(slot.Rect, new[] { i * width + 1f, 0f, (i + 1) * width - 1f, box[3] - box[1] });
                Transform unitName = slot.transform.Find("Name");
                if (unitName != null)
                {
                    unitName.gameObject.SetActive(false);
                }

                Transform count = slot.transform.Find("Count");
                if (count != null)
                {
                    Anchor((RectTransform)count, new Vector2(0.1f, 0.02f), new Vector2(0.96f, 0.32f));
                }

                Transform icon = slot.transform.Find("Icon");
                if (icon != null)
                {
                    Anchor((RectTransform)icon, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
                }

                slots.Add(slot);
            }

            AssignList(view, "slots", slots);
            return view;
        }

        private static void NestCasualties(string screenPath, GameObject casualties)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(screenPath);
            try
            {
                CanvasScreenView view = root.GetComponent<CanvasScreenView>();
                SerializedObject serialized = new SerializedObject(view);
                SerializedProperty property = serialized.FindProperty("casualties");
                if (property == null || property.objectReferenceValue != null)
                {
                    return;
                }

                CasualtiesView panel = Nest<CasualtiesView>(casualties, root.transform, "Casualties");
                Place((RectTransform)panel.transform, new[] { 24f, 360f, 484f, 760f });
                property.objectReferenceValue = panel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, screenPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The info card overlay: a framed card with a picture, a title, the numbers, a text and a line of lore.</summary>
        private static GameObject CreateInfoPopup(TypographySettings typography)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(INFO_POPUP_PATH);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = NewUi("InfoPopup", null);
            Stretch((RectTransform)root.transform);
            // its own Canvas keeps the card's changes out of the game UI's rebuilds; it never takes clicks
            root.AddComponent<Canvas>();
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            InfoPopupView view = root.AddComponent<InfoPopupView>();
            Image panel = NewImage(root.transform, "Card", LoadSprite(SPRITES + "/Dialog/PanelFrame.png"), false);
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = 2f;
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(640f, 560f);
            Image icon = NewImage(panel.transform, "Icon", null, false);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new[] { 44f, 52f, 220f, 228f });
            TMP_Text title = PlacedText(panel.transform, "TitleText", typography, TextStyle.Title, 30f, TextAlignmentOptions.MidlineLeft, GoldColor, 236, 52, 596, 102);
            TMP_Text subtitle = PlacedText(panel.transform, "SubtitleText", typography, TextStyle.Label, 18f, TextAlignmentOptions.MidlineLeft, TextColor, 236, 104, 596, 138);
            TMP_Text stats = PlacedText(panel.transform, "StatsText", typography, TextStyle.Label, 19f, TextAlignmentOptions.TopLeft, TextColor, 236, 146, 596, 330);
            TMP_Text body = PlacedText(panel.transform, "BodyText", typography, TextStyle.Body, 18f, TextAlignmentOptions.TopLeft, TextColor, 44, 336, 596, 422);
            TMP_Text lore = PlacedText(panel.transform, "LoreText", typography, TextStyle.Body, 17f, TextAlignmentOptions.TopLeft, LoreColor, 44, 426, 596, 506);
            Assign(view, "panel", panelRect);
            Assign(view, "iconImage", icon);
            Assign(view, "titleText", title);
            Assign(view, "subtitleText", subtitle);
            Assign(view, "statsText", stats);
            Assign(view, "bodyText", body);
            Assign(view, "loreText", lore);
            panel.gameObject.SetActive(false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, INFO_POPUP_PATH);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>The AUTO button sits beside the war council's heading.</summary>
        private static void AddAutoButton()
        {
            string path = SCREENS + "/Combat.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SerializedObject serialized = new SerializedObject(root.GetComponent<CombatCanvasView>());
                SerializedProperty property = serialized.FindProperty("autoButton");
                if (property.objectReferenceValue != null)
                {
                    return;
                }

                UiWidget button = Nest<UiWidget>(AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab"), root.transform, "AutoButton");
                Place(button.Rect, new[] { 1724f, 136f, 1872f, 186f });
                property.objectReferenceValue = button;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The quick combat toggle is a copy of the mute toggle and its label, on the same row.</summary>
        private static void AddQuickCombatToggle()
        {
            string path = SCREENS + "/Options.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SerializedObject serialized = new SerializedObject(root.GetComponent<SettingsCanvasView>());
                SerializedProperty property = serialized.FindProperty("quickCombatToggle");
                if (property.objectReferenceValue != null)
                {
                    return;
                }

                Toggle mute = (Toggle)serialized.FindProperty("muteToggle").objectReferenceValue;
                Transform muteLabel = mute.transform.parent.Find(mute.name + "Label");
                Toggle quick = Object.Instantiate(mute.gameObject, mute.transform.parent).GetComponent<Toggle>();
                quick.name = "QuickCombatToggle";
                RectTransform quickRect = (RectTransform)quick.transform;
                quickRect.anchoredPosition += new Vector2(392f, 0f);
                if (muteLabel != null)
                {
                    TMP_Text label = Object.Instantiate(muteLabel.gameObject, muteLabel.parent).GetComponent<TMP_Text>();
                    label.name = "QuickCombatToggleLabel";
                    label.text = "Quick combat";
                    label.rectTransform.anchoredPosition += new Vector2(392f, 0f);
                }

                property.objectReferenceValue = quick;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The card is the last child of the design frame, so it draws over every screen.</summary>
        private static void NestPopupIntoRoot(GameObject popup)
        {
            string rootPath = ContentAssets.UI_FOLDER + "/GameUiRoot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(rootPath);
            try
            {
                GameHud hud = root.GetComponent<GameHud>();
                SerializedObject serialized = new SerializedObject(hud);
                SerializedProperty property = serialized.FindProperty("infoPopup");
                if (property.objectReferenceValue != null)
                {
                    return;
                }

                RectTransform frame = (RectTransform)serialized.FindProperty("designFrame").objectReferenceValue;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(popup, frame);
                instance.transform.SetAsLastSibling();
                property.objectReferenceValue = instance.GetComponent<InfoPopupView>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, rootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #region HELPERS
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
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            gameObject.layer = LayerMask.NameToLayer("UI");
            return gameObject;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, bool isRaycastTarget)
        {
            Image image = NewUi(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = isRaycastTarget;
            return image;
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
            Place(text.rectTransform, new[] { x0, y0, x1, y1 });
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
        private static void Place(RectTransform rect, float[] box)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(box[0], -box[1]);
            rect.sizeDelta = new Vector2(box[2] - box[0], box[3] - box[1]);
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Assign(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException(target.GetType().Name + " has no serialized field " + field);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignList(Object target, string field, List<Object> values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion
    }
}
