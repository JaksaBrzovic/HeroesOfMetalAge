using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The town: its picture and actions, one recruitment row per recruitable creature, the chosen quantity and the home and
    /// visiting armies. Prices and availability come from the same rules the purchase command validates against.
    /// </summary>
    public sealed class TownCanvasView : CanvasScreenView
    {
        private const int MAX_QUANTITY = 99;

        [Header("Town panel")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Image townImage;
        [SerializeField] private TMP_Text statusTitleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private UiWidget claimButton;
        [SerializeField] private UiWidget backButton;

        [Header("Recruitment")]
        [Tooltip("One row per recruitable creature, in the mission's recruitment order.")]
        [SerializeField] private List<RecruitRowView> recruitRows = new List<RecruitRowView>();
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private UiWidget lessButton;
        [SerializeField] private UiWidget moreButton;
        [SerializeField] private UiWidget maxButton;

        [Header("Armies")]
        [SerializeField] private TMP_Text homeText;
        [SerializeField] private TMP_Text visitingText;
        [SerializeField] private UiWidget garrisonHeroButton;
        [SerializeField] private UiWidget stationButton;
        [SerializeField] private ArmySlotsView garrisonSlots;
        [SerializeField] private ArmySlotsView visitingSlots;

        private int quantity = 1;

        /// <summary>The garrison (home) hero's button, where the garrison hero speaks from.</summary>
        public RectTransform GarrisonHeroAnchor => garrisonHeroButton.Rect;

        private bool IsMaxQuantity => quantity == MAX_QUANTITY;

        public UiWidget RecruitmentCard(int index)
        {
            return recruitRows[index].RecruitButton;
        }

        public override void Refresh()
        {
            MissionDefinition mission = Catalog.Mission;
            string heroName = Catalog.Hero(Campaign.HeroId).Name;
            UiBinding.SetText(titleText, mission.Town.Name);
            UiBinding.SetText(subtitleText, Campaign.IsTownOwned
                ? "YOUR STRONGHOLD / PRODUCTION AT CLAN COST"
                : mission.ClanName.ToUpperInvariant() + " TERRITORY / RECRUITMENT MARKET");
            UiBinding.SetSprite(townImage, Context.TownIcon, Context.TownTint);
            UiBinding.SetText(statusTitleText, Campaign.IsTownOwned ? "THE FORGES ARE YOURS" : "THE FORGES ARE ALREADY BUILT");
            UiBinding.SetText(descriptionText, "Stocks accumulate every " + mission.DaysPerWeek + " days. Owned production costs "
                + Math.Round(mission.OwnedPriceMultiplier * 100) + "% of market prices.");
            UiBinding.SetButton(claimButton, Campaign.IsTownOwned ? "CLAN OATH HONOURED" : "CLAIM THE HOLD / FIGHT GARRISON",
                !Campaign.IsTownOwned && Campaign.Army.Count > 0);
            UiBinding.SetButton(backButton, "RETURN TO THE PASS", true);
            UiBinding.SetText(quantityText, IsMaxQuantity ? "MAX" : quantity.ToString());
            RefreshRecruitment();

            UiBinding.SetText(homeText, Campaign.IsTownOwned
                ? (Campaign.IsHeroHome ? "HOME / " + heroName : "HOME / YOUR GARRISON")
                : "HOME / " + mission.ClanName.ToUpperInvariant());
            UiBinding.SetText(visitingText, Campaign.IsHeroHome ? "VISITING / EMPTY" : "VISITING / " + heroName);
            UiBinding.SetButton(garrisonHeroButton, Campaign.IsTownOwned ? "HERO -> VISITING" : mission.ClanHeroName, Campaign.IsTownOwned && Campaign.IsHeroHome);
            UiBinding.SetButton(stationButton, Campaign.IsHeroHome ? "EMPTY" : "HERO -> HOME", Campaign.IsTownOwned && !Campaign.IsHeroHome);

            // stacks can be moved only between two armies the player owns, with the hero visiting
            bool canTransfer = Campaign.IsTownOwned && !Campaign.IsHeroHome;
            Action<int> homeAction = canTransfer ? index => Session.Transfer(true, index) : (Action<int>)null;
            Action<int> visitorAction = canTransfer ? index => Session.Transfer(false, index) : (Action<int>)null;
            IReadOnlyList<Troop> home = Campaign.IsTownOwned ? Campaign.Garrison : mission.Town.Army;
            garrisonSlots.Display(home, Campaign.IsTownOwned, homeAction);
            visitingSlots.Display(Campaign.Army, true, visitorAction);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(subtitleText, nameof(subtitleText));
            Require(townImage, nameof(townImage));
            Require(statusTitleText, nameof(statusTitleText));
            Require(descriptionText, nameof(descriptionText));
            Require(claimButton, nameof(claimButton));
            Require(backButton, nameof(backButton));
            Require(quantityText, nameof(quantityText));
            Require(lessButton, nameof(lessButton));
            Require(moreButton, nameof(moreButton));
            Require(maxButton, nameof(maxButton));
            Require(homeText, nameof(homeText));
            Require(visitingText, nameof(visitingText));
            Require(garrisonHeroButton, nameof(garrisonHeroButton));
            Require(stationButton, nameof(stationButton));
            Require(garrisonSlots, nameof(garrisonSlots));
            Require(visitingSlots, nameof(visitingSlots));
            if (recruitRows.Count < Catalog.Mission.RecruitmentIds.Count || recruitRows.Exists(row => row == null))
            {
                Require(null, nameof(recruitRows));
            }
        }

        protected override void BindControls()
        {
            claimButton.Bind(Session.ClaimTown);
            backButton.Bind(Session.LeaveTown);
            garrisonHeroButton.Bind(Session.StationHero);
            stationButton.Bind(Session.StationHero);
            lessButton.Bind(() => SetQuantity(Math.Max(1, quantity - 1)));
            moreButton.Bind(() => SetQuantity(Math.Min(MAX_QUANTITY, quantity + 1)));
            maxButton.Bind(() => SetQuantity(MAX_QUANTITY));
            garrisonSlots.Initialize(Context);
            visitingSlots.Initialize(Context);
            for (int i = 0; i < recruitRows.Count; i++)
            {
                bool isUsed = i < Catalog.Mission.RecruitmentIds.Count;
                UiBinding.SetVisible(recruitRows[i].gameObject, isUsed);
                if (isUsed)
                {
                    string unitId = Catalog.Mission.RecruitmentIds[i];
                    recruitRows[i].Initialize(() => Session.Buy(unitId, PurchaseCount(unitId)));
                }
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

        private void RefreshRecruitment()
        {
            IReadOnlyList<string> ids = Catalog.Mission.RecruitmentIds;
            for (int i = 0; i < ids.Count && i < recruitRows.Count; i++)
            {
                string unitId = ids[i];
                UnitDefinition unit = Catalog.Unit(unitId);
                int count = PurchaseCount(unitId);
                int price = Rules.Price(Campaign, unitId);
                long total = (long)price * count;
                bool isAvailable = !Campaign.IsHeroHome && count > 0 && total <= Campaign.Gold && Rules.CanAdd(Campaign.Army, unitId, count);
                string stats = "HP " + unit.Health + "   /   DMG " + unit.MinimumDamage + "-" + unit.MaximumDamage + "   /   STOCK " + Campaign.Stock(unitId);
                recruitRows[i].Display(unit.Name, unit.Description, stats, "RECRUIT " + count + "\n<size=80%>" + total + " GOLD</size>",
                    Context.UnitCard(unitId), Context.UnitTint(unitId), isAvailable);
            }
        }
    }
}
