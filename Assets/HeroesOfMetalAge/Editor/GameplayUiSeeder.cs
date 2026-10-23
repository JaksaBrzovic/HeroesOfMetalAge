using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// One-off generation tool: it rebuilt the gameplay screens in the style of the UI mockups. The artwork was cut from the
    /// mockups into <c>Content/UI/Sprites/Gameplay/</c> (every text erased, viewports cut out) by the scripts in
    /// <c>Tools/UiArt/</c>, which also wrote one layout per screen into <c>Editor/UiLayouts/</c>. From those it created the widget
    /// prefabs <c>HotspotButton</c>, <c>ItemSlot</c> and <c>ObjectiveRow</c>, restyled <c>Button</c>, <c>ArmySlot</c>, the
    /// notifications, the development panel and the dialogue box, and rebuilt the Adventure, Combat, Town, Hero, Dialog,
    /// Defeat, Victory, Rewards and Options prefabs: each screen's artwork as its backdrop with live texts (in the
    /// typography's styles), images, buttons and slots placed over it and wired to the screen's view. It removed the shared
    /// Header and Footer (every screen now has its own) and the unused ArtefactCard and RecruitmentCard widgets. A screen whose
    /// backdrop already exists is left as it is, so the tool has nothing to do once it ran; the prefabs are authored assets
    /// edited in Prefab Mode from then on. The project works without it: delete this file, the <c>Editor/UiLayouts/</c> folder
    /// and its call in <see cref="PrototypePipeline.Setup"/>, and the mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class GameplayUiSeeder
    {
        private const string SPRITES = "Assets/HeroesOfMetalAge/Content/UI/Sprites/Gameplay";
        private const string OBJECT_SPRITES = "Assets/HeroesOfMetalAge/Content/World/MapObjects";
        private const string LAYOUTS = "Assets/HeroesOfMetalAge/Editor/UiLayouts";
        private const string WIDGETS = ContentAssets.UI_FOLDER + "/Widgets";
        private const string SCREENS = ContentAssets.UI_FOLDER + "/Screens";
        private const string BACKDROP = "Backdrop";
        private const string FRAME = "Frame";
        private const int MAX_TEXTURE_SIZE = 2048;
        private const float AUTO_SIZE_MINIMUM = 0.55f;

        private static readonly Color TextColor = new Color(0.93f, 0.89f, 0.8f, 1f);
        private static readonly Color GoldColor = new Color(0.93f, 0.76f, 0.45f, 1f);
        private static readonly Color InkColor = new Color(0.2f, 0.12f, 0.05f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Vector4 PanelBorder = new Vector4(110f, 120f, 110f, 140f);
        private static readonly Vector4 ButtonBorder = new Vector4(42f, 38f, 42f, 38f);
        private static readonly Vector4 GlowBorder = new Vector4(30f, 30f, 30f, 30f);
        private static readonly string[] Screens = { "Adventure", "Combat", "Town", "Hero", "Dialog", "Defeat", "Victory", "Rewards", "Options" };

        [MenuItem("Tools/Heroes of Metal Age/Create missing gameplay UI")]
        public static void CreateMissingAssets()
        {
            if (!AssetDatabase.IsValidFolder(LAYOUTS))
            {
                return;
            }

            ImportSprites();
            TypographySettings typography = TypographyTools.Settings;
            if (typography == null)
            {
                throw new InvalidOperationException("The gameplay UI needs " + ContentAssets.TYPOGRAPHY_PATH + "; create the visual style assets first.");
            }

            Widgets widgets = new Widgets(typography);
            foreach (string screen in Screens)
            {
                BuildScreen(screen, widgets, typography);
            }

            BuildDialogueBox(typography);
            BuildDebugTabs(typography);
            RestyleChrome();
            RemoveSharedHeaderAndFooter();
            AssetDatabase.SaveAssets();
        }

        #region SPRITES
        /// <summary>Imports the gameplay UI and map object pictures as sprites (with the frames' 9-slice borders).</summary>
        internal static void ImportSprites()
        {
            foreach (string folder in new[] { SPRITES, OBJECT_SPRITES })
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Vector4 border = BorderOf(Path.GetFileNameWithoutExtension(path));
                    bool isWorld = path.StartsWith(OBJECT_SPRITES, StringComparison.Ordinal);
                    if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border && importer.mipmapEnabled == isWorld)
                    {
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = isWorld;
                    importer.maxTextureSize = MAX_TEXTURE_SIZE;
                    importer.spriteBorder = border;
                    importer.SaveAndReimport();
                }
            }
        }

        private static Vector4 BorderOf(string file)
        {
            switch (file)
            {
                case "PanelFrame":
                    return PanelBorder;
                case "ButtonFrame":
                    return ButtonBorder;
                case "ButtonGlow":
                    return GlowBorder;
                case "Divider":
                    return new Vector4(60f, 0f, 60f, 0f);
                default:
                    return Vector4.zero;
            }
        }

        private static Sprite Sprite(string folder, string file)
        {
            string path = folder + "/" + file + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("Missing gameplay UI sprite " + path);
            }

            return sprite;
        }

        private static Sprite Common(string file)
        {
            return Sprite(SPRITES + "/Common", file);
        }
        #endregion

        #region SCREENS
        private static void BuildScreen(string screen, Widgets widgets, TypographySettings typography)
        {
            string path = SCREENS + "/" + screen + ".prefab";
            Layout layout = Layout.Load(LAYOUTS + "/" + screen + ".json");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.transform.Find(BACKDROP) != null || root.transform.Find(FRAME) != null)
                {
                    return;
                }

                CanvasScreenView view = root.GetComponent<CanvasScreenView>();
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }

                Builder builder = new Builder(view, layout, widgets, typography);
                builder.Build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildDialogueBox(TypographySettings typography)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.DIALOGUE_BOX_PATH);
            try
            {
                Layout layout = Layout.Load(LAYOUTS + "/DialogueBox.json");
                Image background = root.GetComponent<Image>();
                Sprite frame = Sprite(layout.spriteFolder, layout.backdrop);
                if (background.sprite == frame)
                {
                    return;
                }

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }

                background.sprite = frame;
                background.type = Image.Type.Simple;
                background.color = Color.white;
                RectTransform rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(layout.frameSize[0], layout.frameSize[1]);
                Builder builder = new Builder(root.GetComponent<DialogueBoxView>(), layout, null, typography);
                builder.Elements(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.DIALOGUE_BOX_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The development panel in two tabs: GENERAL (options, the campaign shortcuts, dialogue previews) and SOUNDS (the
        /// audition buttons). Only while the panel has no tabs yet.
        /// </summary>
        private static void BuildDebugTabs(TypographySettings typography)
        {
            string path = SCREENS + "/DebugPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                DebugPanelView view = root.GetComponent<DebugPanelView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("generalPage").objectReferenceValue != null)
                {
                    return;
                }

                // room for the frame's border, and a title that says how to open the panel
                VerticalLayoutGroup panelLayout = root.GetComponent<VerticalLayoutGroup>();
                panelLayout.padding = new RectOffset(48, 48, 60, 52);
                panelLayout.spacing = 12f;
                TMP_Text title = root.transform.Find("TitleText").GetComponent<TMP_Text>();
                title.text = "DEVELOPMENT [D]";

                RectTransform cueButtons = (RectTransform)serialized.FindProperty("cueButtons").objectReferenceValue;
                Transform actions = ((Component)serialized.FindProperty("terrainButton").objectReferenceValue).transform.parent;
                GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab");
                int index = cueButtons.GetSiblingIndex();

                RectTransform tabs = LayoutRow("Tabs", root.transform, 52.8f);
                tabs.SetSiblingIndex(index);
                UiWidget generalTab = Nest<UiWidget>(buttonPrefab, tabs, "GeneralTab");
                UiWidget soundsTab = Nest<UiWidget>(buttonPrefab, tabs, "SoundsTab");

                RectTransform general = Page("GeneralPage", root.transform);
                general.SetSiblingIndex(index + 1);
                Heading(general, "OptionsTitle", "OPTIONS", typography);
                RectTransform options = Grid("OptionButtons", general, new Vector2(340f, 52.8f));
                Heading(general, "CampaignTitle", "CAMPAIGN", typography);
                actions.SetParent(general, false);
                // the framed buttons' sprite borders would make the row as tall as the frame; the row decides
                LayoutElement actionsLayout = actions.GetComponent<LayoutElement>();
                actionsLayout.minHeight = 52.8f;
                actionsLayout.preferredHeight = 52.8f;
                Heading(general, "DialogueTitle", "DIALOGUE PREVIEW", typography);
                RectTransform dialogue = Grid("ActionButtons", general, new Vector2(262f, 48f));

                RectTransform sounds = Page("SoundsPage", root.transform);
                sounds.SetSiblingIndex(index + 2);
                cueButtons.SetParent(sounds, false);
                GridLayoutGroup cueGrid = cueButtons.GetComponent<GridLayoutGroup>();
                cueGrid.cellSize = new Vector2(208f, 48f);
                cueGrid.spacing = new Vector2(8f, 8f);
                LayoutElement cueLayout = cueButtons.GetComponent<LayoutElement>();
                if (cueLayout != null)
                {
                    cueLayout.preferredHeight = -1f;
                    cueLayout.flexibleHeight = 1f;
                }

                serialized.FindProperty("generalTab").objectReferenceValue = generalTab;
                serialized.FindProperty("soundsTab").objectReferenceValue = soundsTab;
                serialized.FindProperty("generalPage").objectReferenceValue = general.gameObject;
                serialized.FindProperty("soundsPage").objectReferenceValue = sounds.gameObject;
                serialized.FindProperty("optionButtons").objectReferenceValue = options;
                serialized.FindProperty("actionButtons").objectReferenceValue = dialogue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                sounds.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform LayoutRow(string name, Transform parent, float height)
        {
            RectTransform row = (RectTransform)NewUi(name, parent).transform;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 9.6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return row;
        }

        private static RectTransform Page(string name, Transform parent)
        {
            RectTransform page = (RectTransform)NewUi(name, parent).transform;
            VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            page.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            return page;
        }

        private static RectTransform Grid(string name, Transform parent, Vector2 cell)
        {
            RectTransform grid = (RectTransform)NewUi(name, parent).transform;
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = cell;
            layout.spacing = new Vector2(8f, 8f);
            return grid;
        }

        private static void Heading(Transform parent, string name, string text, TypographySettings typography)
        {
            TMP_Text heading = Text(parent, name, typography, TextStyle.Heading, 24f, TextAlignmentOptions.MidlineLeft, GoldColor);
            heading.text = text;
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
        }

        /// <summary>The notices and the development panel take the mockups' frames.</summary>
        private static void RestyleChrome()
        {
            Restyle(SCREENS + "/Notifications.prefab", image => image.name == "Notice" || image.name == "Impact", Sprite(SPRITES + "/Dialog", "ButtonFrame"), 2.4f);
            Restyle(SCREENS + "/DebugPanel.prefab", image => image.transform.parent == null,
                Sprite(SPRITES + "/Dialog", "PanelFrame"), 2f);
        }

        private static void Restyle(string path, Func<Image, bool> isPanel, Sprite sprite, float multiplier)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool isChanged = false;
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.GetComponent<Button>() != null || image.GetComponentInParent<UiWidget>(true) != null || !isPanel(image) || image.sprite == sprite)
                    {
                        continue;
                    }

                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = multiplier;
                    image.color = Color.white;
                    isChanged = true;
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

        /// <summary>Every screen carries its own header and footer now; the shared ones leave the root.</summary>
        private static void RemoveSharedHeaderAndFooter()
        {
            string rootPath = ContentAssets.UI_FOLDER + "/GameUiRoot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(rootPath);
            try
            {
                bool isChanged = false;
                // the root's instances of the screens must show the screens' own header and footer
                foreach (CanvasScreenView view in root.GetComponentsInChildren<CanvasScreenView>(true))
                {
                    PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(view.gameObject);
                    if (modifications == null)
                    {
                        continue;
                    }

                    PropertyModification[] kept = modifications
                        .Where(modification => !(modification.target is CanvasScreenView && (modification.propertyPath == "header" || modification.propertyPath == "footer")))
                        .ToArray();
                    if (kept.Length != modifications.Length)
                    {
                        PrefabUtility.SetPropertyModifications(view.gameObject, kept);
                        isChanged = true;
                    }
                }

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true).ToList())
                {
                    if (child != null && (child.GetComponent<HeaderView>() != null || child.GetComponent<FooterView>() != null)
                        && child.GetComponentInParent<CanvasScreenView>(true) == null)
                    {
                        Object.DestroyImmediate(child.gameObject);
                        isChanged = true;
                    }
                }

                if (isChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, rootPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (string unused in new[] { SCREENS + "/Header.prefab", SCREENS + "/Footer.prefab", WIDGETS + "/ArtefactCard.prefab", WIDGETS + "/RecruitmentCard.prefab" })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(unused) != null)
                {
                    AssetDatabase.DeleteAsset(unused);
                }
            }
        }
        #endregion

        #region LAYOUT DATA
        [Serializable]
        private sealed class Layout
        {
            public string screen;
            public string backdrop;
            public bool fullscreen = true;
            public float[] frameSize;
            public float[] viewport;
            public string spriteFolder;
            public Element[] elements;

            public static Layout Load(string path)
            {
                return JsonUtility.FromJson<Layout>(File.ReadAllText(path));
            }
        }

        [Serializable]
        private sealed class Element
        {
            public string name;
            public string type;
            public string field;
            public string style;
            public string align;
            public string text;
            public string color;
            public string sprite;
            public string place;
            public float[] rect;
            public float[] label;
            public float[] portrait;
            public float[] nameRect;
            public float[] description;
            public float[] stats;
            public float[] button;
            public float[] rects;
            public float[] handleSize;
            public float size;
            public int rows;
            public bool named;
            public float lineSpacing;
        }
        #endregion

        #region WIDGET PREFABS
        /// <summary>The reusable widget prefabs the screens nest: created when missing, restyled once.</summary>
        private sealed class Widgets
        {
            private readonly TypographySettings typography;

            public Widgets(TypographySettings typography)
            {
                this.typography = typography;
                Hotspot = CreateHotspot();
                ItemSlot = CreateItemSlot();
                ObjectiveRow = CreateObjectiveRow();
                ArmySlot = RestyleArmySlot();
                FramedButton = RestyleButton();
            }

            /// <summary>A transparent button over a button painted on the backdrop: a golden glow on hover and its caption.</summary>
            public GameObject Hotspot { get; }
            public GameObject ItemSlot { get; }
            public GameObject ObjectiveRow { get; }
            public GameObject ArmySlot { get; }
            /// <summary>A button with its own painted frame, for places the backdrop has none.</summary>
            public GameObject FramedButton { get; }

            private GameObject CreateHotspot()
            {
                string path = WIDGETS + "/HotspotButton.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null)
                {
                    return existing;
                }

                GameObject root = NewUi("HotspotButton", null);
                Image glow = Glow(root);
                Button button = root.AddComponent<Button>();
                Hover(button, glow);
                TMP_Text title = Text(root.transform, "Title", typography, TextStyle.Button, 20f, TextAlignmentOptions.Center, TextColor);
                Stretch(title.rectTransform);
                UiWidget widget = root.AddComponent<UiWidget>();
                Wire(widget, ("button", button), ("titleText", title));
                return Save(root, path);
            }

            private GameObject CreateItemSlot()
            {
                string path = WIDGETS + "/ItemSlot.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null)
                {
                    return existing;
                }

                GameObject root = NewUi("ItemSlot", null);
                Image plate = NewImage(root.transform, "Plate", Sprite(SPRITES + "/Hero", "SlotPlate"), false);
                Anchor(plate.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
                plate.color = Color.clear;
                Image icon = NewImage(root.transform, "Icon", null, false);
                icon.preserveAspect = true;
                Anchor(icon.rectTransform, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.92f));
                TMP_Text label = Text(root.transform, "Label", typography, TextStyle.Label, 14f, TextAlignmentOptions.Center, TextColor);
                Anchor(label.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.26f));
                Image glow = Glow(root);
                Button button = root.AddComponent<Button>();
                Hover(button, glow);
                UiWidget widget = root.AddComponent<UiWidget>();
                Wire(widget, ("button", button), ("titleText", label), ("iconImage", icon), ("backgroundImage", plate));
                return Save(root, path);
            }

            private GameObject CreateObjectiveRow()
            {
                string path = WIDGETS + "/ObjectiveRow.prefab";
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null)
                {
                    return existing;
                }

                GameObject root = NewUi("ObjectiveRow", null);
                Image box = NewImage(root.transform, "Box", Sprite(SPRITES + "/Options", "CheckboxFrame"), false);
                box.preserveAspect = true;
                Anchor(box.rectTransform, new Vector2(0f, 0.1f), new Vector2(0.11f, 0.9f));
                Image check = NewImage(box.transform, "Check", Common("CheckMark"), false);
                check.preserveAspect = true;
                Stretch(check.rectTransform);
                TMP_Text goal = Text(root.transform, "Goal", typography, TextStyle.Body, 18f, TextAlignmentOptions.MidlineLeft, TextColor);
                Anchor(goal.rectTransform, new Vector2(0.15f, 0f), Vector2.one);
                UiWidget widget = root.AddComponent<UiWidget>();
                Wire(widget, ("titleText", goal), ("iconImage", check));
                return Save(root, path);
            }

            /// <summary>The army slot becomes a frameless overlay for the slot frames painted on the screens.</summary>
            private GameObject RestyleArmySlot()
            {
                string path = WIDGETS + "/ArmySlot.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.transform.Find("Glow") == null)
                    {
                        for (int i = root.transform.childCount - 1; i >= 0; i--)
                        {
                            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                        }

                        foreach (Graphic graphic in root.GetComponents<Graphic>())
                        {
                            Object.DestroyImmediate(graphic);
                        }

                        Image icon = NewImage(root.transform, "Icon", null, false);
                        icon.preserveAspect = true;
                        Anchor(icon.rectTransform, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.96f));
                        TMP_Text count = Text(root.transform, "Count", typography, TextStyle.Number, 24f, TextAlignmentOptions.BottomRight, TextColor);
                        Anchor(count.rectTransform, new Vector2(0.3f, 0.12f), new Vector2(0.94f, 0.36f));
                        TMP_Text unitName = Text(root.transform, "Name", typography, TextStyle.Label, 13f, TextAlignmentOptions.Center, TextColor);
                        Anchor(unitName.rectTransform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.14f));
                        GameObject glowObject = NewUi("Glow", root.transform);
                        Stretch((RectTransform)glowObject.transform);
                        Image glow = Glow(glowObject);
                        Button button = root.GetComponent<Button>();
                        if (button == null)
                        {
                            button = root.AddComponent<Button>();
                        }

                        Hover(button, glow);
                        UiWidget widget = root.GetComponent<UiWidget>();
                        if (widget == null)
                        {
                            widget = root.AddComponent<UiWidget>();
                        }

                        Wire(widget, ("button", button), ("titleText", unitName), ("amountText", count), ("iconImage", icon), ("descriptionText", null),
                            ("backgroundImage", null));
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            /// <summary>The generic button takes the mockups' button frame.</summary>
            private GameObject RestyleButton()
            {
                string path = WIDGETS + "/Button.prefab";
                Sprite frame = Sprite(SPRITES + "/Dialog", "ButtonFrame");
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Image image = root.GetComponent<Image>();
                    if (image.sprite != frame)
                    {
                        image.sprite = frame;
                        image.type = Image.Type.Sliced;
                        image.pixelsPerUnitMultiplier = 2f;
                        image.color = Color.white;
                        Transform border = root.transform.Find("Border");
                        if (border != null)
                        {
                            Object.DestroyImmediate(border.gameObject);
                        }

                        Button button = root.GetComponent<Button>();
                        ColorBlock colors = button.colors;
                        colors.normalColor = Color.white;
                        colors.highlightedColor = new Color(1f, 0.92f, 0.75f, 1f);
                        colors.pressedColor = new Color(0.85f, 0.72f, 0.5f, 1f);
                        colors.selectedColor = new Color(1f, 0.95f, 0.85f, 1f);
                        colors.disabledColor = new Color(0.45f, 0.42f, 0.4f, 1f);
                        button.colors = colors;
                        TMP_Text title = root.GetComponentInChildren<TMP_Text>(true);
                        if (title != null)
                        {
                            title.color = TextColor;
                            RectTransform titleRect = title.rectTransform;
                            titleRect.offsetMin = new Vector2(14f, 6f);
                            titleRect.offsetMax = new Vector2(-14f, -6f);
                        }

                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            private static GameObject Save(GameObject root, string path)
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                return prefab;
            }
        }
        #endregion

        #region BUILDER
        /// <summary>Places a layout's elements over its backdrop and wires them to the view's serialized fields.</summary>
        private sealed class Builder
        {
            private readonly MonoBehaviour view;
            private readonly Layout layout;
            private readonly Widgets widgets;
            private readonly TypographySettings typography;
            private readonly Dictionary<string, List<Object>> lists = new Dictionary<string, List<Object>>();
            private HeaderView header;
            private FooterView footer;
            private Transform headerRoot;
            private Transform footerRoot;

            public Builder(MonoBehaviour view, Layout layout, Widgets widgets, TypographySettings typography)
            {
                this.view = view;
                this.layout = layout;
                this.widgets = widgets;
                this.typography = typography;
            }

            public void Build(Transform root)
            {
                Sprite backdrop = Sprite(layout.spriteFolder, layout.backdrop);
                Transform parent = root;
                if (layout.fullscreen)
                {
                    Image image = NewImage(root, BACKDROP, backdrop, false);
                    Stretch(image.rectTransform);
                    if (layout.viewport != null && layout.viewport.Length == 4)
                    {
                        string viewportName = layout.screen == "Combat" ? "BattleViewport" : "MapViewport";
                        RectTransform viewport = (RectTransform)NewUi(viewportName, root).transform;
                        Place(viewport, layout.viewport);
                        Assign(view, layout.screen == "Combat" ? "battleViewport" : "mapViewport", viewport);
                    }
                }
                else
                {
                    Image dim = NewImage(root, "InputBlocker", null, true);
                    dim.color = DimColor;
                    Stretch(dim.rectTransform);
                    Image frame = NewImage(root, FRAME, backdrop, true);
                    RectTransform frameRect = frame.rectTransform;
                    frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                    frameRect.pivot = new Vector2(0.5f, 0.5f);
                    frameRect.sizeDelta = new Vector2(layout.frameSize[0], layout.frameSize[1]);
                    frameRect.anchoredPosition = Vector2.zero;
                    parent = frame.transform;
                }

                Elements(parent);
            }

            public void Elements(Transform parent)
            {
                foreach (Element element in layout.elements)
                {
                    Transform target = ParentFor(element, parent);
                    Object built = Create(element, target);
                    if (!string.IsNullOrEmpty(element.field) && built != null)
                    {
                        Wire(element.field, built);
                    }
                }

                foreach (KeyValuePair<string, List<Object>> list in lists)
                {
                    AssignList(view, list.Key, list.Value);
                }

                if (header != null)
                {
                    Assign(view, "header", header);
                }

                if (footer != null)
                {
                    Assign(view, "footer", footer);
                }
            }

            private Transform ParentFor(Element element, Transform parent)
            {
                if (element.field == null)
                {
                    return parent;
                }

                if (element.field.StartsWith("header.", StringComparison.Ordinal))
                {
                    if (headerRoot == null)
                    {
                        headerRoot = NewUi("Header", parent).transform;
                        Stretch((RectTransform)headerRoot);
                        header = headerRoot.gameObject.AddComponent<HeaderView>();
                    }

                    return headerRoot;
                }

                if (element.field.StartsWith("footer.", StringComparison.Ordinal))
                {
                    if (footerRoot == null)
                    {
                        footerRoot = NewUi("Footer", parent).transform;
                        Stretch((RectTransform)footerRoot);
                        footer = footerRoot.gameObject.AddComponent<FooterView>();
                    }

                    return footerRoot;
                }

                return parent;
            }

            private void Wire(string field, Object value)
            {
                if (field.StartsWith("header.", StringComparison.Ordinal))
                {
                    Assign(header, field.Substring("header.".Length), value);
                    return;
                }

                if (field.StartsWith("footer.", StringComparison.Ordinal))
                {
                    Assign(footer, field.Substring("footer.".Length), value);
                    return;
                }

                SerializedProperty property = new SerializedObject(view).FindProperty(field);
                if (property != null && property.isArray && property.propertyType != SerializedPropertyType.String)
                {
                    if (!lists.TryGetValue(field, out List<Object> list))
                    {
                        lists.Add(field, list = new List<Object>());
                    }

                    list.Add(value);
                    return;
                }

                Assign(view, field, value);
            }

            private Object Create(Element element, Transform parent)
            {
                switch (element.type)
                {
                    case "text":
                        return CreateText(element, parent);
                    case "image":
                        Image image = NewImage(parent, element.name, null, false);
                        image.preserveAspect = true;
                        Place(image.rectTransform, element.rect);
                        return image;
                    case "bar":
                        return CreateBar(element, parent);
                    case "button":
                        return CreateButton(element, parent, widgets.Hotspot, element.label);
                    case "framedButton":
                        return CreateButton(element, parent, widgets.FramedButton, null);
                    case "slots":
                        return CreateSlots(element, parent);
                    case "objectives":
                        CreateObjectives(element, parent);
                        return null;
                    case "equipment":
                    case "inventory":
                        return CreateItemSlot(element, parent);
                    case "recruit":
                        return CreateRecruitRow(element, parent);
                    case "slider":
                        return CreateSlider(element, parent);
                    case "toggle":
                        return CreateToggle(element, parent);
                    default:
                        throw new InvalidOperationException("Unknown layout element type " + element.type + " in " + layout.screen);
                }
            }

            private TMP_Text CreateText(Element element, Transform parent)
            {
                TextStyle style = ParseStyle(element.style, TextStyle.Body);
                TMP_Text text = Text(parent, element.name, typography, style, element.size, Alignment(element.align), ColorFor(element, style));
                Place(text.rectTransform, element.rect);
                text.text = element.text ?? string.Empty;
                if (element.lineSpacing != 0f)
                {
                    text.lineSpacing = element.lineSpacing;
                }

                return text;
            }

            private Image CreateBar(Element element, Transform parent)
            {
                RectTransform track = (RectTransform)NewUi(element.name, parent).transform;
                Place(track, element.rect);
                Sprite sprite = string.IsNullOrEmpty(element.sprite) ? null : Sprite(layout.spriteFolder, element.sprite);
                Image fill = NewImage(track, "Fill", sprite, false);
                Stretch(fill.rectTransform);
                if (sprite == null)
                {
                    fill.color = GoldColor;
                }

                return fill;
            }

            private UiWidget CreateButton(Element element, Transform parent, GameObject prefab, float[] label)
            {
                UiWidget button = Nest<UiWidget>(prefab, parent, element.name);
                Place(button.Rect, element.rect);
                TMP_Text title = button.GetComponentInChildren<TMP_Text>(true);
                title.text = element.text ?? string.Empty;
                title.fontSize = element.size > 0f ? element.size : title.fontSize;
                title.fontSizeMax = title.fontSize;
                title.fontSizeMin = title.fontSize * AUTO_SIZE_MINIMUM;
                if (!string.IsNullOrEmpty(element.style))
                {
                    TypographyText typographyText = title.GetComponent<TypographyText>();
                    typographyText.Configure(typography, ParseStyle(element.style, TextStyle.Button));
                }

                if (label != null && label.Length == 4)
                {
                    PlaceWithin(title.rectTransform, element.rect, label);
                }

                if (string.IsNullOrEmpty(element.text))
                {
                    title.gameObject.SetActive(false);
                }

                return button;
            }

            private ArmySlotsView CreateSlots(Element element, Transform parent)
            {
                float[] bounds = Bounds(element.rects);
                RectTransform container = (RectTransform)NewUi(element.name, parent).transform;
                Place(container, bounds);
                ArmySlotsView slotsView = container.gameObject.AddComponent<ArmySlotsView>();
                List<Object> slots = new List<Object>();
                for (int i = 0; i + 3 < element.rects.Length; i += 4)
                {
                    float[] rect = { element.rects[i], element.rects[i + 1], element.rects[i + 2], element.rects[i + 3] };
                    UiWidget slot = Nest<UiWidget>(widgets.ArmySlot, container, "Slot" + (i / 4 + 1));
                    PlaceWithin(slot.Rect, bounds, rect);
                    if (!element.named)
                    {
                        slot.transform.Find("Name").gameObject.SetActive(false);
                        Anchor((RectTransform)slot.transform.Find("Count"), new Vector2(0.3f, 0.03f), new Vector2(0.94f, 0.3f));
                        Anchor((RectTransform)slot.transform.Find("Icon"), new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.96f));
                    }

                    slots.Add(slot);
                }

                AssignList(slotsView, "slots", slots);
                return slotsView;
            }

            private void CreateObjectives(Element element, Transform parent)
            {
                RectTransform container = (RectTransform)NewUi(element.name, parent).transform;
                Place(container, element.rect);
                float rowHeight = 1f / Mathf.Max(1, element.rows);
                for (int i = 0; i < element.rows; i++)
                {
                    UiWidget row = Nest<UiWidget>(widgets.ObjectiveRow, container, "Goal" + (i + 1));
                    RectTransform rect = row.Rect;
                    rect.anchorMin = new Vector2(0f, 1f - (i + 1) * rowHeight);
                    rect.anchorMax = new Vector2(1f, 1f - i * rowHeight);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    rect.pivot = new Vector2(0f, 1f);
                    Wire(element.field, row);
                }
            }

            private UiWidget CreateItemSlot(Element element, Transform parent)
            {
                UiWidget slot = Nest<UiWidget>(widgets.ItemSlot, parent, element.name);
                Place(slot.Rect, element.rect);
                if (element.type == "inventory")
                {
                    slot.transform.Find("Label").gameObject.SetActive(false);
                    Anchor((RectTransform)slot.transform.Find("Icon"), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
                }

                return slot;
            }

            private RecruitRowView CreateRecruitRow(Element element, Transform parent)
            {
                RectTransform row = (RectTransform)NewUi(element.name, parent).transform;
                Place(row, element.rect);
                RecruitRowView rowView = row.gameObject.AddComponent<RecruitRowView>();
                Image portrait = NewImage(row, "Portrait", null, false);
                portrait.preserveAspect = true;
                PlaceWithin(portrait.rectTransform, element.rect, element.portrait);
                TMP_Text unitName = Text(row, "Name", typography, TextStyle.Title, 26f, TextAlignmentOptions.MidlineLeft, GoldColor);
                PlaceWithin(unitName.rectTransform, element.rect, element.nameRect);
                TMP_Text description = Text(row, "Description", typography, TextStyle.Body, 18f, TextAlignmentOptions.TopLeft, TextColor);
                PlaceWithin(description.rectTransform, element.rect, element.description);
                TMP_Text stats = Text(row, "Stats", typography, TextStyle.Label, 18f, TextAlignmentOptions.MidlineLeft, TextColor);
                PlaceWithin(stats.rectTransform, element.rect, element.stats);
                UiWidget recruit = Nest<UiWidget>(widgets.Hotspot, row, "RecruitButton");
                PlaceWithin(recruit.Rect, element.rect, element.button);
                TMP_Text caption = recruit.GetComponentInChildren<TMP_Text>(true);
                caption.fontSize = caption.fontSizeMax = 20f;
                caption.fontSizeMin = 11f;
                PlaceWithin(caption.rectTransform, element.button, element.label);
                Assign(rowView, "portraitImage", portrait);
                Assign(rowView, "nameText", unitName);
                Assign(rowView, "descriptionText", description);
                Assign(rowView, "statsText", stats);
                Assign(rowView, "recruitButton", recruit);
                return rowView;
            }

            private Slider CreateSlider(Element element, Transform parent)
            {
                RectTransform root = (RectTransform)NewUi(element.name, parent).transform;
                Place(root, element.rect);
                Slider slider = root.gameObject.AddComponent<Slider>();
                RectTransform fillArea = (RectTransform)NewUi("Fill Area", root).transform;
                Stretch(fillArea);
                Image fill = NewImage(fillArea, "Fill", Sprite(layout.spriteFolder, "SliderFill"), false);
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.sizeDelta = Vector2.zero;
                float grooveHeight = element.rect[3] - element.rect[1];
                float handleWidth = element.handleSize != null && element.handleSize.Length == 2 ? element.handleSize[0] : grooveHeight;
                float handleHeight = element.handleSize != null && element.handleSize.Length == 2 ? element.handleSize[1] : grooveHeight;
                RectTransform slideArea = (RectTransform)NewUi("Handle Slide Area", root).transform;
                Stretch(slideArea);
                float overhang = Mathf.Max(0f, (handleHeight - grooveHeight) / 2f);
                slideArea.offsetMin = new Vector2(handleWidth / 2f, -overhang);
                slideArea.offsetMax = new Vector2(-handleWidth / 2f, overhang);
                Image handle = NewImage(slideArea, "Handle", Sprite(layout.spriteFolder, "SliderHandle"), true);
                RectTransform handleRect = handle.rectTransform;
                handleRect.anchorMin = new Vector2(0f, 0f);
                handleRect.anchorMax = new Vector2(0f, 1f);
                handleRect.sizeDelta = new Vector2(handleWidth, 0f);
                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = handle;
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 0.8f;
                return slider;
            }

            private Toggle CreateToggle(Element element, Transform parent)
            {
                Image box = NewImage(parent, element.name, Sprite(layout.spriteFolder, "CheckboxFrame"), true);
                Place(box.rectTransform, element.rect);
                Image check = NewImage(box.transform, "Check", Common("CheckMark"), false);
                check.preserveAspect = true;
                Stretch(check.rectTransform);
                Toggle toggle = box.gameObject.AddComponent<Toggle>();
                toggle.targetGraphic = box;
                toggle.graphic = check;
                TMP_Text label = Text(parent, element.name + "Label", typography, TextStyle.Body, element.size, TextAlignmentOptions.MidlineLeft, TextColor);
                Place(label.rectTransform, element.label);
                label.text = element.text ?? string.Empty;
                return toggle;
            }

            private static float[] Bounds(float[] rects)
            {
                float x0 = float.MaxValue;
                float y0 = float.MaxValue;
                float x1 = float.MinValue;
                float y1 = float.MinValue;
                for (int i = 0; i + 3 < rects.Length; i += 4)
                {
                    x0 = Mathf.Min(x0, rects[i]);
                    y0 = Mathf.Min(y0, rects[i + 1]);
                    x1 = Mathf.Max(x1, rects[i + 2]);
                    y1 = Mathf.Max(y1, rects[i + 3]);
                }

                return new[] { x0, y0, x1, y1 };
            }

            private static Color ColorFor(Element element, TextStyle style)
            {
                if (element.color == "ink")
                {
                    return InkColor;
                }

                if (element.color == "gold" || style == TextStyle.Title || style == TextStyle.Heading)
                {
                    return GoldColor;
                }

                return style == TextStyle.Display ? Color.white : TextColor;
            }
        }
        #endregion

        #region HELPERS
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
            GameObject gameObject = NewUi(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = isRaycastTarget;
            return image;
        }

        private static Image Glow(GameObject target)
        {
            Image glow = target.AddComponent<Image>();
            glow.sprite = Common("ButtonGlow");
            glow.type = Image.Type.Sliced;
            glow.color = Color.white;
            glow.raycastTarget = true;
            return glow;
        }

        /// <summary>The glow is invisible at rest and shows on hover, press and keyboard selection; a disabled button is dimmed.</summary>
        private static void Hover(Button button, Graphic glow)
        {
            button.targetGraphic = glow;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.55f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.3f);
            colors.disabledColor = new Color(0.08f, 0.06f, 0.05f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }

        private static TMP_Text Text(Transform parent, string name, TypographySettings typography, TextStyle style, float size,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject gameObject = NewUi(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.color = color;
            text.fontSize = size > 0f ? size : 20f;
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = text.fontSize * AUTO_SIZE_MINIMUM;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            gameObject.AddComponent<TypographyText>().Configure(typography, style);
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

        /// <summary>Places a rect given in the same units as its parent's rect <paramref name="parentBox"/>.</summary>
        private static void PlaceWithin(RectTransform rect, float[] parentBox, float[] box)
        {
            Place(rect, new[] { box[0] - parentBox[0], box[1] - parentBox[1], box[2] - parentBox[0], box[3] - parentBox[1] });
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

        private static TextStyle ParseStyle(string style, TextStyle fallback)
        {
            return !string.IsNullOrEmpty(style) && Enum.TryParse(style, out TextStyle parsed) ? parsed : fallback;
        }

        private static TextAlignmentOptions Alignment(string align)
        {
            switch (align)
            {
                case "center":
                    return TextAlignmentOptions.Center;
                case "right":
                    return TextAlignmentOptions.MidlineRight;
                case "topleft":
                    return TextAlignmentOptions.TopLeft;
                default:
                    return TextAlignmentOptions.MidlineLeft;
            }
        }

        private static void Wire(Object target, params (string Field, Object Value)[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            foreach ((string field, Object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property != null)
                {
                    property.objectReferenceValue = value;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(target.GetType().Name + " has no serialized list " + field);
            }

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
