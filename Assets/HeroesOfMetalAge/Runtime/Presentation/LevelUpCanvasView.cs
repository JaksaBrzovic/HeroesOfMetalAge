using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The level-up dialog: the level reached, the primary stat it raised and the two secondary skills to choose from (a
    /// click on a card learns it). When nothing is left to learn a single button continues.
    /// </summary>
    public sealed class LevelUpCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("One card per offered skill, left to right.")]
        [SerializeField] private List<UiWidget> optionCards = new List<UiWidget>();
        [Tooltip("Shown instead of the cards when no skill is offered.")]
        [SerializeField] private UiWidget continueButton;

        /// <summary>The control that takes keyboard focus when the dialog opens.</summary>
        public UiWidget PrimaryButton => Campaign.PendingLevelUp != null && Campaign.PendingLevelUp.Options.Count > 0 ? optionCards[0] : continueButton;
        public IReadOnlyList<UiWidget> OptionCards => optionCards;

        public override void Refresh()
        {
            LevelUpOffer offer = Campaign.PendingLevelUp;
            if (offer == null)
            {
                return;
            }

            HeroPolicy policy = Catalog.HeroPolicy;
            UiBinding.SetText(titleText, Catalog.Hero(Campaign.HeroId).Name + " REACHES LEVEL " + offer.Level);
            UiBinding.SetText(subtitleText, "+1 " + MagicLabels.StatName(offer.Stat) + " (NOW " + Rules.Primary(Campaign, offer.Stat) + ")");
            UiBinding.SetText(bodyText, offer.Options.Count > 0 ? "Choose a secondary skill to learn." : "There is no skill left for your hero to learn.");
            for (int i = 0; i < optionCards.Count; i++)
            {
                UiWidget card = optionCards[i];
                bool hasOption = i < offer.Options.Count;
                UiBinding.SetVisible(card.gameObject, hasOption);
                if (!hasOption)
                {
                    continue;
                }

                HeroSkill option = offer.Options[i];
                SkillDefinition skill = policy.Skill(option.Skill);
                card.BindInfo(() => Context.Info.ShowSkill(option.Skill, Campaign.SkillLevelOf(option.Skill)));
                card.Display(MagicLabels.SkillTitle(policy, option), MagicLabels.SkillEffect(policy, option) + "\n" + skill.Description,
                    option.Level == SkillLevel.Basic ? "NEW SKILL" : "IMPROVED", Context.SkillIcon(option.Skill), Context.SkillTint(option.Skill), true);
            }

            UiBinding.SetVisible(continueButton.gameObject, offer.Options.Count == 0);
            UiBinding.SetButton(continueButton, "CONTINUE", true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(bodyText, nameof(bodyText));
            Require(continueButton, nameof(continueButton));
            if (optionCards.Count != LevelUpRules.OPTION_COUNT || optionCards.Exists(card => card == null))
            {
                Require(null, nameof(optionCards));
            }
        }

        protected override void BindControls()
        {
            for (int i = 0; i < optionCards.Count; i++)
            {
                int index = i;
                optionCards[i].Bind(() => Session.ChooseLevelUpSkill(index));
            }

            continueButton.Bind(() => Session.ChooseLevelUpSkill(0));
        }
    }
}
