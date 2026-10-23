using System;
using System.IO;
using HeroesOfMetalAge.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off generation tool: it created the TextMeshPro font assets of the fonts in <c>Content/UI/Fonts/</c> (New Rocker,
    /// Cinzel, Cinzel Decorative, IM Fell, Almendra) with their material presets, the typography settings
    /// <c>Content/UI/Typography.asset</c>, gave every text in the prefabs a <see cref="TypographyText"/> style picked from its
    /// name, pointed the UI theme at the body font, and gave the main menu's four titles the MenuGild material and a
    /// <see cref="GildEffect"/> so gold pours over them on hover. It only creates what is missing and never changes what
    /// exists, so it has nothing to do once they exist; the fonts are then changed in Typography.asset (and applied with
    /// Tools > Heroes of Metal Age > Apply typography). The project works without it: delete this file together with its call
    /// in <see cref="PrototypePipeline.Setup"/> and its mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class VisualStyleSeeder
    {
        private const string FONT_FOLDER = "Assets/HeroesOfMetalAge/Content/UI/Fonts";
        private const int SAMPLING_POINT_SIZE = 56;
        private const int ATLAS_PADDING = 7;
        private const int ATLAS_SIZE = 1024;
        private const int REGULAR_WEIGHT_INDEX = 4;
        private const string DISPLAY_FONT = FONT_FOLDER + "/NewRocker/NewRocker-Regular.ttf";
        private const string TITLE_FONT = FONT_FOLDER + "/CinzelDecorative/CinzelDecorative-Bold.ttf";
        private const string HEADING_FONT = FONT_FOLDER + "/Cinzel/Cinzel-Bold.ttf";
        private const string LABEL_FONT = FONT_FOLDER + "/Cinzel/Cinzel-SemiBold.ttf";
        private const string BODY_FONT = FONT_FOLDER + "/IMFell/IMFellDWPica-Regular.ttf";
        private const string BODY_ITALIC_FONT = FONT_FOLDER + "/IMFell/IMFellDWPica-Italic.ttf";
        private const string VERSE_FONT = FONT_FOLDER + "/IMFell/IMFellEnglishSC-Regular.ttf";
        private const string SCROLL_FONT = FONT_FOLDER + "/Almendra/Almendra-Regular.ttf";
        // Latin with the Croatian letters and the usual typographic marks; the atlases are static
        private const string EXTRA_CHARACTERS = "čćđšžČĆĐŠŽ—–‘’‚“”„…•·×€†‡°±";

        private static readonly Color MoltenOutline = new Color(0.16f, 0.06f, 0.02f, 1f);
        private static readonly Color EngravedOutline = new Color(0.07f, 0.04f, 0.03f, 1f);
        private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color GoldTop = new Color(1f, 0.9f, 0.58f, 1f);
        private static readonly Color GoldBottom = new Color(0.86f, 0.5f, 0.16f, 1f);

        [MenuItem("Tools/Heroes of Metal Age/Create missing visual style assets")]
        public static void CreateMissingAssets()
        {
            TypographySettings typography = AssetDatabase.LoadAssetAtPath<TypographySettings>(ContentAssets.TYPOGRAPHY_PATH);
            if (typography == null)
            {
                typography = CreateTypography();
                TypographyTools.AddMissingStyles(typography);
                UseBodyFontInTheme(typography);
            }

            AddMissingMenuGild();
            AssetDatabase.SaveAssets();
        }

        private static TypographySettings CreateTypography()
        {
            TMP_FontAsset display = FontAsset(DISPLAY_FONT);
            TMP_FontAsset title = FontAsset(TITLE_FONT);
            TMP_FontAsset heading = FontAsset(HEADING_FONT);
            TMP_FontAsset label = FontAsset(LABEL_FONT);
            TMP_FontAsset body = FontAsset(BODY_FONT);
            TMP_FontAsset bodyItalic = FontAsset(BODY_ITALIC_FONT);
            TMP_FontAsset verse = FontAsset(VERSE_FONT);
            TMP_FontAsset scroll = FontAsset(SCROLL_FONT);
            SetItalic(body, bodyItalic);

            Material molten = Preset(display, "Molten", MoltenOutline, 0.2f, 1.2f, 0.35f);
            Material burning = Preset(display, "Burning", MoltenOutline, 0.28f, 1.6f, 0.5f);
            Material engravedTitle = Preset(title, "Engraved", EngravedOutline, 0.12f, 0.9f, 0.3f);
            Material engravedHeading = Preset(heading, "Engraved", EngravedOutline, 0.1f, 0.8f, 0.3f);
            Material shadowedLabel = Preset(label, "Shadowed", EngravedOutline, 0.05f, 0.7f, 0.4f);
            Material shadowedBody = Preset(body, "Shadowed", EngravedOutline, 0f, 0.6f, 0.4f);
            Material shadowedVerse = Preset(verse, "Shadowed", EngravedOutline, 0f, 0.8f, 0.5f);

            TypographySettings typography = ScriptableObject.CreateInstance<TypographySettings>();
            TextStyleEntry displayEntry = new TextStyleEntry(TextStyle.Display, display, molten, 1f, 2f, false);
            displayEntry.SetGradient(GoldTop, GoldBottom);
            typography.Set(displayEntry);
            typography.Set(new TextStyleEntry(TextStyle.Title, title, engravedTitle, 1f, 1f, false));
            typography.Set(new TextStyleEntry(TextStyle.Heading, heading, engravedHeading, 1f, 2f, true));
            typography.Set(new TextStyleEntry(TextStyle.Button, heading, engravedHeading, 1f, 1f, true));
            typography.Set(new TextStyleEntry(TextStyle.Label, label, shadowedLabel, 0.95f, 1f, true));
            typography.Set(new TextStyleEntry(TextStyle.Body, body, shadowedBody, 1.1f, 0f, false));
            typography.Set(new TextStyleEntry(TextStyle.Number, heading, engravedHeading, 1f, 0f, false));
            TextStyleEntry scrollEntry = new TextStyleEntry(TextStyle.Scroll, scroll, null, 1.05f, 0f, false);
            scrollEntry.SetLineSpacing(-4f);
            typography.Set(scrollEntry);
            typography.Set(new TextStyleEntry(TextStyle.Verse, verse, shadowedVerse, 1f, 1f, false));
            TextStyleEntry floating = new TextStyleEntry(TextStyle.Floating, display, burning, 1f, 1f, false);
            floating.SetGradient(GoldTop, GoldBottom);
            typography.Set(floating);
            AssetDatabase.CreateAsset(typography, ContentAssets.TYPOGRAPHY_PATH);
            return typography;
        }

        /// <summary>A static SDF font asset next to its font file, with Latin and the Croatian letters baked in.</summary>
        private static TMP_FontAsset FontAsset(string fontPath)
        {
            string path = Path.ChangeExtension(fontPath, null) + " SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                throw new InvalidOperationException("Missing font file: " + fontPath);
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, ATLAS_SIZE, ATLAS_SIZE,
                AtlasPopulationMode.Dynamic, false);
            if (asset == null)
            {
                throw new InvalidOperationException("TextMeshPro could not read the font " + fontPath);
            }

            string name = Path.GetFileNameWithoutExtension(path);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTexture.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            asset.TryAddCharacters(Characters(), out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.Log(name + " has no glyph for: " + missing);
            }

            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.atlasTexture);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static string Characters()
        {
            System.Text.StringBuilder characters = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++)
            {
                characters.Append(c);
            }

            for (char c = ' '; c <= 'ÿ'; c++)
            {
                characters.Append(c);
            }

            return characters.Append(EXTRA_CHARACTERS).ToString();
        }

        private static void SetItalic(TMP_FontAsset regular, TMP_FontAsset italic)
        {
            TMP_FontWeightPair[] weights = regular.fontWeightTable;
            if (weights == null || weights.Length <= REGULAR_WEIGHT_INDEX)
            {
                return;
            }

            // the table hands out its own array, so the pair is set in place
            weights[REGULAR_WEIGHT_INDEX].italicTypeface = italic;
            EditorUtility.SetDirty(regular);
        }

        /// <summary>A material preset of the font: an outline and a soft drop shadow (TMP finds it by the font's name).</summary>
        private static Material Preset(TMP_FontAsset font, string look, Color outline, float outlineWidth, float shadowOffset, float shadowSoftness)
        {
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font)).Replace('\\', '/') + "/" + font.name + " " + look + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            ShaderUtilities.GetShaderPropertyIDs();
            Material material = new Material(font.material) { name = font.name + " " + look };
            material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            if (outlineWidth > 0f)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }

            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, Shadow);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, shadowOffset * 0.6f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -shadowOffset);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, shadowSoftness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void UseBodyFontInTheme(TypographySettings typography)
        {
            UiTheme theme = ContentAssets.Theme;
            TextStyleEntry body = typography.Find(TextStyle.Body);
            if (theme == null || body == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(theme);
            serialized.FindProperty("font").objectReferenceValue = body.Font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The four main menu titles get the gold material and a GildEffect their buttons drive.</summary>
        private static void AddMissingMenuGild()
        {
            Material gild = ContentAssets.MenuGildMaterial;
            if (gild == null || ContentAssets.FrontEndPrefab == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ContentAssets.FRONT_END_PATH);
            try
            {
                bool isChanged = false;
                foreach (NewGameButtonView newGame in root.GetComponentsInChildren<NewGameButtonView>(true))
                {
                    SerializedObject serialized = new SerializedObject(newGame);
                    SerializedProperty field = serialized.FindProperty("titleGild");
                    if (field.objectReferenceValue != null)
                    {
                        continue;
                    }

                    Image title = FindTitle(newGame.transform);
                    field.objectReferenceValue = AddGild(title, gild);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    isChanged = true;
                }

                foreach (MenuButtonView entry in root.GetComponentsInChildren<MenuButtonView>(true))
                {
                    SerializedObject serialized = new SerializedObject(entry);
                    SerializedProperty field = serialized.FindProperty("titleGild");
                    if (field.objectReferenceValue != null)
                    {
                        continue;
                    }

                    RectTransform highlight = (RectTransform)serialized.FindProperty("highlight").objectReferenceValue;
                    Image title = highlight != null ? highlight.GetComponent<Image>() : FindTitle(entry.transform);
                    field.objectReferenceValue = AddGild(title, gild);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    isChanged = true;
                }

                if (isChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.FRONT_END_PATH);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Image FindTitle(Transform button)
        {
            Transform title = button.Find("TitleImage");
            Image image = title != null ? title.GetComponent<Image>() : null;
            if (image == null)
            {
                throw new InvalidOperationException("The main menu entry '" + button.name + "' has no TitleImage.");
            }

            return image;
        }

        private static GildEffect AddGild(Image title, Material gild)
        {
            title.material = gild;
            GildEffect effect = title.GetComponent<GildEffect>();
            if (effect == null)
            {
                effect = title.gameObject.AddComponent<GildEffect>();
            }

            SerializedObject serialized = new SerializedObject(effect);
            serialized.FindProperty("image").objectReferenceValue = title;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }
    }
}
