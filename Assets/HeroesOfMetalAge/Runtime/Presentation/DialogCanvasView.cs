using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The modal dialog for intro, encounters, victory and defeat. Its three buttons map to the commands that the current
    /// dialog offers.
    /// </summary>
    public sealed class DialogCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private UiWidget primaryButton;
        [SerializeField] private UiWidget secondaryButton;
        [SerializeField] private UiWidget tertiaryButton;

        /// <summary>The button that receives keyboard focus when the dialog opens.</summary>
        public UiWidget PrimaryButton => primaryButton;

        public override void Refresh()
        {
            DialogKind dialog = Session.Dialog;
            UiBinding.SetText(titleText, Title(dialog));
            UiBinding.SetText(bodyText, Body(dialog));
            UiBinding.SetButton(primaryButton, PrimaryLabel(dialog), true);
            UiBinding.SetVisible(secondaryButton.gameObject, dialog != DialogKind.Victory);
            UiBinding.SetButton(secondaryButton, SecondaryLabel(dialog), dialog != DialogKind.Intro || Session.HasSave);
            UiBinding.SetVisible(tertiaryButton.gameObject, dialog == DialogKind.Defeat);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(bodyText, nameof(bodyText));
            Require(primaryButton, nameof(primaryButton));
            Require(secondaryButton, nameof(secondaryButton));
            Require(tertiaryButton, nameof(tertiaryButton));
        }

        protected override void BindControls()
        {
            primaryButton.Bind(HandlePrimary);
            secondaryButton.Bind(HandleSecondary);
            tertiaryButton.Bind(Session.Restart);
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
                case DialogKind.Victory:
                    Session.ContinueVictory();
                    break;
                default:
                    Session.FightEncounter();
                    break;
            }
        }

        private void HandleSecondary()
        {
            switch (Session.Dialog)
            {
                case DialogKind.Intro:
                    Session.Load();
                    break;
                case DialogKind.Defeat:
                    Session.PrepareAfterDefeat();
                    break;
                default:
                    Session.Back();
                    break;
            }
        }

        private string Title(DialogKind dialog)
        {
            switch (dialog)
            {
                case DialogKind.Intro:
                    return Catalog.Mission.Name;
                case DialogKind.Defeat:
                    return "YOUR OATH FALLS SILENT";
                case DialogKind.Victory:
                    return Session.Encounter.Kind == EncounterKind.Boss ? "THE PASS IS YOURS" : "VICTORY";
                default:
                    return Session.Encounter.Name;
            }
        }

        private string Body(DialogKind dialog)
        {
            switch (dialog)
            {
                case DialogKind.Intro:
                    return "You are " + Catalog.Hero(Campaign.HeroId).Name + ".\n" + Catalog.Mission.Description
                        + "\n\nGather the willing, clear the pass, recruit and defeat the stronger warhost."
                        + "\n\nClick a tile to plan. Click again or Space to ride. E ends the day. Sound follows your journey.";
                case DialogKind.Defeat:
                    return "Your warhost has been defeated.\n\nRetry with the army you brought into this battle, return to prepare a stronger force, or begin the mission again.";
                case DialogKind.Victory:
                    return VictoryBody();
                default:
                    return EncounterBody(dialog);
            }
        }

        private string VictoryBody()
        {
            VictorySummary result = Session.Victory;
            EncounterKind kind = Session.Encounter.Kind;
            string artefact = string.IsNullOrEmpty(result.ArtefactId) ? string.Empty : "ARTEFACT   " + Catalog.Artefact(result.ArtefactId).Name + "\n";
            string level = result.HasLeveledUp ? "LEVEL UP / Your hero is now level " + Rules.Level(Campaign) : "The road is yours.";
            string closing = kind == EncounterKind.Boss
                ? "\nFirst mission complete."
                : kind == EncounterKind.Town ? "\nThis town is now yours." : "\nThe road to the town is open.";
            return "REWARDS   " + result.Experience + " XP / " + result.Gold + " GOLD\n" + artefact + "LOSSES   " + result.Losses + " CREATURES\n\n"
                + level + closing;
        }

        private string EncounterBody(DialogKind dialog)
        {
            EncounterDefinition encounter = Session.Encounter;
            string army = string.Join(", ", encounter.Army.Select(troop => troop.Count + " " + Catalog.Unit(troop.UnitId).Name));
            if (dialog == DialogKind.Friendly)
            {
                return army + "\n\n" + encounter.Description + "\n\nThey ask for no gold.";
            }

            string artefact = string.IsNullOrEmpty(encounter.RewardArtefactId) ? string.Empty : " / " + Catalog.Artefact(encounter.RewardArtefactId).Name;
            return army + "\n\n" + encounter.Description + "\n\nVictory: " + encounter.RewardExperience + " XP / " + encounter.RewardGold + " GOLD" + artefact;
        }

        private static string PrimaryLabel(DialogKind dialog)
        {
            switch (dialog)
            {
                case DialogKind.Intro:
                    return "TAKE THE IRON OATH";
                case DialogKind.Friendly:
                    return "WELCOME THEM";
                case DialogKind.Victory:
                    return "CONTINUE THE OATH";
                case DialogKind.Defeat:
                    return "RETRY BATTLE";
                default:
                    return "TO BATTLE";
            }
        }

        private static string SecondaryLabel(DialogKind dialog)
        {
            switch (dialog)
            {
                case DialogKind.Intro:
                    return "LOAD CAMPAIGN";
                case DialogKind.Defeat:
                    return "RETURN / PREPARE";
                default:
                    return "LEAVE";
            }
        }
    }
}
