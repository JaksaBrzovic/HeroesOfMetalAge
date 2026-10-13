using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>The hero screen: stats, level progress, equipped artefacts, the inventory and the hero's army.</summary>
    public sealed class HeroCanvasView : CanvasScreenView
    {
        [Header("Hero")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image experienceBarImage;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text progressionText;
        [SerializeField] private UiWidget backButton;

        [Header("Equipment")]
        [SerializeField] private UiWidget weaponSlot;
        [SerializeField] private UiWidget charmSlot;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private UiWidget artefactPrefab;
        [SerializeField] private RectTransform inventoryContent;
        [SerializeField] private ArmySlotsView armySlots;

        private readonly List<UiWidget> inventoryItems = new List<UiWidget>();

        public override void Refresh()
        {
            HeroDefinition hero = Catalog.Hero(Campaign.HeroId);
            UiBinding.SetText(titleText, hero.Name);
            UiBinding.SetText(descriptionText, hero.Description);
            UiBinding.SetSprite(portraitImage, Context.HeroIcon, Context.HeroTint);
            UiBinding.SetText(levelText, "LEVEL " + Rules.Level(Campaign) + " / XP " + Campaign.Experience);
            UiBinding.SetProgress(experienceBarImage, (Campaign.Experience % hero.ExperiencePerLevel) / (float)hero.ExperiencePerLevel);
            UiBinding.SetText(statsText, "ATTACK     " + Rules.Attack(Campaign) + "\nDEFENSE   " + Rules.Defense(Campaign)
                + "\nSPEED BONUS   +" + Rules.SpeedBonus(Campaign));
            UiBinding.SetText(progressionText, "Every " + hero.ExperiencePerLevel + " XP grants a level. Attack improves by " + hero.AttackPerLevel
                + "; defense every " + hero.DefenseLevelInterval + " levels.");
            ShowEquipment(weaponSlot, Campaign.WeaponId, "WEAPON");
            ShowEquipment(charmSlot, Campaign.CharmId, "CHARM");
            UiBinding.SetText(emptyText, Campaign.Inventory.Count == 0 ? "No unequipped artefacts carried." : "Select an artefact to equip it.");
            RefreshInventory();
            armySlots.Display(Campaign.IsHeroHome ? Campaign.Garrison : Campaign.Army, true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(descriptionText, nameof(descriptionText));
            Require(portraitImage, nameof(portraitImage));
            Require(levelText, nameof(levelText));
            Require(experienceBarImage, nameof(experienceBarImage));
            Require(statsText, nameof(statsText));
            Require(progressionText, nameof(progressionText));
            Require(backButton, nameof(backButton));
            Require(weaponSlot, nameof(weaponSlot));
            Require(charmSlot, nameof(charmSlot));
            Require(emptyText, nameof(emptyText));
            Require(artefactPrefab, nameof(artefactPrefab));
            Require(inventoryContent, nameof(inventoryContent));
            Require(armySlots, nameof(armySlots));
        }

        protected override void BindControls()
        {
            backButton.Bind(Session.Back);
            weaponSlot.Bind(() => Session.Unequip(EquipmentSlot.Weapon));
            charmSlot.Bind(() => Session.Unequip(EquipmentSlot.Charm));
            armySlots.Initialize(Context);
        }

        /// <summary>Reuses the instantiated cards; extra cards are hidden and every card is rebound to its current index.</summary>
        private void RefreshInventory()
        {
            while (inventoryItems.Count < Campaign.Inventory.Count)
            {
                inventoryItems.Add(Instantiate(artefactPrefab, inventoryContent));
            }

            for (int i = 0; i < inventoryItems.Count; i++)
            {
                UiWidget item = inventoryItems[i];
                item.Unbind();
                bool isVisible = i < Campaign.Inventory.Count;
                UiBinding.SetVisible(item.gameObject, isVisible);
                if (!isVisible)
                {
                    continue;
                }

                int index = i;
                string id = Campaign.Inventory[i];
                ArtefactDefinition artefact = Catalog.Artefact(id);
                item.Display(artefact.Name, ArtefactLabels.Describe(artefact), string.Empty, Context.ArtefactIcon(id), Context.ArtefactTint(id), true);
                item.Bind(() => Session.Equip(index));
            }
        }

        private void ShowEquipment(UiWidget slot, string artefactId, string slotName)
        {
            if (string.IsNullOrEmpty(artefactId))
            {
                slot.Display(slotName, "Empty", string.Empty, null, Color.white, false);
                return;
            }

            ArtefactDefinition artefact = Catalog.Artefact(artefactId);
            slot.Display(slotName + " / " + artefact.Name, ArtefactLabels.Describe(artefact), string.Empty, Context.ArtefactIcon(artefactId),
                Context.ArtefactTint(artefactId), true);
        }
    }
}
