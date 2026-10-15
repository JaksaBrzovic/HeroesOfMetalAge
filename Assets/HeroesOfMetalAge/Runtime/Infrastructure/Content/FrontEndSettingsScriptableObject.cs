using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>Timing of the sequence from the main menu's New Game into the mission.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Front End Settings")]
    public sealed class FrontEndSettingsScriptableObject : ScriptableObject
    {
        [Tooltip("Seconds the main menu and the music take to fade to black after New Game.")]
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 1.5f;
        [Tooltip("Seconds of black silence before the intro.")]
        [Min(0f)]
        [SerializeField] private float blackSeconds = 2f;
        [Tooltip("Length of the intro (for now the end-of-day hourglass as a placeholder).")]
        [Min(0f)]
        [SerializeField] private float introSeconds = 3f;
        [Tooltip("Seconds the mission and its music take to fade in from black.")]
        [Min(0f)]
        [SerializeField] private float fadeInSeconds = 1.5f;

        public float FadeOutSeconds => fadeOutSeconds;
        public float BlackSeconds => blackSeconds;
        public float IntroSeconds => introSeconds;
        public float FadeInSeconds => fadeInSeconds;
    }
}
