using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// A spell shop on the map: its spell book (the first row) and the spells it sells, each with its price, or why it
    /// cannot be learned yet (no spell book, too little Wisdom, too little gold). A click on a row buys it.
    /// </summary>
    public sealed class SpellShopCanvasView : CanvasScreenView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private UiWidget bookRow;
        [Tooltip("One row per spell for sale, top to bottom.")]
        [SerializeField] private List<UiWidget> spellRows = new List<UiWidget>();
        [SerializeField] private UiWidget closeButton;
        [Tooltip("The picture on the spell book row.")]
        [SerializeField] private Sprite bookIcon;

        public UiWidget CloseButton => closeButton;
        public UiWidget BookRow => bookRow;
        public IReadOnlyList<UiWidget> SpellRows => spellRows;

        public override void Refresh()
        {
            MapObjectDefinition shop = Session.MapObject;
            if (shop == null || shop.Kind != MapObjectKind.SpellShop)
            {
                return;
            }

            UiBinding.SetText(titleText, shop.Name);
            UiBinding.SetText(subtitleText, "GOLD " + Campaign.Gold + " / YOUR WISDOM: SPELLS UP TO LEVEL " + Rules.MaximumSpellLevel(Campaign));
            bookRow.Display("SPELL BOOK", Campaign.HasSpellBook ? "You carry a spell book." : "A hero needs a spell book to learn and cast spells.",
                Campaign.HasSpellBook ? "OWNED" : shop.Gold + " GOLD", bookIcon, Color.white, !Campaign.HasSpellBook && shop.Gold <= Campaign.Gold);
            for (int i = 0; i < spellRows.Count; i++)
            {
                UiWidget row = spellRows[i];
                bool hasSpell = i < shop.SpellIds.Count;
                UiBinding.SetVisible(row.gameObject, hasSpell);
                if (!hasSpell)
                {
                    continue;
                }

                SpellDefinition spell = Catalog.Spell(shop.SpellIds[i]);
                bool isKnown = Campaign.KnowsSpell(spell.Id);
                bool canLearn = Rules.CanLearn(Campaign, spell);
                string price = isKnown ? "KNOWN" : !canLearn ? "NEEDS WISDOM" : spell.Price + " GOLD";
                bool canBuy = Campaign.HasSpellBook && !isKnown && canLearn && spell.Price <= Campaign.Gold;
                row.Display(spell.Name.ToUpperInvariant() + " / " + MagicLabels.SpellCost(spell),
                    MagicLabels.SpellDescription(spell, Rules.SpellPower(Campaign)) + ".", price, Context.SpellIcon(spell.Id), Context.SpellTint(spell.Id), canBuy);
                string spellId = spell.Id;
                row.Bind(() => Session.BuySpell(spellId));
                row.BindInfo(() => Context.Info.ShowSpell(spellId));
            }

            UiBinding.SetButton(closeButton, "LEAVE", true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(bookRow, nameof(bookRow));
            Require(closeButton, nameof(closeButton));
            if (spellRows.Count == 0 || spellRows.Exists(row => row == null))
            {
                Require(null, nameof(spellRows));
            }
        }

        protected override void BindControls()
        {
            bookRow.Bind(Session.BuySpellBook);
            closeButton.Bind(Session.Back);
        }
    }
}
