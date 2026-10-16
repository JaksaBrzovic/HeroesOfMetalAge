using System;
using HeroesOfMetalAge.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off bootstrap tool: it created the FrontEnd prefab (the main menu with its background, the animated New Game
    /// button and the Load Game, Credits and Quit placeholders, the black fade and the intro with its verses, title and
    /// burning curtain), the curtain's <c>Content/Rendering/IntroBurn.mat</c>, and set the main menu images in
    /// <c>Content/UI/Sprites/MainMenu/</c> to import as sprites. On a FrontEnd prefab from before the intro it replaced the
    /// placeholder intro (the nested end-of-day hourglass) with the new one and left everything else as it was. The prefab
    /// is now an authored asset edited by hand; the tool only adds what is missing and never changes anything that exists,
    /// so it has nothing to do once they exist. Safe to delete together with its call in
    /// <see cref="PrototypePipeline.Setup"/> and the mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class MainMenuAssetSeeder
    {
        private const string SPRITE_FOLDER = "Assets/HeroesOfMetalAge/Content/UI/Sprites/MainMenu";
        private const string BACKGROUND_PATH = SPRITE_FOLDER + "/MainMenuBackground.png";
        private const string TITLE_PATH = SPRITE_FOLDER + "/NewGameText.png";
        private const string FLEX_PATH_FORMAT = SPRITE_FOLDER + "/HoverFlex{0:00}.png";
        private const string HORNS_PATH_FORMAT = SPRITE_FOLDER + "/HornsClick{0:00}.png";
        private const string BUTTON_WIDGET_PATH = ContentAssets.UI_FOLDER + "/Widgets/Button.prefab";
        private const int FRAME_COUNT = 6;
        private const int LINE_COUNT = 3;
        private const float LINE_SPACING = 22f;
        private const float VERSE_FONT_SIZE = 56f;
        private const float TITLE_FONT_SIZE = 128f;
        private const float TITLE_CHARACTER_SPACING = 6f;
        // above GameUiRoot (0) and the dialogue overlay (10)
        private const int SORTING_ORDER = 20;
        private const float BUTTONS_MARGIN_RIGHT = 120f;
        private const float BUTTONS_MARGIN_TOP = 40f;
        private const float PLACEHOLDER_GAP = 18f;
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);
        // every arm frame and the title share one transparent canvas, so they line up
        private static readonly Vector2 NewGameSize = new Vector2(416f, 342f);
        private static readonly Vector2 PlaceholderSize = new Vector2(320f, 64f);
        private static readonly Vector2 VersesSize = new Vector2(1700f, 600f);
        private static readonly Vector2 TitleSize = new Vector2(1800f, 300f);
        private static readonly Color VerseColor = new Color(0.9f, 0.86f, 0.78f, 1f);
        private static readonly Color TitleColor = new Color(0.95f, 0.91f, 0.84f, 1f);
        private static readonly string[] PlaceholderNames = { "LoadGameButton", "CreditsButton", "QuitButton" };
        private static readonly string[] PlaceholderTitles = { "LOAD GAME", "CREDITS", "QUIT" };
        private static readonly string[] PlaceholderFields = { "loadGameButton", "creditsButton", "quitButton" };

        [MenuItem("Tools/Heroes of Metal Age/Create missing main menu assets")]
        public static void CreateMissingAssets()
        {
            Material burn = CreateMissingBurnMaterial();
            if (ContentAssets.FrontEndPrefab == null)
            {
                ImportAsSprites();
                CreateFrontEndPrefab(burn);
            }
            else
            {
                AddMissingIntro(burn);
            }

            AssetDatabase.SaveAssets();
        }

        private static Material CreateMissingBurnMaterial()
        {
            Material material = ContentAssets.IntroBurnMaterial;
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(ContentAssets.INTRO_BURN_SHADER);
            if (shader == null)
            {
                throw new InvalidOperationException("The intro curtain needs the shader " + ContentAssets.INTRO_BURN_SHADER + " (Content/Rendering/IntroBurn.shader).");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ContentAssets.INTRO_BURN_MATERIAL_PATH);
            return material;
        }

        // a FrontEnd prefab from before the intro: the placeholder hourglass gives way to the intro, the rest stays as authored
        private static void AddMissingIntro(Material burn)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.FRONT_END_PATH);
            try
            {
                SerializedObject serialized = new SerializedObject(root.GetComponent<FrontEndView>());
                SerializedProperty intro = serialized.FindProperty("introView");
                if (intro.objectReferenceValue != null)
                {
                    return;
                }

                Transform overlays = root.transform.Find("Overlays");
                if (overlays == null)
                {
                    throw new InvalidOperationException(ContentAssets.FRONT_END_PATH + " has no Overlays to add the intro to.");
                }

                Transform placeholder = overlays.Find("IntroFrame");
                if (placeholder != null)
                {
                    Object.DestroyImmediate(placeholder.gameObject);
                }

                intro.objectReferenceValue = CreateIntro(overlays, burn);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.FRONT_END_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ImportAsSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SPRITE_FOLDER }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void CreateFrontEndPrefab(Material burn)
        {
            GameObject buttonWidget = AssetDatabase.LoadAssetAtPath<GameObject>(BUTTON_WIDGET_PATH);
            if (buttonWidget == null || buttonWidget.GetComponent<UiWidget>() == null)
            {
                throw new InvalidOperationException("The main menu placeholders nest " + BUTTON_WIDGET_PATH + ", which is missing or has no UiWidget.");
            }

            Vector2 middle = new Vector2(0.5f, 0.5f);
            Vector2 topRight = Vector2.one;

            GameObject root = NewRect("FrontEnd", null);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.AddComponent<GraphicRaycaster>();

            GameObject screens = NewRect("Screens", root.transform);
            Stretch((RectTransform)screens.transform);

            GameObject menu = NewRect("MainMenu", screens.transform);
            Stretch((RectTransform)menu.transform);
            CanvasGroup menuGroup = menu.AddComponent<CanvasGroup>();
            // black letterbox around the 16:9 picture, so the game never shows at the sides
            GameObject backdrop = NewRect("Backdrop", menu.transform);
            Stretch((RectTransform)backdrop.transform);
            AddImage(backdrop, null, Color.black, true);

            GameObject frame = NewRect("DesignFrame", menu.transform);
            SetRect((RectTransform)frame.transform, middle, middle, Vector2.zero, DesignSize);
            GameObject background = NewRect("Background", frame.transform);
            Stretch((RectTransform)background.transform);
            AddImage(background, LoadSprite(BACKGROUND_PATH), Color.white, false);

            GameObject buttons = NewRect("Buttons", frame.transform);
            float columnHeight = NewGameSize.y + PlaceholderNames.Length * (PlaceholderSize.y + PLACEHOLDER_GAP);
            SetRect((RectTransform)buttons.transform, topRight, topRight, new Vector2(-BUTTONS_MARGIN_RIGHT, -BUTTONS_MARGIN_TOP),
                new Vector2(NewGameSize.x, columnHeight));

            NewGameButtonView newGame = CreateNewGameButton(buttons.transform);
            UiWidget[] placeholders = new UiWidget[PlaceholderNames.Length];
            for (int i = 0; i < PlaceholderNames.Length; i++)
            {
                float top = NewGameSize.y + PLACEHOLDER_GAP + i * (PlaceholderSize.y + PLACEHOLDER_GAP);
                placeholders[i] = CreatePlaceholder(buttonWidget, buttons.transform, PlaceholderNames[i], PlaceholderTitles[i], top);
            }

            MainMenuView menuView = menu.AddComponent<MainMenuView>();
            SerializedObject menuSerialized = new SerializedObject(menuView);
            menuSerialized.FindProperty("canvasGroup").objectReferenceValue = menuGroup;
            menuSerialized.FindProperty("newGameButton").objectReferenceValue = newGame;
            for (int i = 0; i < placeholders.Length; i++)
            {
                menuSerialized.FindProperty(PlaceholderFields[i]).objectReferenceValue = placeholders[i];
            }

            menuSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject overlays = NewRect("Overlays", root.transform);
            Stretch((RectTransform)overlays.transform);
            GameObject fade = NewRect("Fade", overlays.transform);
            Stretch((RectTransform)fade.transform);
            AddImage(fade, null, Color.black, true);
            CanvasGroup fadeGroup = fade.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fade.SetActive(false);

            IntroView intro = CreateIntro(overlays.transform, burn);

            FrontEndView view = root.AddComponent<FrontEndView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("mainMenu").objectReferenceValue = menuView;
            serialized.FindProperty("fade").objectReferenceValue = fadeGroup;
            serialized.FindProperty("introView").objectReferenceValue = intro;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.FRONT_END_PATH);
            Object.DestroyImmediate(root);
        }

        // a black curtain over the whole screen; its Mask cuts the texts out where it burns away
        private static IntroView CreateIntro(Transform overlays, Material burn)
        {
            UiTheme theme = ContentAssets.Theme;
            if (theme == null)
            {
                throw new InvalidOperationException("The intro texts use the font of " + ContentAssets.THEME_PATH + ", which is missing.");
            }

            Vector2 middle = new Vector2(0.5f, 0.5f);
            GameObject intro = NewRect("Intro", overlays);
            Stretch((RectTransform)intro.transform);
            Image curtain = AddImage(intro, null, Color.black, true);
            curtain.material = burn;
            Mask mask = intro.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // the lines stack in the middle; a shorter stanza hides the rest and stays centred
            GameObject verses = NewRect("Verses", intro.transform);
            SetRect((RectTransform)verses.transform, middle, middle, Vector2.zero, VersesSize);
            CanvasGroup versesGroup = verses.AddComponent<CanvasGroup>();
            versesGroup.interactable = false;
            versesGroup.blocksRaycasts = false;
            VerticalLayoutGroup layout = verses.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = LINE_SPACING;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            TMP_Text[] lines = new TMP_Text[LINE_COUNT];
            for (int i = 0; i < LINE_COUNT; i++)
            {
                lines[i] = AddText(NewRect("Line" + (i + 1) + "Text", verses.transform), theme, VERSE_FONT_SIZE, VerseColor);
                lines[i].maxVisibleCharacters = 0;
            }

            GameObject title = NewRect("TitleText", intro.transform);
            SetRect((RectTransform)title.transform, middle, middle, Vector2.zero, TitleSize);
            TMP_Text titleText = AddText(title, theme, TITLE_FONT_SIZE, TitleColor);
            titleText.characterSpacing = TITLE_CHARACTER_SPACING;
            title.SetActive(false);

            IntroView view = intro.AddComponent<IntroView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("curtain").objectReferenceValue = curtain;
            serialized.FindProperty("verses").objectReferenceValue = versesGroup;
            SerializedProperty lineTexts = serialized.FindProperty("lineTexts");
            lineTexts.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                lineTexts.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
            }

            serialized.FindProperty("titleText").objectReferenceValue = titleText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            intro.SetActive(false);
            return view;
        }

        // the title over the arm on one shared canvas; both images take the pointer, so hovering either flexes the arm
        private static NewGameButtonView CreateNewGameButton(Transform parent)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            GameObject target = NewRect("NewGameButton", parent);
            SetRect((RectTransform)target.transform, top, top, Vector2.zero, NewGameSize);

            Sprite[] flexFrames = LoadFrames(FLEX_PATH_FORMAT);
            Sprite[] hornsFrames = LoadFrames(HORNS_PATH_FORMAT);
            GameObject arm = NewRect("ArmImage", target.transform);
            Stretch((RectTransform)arm.transform);
            Image armImage = AddImage(arm, flexFrames[0], Color.white, true);
            GameObject title = NewRect("TitleImage", target.transform);
            Stretch((RectTransform)title.transform);
            AddImage(title, LoadSprite(TITLE_PATH), Color.white, true);

            Button button = target.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            NewGameButtonView view = target.AddComponent<NewGameButtonView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("armImage").objectReferenceValue = armImage;
            SetSprites(serialized.FindProperty("flexFrames"), flexFrames);
            SetSprites(serialized.FindProperty("hornsFrames"), hornsFrames);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static UiWidget CreatePlaceholder(GameObject widgetPrefab, Transform parent, string objectName, string title, float top)
        {
            Vector2 anchor = new Vector2(0.5f, 1f);
            GameObject target = (GameObject)PrefabUtility.InstantiatePrefab(widgetPrefab, parent);
            target.name = objectName;
            SetRect((RectTransform)target.transform, anchor, anchor, new Vector2(0f, -top), PlaceholderSize);
            UiWidget widget = target.GetComponent<UiWidget>();
            TMP_Text text = (TMP_Text)new SerializedObject(widget).FindProperty("titleText").objectReferenceValue;
            if (text != null)
            {
                text.text = title;
            }

            return widget;
        }

        private static Sprite[] LoadFrames(string pathFormat)
        {
            Sprite[] frames = new Sprite[FRAME_COUNT];
            for (int i = 0; i < FRAME_COUNT; i++)
            {
                frames[i] = LoadSprite(string.Format(pathFormat, i + 1));
            }

            return frames;
        }

        private static void SetSprites(SerializedProperty property, Sprite[] sprites)
        {
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("The main menu needs the sprite " + path + ".");
            }

            return sprite;
        }

        private static GameObject NewRect(string objectName, Transform parent)
        {
            GameObject target = new GameObject(objectName, typeof(RectTransform));
            target.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
            {
                target.transform.SetParent(parent, false);
            }

            return target;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text AddText(GameObject target, UiTheme theme, float size, Color color)
        {
            TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
            text.font = theme.Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Image AddImage(GameObject target, Sprite sprite, Color color, bool isRaycastTarget)
        {
            Image image = target.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = isRaycastTarget;
            return image;
        }
    }
}
