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
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text lossesText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget continueButton;
        [Tooltip("What each side lost, beside the victory frame.")]
        [SerializeField] private CasualtiesView casualties;

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => continueButton;

        public override void Refresh()
        {
            VictorySummary result = Session.Victory;
            EncounterDefinition encounter = Session.Encounter;
            UiBinding.SetText(titleText, "VICTORY");
            UiBinding.SetText(subtitleText, encounter.Name + " has fallen.");
            UiBinding.SetText(experienceText, "EXPERIENCE<pos=45%>+" + result.Experience + " XP");
            UiBinding.SetText(lossesText, "LOSSES<pos=45%>" + result.Losses + " CREATURES");
            UiBinding.SetText(bodyText, Closing(result, encounter.Kind));
            UiBinding.SetButton(continueButton, "CLAIM THE SPOILS", true);
            casualties.Display(Session.Casualties);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(experienceText, nameof(experienceText));
            Require(lossesText, nameof(lossesText));
            Require(bodyText, nameof(bodyText));
            Require(continueButton, nameof(continueButton));
            Require(casualties, nameof(casualties));
        }

        protected override void BindControls()
        {
            continueButton.Bind(Session.ContinueVictory);
            casualties.Initialize(Context);
        }

        private string Closing(VictorySummary result, EncounterKind kind)
        {
            string level = result.HasLeveledUp ? "LEVEL UP! Your hero is now level " + Rules.Level(Campaign) + "." : "The road is yours.";
            string closing = kind == EncounterKind.Boss ? "The way to the portal lies open."
                : kind == EncounterKind.Town ? "This town is now yours." : "The road ahead is open.";
            return level + "\n" + closing;
        }
    }
}
