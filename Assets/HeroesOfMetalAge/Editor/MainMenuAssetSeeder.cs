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
    /// One-off bootstrap tool: it created the FrontEnd prefab (the main menu with the background in parallax layers, the
    /// animated New Game button with the gold-pouring title, the Load Game, Credits and Quit entries with the hell hole,
    /// the sealed scroll and the rider on his chopper, the credits screen, the black fade and the intro with its verses,
    /// title and burning curtain), the materials <c>Content/Rendering/IntroBurn.mat</c> and <c>MenuGild.mat</c>, and set
    /// the main menu images in <c>Content/UI/Sprites/MainMenu/</c> to import as sprites. On a FrontEnd prefab from an
    /// earlier version it replaced the placeholder intro (the nested end-of-day hourglass), the single background image,
    /// the placeholder buttons and then the first painted Load Game, Credits and Quit entries, removed the darkness blot
    /// of the first Load Game transition, and moved the New Game arm only while it was still at its first seeded place;
    /// everything else stayed as it was. The prefab is now an authored asset edited by hand; the tool only adds what is
    /// missing and never changes anything that exists, so it has nothing to do once they exist. Safe to delete together
    /// with its call in <see cref="PrototypePipeline.Setup"/> and the mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class MainMenuAssetSeeder
    {
        private const string SPRITE_FOLDER = "Assets/HeroesOfMetalAge/Content/UI/Sprites/MainMenu";
        private const string BACKGROUND_PATH = SPRITE_FOLDER + "/MainMenuBackground.png";
        private const string TITLE_PATH = SPRITE_FOLDER + "/NewGameText.png";
        private const string FLEX_PATH_FORMAT = SPRITE_FOLDER + "/HoverFlex{0:00}.png";
        private const string HORNS_PATH_FORMAT = SPRITE_FOLDER + "/HornsClick{0:00}.png";
        private const string SPRITE_PATH_FORMAT = SPRITE_FOLDER + "/{0}.png";
        private const string PARALLAX_FOLDER = SPRITE_FOLDER + "/Parallax";
        // the layers are wider than 2048 and stay one texture each, uncut
        private const int PARALLAX_MAX_TEXTURE_SIZE = 4096;
        private const int FRAME_COUNT = 6;
        private const int LINE_COUNT = 3;
        private const float LINE_SPACING = 22f;
        private const float VERSE_FONT_SIZE = 56f;
        private const float TITLE_FONT_SIZE = 128f;
        private const float TITLE_CHARACTER_SPACING = 6f;
        private const float CREDITS_FONT_SIZE = 40f;
        private const float CREDITS_HINT_FONT_SIZE = 26f;
        // above GameUiRoot (0) and the dialogue overlay (10)
        private const int SORTING_ORDER = 20;
        private const float BUTTONS_MARGIN_RIGHT = 120f;
        private const float BUTTONS_MARGIN_TOP = 40f;
        private const float ENTRY_GAP = 6f;
        // the scroll's frames at 400 wide, the chopper's at 230
        private const float SCROLL_SCALE = 0.78f;
        private const float CHOPPER_SIZE = 230f;
        private const float HOLE_WIDTH = 440f;
        private const string HOLE_FILE = "LoadGameHole";
        private const string SCROLL_PATH_FORMAT = SPRITE_FOLDER + "/CreditsScroll{0:00}.png";
        private const string CHOPPER_HOVER_PATH_FORMAT = SPRITE_FOLDER + "/ChopperHover{0:00}.png";
        private const string CHOPPER_QUIT_PATH_FORMAT = SPRITE_FOLDER + "/ChopperQuit{0:00}.png";
        private const int SCROLL_FRAME_COUNT = 6;
        private const int CHOPPER_HOVER_FRAME_COUNT = 6;
        private const int CHOPPER_QUIT_FRAME_COUNT = 7;
        private const float UNROLL_END = 0.4f;
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);
        // every arm frame and the title share one transparent canvas, so they line up
        private static readonly Vector2 NewGameSize = new Vector2(416f, 342f);
        // the arm sits just under the title and its fist rises over it when it flexes
        private static readonly Vector2 ArmOffset = new Vector2(24f, 36f);
        private static readonly Vector2 LoadGameSize = new Vector2(416f, 160f);
        private static readonly Vector2 CreditsSize = new Vector2(416f, 166f);
        private static readonly Vector2 QuitSize = new Vector2(416f, 272f);
        // on the scroll's 512 x 640 frames the rolled scroll lies from 24 to 141, between 15 and 497
        private static readonly Rect RolledScroll = new Rect(15f, 24f, 482f, 117f);
        private static readonly Vector2 VersesSize = new Vector2(1700f, 600f);
        private static readonly Vector2 TitleSize = new Vector2(1800f, 300f);
        private static readonly Vector2 CreditsViewportSize = new Vector2(1200f, 960f);
        private static readonly Color VerseColor = new Color(0.9f, 0.86f, 0.78f, 1f);
        private static readonly Color TitleColor = new Color(0.95f, 0.91f, 0.84f, 1f);
        private static readonly Color CreditsBackgroundTint = new Color(1f, 1f, 1f, 0.16f);
        private static readonly string[] PlaceholderNames = { "LoadGameButton", "CreditsButton", "QuitButton" };
        // back to front: file, object name and how strongly it moves against the camera (from the layers' manifest)
        private static readonly string[] ParallaxFiles =
        {
            "00_sky", "01_sun", "02_clouds_far", "03_clouds_near", "04_mountains_far", "05_mountains_near", "06_castle_terrain",
            "07_bridge_terrain", "08_near_terrain", "09_rider_horse", "10_foreground_rocks"
        };
        private static readonly string[] ParallaxNames =
        {
            "SkyImage", "SunImage", "FarCloudsImage", "NearCloudsImage", "FarMountainsImage", "NearMountainsImage", "CastleTerrainImage",
            "BridgeTerrainImage", "NearTerrainImage", "RiderImage", "ForegroundRocksImage"
        };
        private static readonly float[] ParallaxFactors = { 0f, 0.03f, 0.06f, 0.1f, 0.14f, 0.22f, 0.34f, 0.48f, 0.65f, 0.85f, 1f };

        [MenuItem("Tools/Heroes of Metal Age/Create missing main menu assets")]
        public static void CreateMissingAssets()
        {
            ImportAsSprites();
            Material burn = CreateMissingMaterial(ContentAssets.INTRO_BURN_MATERIAL_PATH, ContentAssets.INTRO_BURN_SHADER);
            Material gild = CreateMissingMaterial(ContentAssets.MENU_GILD_MATERIAL_PATH, ContentAssets.MENU_GILD_SHADER);
            if (ContentAssets.FrontEndPrefab == null)
            {
                CreateFrontEndPrefab();
            }

            AddMissingParts(burn, gild);
            AssetDatabase.SaveAssets();
        }

        private static void ImportAsSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SPRITE_FOLDER }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isLayer = path.StartsWith(PARALLAX_FOLDER + "/", StringComparison.Ordinal);
                int maxSize = isLayer ? PARALLAX_MAX_TEXTURE_SIZE : importer.maxTextureSize;
                // the hole is drawn far smaller than its picture at rest and far larger while the camera falls into it
                bool hasMipmaps = path.EndsWith("/" + HOLE_FILE + ".png", StringComparison.Ordinal);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.mipmapEnabled == hasMipmaps && importer.maxTextureSize == maxSize)
                {
                    continue;
                }

                importer.maxTextureSize = maxSize;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = hasMipmaps;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static Material CreateMissingMaterial(string path, string shaderName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("The main menu needs the shader " + shaderName + " in Content/Rendering.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // the canvas, the menu with New Game, and the fade; everything else is added as a missing part
        private static void CreateFrontEndPrefab()
        {
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

            GameObject buttons = NewRect("Buttons", frame.transform);
            SetRect((RectTransform)buttons.transform, topRight, topRight, new Vector2(-BUTTONS_MARGIN_RIGHT, -BUTTONS_MARGIN_TOP), NewGameSize);
            NewGameButtonView newGame = CreateNewGameButton(buttons.transform);

            MainMenuView menuView = menu.AddComponent<MainMenuView>();
            SerializedObject menuSerialized = new SerializedObject(menuView);
            menuSerialized.FindProperty("canvasGroup").objectReferenceValue = menuGroup;
            menuSerialized.FindProperty("newGameButton").objectReferenceValue = newGame;
            menuSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject overlays = NewRect("Overlays", root.transform);
            Stretch((RectTransform)overlays.transform);
            GameObject fade = NewRect("Fade", overlays.transform);
            Stretch((RectTransform)fade.transform);
            AddImage(fade, null, Color.black, true);
            CanvasGroup fadeGroup = fade.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fade.SetActive(false);

            FrontEndView view = root.AddComponent<FrontEndView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("mainMenu").objectReferenceValue = menuView;
            serialized.FindProperty("fade").objectReferenceValue = fadeGroup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.FRONT_END_PATH);
            Object.DestroyImmediate(root);
        }

        // each part is added only when its reference is still empty; the rest of the prefab stays as authored
        private static void AddMissingParts(Material burn, Material gild)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.FRONT_END_PATH);
            try
            {
                FrontEndView frontEnd = root.GetComponent<FrontEndView>();
                SerializedObject view = new SerializedObject(frontEnd);
                MainMenuView menuView = (MainMenuView)view.FindProperty("mainMenu").objectReferenceValue;
                CanvasGroup fade = (CanvasGroup)view.FindProperty("fade").objectReferenceValue;
                if (menuView == null || fade == null)
                {
                    throw new InvalidOperationException(ContentAssets.FRONT_END_PATH + " needs its main menu and fade to add the missing parts to.");
                }

                Transform overlays = fade.transform.parent;
                SerializedObject menu = new SerializedObject(menuView);
                bool isChanged = AddMissingIntro(view, overlays, burn);
                isChanged |= AddMissingGild((NewGameButtonView)menu.FindProperty("newGameButton").objectReferenceValue, gild);
                isChanged |= AddMissingEntries(menu);
                isChanged |= AddMissingCredits(view, menuView.transform.parent);
                isChanged |= RemoveDarkness(overlays);
                isChanged |= AddMissingParallax(menu);
                isChanged |= AddMissingFallFrame(menu);
                if (isChanged)
                {
                    view.ApplyModifiedPropertiesWithoutUndo();
                    menu.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.FRONT_END_PATH);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // replaces the placeholder hourglass of the first version
        private static bool AddMissingIntro(SerializedObject frontEnd, Transform overlays, Material burn)
        {
            SerializedProperty intro = frontEnd.FindProperty("introView");
            if (intro.objectReferenceValue != null)
            {
                return false;
            }

            Transform placeholder = overlays.Find("IntroFrame");
            if (placeholder != null)
            {
                Object.DestroyImmediate(placeholder.gameObject);
            }

            intro.objectReferenceValue = CreateIntro(overlays, burn);
            return true;
        }

        // the title gets the gold material and the arm moves up and right and over the title, if it is still where it was seeded
        private static bool AddMissingGild(NewGameButtonView newGame, Material gild)
        {
            SerializedObject button = new SerializedObject(newGame);
            SerializedProperty title = button.FindProperty("titleImage");
            if (title.objectReferenceValue != null)
            {
                return false;
            }

            Transform titleTransform = newGame.transform.Find("TitleImage");
            if (titleTransform == null || titleTransform.GetComponent<Image>() == null)
            {
                throw new InvalidOperationException("NewGameButton in " + ContentAssets.FRONT_END_PATH + " has no TitleImage to pour the gold over.");
            }

            Image titleImage = titleTransform.GetComponent<Image>();
            titleImage.material = gild;
            title.objectReferenceValue = titleImage;
            button.ApplyModifiedPropertiesWithoutUndo();

            RectTransform arm = ((Image)button.FindProperty("armImage").objectReferenceValue).rectTransform;
            if (arm.anchorMin == Vector2.zero && arm.anchorMax == Vector2.one && arm.offsetMin == Vector2.zero && arm.offsetMax == Vector2.zero)
            {
                arm.offsetMin = ArmOffset;
                arm.offsetMax = ArmOffset;
            }

            arm.SetAsLastSibling();
            return true;
        }

        // replaces the placeholder buttons of the first version, and the first painted entries (a scroll without frames),
        // with the artwork entries and their effects
        private static bool AddMissingEntries(SerializedObject menu)
        {
            ScrollUnrollView scroll = (ScrollUnrollView)menu.FindProperty("creditsScroll").objectReferenceValue;
            bool isCurrent = menu.FindProperty("loadGame").objectReferenceValue != null && scroll != null
                && new SerializedObject(scroll).FindProperty("frames").arraySize > 0;
            if (isCurrent)
            {
                return false;
            }

            NewGameButtonView newGame = (NewGameButtonView)menu.FindProperty("newGameButton").objectReferenceValue;
            RectTransform buttons = (RectTransform)newGame.transform.parent;
            foreach (string placeholder in PlaceholderNames)
            {
                Transform old = buttons.Find(placeholder);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            // Credits is drawn last, so its opened scroll lies over Quit
            float loadGameTop = NewGameSize.y + ENTRY_GAP;
            float creditsTop = loadGameTop + LoadGameSize.y + ENTRY_GAP;
            float quitTop = creditsTop + CreditsSize.y + ENTRY_GAP;
            MenuButtonView loadGame = CreateLoadGame(buttons, loadGameTop, menu);
            MenuButtonView quit = CreateQuit(buttons, quitTop, menu);
            MenuButtonView credits = CreateCredits(buttons, creditsTop, menu);
            buttons.sizeDelta = new Vector2(buttons.sizeDelta.x, quitTop + QuitSize.y);
            menu.FindProperty("unrollEnd").floatValue = UNROLL_END;

            menu.FindProperty("loadGame").objectReferenceValue = loadGame;
            menu.FindProperty("credits").objectReferenceValue = credits;
            menu.FindProperty("quit").objectReferenceValue = quit;
            return true;
        }

        // the title in the middle of the hell hole; it falls into it on a click
        private static MenuButtonView CreateLoadGame(RectTransform buttons, float top, SerializedObject menu)
        {
            GameObject entry = NewEntry("LoadGameButton", buttons, top, LoadGameSize);
            Vector2 middle = new Vector2(0.5f, 0.5f);
            Sprite holeSprite = LoadSprite(HOLE_FILE);
            GameObject hole = NewRect("HoleImage", entry.transform);
            SetRect((RectTransform)hole.transform, middle, middle, Vector2.zero,
                new Vector2(HOLE_WIDTH, HOLE_WIDTH * holeSprite.rect.height / holeSprite.rect.width));
            AddImage(hole, holeSprite, Color.white, true);
            Image title = AddNativeImage(NewRect("TitleImage", entry.transform), LoadSprite("LoadGameText"), middle, new Vector2(0f, 4f));
            CrumbleEffect crumble = title.gameObject.AddComponent<CrumbleEffect>();

            menu.FindProperty("loadGameCrumble").objectReferenceValue = crumble;
            menu.FindProperty("loadGameHole").objectReferenceValue = hole.transform;
            return AddEntryButton(entry, title);
        }

        // the title over the sealed scroll; a click breaks the seal and unrolls it downwards
        private static MenuButtonView CreateCredits(RectTransform buttons, float top, SerializedObject menu)
        {
            GameObject entry = NewEntry("CreditsButton", buttons, top, CreditsSize);
            Vector2 topCenter = new Vector2(0.5f, 1f);
            Sprite[] frames = LoadFrames(SCROLL_PATH_FORMAT, SCROLL_FRAME_COUNT);
            Image title = AddNativeImage(NewRect("TitleImage", entry.transform), LoadSprite("CreditsText"), topCenter, Vector2.zero);
            // the rolled scroll starts just under the title; the frames' empty lower part is where it unrolls to
            float rolledTop = title.rectTransform.sizeDelta.y - 3f;
            GameObject scroll = NewRect("ScrollImage", entry.transform);
            SetRect((RectTransform)scroll.transform, topCenter, topCenter, new Vector2(0f, -(rolledTop - RolledScroll.y * SCROLL_SCALE)),
                frames[0].rect.size * SCROLL_SCALE);
            Image scrollImage = AddImage(scroll, frames[0], Color.white, false);
            // the frames are mostly empty below the rolled scroll, so only the rolled scroll takes the pointer
            GameObject hit = NewRect("HitArea", entry.transform);
            SetRect((RectTransform)hit.transform, topCenter, topCenter, new Vector2(0f, -rolledTop), RolledScroll.size * SCROLL_SCALE);
            AddImage(hit, null, Color.clear, true);
            title.transform.SetAsLastSibling();

            ScrollUnrollView unroll = scroll.AddComponent<ScrollUnrollView>();
            SerializedObject serialized = new SerializedObject(unroll);
            serialized.FindProperty("scroll").objectReferenceValue = scrollImage;
            SetSprites(serialized.FindProperty("frames"), frames);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            menu.FindProperty("creditsScroll").objectReferenceValue = unroll;
            return AddEntryButton(entry, title);
        }

        // the title over the hooded rider on his chopper; hovering lifts his foot, a click sends him off
        private static MenuButtonView CreateQuit(RectTransform buttons, float top, SerializedObject menu)
        {
            GameObject entry = NewEntry("QuitButton", buttons, top, QuitSize);
            Vector2 topCenter = new Vector2(0.5f, 1f);
            Sprite[] hover = LoadFrames(CHOPPER_HOVER_PATH_FORMAT, CHOPPER_HOVER_FRAME_COUNT);
            Sprite[] departure = LoadFrames(CHOPPER_QUIT_PATH_FORMAT, CHOPPER_QUIT_FRAME_COUNT);
            GameObject chopper = NewRect("ChopperImage", entry.transform);
            SetRect((RectTransform)chopper.transform, topCenter, topCenter, new Vector2(0f, -(QuitSize.y - CHOPPER_SIZE)), Vector2.one * CHOPPER_SIZE);
            Image chopperImage = AddImage(chopper, hover[0], Color.white, true);
            ChopperView view = entry.AddComponent<ChopperView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("chopper").objectReferenceValue = chopperImage;
            SetSprites(serialized.FindProperty("hoverFrames"), hover);
            SetSprites(serialized.FindProperty("departureFrames"), departure);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Image title = AddNativeImage(NewRect("TitleImage", entry.transform), LoadSprite("QuitText"), topCenter, Vector2.zero);
            menu.FindProperty("quitChopper").objectReferenceValue = view;
            return AddEntryButton(entry, title);
        }

        private static GameObject NewEntry(string objectName, RectTransform buttons, float top, Vector2 size)
        {
            Vector2 topCenter = new Vector2(0.5f, 1f);
            GameObject entry = NewRect(objectName, buttons);
            SetRect((RectTransform)entry.transform, topCenter, topCenter, new Vector2(0f, -top), size);
            return entry;
        }

        private static MenuButtonView AddEntryButton(GameObject entry, Image title)
        {
            Button button = entry.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = title;
            MenuButtonView view = entry.AddComponent<MenuButtonView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("highlight").objectReferenceValue = title.rectTransform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // black over the whole screen with the menu art faintly behind; the credits roll in the middle
        private static bool AddMissingCredits(SerializedObject frontEnd, Transform screens)
        {
            SerializedProperty property = frontEnd.FindProperty("credits");
            if (property.objectReferenceValue != null)
            {
                return false;
            }

            UiTheme theme = RequireTheme();
            Vector2 middle = new Vector2(0.5f, 0.5f);
            GameObject credits = NewRect("Credits", screens);
            Stretch((RectTransform)credits.transform);
            Image backdrop = AddImage(credits, null, Color.black, true);
            CanvasGroup group = credits.AddComponent<CanvasGroup>();
            Button back = credits.AddComponent<Button>();
            back.transition = Selectable.Transition.None;
            back.targetGraphic = backdrop;

            GameObject frame = NewRect("DesignFrame", credits.transform);
            SetRect((RectTransform)frame.transform, middle, middle, Vector2.zero, DesignSize);
            GameObject background = NewRect("Background", frame.transform);
            Stretch((RectTransform)background.transform);
            AddImage(background, LoadSprite(BACKGROUND_PATH), CreditsBackgroundTint, false);

            GameObject viewport = NewRect("Viewport", frame.transform);
            SetRect((RectTransform)viewport.transform, middle, middle, Vector2.zero, CreditsViewportSize);
            viewport.AddComponent<RectMask2D>();
            GameObject text = NewRect("CreditsText", viewport.transform);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -CreditsViewportSize.y);
            textRect.sizeDelta = new Vector2(0f, CreditsViewportSize.y);
            TMP_Text creditsText = AddText(text, theme, CREDITS_FONT_SIZE, VerseColor);
            creditsText.alignment = TextAlignmentOptions.Top;
            creditsText.textWrappingMode = TextWrappingModes.Normal;
            creditsText.richText = true;

            GameObject hint = NewRect("HintText", frame.transform);
            SetRect((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1200f, 40f));
            TMP_Text hintText = AddText(hint, theme, CREDITS_HINT_FONT_SIZE, new Color(VerseColor.r, VerseColor.g, VerseColor.b, 0.55f));
            hintText.text = "Click anywhere to return";

            CreditsView view = credits.AddComponent<CreditsView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.FindProperty("backButton").objectReferenceValue = back;
            serialized.FindProperty("viewport").objectReferenceValue = viewport.transform;
            serialized.FindProperty("creditsText").objectReferenceValue = creditsText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            credits.SetActive(false);
            property.objectReferenceValue = view;
            return true;
        }

        // replaces the single background image of the first version; every layer is the 16:9 picture with the same
        // extension to both sides, so they are all centred on the design frame at its height
        private static bool AddMissingParallax(SerializedObject menu)
        {
            SerializedProperty property = menu.FindProperty("parallax");
            if (property.objectReferenceValue != null)
            {
                return false;
            }

            NewGameButtonView newGame = (NewGameButtonView)menu.FindProperty("newGameButton").objectReferenceValue;
            Transform frame = newGame.transform.parent.parent;
            Transform background = frame.Find("Background");
            if (background != null)
            {
                Object.DestroyImmediate(background.gameObject);
            }

            GameObject parallax = NewRect("Parallax", frame);
            Stretch((RectTransform)parallax.transform);
            parallax.transform.SetAsFirstSibling();
            // its own Canvas: the layers move every frame, the buttons do not
            parallax.AddComponent<Canvas>();
            ParallaxView view = parallax.AddComponent<ParallaxView>();
            SerializedObject serialized = new SerializedObject(view);
            SerializedProperty layers = serialized.FindProperty("layers");
            layers.arraySize = ParallaxFiles.Length;
            Vector2 middle = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < ParallaxFiles.Length; i++)
            {
                Sprite sprite = LoadSprite(PARALLAX_FOLDER + "/" + ParallaxFiles[i] + ".png");
                GameObject layer = NewRect(ParallaxNames[i], parallax.transform);
                Vector2 size = new Vector2(DesignSize.y * sprite.rect.width / sprite.rect.height, DesignSize.y);
                SetRect((RectTransform)layer.transform, middle, middle, Vector2.zero, size);
                AddImage(layer, sprite, Color.white, false);
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("rect").objectReferenceValue = layer.transform;
                element.FindPropertyRelative("factor").floatValue = ParallaxFactors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            property.objectReferenceValue = view;
            return true;
        }

        // the first Load Game transition spread a black blot from the hole; the camera now falls into the hole instead
        private static bool RemoveDarkness(Transform overlays)
        {
            Transform darkness = overlays.Find("Darkness");
            if (darkness == null)
            {
                return false;
            }

            Object.DestroyImmediate(darkness.gameObject);
            return true;
        }

        // the camera falls into Load Game's hole by scaling and turning everything the menu shows
        private static bool AddMissingFallFrame(SerializedObject menu)
        {
            SerializedProperty property = menu.FindProperty("designFrame");
            if (property.objectReferenceValue != null)
            {
                return false;
            }

            NewGameButtonView newGame = (NewGameButtonView)menu.FindProperty("newGameButton").objectReferenceValue;
            property.objectReferenceValue = newGame.transform.parent.parent;
            return true;
        }

        // a black curtain over the whole screen; its Mask cuts the texts out where it burns away
        private static IntroView CreateIntro(Transform overlays, Material burn)
        {
            UiTheme theme = RequireTheme();
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

        // the title and the arm on one shared canvas; both images take the pointer, so hovering either flexes the arm
        private static NewGameButtonView CreateNewGameButton(Transform parent)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            GameObject target = NewRect("NewGameButton", parent);
            SetRect((RectTransform)target.transform, top, top, Vector2.zero, NewGameSize);

            Sprite[] flexFrames = LoadFrames(FLEX_PATH_FORMAT);
            Sprite[] hornsFrames = LoadFrames(HORNS_PATH_FORMAT);
            GameObject title = NewRect("TitleImage", target.transform);
            Stretch((RectTransform)title.transform);
            AddImage(title, LoadSprite(TITLE_PATH), Color.white, true);
            GameObject arm = NewRect("ArmImage", target.transform);
            Stretch((RectTransform)arm.transform);
            Image armImage = AddImage(arm, flexFrames[0], Color.white, true);

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

        private static UiTheme RequireTheme()
        {
            UiTheme theme = ContentAssets.Theme;
            if (theme == null)
            {
                throw new InvalidOperationException("The front end texts use the font of " + ContentAssets.THEME_PATH + ", which is missing.");
            }

            return theme;
        }

        private static Sprite[] LoadFrames(string pathFormat, int count = FRAME_COUNT)
        {
            Sprite[] frames = new Sprite[count];
            for (int i = 0; i < count; i++)
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

        // a bare name is a sprite in the main menu folder
        private static Sprite LoadSprite(string pathOrName)
        {
            string path = pathOrName.StartsWith("Assets/", StringComparison.Ordinal) ? pathOrName : string.Format(SPRITE_PATH_FORMAT, pathOrName);
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

        // an image at the sprite's own size, anchored and pivoted at the same point of its parent
        private static Image AddNativeImage(GameObject target, Sprite sprite, Vector2 anchor, Vector2 position)
        {
            SetRect((RectTransform)target.transform, anchor, anchor, position, sprite.rect.size);
            return AddImage(target, sprite, Color.white, true);
        }
    }
}
