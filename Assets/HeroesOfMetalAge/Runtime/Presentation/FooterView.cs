using System;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Bottom bar: controls help and the current audio state.</summary>
    public sealed class FooterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text helpText;
        [SerializeField] private TMP_Text audioText;

        /// <exception cref="InvalidOperationException">A text reference is missing on the Footer prefab.</exception>
        public void Initialize()
        {
            if (helpText == null || audioText == null)
            {
                throw new InvalidOperationException($"FooterView on '{name}' has unassigned references. Assign them in the Footer prefab.");
            }
        }

        public void Display(string help, string audio)
        {
            UiBinding.SetText(helpText, help);
            UiBinding.SetText(audioText, audio);
        }
    }
}
