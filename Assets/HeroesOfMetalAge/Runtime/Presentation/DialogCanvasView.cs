using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The framed modal dialog: encounters (join, fight, claim the town), treasure chests (gold or experience), dwellings
    /// (recruit) and the exit portal (finish the mission). Its two buttons map to the commands the current dialog offers.
    /// The mission's intro is the briefing scroll (<see cref="MissionBriefingView"/>); victory, rewards and defeat have their
    /// own screens.
    /// </summary>
    public sealed class DialogCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget primaryButton;
        [SerializeField] private UiWidget secondaryButton;

        /// <summary>The button that receives keyboard focus when the dialog opens.</summary>
        public UiWidget PrimaryButton => primaryButton;
        public UiWidget SecondaryButton => secondaryButton;

        /// <summary>True for the dialogs this view shows.</summary>
        public static bool Shows(DialogKind dialog)
        {
            return dialog == DialogKind.Friendly || dialog == DialogKind.Hostile || dialog == DialogKind.Claim || dialog == DialogKind.Chest
                || dialog == DialogKind.Dwelling || dialog == DialogKind.Exit;
        }

        public override void Refresh()
        {
            switch (Session.Dialog)
            {
                case DialogKind.Chest:
                    ShowChest(Session.MapObject);
                    break;
                case DialogKind.Dwelling:
                    ShowDwelling(Session.MapObject);
                    break;
                case DialogKind.Exit:
                    ShowExit(Session.MapObject);
                    break;
                default:
                    ShowEncounter(Session.Dialog, Session.Encounter);
                    break;
            }
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(bodyText, nameof(bodyText));
            Require(primaryButton, nameof(primaryButton));
            Require(secondaryButton, nameof(secondaryButton));
        }

        protected override void BindControls()
        {
            primaryButton.Bind(HandlePrimary);
            secondaryButton.Bind(HandleSecondary);
        }

        private void HandlePrimary()
        {
            switch (Session.Dialog)
            {
                case DialogKind.Friendly:
                    Session.AcceptFriendly();
                    break;
                case DialogKind.Chest:
                    Session.OpenChest(true);
                    break;
                case DialogKind.Dwelling:
                    Session.RecruitAtDwelling(Session.Objects.AffordableAtDwelling(Campaign, Session.MapObject));
                    break;
                case DialogKind.Exit:
                    Session.FinishMission();
                    break;
                default:
                    if (IsOfferingToJoin())
                    {
                        Session.HireEncounter();
                    }
                    else
                    {
                        Session.FightEncounter();
                    }

                    break;
            }
        }

        private void HandleSecondary()
        {
            if (Session.Dialog == DialogKind.Chest)
            {
                Session.OpenChest(false);
                return;
            }

            // an army willing to join can still be fought; Escape leaves
            if (IsOfferingToJoin())
            {
                Session.FightEncounter();
                return;
            }

            Session.Back();
        }

        private void ShowEncounter(DialogKind dialog, EncounterDefinition encounter)
        {
            string army = string.Join(", ", encounter.Army.Select(troop => troop.Count + " " + Catalog.Unit(troop.UnitId).Name));
            UiBinding.SetText(titleText, encounter.Name);
            UiBinding.SetText(subtitleText, army);
            if (dialog == DialogKind.Friendly)
            {
                Show(encounter.Description + "\nThey ask for no gold.", "WELCOME THEM", true, "LEAVE");
                return;
            }

            string treasure = encounter.RewardArtefactId != null ? "They guard " + Catalog.Artefact(encounter.RewardArtefactId).Name + "."
                : "Perhaps an artefact among the fallen.";
            string commander = encounter.Commander != null ? "\n" + encounter.Commander.Name + " leads them and wields magic." : string.Empty;
            string victory = encounter.Description + commander + "\nVictory: " + encounter.RewardExperience + " XP, about " + encounter.RewardGold
                + " gold. " + treasure;
            int price = dialog == DialogKind.Hostile ? Rules.JoinPrice(Campaign, encounter) : -1;
            if (price >= 0)
            {
                Show(victory + "\nYour Diplomacy sways them: they would join you for " + price + " gold.", "HIRE FOR " + price + " GOLD",
                    price <= Campaign.Gold, "TO BATTLE");
                return;
            }

            Show(victory, "TO BATTLE", true, "LEAVE");
        }

        private bool IsOfferingToJoin()
        {
            return Session.Dialog == DialogKind.Hostile && Session.Encounter != null && Rules.JoinPrice(Campaign, Session.Encounter) >= 0;
        }

        private void ShowChest(MapObjectDefinition chest)
        {
            UiBinding.SetText(titleText, chest.Name);
            UiBinding.SetText(subtitleText, "TAKE THE GOLD OR LEARN FROM WHAT LIES WITHIN");
            Show(chest.Description, "+" + chest.Gold + " GOLD", true, "+" + chest.Experience + " XP");
        }

        private void ShowDwelling(MapObjectDefinition dwelling)
        {
            UnitDefinition unit = Catalog.Unit(dwelling.UnitId);
            int stock = Campaign.DwellingStock(dwelling.Id);
            int affordable = Session.Objects.AffordableAtDwelling(Campaign, dwelling);
            UiBinding.SetText(titleText, dwelling.Name);
            UiBinding.SetText(subtitleText, stock + " " + unit.Name + " / " + unit.Cost + " GOLD EACH");
            Show(dwelling.Description + "\n" + unit.Description + " Their numbers grow by " + dwelling.WeeklyGrowth + " every week.",
                affordable > 0 ? "RECRUIT " + affordable + " / " + affordable * unit.Cost + " GOLD" : "NONE YOU CAN AFFORD", affordable > 0, "LEAVE");
        }

        private void ShowExit(MapObjectDefinition portal)
        {
            int reached = Session.Objects.ReachedCount(Campaign);
            int total = Catalog.Mission.Objectives.Count;
            bool canFinish = Session.Objects.CanFinish(Campaign);
            UiBinding.SetText(titleText, portal.Name);
            UiBinding.SetText(subtitleText, "GOALS REACHED " + reached + " / " + total);
            Show(canFinish ? portal.Description + "\nFinish the mission?" : "The portal stays dark until every required goal (*) is reached.",
                "FINISH MISSION", canFinish, "NOT YET");
        }

        private void Show(string body, string primary, bool canUsePrimary, string secondary)
        {
            UiBinding.SetText(bodyText, body);
            UiBinding.SetButton(primaryButton, primary, canUsePrimary);
            UiBinding.SetButton(secondaryButton, secondary, true);
        }
    }
}
