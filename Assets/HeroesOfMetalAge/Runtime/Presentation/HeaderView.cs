using System;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Top bar: mission title, day/week, gold and the hero and options buttons.</summary>
    public sealed class HeaderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private UiWidget heroButton;
        [SerializeField] private UiWidget optionsButton;

        public UiWidget HeroButton => heroButton;
        public UiWidget OptionsButton => optionsButton;

        /// <exception cref="InvalidOperationException">A control reference is missing on the Header prefab.</exception>
        public void Initialize(Action showHero, Action openOptions)
        {
            if (missionText == null || dayText == null || goldText == null || heroButton == null || optionsButton == null)
            {
                throw new InvalidOperationException($"HeaderView on '{name}' has unassigned references. Assign them in the Header prefab.");
            }

            heroButton.Bind(showHero);
            optionsButton.Bind(openOptions);
        }

        public void Display(string mission, string day, string gold, bool canShowHero)
        {
            UiBinding.SetText(missionText, mission);
            UiBinding.SetText(dayText, day);
            UiBinding.SetText(goldText, gold);
            UiBinding.SetButton(heroButton, "HERO [H]", canShowHero);
        }
    }
}
