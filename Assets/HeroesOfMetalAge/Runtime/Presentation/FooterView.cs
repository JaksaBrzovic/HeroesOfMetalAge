using System;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The bottom bar of a screen: controls help and, where the artwork has room, the current audio state.</summary>
    public sealed class FooterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text helpText;
        [Tooltip("Optional: screens without room for it leave it empty.")]
        [SerializeField] private TMP_Text audioText;

        /// <exception cref="InvalidOperationException">The help text is missing on the screen prefab.</exception>
        public void Initialize()
        {
            if (helpText == null)
            {
                throw new InvalidOperationException($"FooterView on '{name}' has no help text. Assign it in the screen prefab.");
            }
        }

        public void Display(string help, string audio)
        {
            UiBinding.SetText(helpText, help);
            if (audioText != null)
            {
                UiBinding.SetText(audioText, audio);
            }
        }
    }
}
