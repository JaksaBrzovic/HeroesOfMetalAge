using System;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The top bar of a screen: day/week, gold and the hero and options buttons. Every full screen carries its own, placed
    /// on its own artwork; the root shows the one of the screen on display.
    /// </summary>
    public sealed class HeaderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private UiWidget heroButton;
        [SerializeField] private UiWidget optionsButton;

        public UiWidget HeroButton => heroButton;
        public UiWidget OptionsButton => optionsButton;

        /// <exception cref="InvalidOperationException">A control reference is missing on the screen prefab.</exception>
        public void Initialize(Action showHero, Action openOptions)
        {
            if (dayText == null || goldText == null || heroButton == null || optionsButton == null)
            {
                throw new InvalidOperationException($"HeaderView on '{name}' has unassigned references. Assign them in the screen prefab.");
            }

            heroButton.Bind(showHero);
            optionsButton.Bind(openOptions);
        }

        public void Display(string day, string gold, bool canShowHero)
        {
            UiBinding.SetText(dayText, day);
            UiBinding.SetText(goldText, gold);
            heroButton.SetInteractable(canShowHero);
        }
    }
}
