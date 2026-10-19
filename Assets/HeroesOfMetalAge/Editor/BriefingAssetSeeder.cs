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
    /// One-off bootstrap tool: it created the MissionBriefing prefab (the big story scroll as a new mission starts, with
    /// its page text, the dim click area, the explosion and the white flash) and the explosion's
    /// <c>Content/Rendering/NukeExplosion.mat</c>. The prefab is now an authored asset edited by hand; the tool only creates
    /// what is missing and never changes anything that exists, so it has nothing to do once they exist. Safe to delete
    /// together with its call in <see cref="PrototypePipeline.Setup"/> and the mentions in README.md and
    /// Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class BriefingAssetSeeder
    {
        private const string SCROLL_PATH_FORMAT = "Assets/HeroesOfMetalAge/Content/UI/Sprites/MainMenu/CreditsScroll{0:00}.png";
        private const int SCROLL_FRAME_COUNT = 6;
        // above GameUiRoot (0) and the dialogue overlay (10), below the front end (20)
        private const int SORTING_ORDER = 15;
        // the main menu's scroll frames (512 x 640) at 1.6 times their size
        private const float SCROLL_SCALE = 1.6f;
        private const float PAGE_FONT_SIZE = 34f;
        private const float EXPLOSION_SIZE = 1800f;
        // the explosion's ground (a fifth above its bottom) on the scroll's lower roll
        private const float EXPLOSION_OFFSET_Y = 25f;
        private static readonly Vector2 DesignSize = new Vector2(1920f, 1080f);
        // the open parchment on the 512 x 640 frames, between the rolls and the torn edges
        private static readonly Rect Parchment = new Rect(125f, 130f, 270f, 335f);
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color InkColor = new Color(0.24f, 0.13f, 0.05f, 1f);

        [MenuItem("Tools/Heroes of Metal Age/Create missing mission briefing assets")]
        public static void CreateMissingAssets()
        {
            Material nuke = CreateMissingMaterial();
            if (ContentAssets.MissionBriefingPrefab == null)
            {
                CreateBriefingPrefab(nuke);
            }

            AssetDatabase.SaveAssets();
        }

        private static Material CreateMissingMaterial()
        {
            Material material = ContentAssets.NukeMaterial;
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(ContentAssets.NUKE_SHADER);
            if (shader == null)
            {
                throw new InvalidOperationException("The briefing's explosion needs the shader " + ContentAssets.NUKE_SHADER + " in Content/Rendering.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ContentAssets.NUKE_MATERIAL_PATH);
            return material;
        }

        private static void CreateBriefingPrefab(Material nuke)
        {
            UiTheme theme = ContentAssets.Theme;
            if (theme == null)
            {
                throw new InvalidOperationException("The briefing's page uses the font of " + ContentAssets.THEME_PATH + ", which is missing.");
            }

            Sprite[] frames = new Sprite[SCROLL_FRAME_COUNT];
            for (int i = 0; i < SCROLL_FRAME_COUNT; i++)
            {
                string path = string.Format(SCROLL_PATH_FORMAT, i + 1);
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null)
                {
                    throw new InvalidOperationException("The briefing needs the scroll frame " + path + ".");
                }
            }

            Vector2 middle = new Vector2(0.5f, 0.5f);
            GameObject root = NewRect("MissionBriefing", null);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.AddComponent<GraphicRaycaster>();

            // any click on the dim goes on
            GameObject backdrop = NewRect("Backdrop", root.transform);
            Stretch((RectTransform)backdrop.transform);
            Image backdropImage = AddImage(backdrop, null, BackdropColor, true);
            Button clickArea = backdrop.AddComponent<Button>();
            clickArea.transition = Selectable.Transition.None;
            clickArea.targetGraphic = backdropImage;
            backdrop.SetActive(false);

            Vector2 frameSize = frames[0].rect.size;
            GameObject content = NewRect("Content", root.transform);
            SetRect((RectTransform)content.transform, middle, Vector2.zero, frameSize * SCROLL_SCALE);
            GameObject scroll = NewRect("ScrollImage", content.transform);
            Stretch((RectTransform)scroll.transform);
            Image scrollImage = AddImage(scroll, frames[0], Color.white, false);

            // the parchment's middle relative to the frame's middle; frames count y downwards, the canvas upwards
            Vector2 textCenter = new Vector2(Parchment.center.x - frameSize.x * 0.5f, frameSize.y * 0.5f - Parchment.center.y) * SCROLL_SCALE;
            GameObject page = NewRect("PageText", content.transform);
            SetRect((RectTransform)page.transform, middle, textCenter, Parchment.size * SCROLL_SCALE);
            TextMeshProUGUI pageText = page.AddComponent<TextMeshProUGUI>();
            pageText.font = theme.Font;
            pageText.fontSize = PAGE_FONT_SIZE;
            pageText.color = InkColor;
            pageText.alignment = TextAlignmentOptions.Center;
            pageText.textWrappingMode = TextWrappingModes.Normal;
            pageText.raycastTarget = false;
            pageText.alpha = 0f;
            content.SetActive(false);

            GameObject explosion = NewRect("ExplosionImage", root.transform);
            SetRect((RectTransform)explosion.transform, middle, new Vector2(0f, EXPLOSION_OFFSET_Y), Vector2.one * EXPLOSION_SIZE);
            Image explosionImage = AddImage(explosion, null, Color.white, false);
            explosionImage.material = nuke;
            explosion.SetActive(false);

            GameObject flash = NewRect("FlashImage", root.transform);
            Stretch((RectTransform)flash.transform);
            Image flashImage = AddImage(flash, null, new Color(1f, 0.98f, 0.92f, 0f), false);
            flash.SetActive(false);

            MissionBriefingView view = root.AddComponent<MissionBriefingView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("backdrop").objectReferenceValue = backdropImage;
            serialized.FindProperty("clickArea").objectReferenceValue = clickArea;
            serialized.FindProperty("content").objectReferenceValue = content.transform;
            serialized.FindProperty("scroll").objectReferenceValue = scrollImage;
            SerializedProperty frameProperty = serialized.FindProperty("scrollFrames");
            frameProperty.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                frameProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            serialized.FindProperty("pageText").objectReferenceValue = pageText;
            serialized.FindProperty("explosion").objectReferenceValue = explosionImage;
            serialized.FindProperty("flash").objectReferenceValue = flashImage;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.MISSION_BRIEFING_PATH);
            Object.DestroyImmediate(root);
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

        // anchored and pivoted in the middle of the parent
        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
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
