using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The second step after a won battle: the experience, the gold and the artefact in its frame (at most one per victory).
    /// The rewards were applied when the battle was won; this screen only shows them.
    /// </summary>
    public sealed class RewardsCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private Image artefactImage;
        [SerializeField] private TMP_Text artefactNameText;
        [SerializeField] private TMP_Text noteText;
        [SerializeField] private UiWidget continueButton;

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => continueButton;
        public Image ArtefactImage => artefactImage;

        public override void Refresh()
        {
            VictorySummary result = Session.Victory;
            UiBinding.SetText(titleText, result.HasLegendary ? "LEGENDARY SPOILS" : "SPOILS OF WAR");
            UiBinding.SetText(goldText, "+" + result.Gold + " GOLD");
            UiBinding.SetText(experienceText, "+" + result.Experience + " EXPERIENCE" + (result.HasLeveledUp ? " / LEVEL UP!" : string.Empty));
            UiBinding.SetButton(continueButton, "OK", true);
            if (result.ArtefactIds.Count == 0)
            {
                UiBinding.SetSprite(artefactImage, null, Color.white);
                UiBinding.SetText(artefactNameText, string.Empty);
                UiBinding.SetText(noteText, "No artefact among the fallen this time.");
                return;
            }

            string id = result.ArtefactIds[0];
            ArtefactDefinition artefact = Catalog.Artefact(id);
            UiBinding.SetSprite(artefactImage, Context.ArtefactIcon(id), Context.ArtefactTint(id));
            UiBinding.SetText(artefactNameText, artefact.Name.ToUpperInvariant());
            UiBinding.SetText(noteText, ArtefactLabels.Describe(artefact));
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(goldText, nameof(goldText));
            Require(experienceText, nameof(experienceText));
            Require(artefactImage, nameof(artefactImage));
            Require(artefactNameText, nameof(artefactNameText));
            Require(noteText, nameof(noteText));
            Require(continueButton, nameof(continueButton));
        }

        protected override void BindControls()
        {
            continueButton.Bind(Session.ContinueRewards);
        }
    }
}
