using System;
using System.Linq;
using HeroesOfMetalAge.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off bootstrap tool: it created the EndDayOverlay prefab (the end-of-day hourglass) and nested it into
    /// GameUiRoot with the GameHud reference wired, and later gave the overlay the painted 32-frame hourglass animation from
    /// <c>Content/UI/Sprites/EndDay/Frames/</c> in place of the first procedural hourglass. Both are now authored assets edited
    /// by hand. It only creates the prefab when it is missing, only nests it when GameHud has no end-of-day view and only
    /// replaces the hourglass while the overlay has no frames, so it has nothing to do once they exist. Safe to delete
    /// together with its call in <see cref="PrototypePipeline.Setup"/> and the mentions in README.md and
    /// Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class EndDayAssetSeeder
    {
        private const string FRAME_FOLDER = "Assets/HeroesOfMetalAge/Content/UI/Sprites/EndDay/Frames";
        private const string FRAME_PATH_FORMAT = FRAME_FOLDER + "/hourglass-{0:000}.png";
        private const int FRAME_COUNT = 32;
        private const float HOURGLASS_SIZE = 420f;
        // the authored milliseconds of each frame (the art drop's animation.json): the sand runs slowly, the turn is quick
        private static readonly float[] FrameMilliseconds =
        {
            350, 350, 350, 350, 350, 350, 350, 450, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40, 30, 30, 30, 30, 30, 30
        };

        [MenuItem("Tools/Heroes of Metal Age/Create missing end-of-day assets")]
        public static void CreateMissingAssets()
        {
            ImportFrames();
            if (ContentAssets.EndDayOverlayPrefab == null)
            {
                CreateOverlayPrefab();
            }
            else if (new SerializedObject(ContentAssets.EndDayOverlayPrefab).FindProperty("frames").arraySize == 0)
            {
                ReplaceHourglass();
            }

            GameHud root = ContentAssets.RootPrefab;
            if (root != null && root.EndDayView == null)
            {
                NestIntoRoot();
            }

            AssetDatabase.SaveAssets();
        }

        private static void ImportFrames()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FRAME_FOLDER }))
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer.textureType == TextureImporterType.Sprite)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void CreateOverlayPrefab()
        {
            // fills the design frame; its own Canvas keeps the per-frame animation out of the game UI's rebuilds
            GameObject overlay = NewRect("EndDayOverlay", null);
            Stretch((RectTransform)overlay.transform);
            overlay.AddComponent<Canvas>();
            CanvasGroup group = overlay.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            EndDayView view = overlay.AddComponent<EndDayView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AddHourglass(view);
            PrefabUtility.SaveAsPrefabAsset(overlay, ContentAssets.END_DAY_OVERLAY_PATH);
            Object.DestroyImmediate(overlay);
        }

        // the first, procedural hourglass (plate, glass, filled sand) gives way to the painted animation
        private static void ReplaceHourglass()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.END_DAY_OVERLAY_PATH);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }

                AddHourglass(root.GetComponent<EndDayView>());
                PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.END_DAY_OVERLAY_PATH);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddHourglass(EndDayView view)
        {
            Sprite[] frames = Enumerable.Range(0, FRAME_COUNT).Select(i => LoadSprite(string.Format(FRAME_PATH_FORMAT, i))).ToArray();
            GameObject hourglass = NewRect("Hourglass", view.transform);
            RectTransform rect = (RectTransform)hourglass.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(HOURGLASS_SIZE, HOURGLASS_SIZE);
            Image image = hourglass.AddComponent<Image>();
            image.sprite = frames[0];
            image.preserveAspect = true;
            image.raycastTarget = false;

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("hourglass").objectReferenceValue = image;
            SerializedProperty frameList = serialized.FindProperty("frames");
            frameList.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                frameList.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            SerializedProperty weights = serialized.FindProperty("frameWeights");
            weights.arraySize = FrameMilliseconds.Length;
            for (int i = 0; i < FrameMilliseconds.Length; i++)
            {
                weights.GetArrayElementAtIndex(i).floatValue = FrameMilliseconds[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // the overlay goes last in the design frame, so it draws over the screens, dialogs and notifications
        private static void NestIntoRoot()
        {
            string rootPath = AssetDatabase.GetAssetPath(ContentAssets.RootPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(rootPath);
            try
            {
                GameHud hud = root.GetComponent<GameHud>();
                GameObject overlay = (GameObject)PrefabUtility.InstantiatePrefab(ContentAssets.EndDayOverlayPrefab.gameObject, hud.DesignFrame);
                overlay.transform.SetAsLastSibling();
                overlay.SetActive(false);
                SerializedObject serialized = new SerializedObject(hud);
                serialized.FindProperty("endDayView").objectReferenceValue = overlay.GetComponent<EndDayView>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, rootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("The end-of-day hourglass needs the sprite " + path + ".");
            }

            return sprite;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
