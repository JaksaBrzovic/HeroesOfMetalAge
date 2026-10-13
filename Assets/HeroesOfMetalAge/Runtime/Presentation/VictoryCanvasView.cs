using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The victory screen after a won battle: experience, losses and level; it leads to the rewards screen.</summary>
    public sealed class VictoryCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget continueButton;

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => continueButton;

        public override void Refresh()
        {
            VictorySummary result = Session.Victory;
            EncounterDefinition encounter = Session.Encounter;
            UiBinding.SetText(titleText, encounter.Kind == EncounterKind.Boss ? "THE PASS IS YOURS" : "VICTORY");
            UiBinding.SetText(subtitleText, encounter.Name + " has fallen.");
            UiBinding.SetText(bodyText, Body(result, encounter.Kind));
            UiBinding.SetButton(continueButton, "CLAIM THE SPOILS", true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(bodyText, nameof(bodyText));
            Require(continueButton, nameof(continueButton));
        }

        protected override void BindControls()
        {
            continueButton.Bind(Session.ContinueVictory);
        }

        private string Body(VictorySummary result, EncounterKind kind)
        {
            string level = result.HasLeveledUp ? "LEVEL UP / Your hero is now level " + Rules.Level(Campaign) + "." : "The road is yours.";
            string closing = kind == EncounterKind.Boss
                ? "First mission complete."
                : kind == EncounterKind.Town ? "This town is now yours." : "The road to the town is open.";
            return "EXPERIENCE   +" + result.Experience + " XP\nLOSSES   " + result.Losses + " CREATURES\n\n" + level + "\n" + closing;
        }
    }
}
