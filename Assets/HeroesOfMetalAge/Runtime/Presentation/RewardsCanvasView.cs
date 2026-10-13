using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The spoils of the battle just won: the gold and the artefact cards (none or one; legendary ones stand out). The
    /// rewards were applied when the battle was won; this screen only shows them.
    /// </summary>
    public sealed class RewardsCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text noteText;
        [SerializeField] private RectTransform artefactsContent;
        [SerializeField] private UiWidget artefactPrefab;
        [SerializeField] private UiWidget continueButton;

        private readonly List<UiWidget> artefactCards = new List<UiWidget>();

        /// <summary>The button that receives keyboard focus when the screen opens.</summary>
        public UiWidget PrimaryButton => continueButton;
        public IReadOnlyList<UiWidget> ArtefactCards => artefactCards;

        public override void Refresh()
        {
            VictorySummary result = Session.Victory;
            UiBinding.SetText(titleText, result.HasLegendary ? "LEGENDARY SPOILS" : "SPOILS OF WAR");
            UiBinding.SetText(goldText, "+" + result.Gold + " GOLD");
            UiBinding.SetText(noteText, Note(result));
            UiBinding.SetButton(continueButton, "CONTINUE THE OATH", true);
            RefreshArtefacts(result.ArtefactIds);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(goldText, nameof(goldText));
            Require(noteText, nameof(noteText));
            Require(artefactsContent, nameof(artefactsContent));
            Require(artefactPrefab, nameof(artefactPrefab));
            Require(continueButton, nameof(continueButton));
        }

        protected override void BindControls()
        {
            continueButton.Bind(Session.ContinueRewards);
        }

        /// <summary>Reuses the instantiated cards; extra cards are hidden. The cards only display, they take no clicks.</summary>
        private void RefreshArtefacts(IReadOnlyList<string> artefactIds)
        {
            while (artefactCards.Count < artefactIds.Count)
            {
                artefactCards.Add(Instantiate(artefactPrefab, artefactsContent));
            }

            for (int i = 0; i < artefactCards.Count; i++)
            {
                UiWidget card = artefactCards[i];
                bool isVisible = i < artefactIds.Count;
                UiBinding.SetVisible(card.gameObject, isVisible);
                if (!isVisible)
                {
                    continue;
                }

                string id = artefactIds[i];
                ArtefactDefinition artefact = Catalog.Artefact(id);
                card.Unbind();
                card.Display(artefact.Name, ArtefactLabels.Describe(artefact), string.Empty, Context.ArtefactIcon(id), Context.ArtefactTint(id), false);
                card.SetBackground(artefact.Rarity == ArtefactRarity.Legendary ? Context.Theme.Legendary : Context.Theme.Panel);
            }
        }

        private static string Note(VictorySummary result)
        {
            if (result.ArtefactIds.Count == 0)
            {
                return "No artefact among the fallen this time.";
            }

            return result.HasLegendary ? "A LEGENDARY ARTEFACT! Equip it on the hero screen [H]." : "An artefact joins your inventory. Equip it on the hero screen [H].";
        }
    }
}
