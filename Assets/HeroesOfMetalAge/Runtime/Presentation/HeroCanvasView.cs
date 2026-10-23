using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The hero screen: the hero on horseback, level and stats, the eleven equipment places around the hero, the inventory
    /// and the hero's army. A click on a carried artefact wears it, a click on a worn one takes it off.
    /// </summary>
    public sealed class HeroCanvasView : CanvasScreenView
    {
        private static readonly string[] PlaceNames =
        {
            "HELM", "LEFT ARM", "RIGHT ARM", "GLOVES", "TORSO", "LEFT RING", "RIGHT RING", "AMULET", "NECKLACE", "SASH", "BOOTS"
        };

        [Header("Hero")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image experienceBarImage;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private UiWidget backButton;

        [Header("Equipment")]
        [Tooltip("One slot per equipment place, in EquipmentPlace order: helm, left arm, right arm, gloves, torso, left ring, right ring, amulet, necklace, sash, boots.")]
        [SerializeField] private List<UiWidget> equipmentSlots = new List<UiWidget>();
        [Tooltip("The carried artefacts, in inventory order.")]
        [SerializeField] private List<UiWidget> inventorySlots = new List<UiWidget>();
        [SerializeField] private ArmySlotsView armySlots;

        public IReadOnlyList<UiWidget> EquipmentSlots => equipmentSlots;
        public IReadOnlyList<UiWidget> InventorySlots => inventorySlots;

        public override void Refresh()
        {
            HeroDefinition hero = Catalog.Hero(Campaign.HeroId);
            UiBinding.SetText(titleText, hero.Name);
            UiBinding.SetSprite(portraitImage, Context.HeroFullPortrait, Context.HeroTint);
            UiBinding.SetText(levelText, "LEVEL " + Rules.Level(Campaign) + " / XP " + Campaign.Experience);
            UiBinding.SetProgress(experienceBarImage, (Campaign.Experience % hero.ExperiencePerLevel) / (float)hero.ExperiencePerLevel);
            UiBinding.SetText(statsText, "ATTACK<pos=62%>" + Rules.Attack(Campaign) + "\nDEFENSE<pos=62%>" + Rules.Defense(Campaign)
                + "\nSPEED BONUS<pos=62%>+" + Rules.SpeedBonus(Campaign) + "\nMORALE<pos=62%>" + GameSession.MoraleText(Rules.Morale(Campaign)));
            int hidden = Mathf.Max(0, Campaign.Inventory.Count - inventorySlots.Count);
            UiBinding.SetText(hintText, Campaign.Inventory.Count == 0 ? "Nothing carried." : hidden > 0 ? "+" + hidden + " more carried" : "Click to wear");
            ShowEquipment();
            ShowInventory();
            armySlots.Display(Campaign.IsHeroHome ? Campaign.Garrison : Campaign.Army, true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(portraitImage, nameof(portraitImage));
            Require(levelText, nameof(levelText));
            Require(experienceBarImage, nameof(experienceBarImage));
            Require(statsText, nameof(statsText));
            Require(hintText, nameof(hintText));
            Require(backButton, nameof(backButton));
            Require(armySlots, nameof(armySlots));
            if (equipmentSlots.Count != Equipment.PLACE_COUNT || equipmentSlots.Exists(slot => slot == null))
            {
                Require(null, nameof(equipmentSlots));
            }

            if (inventorySlots.Count == 0 || inventorySlots.Exists(slot => slot == null))
            {
                Require(null, nameof(inventorySlots));
            }
        }

        protected override void BindControls()
        {
            backButton.Bind(Session.Back);
            for (int i = 0; i < equipmentSlots.Count; i++)
            {
                EquipmentPlace place = (EquipmentPlace)i;
                equipmentSlots[i].Bind(() => Session.Unequip(place));
            }

            for (int i = 0; i < inventorySlots.Count; i++)
            {
                int index = i;
                inventorySlots[i].Bind(() => Session.Equip(index));
            }

            armySlots.Initialize(Context);
        }

        private void ShowEquipment()
        {
            for (int i = 0; i < equipmentSlots.Count; i++)
            {
                string id = Campaign.Equipped((EquipmentPlace)i);
                ShowArtefact(equipmentSlots[i], id, PlaceNames[i]);
            }
        }

        private void ShowInventory()
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                ShowArtefact(inventorySlots[i], i < Campaign.Inventory.Count ? Campaign.Inventory[i] : null, null);
            }
        }

        /// <summary>A worn or carried artefact shows its icon on a plate that hides the empty slot's emblem.</summary>
        private void ShowArtefact(UiWidget slot, string artefactId, string placeName)
        {
            if (artefactId == null)
            {
                slot.Display(placeName, string.Empty, string.Empty, null, Color.white, false);
                slot.SetBackground(Color.clear);
                return;
            }

            ArtefactDefinition artefact = Catalog.Artefact(artefactId);
            slot.Display(placeName ?? artefact.Name, ArtefactLabels.Describe(artefact), string.Empty, Context.ArtefactIcon(artefactId),
                Context.ArtefactTint(artefactId), true);
            slot.SetBackground(Color.white);
        }
    }
}
