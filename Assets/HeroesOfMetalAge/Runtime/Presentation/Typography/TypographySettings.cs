using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The game's fonts in one place: each <see cref="TextStyle"/> has a font, an optional material preset (outline, glow,
    /// shadow), a size scale, letter spacing, capitals and an optional gold gradient. Every text carries a
    /// <see cref="TypographyText"/> that names its style; changing an entry here changes every text of that style (in the
    /// Editor at once through Tools > Heroes of Metal Age > Apply typography, in the game when the text wakes).
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Typography")]
    public sealed class TypographySettings : ScriptableObject
    {
        [SerializeField] private List<TextStyleEntry> styles = new List<TextStyleEntry>();

        public IReadOnlyList<TextStyleEntry> Styles => styles;

        /// <returns>The entry of the style, or null when it is not authored (the text then keeps its own look).</returns>
        public TextStyleEntry Find(TextStyle style)
        {
            foreach (TextStyleEntry entry in styles)
            {
                if (entry != null && entry.Style == style)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Adds or replaces a style's entry; used by tooling.</summary>
        internal void Set(TextStyleEntry entry)
        {
            styles.RemoveAll(existing => existing == null || existing.Style == entry.Style);
            styles.Add(entry);
            styles.Sort((a, b) => a.Style.CompareTo(b.Style));
        }
    }

    [Serializable]
    public sealed class TextStyleEntry
    {
        [SerializeField] private TextStyle style;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Material preset of the font (outline, underlay, glow); empty uses the font's own material.")]
        [SerializeField] private Material material;
        [Tooltip("Multiplies the size each text was authored with, so a font that runs small or large can be evened out.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float sizeScale = 1f;
        [SerializeField] private float characterSpacing;
        [SerializeField] private float lineSpacing;
        [Tooltip("Shows the text in capitals without changing what the code writes.")]
        [SerializeField] private bool isUpperCase;
        [Tooltip("Tints the letters from the top colour to the bottom one (multiplied with the text's own colour).")]
        [SerializeField] private bool hasGradient;
        [SerializeField] private Color gradientTop = Color.white;
        [SerializeField] private Color gradientBottom = Color.white;

        public TextStyleEntry(TextStyle style, TMP_FontAsset font, Material material, float sizeScale, float characterSpacing, bool isUpperCase)
        {
            this.style = style;
            this.font = font;
            this.material = material;
            this.sizeScale = sizeScale;
            this.characterSpacing = characterSpacing;
            this.isUpperCase = isUpperCase;
        }

        public TextStyle Style => style;
        public TMP_FontAsset Font => font;
        public Material Material => material;
        public float SizeScale => sizeScale;
        public float CharacterSpacing => characterSpacing;
        public float LineSpacing => lineSpacing;
        public bool IsUpperCase => isUpperCase;
        public bool HasGradient => hasGradient;
        public Color GradientTop => gradientTop;
        public Color GradientBottom => gradientBottom;

        internal void SetGradient(Color top, Color bottom)
        {
            hasGradient = true;
            gradientTop = top;
            gradientBottom = bottom;
        }

        internal void SetLineSpacing(float spacing)
        {
            lineSpacing = spacing;
        }
    }
}
