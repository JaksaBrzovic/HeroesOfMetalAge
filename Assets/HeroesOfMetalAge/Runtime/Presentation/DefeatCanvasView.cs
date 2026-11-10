using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The defeat screen in the victory screen's frame: what each side lost; retry the battle, return to prepare a stronger army or begin the mission again.</summary>
    public sealed class DefeatCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget retryButton;
        [SerializeField] private UiWidget returnButton;
        [SerializeField] private UiWidget newMissionButton;
        [Tooltip("What each side lost, inside the defeat frame.")]
        [SerializeField] private CasualtiesView casualties;

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => retryButton;

        public override void Refresh()
        {
            UiBinding.SetText(titleText, "YOUR OATH FALLS SILENT");
            UiBinding.SetText(subtitleText, Session.Encounter.Name + " has broken your warhost.");
            UiBinding.SetText(bodyText, "Retry with the army you brought into this battle, return to prepare a stronger force, or begin the mission again.");
            UiBinding.SetButton(retryButton, "RETRY BATTLE", true);
            UiBinding.SetButton(returnButton, "RETURN / PREPARE", true);
            UiBinding.SetButton(newMissionButton, "NEW MISSION", true);
            casualties.Display(Session.Casualties);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(bodyText, nameof(bodyText));
            Require(retryButton, nameof(retryButton));
            Require(returnButton, nameof(returnButton));
            Require(newMissionButton, nameof(newMissionButton));
            Require(casualties, nameof(casualties));
        }

        protected override void BindControls()
        {
            retryButton.Bind(Session.FightEncounter);
            returnButton.Bind(Session.PrepareAfterDefeat);
            newMissionButton.Bind(Session.Restart);
            casualties.Initialize(Context);
        }
    }
}
