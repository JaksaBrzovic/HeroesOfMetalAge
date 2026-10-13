using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The modal dialog for the intro and encounters. Its two buttons map to the commands that the current dialog offers.
    /// Victory, rewards and defeat have their own screens.
    /// </summary>
    public sealed class DialogCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget primaryButton;
        [SerializeField] private UiWidget secondaryButton;

        /// <summary>The button that receives keyboard focus when the dialog opens.</summary>
        public UiWidget PrimaryButton => primaryButton;

        /// <summary>True for the dialogs this view shows.</summary>
        public static bool Shows(DialogKind dialog)
        {
            return dialog == DialogKind.Intro || dialog == DialogKind.Friendly || dialog == DialogKind.Hostile || dialog == DialogKind.Claim;
        }

        public override void Refresh()
        {
            DialogKind dialog = Session.Dialog;
            UiBinding.SetText(titleText, dialog == DialogKind.Intro ? Catalog.Mission.Name : Session.Encounter.Name);
            UiBinding.SetText(bodyText, dialog == DialogKind.Intro ? IntroBody() : EncounterBody(dialog));
            UiBinding.SetButton(primaryButton, PrimaryLabel(dialog), true);
            UiBinding.SetButton(secondaryButton, dialog == DialogKind.Intro ? "LOAD CAMPAIGN" : "LEAVE", dialog != DialogKind.Intro || Session.HasSave);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
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
                case DialogKind.Intro:
                    Session.StartMission();
                    break;
                case DialogKind.Friendly:
                    Session.AcceptFriendly();
                    break;
                default:
                    Session.FightEncounter();
                    break;
            }
        }

        private void HandleSecondary()
        {
            if (Session.Dialog == DialogKind.Intro)
            {
                Session.Load();
            }
            else
            {
                Session.Back();
            }
        }

        private string IntroBody()
        {
            return "You are " + Catalog.Hero(Campaign.HeroId).Name + ".\n" + Catalog.Mission.Description
                + "\n\nGather the willing, clear the pass, recruit and defeat the stronger warhost."
                + "\n\nClick a tile to plan. Click again or Space to ride. E ends the day. Sound follows your journey.";
        }

        private string EncounterBody(DialogKind dialog)
        {
            EncounterDefinition encounter = Session.Encounter;
            string army = string.Join(", ", encounter.Army.Select(troop => troop.Count + " " + Catalog.Unit(troop.UnitId).Name));
            if (dialog == DialogKind.Friendly)
            {
                return army + "\n\n" + encounter.Description + "\n\nThey ask for no gold.";
            }

            return army + "\n\n" + encounter.Description + "\n\nVictory: " + encounter.RewardExperience + " XP / about " + encounter.RewardGold
                + " GOLD / perhaps an artefact";
        }

        private static string PrimaryLabel(DialogKind dialog)
        {
            switch (dialog)
            {
                case DialogKind.Intro:
                    return "TAKE THE IRON OATH";
                case DialogKind.Friendly:
                    return "WELCOME THEM";
                default:
                    return "TO BATTLE";
            }
        }
    }
}
