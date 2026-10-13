using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The defeat screen: retry the battle, return to prepare a stronger army or begin the mission again.</summary>
    public sealed class DefeatCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget retryButton;
        [SerializeField] private UiWidget returnButton;
        [SerializeField] private UiWidget newMissionButton;

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => retryButton;

        public override void Refresh()
        {
            UiBinding.SetText(titleText, "YOUR OATH FALLS SILENT");
            UiBinding.SetText(bodyText, Session.Encounter.Name + " has broken your warhost.\n\nRetry with the army you brought into this battle, "
                + "return to prepare a stronger force, or begin the mission again.");
            UiBinding.SetButton(retryButton, "RETRY BATTLE", true);
            UiBinding.SetButton(returnButton, "RETURN / PREPARE", true);
            UiBinding.SetButton(newMissionButton, "NEW MISSION", true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(bodyText, nameof(bodyText));
            Require(retryButton, nameof(retryButton));
            Require(returnButton, nameof(returnButton));
            Require(newMissionButton, nameof(newMissionButton));
        }

        protected override void BindControls()
        {
            retryButton.Bind(Session.FightEncounter);
            returnButton.Bind(Session.PrepareAfterDefeat);
            newMissionButton.Bind(Session.Restart);
        }
    }
}
