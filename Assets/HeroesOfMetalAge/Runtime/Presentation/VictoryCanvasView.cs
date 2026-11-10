using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The first step after a won battle: what each side lost; its button leads to the rewards screen (experience, gold, artefact).</summary>
    public sealed class VictoryCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget continueButton;
        [Tooltip("What each side lost, inside the victory frame.")]
        [SerializeField] private CasualtiesView casualties;

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => continueButton;

        public override void Refresh()
        {
            EncounterDefinition encounter = Session.Encounter;
            UiBinding.SetText(titleText, "VICTORY");
            UiBinding.SetText(subtitleText, encounter.Name + " has fallen.");
            UiBinding.SetText(bodyText, Closing(encounter.Kind));
            UiBinding.SetButton(continueButton, "CLAIM THE SPOILS", true);
            casualties.Display(Session.Casualties);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(bodyText, nameof(bodyText));
            Require(continueButton, nameof(continueButton));
            Require(casualties, nameof(casualties));
        }

        protected override void BindControls()
        {
            continueButton.Bind(Session.ContinueVictory);
            casualties.Initialize(Context);
        }

        private static string Closing(EncounterKind kind)
        {
            return kind == EncounterKind.Boss ? "The way to the portal lies open."
                : kind == EncounterKind.Town ? "This town is now yours." : "The road ahead is open.";
        }
    }
}
