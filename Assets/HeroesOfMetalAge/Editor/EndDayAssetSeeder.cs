using System;
using HeroesOfMetalAge.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off bootstrap tool: it created the EndDayOverlay prefab (the end-of-day hourglass) and nested it into
    /// GameUiRoot with the GameHud reference wired; both are now authored assets edited by hand. It only creates the prefab
    /// when it is missing and only nests it when GameHud has no end-of-day view; it never changes anything that exists, so
    /// it has nothing to do while they exist. Safe to delete together with its call in <see cref="PrototypePipeline.Setup"/>
    /// and the mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class EndDayAssetSeeder
    {
        private const string SPRITE_FOLDER = "Assets/HeroesOfMetalAge/Content/UI/Sprites/EndDay";
        private const string FRAME_SPRITE_PATH = SPRITE_FOLDER + "/Hourglass.png";
        private const string TOP_SAND_SPRITE_PATH = SPRITE_FOLDER + "/HourglassSandTop.png";
        private const string BOTTOM_SAND_SPRITE_PATH = SPRITE_FOLDER + "/HourglassSandBottom.png";
        private const string PLATE_SPRITE_PATH = SPRITE_FOLDER + "/HourglassPlate.png";
        private const float PLATE_SIZE = 320f;
        private static readonly Vector2 HourglassSize = new Vector2(160f, 240f);
        private static readonly Vector2 BulbSize = new Vector2(160f, 96f);
        private static readonly Vector2 StreamSize = new Vector2(4f, 92f);
        private static readonly Color Sand = new Color(0.87f, 0.67f, 0.32f);

        [MenuItem("Tools/Heroes of Metal Age/Create missing end-of-day assets")]
        public static void CreateMissingAssets()
        {
            if (ContentAssets.EndDayOverlayPrefab == null)
            {
                CreateOverlayPrefab();
            }

            GameHud root = ContentAssets.RootPrefab;
            if (root != null && root.EndDayView == null)
            {
                NestIntoRoot();
            }

            AssetDatabase.SaveAssets();
        }

        private static void CreateOverlayPrefab()
        {
            Sprite frameSprite = LoadSprite(FRAME_SPRITE_PATH);
            Sprite topSprite = LoadSprite(TOP_SAND_SPRITE_PATH);
            Sprite bottomSprite = LoadSprite(BOTTOM_SAND_SPRITE_PATH);
            Sprite plateSprite = LoadSprite(PLATE_SPRITE_PATH);
            Vector2 middle = new Vector2(0.5f, 0.5f);

            // fills the design frame; its own Canvas keeps the per-frame sand animation out of the game UI's rebuilds
            GameObject overlay = NewRect("EndDayOverlay", null);
            Stretch((RectTransform)overlay.transform);
            overlay.AddComponent<Canvas>();
            CanvasGroup group = overlay.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject plate = NewRect("Plate", overlay.transform);
            SetRect((RectTransform)plate.transform, middle, Vector2.zero, new Vector2(PLATE_SIZE, PLATE_SIZE));
            AddImage(plate, plateSprite, Color.white);

            GameObject hourglass = NewRect("Hourglass", overlay.transform);
            SetRect((RectTransform)hourglass.transform, middle, Vector2.zero, HourglassSize);

            // drawn first, so the bottom pile covers its lower end
            GameObject stream = NewRect("SandStream", hourglass.transform);
            SetRect((RectTransform)stream.transform, new Vector2(0.5f, 1f), Vector2.zero, StreamSize);
            Image streamImage = AddImage(stream, null, Sand);

            GameObject topSand = NewRect("TopSand", hourglass.transform);
            SetRect((RectTransform)topSand.transform, middle, new Vector2(0f, BulbSize.y * 0.5f), BulbSize);
            Image topImage = AddFilledImage(topSand, topSprite);

            GameObject bottomSand = NewRect("BottomSand", hourglass.transform);
            SetRect((RectTransform)bottomSand.transform, middle, new Vector2(0f, -BulbSize.y * 0.5f), BulbSize);
            Image bottomImage = AddFilledImage(bottomSand, bottomSprite);
            bottomImage.fillAmount = 0f;

            // the glass and the caps draw over the sand
            GameObject frame = NewRect("Frame", hourglass.transform);
            SetRect((RectTransform)frame.transform, middle, Vector2.zero, HourglassSize);
            AddImage(frame, frameSprite, Color.white);

            EndDayView view = overlay.AddComponent<EndDayView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.FindProperty("hourglass").objectReferenceValue = hourglass.transform;
            serialized.FindProperty("topSand").objectReferenceValue = topImage;
            serialized.FindProperty("bottomSand").objectReferenceValue = bottomImage;
            serialized.FindProperty("sandStream").objectReferenceValue = streamImage;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(overlay, ContentAssets.END_DAY_OVERLAY_PATH);
            Object.DestroyImmediate(overlay);
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

        private static void SetRect(RectTransform rect, Vector2 pivot, Vector2 position, Vector2 size)
        {
            Vector2 middle = new Vector2(0.5f, 0.5f);
            rect.anchorMin = middle;
            rect.anchorMax = middle;
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

        private static Image AddImage(GameObject target, Sprite sprite, Color color)
        {
            Image image = target.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // filled from the bottom: the top bulb's sand sinks towards the neck, the bottom pile grows
        private static Image AddFilledImage(GameObject target, Sprite sprite)
        {
            Image image = AddImage(target, sprite, Color.white);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Vertical;
            image.fillOrigin = (int)Image.OriginVertical.Bottom;
            image.fillAmount = 1f;
            return image;
        }
    }
}
