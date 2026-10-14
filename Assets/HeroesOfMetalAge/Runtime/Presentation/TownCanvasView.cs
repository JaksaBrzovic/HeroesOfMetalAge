using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The town: recruitment cards, the chosen quantity, the claim button and the home/visiting armies. Prices and
    /// availability come from the same rules the purchase command validates against.
    /// </summary>
    public sealed class TownCanvasView : CanvasScreenView
    {
        private const int MAX_QUANTITY = 99;

        [Header("Town panel")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text ownerText;
        [SerializeField] private Image townImage;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private UiWidget claimButton;
        [SerializeField] private UiWidget backButton;

        [Header("Recruitment")]
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private UiWidget minusButton;
        [SerializeField] private UiWidget plusButton;
        [SerializeField] private UiWidget maxButton;
        [SerializeField] private UiWidget recruitmentPrefab;
        [SerializeField] private RectTransform recruitmentContent;

        [Header("Armies")]
        [SerializeField] private TMP_Text homeText;
        [SerializeField] private TMP_Text visitingText;
        [SerializeField] private UiWidget homeHeroButton;
        [SerializeField] private UiWidget visitingHeroButton;
        [SerializeField] private ArmySlotsView homeArmy;
        [SerializeField] private ArmySlotsView visitingArmy;

        private readonly List<UiWidget> recruitmentCards = new List<UiWidget>();
        private int quantity = 1;

        /// <summary>The garrison (home) hero's slot, where the garrison hero speaks from.</summary>
        public RectTransform GarrisonHeroAnchor => homeHeroButton.Rect;

        private bool IsMaxQuantity => quantity == MAX_QUANTITY;

        public UiWidget RecruitmentCard(int index)
        {
            return recruitmentCards[index];
        }

        public override void Refresh()
        {
            MissionDefinition mission = Catalog.Mission;
            string heroName = Catalog.Hero(Campaign.HeroId).Name;
            UiBinding.SetText(titleText, mission.Encounter(EncounterKind.Town).Name);
            UiBinding.SetText(ownerText, Campaign.IsTownOwned
                ? "YOUR STRONGHOLD / PRODUCTION AT CLAN COST"
                : mission.ClanName.ToUpperInvariant() + " TERRITORY / RECRUITMENT MARKET");
            UiBinding.SetSprite(townImage, Context.TownIcon, Context.TownTint);
            UiBinding.SetText(descriptionText, "The forges are operational. Stocks accumulate every " + mission.DaysPerWeek
                + " days. Owned production costs " + Math.Round(mission.OwnedPriceMultiplier * 100) + "% of market prices.");
            UiBinding.SetButton(claimButton, Campaign.IsTownOwned ? "CLAN OATH HONOURED" : "CLAIM THE HOLD / FIGHT GARRISON",
                !Campaign.IsTownOwned && Campaign.Army.Count > 0);
            UiBinding.SetText(quantityText, IsMaxQuantity ? "MAX" : quantity.ToString());
            RefreshRecruitmentCards();

            UiBinding.SetText(homeText, Campaign.IsTownOwned
                ? (Campaign.IsHeroHome ? "HOME / " + heroName : "HOME / YOUR GARRISON")
                : "HOME / " + mission.ClanName);
            UiBinding.SetText(visitingText, Campaign.IsHeroHome ? "VISITING / EMPTY" : "VISITING / " + heroName);
            UiBinding.SetButton(homeHeroButton, Campaign.IsTownOwned ? "HERO -> VISITING" : mission.ClanHeroName, Campaign.IsTownOwned && Campaign.IsHeroHome);
            UiBinding.SetButton(visitingHeroButton, Campaign.IsHeroHome ? "EMPTY" : "HERO -> HOME", Campaign.IsTownOwned && !Campaign.IsHeroHome);

            // stacks can be moved only between two armies the player owns, with the hero visiting
            bool canTransfer = Campaign.IsTownOwned && !Campaign.IsHeroHome;
            Action<int> homeAction = canTransfer ? index => Session.Transfer(true, index) : (Action<int>)null;
            Action<int> visitorAction = canTransfer ? index => Session.Transfer(false, index) : (Action<int>)null;
            IReadOnlyList<Troop> home = Campaign.IsTownOwned ? Campaign.Garrison : mission.Encounter(EncounterKind.Town).Army;
            homeArmy.Display(home, Campaign.IsTownOwned, homeAction);
            visitingArmy.Display(Campaign.Army, true, visitorAction);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(ownerText, nameof(ownerText));
            Require(townImage, nameof(townImage));
            Require(descriptionText, nameof(descriptionText));
            Require(claimButton, nameof(claimButton));
            Require(backButton, nameof(backButton));
            Require(quantityText, nameof(quantityText));
            Require(minusButton, nameof(minusButton));
            Require(plusButton, nameof(plusButton));
            Require(maxButton, nameof(maxButton));
            Require(recruitmentPrefab, nameof(recruitmentPrefab));
            Require(recruitmentContent, nameof(recruitmentContent));
            Require(homeText, nameof(homeText));
            Require(visitingText, nameof(visitingText));
            Require(homeHeroButton, nameof(homeHeroButton));
            Require(visitingHeroButton, nameof(visitingHeroButton));
            Require(homeArmy, nameof(homeArmy));
            Require(visitingArmy, nameof(visitingArmy));
        }

        protected override void BindControls()
        {
            claimButton.Bind(Session.ClaimTown);
            backButton.Bind(Session.LeaveTown);
            homeHeroButton.Bind(Session.StationHero);
            visitingHeroButton.Bind(Session.StationHero);
            minusButton.Bind(() => SetQuantity(Math.Max(1, quantity - 1)));
            plusButton.Bind(() => SetQuantity(Math.Min(MAX_QUANTITY, quantity + 1)));
            maxButton.Bind(() => SetQuantity(MAX_QUANTITY));
            homeArmy.Initialize(Context);
            visitingArmy.Initialize(Context);

            foreach (string id in Catalog.Mission.RecruitmentIds)
            {
                string unitId = id;
                UiWidget card = Instantiate(recruitmentPrefab, recruitmentContent);
                card.Bind(() => Session.Buy(unitId, PurchaseCount(unitId)));
                recruitmentCards.Add(card);
            }
        }

        private void SetQuantity(int value)
        {
            quantity = value;
            Refresh();
        }

        /// <summary>The chosen quantity limited by stock; MAX also stops at what the gold can buy.</summary>
        private int PurchaseCount(string unitId)
        {
            int count = Math.Min(quantity, Campaign.Stock(unitId));
            return IsMaxQuantity ? Math.Min(count, Campaign.Gold / Rules.Price(Campaign, unitId)) : count;
        }

        private void RefreshRecruitmentCards()
        {
            for (int i = 0; i < recruitmentCards.Count; i++)
            {
                string unitId = Catalog.Mission.RecruitmentIds[i];
                UnitDefinition unit = Catalog.Unit(unitId);
                int count = PurchaseCount(unitId);
                int price = Rules.Price(Campaign, unitId);
                long total = (long)price * count;
                bool isAvailable = !Campaign.IsHeroHome && count > 0 && total <= Campaign.Gold && Rules.CanAdd(Campaign.Army, unitId, count);
                string details = unit.Description + " / HP " + unit.Health + " / DMG " + unit.MinimumDamage + "-" + unit.MaximumDamage
                    + "\nSTOCK " + Campaign.Stock(unitId) + " / " + price + " GOLD EACH";
                recruitmentCards[i].Display(unit.Name, details, "RECRUIT " + count + " / " + total + " GOLD", Context.UnitIcon(unitId),
                    Context.UnitTint(unitId), isAvailable);
            }
        }
    }
}
