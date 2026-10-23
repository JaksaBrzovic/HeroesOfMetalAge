using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>One recruitment row of the town screen: the creature's portrait, name, description, stats and the recruit button.</summary>
    public sealed class RecruitRowView : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private UiWidget recruitButton;

        public UiWidget RecruitButton => recruitButton;

        /// <exception cref="InvalidOperationException">A reference is missing on the screen prefab.</exception>
        public void Initialize(Action recruit)
        {
            if (portraitImage == null || nameText == null || descriptionText == null || statsText == null || recruitButton == null)
            {
                throw new InvalidOperationException($"RecruitRowView on '{name}' has unassigned references. Assign them in the town prefab.");
            }

            recruitButton.Bind(recruit);
        }

        public void Display(string unitName, string description, string stats, string recruitLabel, Sprite portrait, Color tint, bool canRecruit)
        {
            UiBinding.SetText(nameText, unitName);
            UiBinding.SetText(descriptionText, description);
            UiBinding.SetText(statsText, stats);
            UiBinding.SetSprite(portraitImage, portrait, tint);
            UiBinding.SetButton(recruitButton, recruitLabel, canRecruit);
        }
    }
}
