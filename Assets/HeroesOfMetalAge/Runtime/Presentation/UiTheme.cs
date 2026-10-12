using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Shared font, palette and placeholder art used when content has no portrait or icon.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/UI Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        private const string BOW_ICON_KEY = "bow";
        private const string RIDER_ICON_KEY = "rider";

        [SerializeField] private TMP_FontAsset font;

        [Header("Palette")]
        [SerializeField] private Color background = new Color(0.045f, 0.055f, 0.062f);
        [SerializeField] private Color panel = new Color(0.075f, 0.087f, 0.094f);
        [SerializeField] private Color gold = new Color(0.86f, 0.68f, 0.34f);
        [SerializeField] private Color text = new Color(0.9f, 0.88f, 0.8f);
        [SerializeField] private Color muted = new Color(0.51f, 0.58f, 0.57f);
        [SerializeField] private Color enemy = new Color(0.82f, 0.29f, 0.22f);
        [SerializeField] private Color friendly = new Color(0.33f, 0.69f, 0.61f);

        [Header("Combat board")]
        [SerializeField] private Color hexColor = new Color(0.22f, 0.24f, 0.22f);
        [SerializeField] private Color reachableHexColor = new Color(0.17f, 0.32f, 0.32f);
        [SerializeField] private Color activeHexColor = new Color(0.46f, 0.38f, 0.2f);

        [Header("Placeholder art")]
        [SerializeField] private Sprite hero;
        [SerializeField] private Sprite town;
        [SerializeField] private Sprite axe;
        [SerializeField] private Sprite bow;
        [SerializeField] private Sprite rider;

        public TMP_FontAsset Font => font;
        public Color Background => background;
        public Color Panel => panel;
        public Color Gold => gold;
        public Color Text => text;
        public Color Muted => muted;
        public Color Enemy => enemy;
        public Color Friendly => friendly;
        public Color HexColor => hexColor;
        public Color ReachableHexColor => reachableHexColor;
        public Color ActiveHexColor => activeHexColor;
        public Sprite Hero => hero;
        public Sprite Town => town;
        public Sprite Axe => axe;
        public Sprite Bow => bow;
        public Sprite Rider => rider;

        /// <summary>Placeholder creature art for a unit's icon key; unknown keys use the axe.</summary>
        public Sprite Unit(string iconKey)
        {
            switch (iconKey)
            {
                case BOW_ICON_KEY:
                    return bow;
                case RIDER_ICON_KEY:
                    return rider;
                default:
                    return axe;
            }
        }
    }
}
