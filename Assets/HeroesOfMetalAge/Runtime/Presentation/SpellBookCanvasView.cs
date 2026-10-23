using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The spell book: an open book with the hero's spells on its two pages, turned with the corner buttons, the mana left
    /// and what the chosen spell does at the hero's spell power. In battle a click on a spell casts it (a spell on a stack or
    /// an area then waits for its target on the battlefield); elsewhere a click describes it.
    /// </summary>
    public sealed class SpellBookCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text pageText;
        [Tooltip("The spell places of both pages: the left page top to bottom, then the right page.")]
        [SerializeField] private List<UiWidget> spellSlots = new List<UiWidget>();
        [SerializeField] private UiWidget previousButton;
        [SerializeField] private UiWidget nextButton;
        [SerializeField] private UiWidget closeButton;

        private readonly List<string> shownSpells = new List<string>();
        private int page;
        private string selectedSpellId;

        public UiWidget CloseButton => closeButton;
        public IReadOnlyList<UiWidget> SpellSlots => spellSlots;
        public int Page => page;

        private bool IsInBattle => Session.Screen == GameScreen.Battle && Session.Battle != null;
        private int SpellPower => IsInBattle ? Session.Battle.Player.SpellPower : Rules.SpellPower(Campaign);

        public override void Refresh()
        {
            List<SpellDefinition> known = Catalog.Spells.Where(spell => Campaign.KnowsSpell(spell.Id)).ToList();
            int pages = Mathf.Max(1, (known.Count + spellSlots.Count - 1) / spellSlots.Count);
            page = Mathf.Clamp(page, 0, pages - 1);
            int mana = IsInBattle ? Session.Battle.Player.Mana : Campaign.Mana;
            UiBinding.SetText(titleText, IsInBattle ? "CHOOSE A SPELL" : "SPELL BOOK");
            UiBinding.SetText(manaText, "MANA " + mana + " / " + Rules.MaximumMana(Campaign));
            UiBinding.SetText(pageText, (page + 1) + " / " + pages);
            shownSpells.Clear();
            for (int i = 0; i < spellSlots.Count; i++)
            {
                UiWidget slot = spellSlots[i];
                int index = page * spellSlots.Count + i;
                if (index >= known.Count)
                {
                    slot.Display(string.Empty, string.Empty, string.Empty, null, Color.white, false);
                    continue;
                }

                SpellDefinition spell = known[index];
                shownSpells.Add(spell.Id);
                bool isAffordable = spell.ManaCost <= mana;
                slot.Display(spell.Name, MagicLabels.SpellCost(spell), string.Empty, Context.SpellIcon(spell.Id),
                    isAffordable ? Context.SpellTint(spell.Id) : new Color(0.45f, 0.42f, 0.4f, 1f), true);
            }

            UiBinding.SetText(descriptionText, Description(known));
            UiBinding.SetButton(previousButton, "<", page > 0);
            UiBinding.SetButton(nextButton, ">", page < pages - 1);
            UiBinding.SetButton(closeButton, "CLOSE", true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(manaText, nameof(manaText));
            Require(descriptionText, nameof(descriptionText));
            Require(pageText, nameof(pageText));
            Require(previousButton, nameof(previousButton));
            Require(nextButton, nameof(nextButton));
            Require(closeButton, nameof(closeButton));
            if (spellSlots.Count == 0 || spellSlots.Exists(slot => slot == null))
            {
                Require(null, nameof(spellSlots));
            }
        }

        protected override void BindControls()
        {
            for (int i = 0; i < spellSlots.Count; i++)
            {
                int index = i;
                spellSlots[i].Bind(() => HandleSpell(index));
            }

            previousButton.Bind(() => TurnPage(-1));
            nextButton.Bind(() => TurnPage(1));
            closeButton.Bind(Session.Back);
        }

        private void HandleSpell(int slotIndex)
        {
            if (slotIndex >= shownSpells.Count)
            {
                return;
            }

            selectedSpellId = shownSpells[slotIndex];
            if (IsInBattle)
            {
                Session.SelectSpell(selectedSpellId);
            }

            Refresh();
        }

        private void TurnPage(int pages)
        {
            page += pages;
            Context.Audio.Play(SoundCue.Click);
            Refresh();
        }

        private string Description(List<SpellDefinition> known)
        {
            if (known.Count == 0)
            {
                return "The pages are empty. Spells are learned where they are sold.";
            }

            SpellDefinition spell = known.FirstOrDefault(candidate => candidate.Id == selectedSpellId);
            if (spell == null)
            {
                return IsInBattle ? "Click a spell to cast it. Your hero casts one spell per round." : "Click a spell to read it.";
            }

            return spell.Name.ToUpperInvariant() + " / " + MagicLabels.SpellCost(spell) + "\n" + MagicLabels.SpellDescription(spell, SpellPower) + ". "
                + spell.Description;
        }
    }
}
