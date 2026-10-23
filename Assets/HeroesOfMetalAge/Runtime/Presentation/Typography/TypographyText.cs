using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Gives its text the font and look of a <see cref="TextStyle"/> from the <see cref="TypographySettings"/>. Applied when
    /// the text wakes and whenever the component is edited; the font size stays the text's own, only the style's size scale
    /// is applied on top of it (a change of scale resizes the text by the ratio, so sizes set by hand are kept).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class TypographyText : MonoBehaviour
    {
        [SerializeField] private TypographySettings settings;
        [SerializeField] private TextStyle style = TextStyle.Body;
        [Tooltip("The style's size scale already in the text's font size; managed by the component.")]
        [SerializeField] private float appliedScale = 1f;

        public TextStyle Style => style;
        public TypographySettings Settings => settings;

        /// <summary>Sets the style and applies it; used by tooling.</summary>
        public void Configure(TypographySettings typography, TextStyle textStyle)
        {
            settings = typography;
            style = textStyle;
            Apply();
        }

        /// <returns>True if anything on the text changed.</returns>
        public bool Apply()
        {
            TMP_Text text = GetComponent<TMP_Text>();
            TextStyleEntry entry = settings != null ? settings.Find(style) : null;
            if (text == null || entry == null || entry.Font == null)
            {
                return false;
            }

            bool isChanged = false;
            if (text.font != entry.Font)
            {
                text.font = entry.Font;
                isChanged = true;
            }

            Material material = entry.Material != null ? entry.Material : entry.Font.material;
            if (text.fontSharedMaterial != material)
            {
                text.fontSharedMaterial = material;
                isChanged = true;
            }

            float scale = Mathf.Max(0.01f, entry.SizeScale);
            if (!Mathf.Approximately(scale, appliedScale))
            {
                float ratio = scale / Mathf.Max(0.01f, appliedScale);
                text.fontSize *= ratio;
                text.fontSizeMin *= ratio;
                text.fontSizeMax *= ratio;
                appliedScale = scale;
                isChanged = true;
            }

            isChanged |= SetSpacing(text, entry);
            isChanged |= SetCase(text, entry.IsUpperCase);
            isChanged |= SetGradient(text, entry);
            return isChanged;
        }

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // a component edited in the Inspector shows its style at once; deferred, because OnValidate may not change other
            // components
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.EditorApplication.delayCall += ApplyIfAlive;
            }
        }

        private void ApplyIfAlive()
        {
            if (this != null && Apply())
            {
                UnityEditor.EditorUtility.SetDirty(GetComponent<TMP_Text>());
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        private static bool SetSpacing(TMP_Text text, TextStyleEntry entry)
        {
            bool isChanged = false;
            if (!Mathf.Approximately(text.characterSpacing, entry.CharacterSpacing))
            {
                text.characterSpacing = entry.CharacterSpacing;
                isChanged = true;
            }

            if (!Mathf.Approximately(text.lineSpacing, entry.LineSpacing))
            {
                text.lineSpacing = entry.LineSpacing;
                isChanged = true;
            }

            return isChanged;
        }

        private static bool SetCase(TMP_Text text, bool isUpperCase)
        {
            FontStyles styles = text.fontStyle;
            FontStyles wanted = isUpperCase ? styles | FontStyles.UpperCase : styles & ~FontStyles.UpperCase;
            if (wanted == styles)
            {
                return false;
            }

            text.fontStyle = wanted;
            return true;
        }

        private static bool SetGradient(TMP_Text text, TextStyleEntry entry)
        {
            if (!entry.HasGradient)
            {
                if (!text.enableVertexGradient)
                {
                    return false;
                }

                text.enableVertexGradient = false;
                return true;
            }

            VertexGradient gradient = new VertexGradient(entry.GradientTop, entry.GradientTop, entry.GradientBottom, entry.GradientBottom);
            VertexGradient current = text.colorGradient;
            bool isSame = text.enableVertexGradient && current.topLeft == gradient.topLeft && current.bottomLeft == gradient.bottomLeft;
            if (isSame)
            {
                return false;
            }

            text.enableVertexGradient = true;
            text.colorGradient = gradient;
            return true;
        }
    }
}
