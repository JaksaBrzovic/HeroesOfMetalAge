using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The hero screen: the hero on horseback, level, primary stats, mana, morale and luck, the eleven equipment places
    /// around the hero, the hero's army and, in two tabs on the right, the inventory or the secondary skills. A click on a
    /// carried artefact wears it, a click on a worn one takes it off; the spell book button opens the spell book.
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
        [Tooltip("Attack, defense, speed and morale, beside the painted stat icons.")]
        [SerializeField] private TMP_Text statsText;
        [Tooltip("Spell power, knowledge, mana and luck.")]
        [SerializeField] private TMP_Text magicText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private UiWidget backButton;
        [SerializeField] private UiWidget spellBookButton;

        [Header("Equipment")]
        [Tooltip("One slot per equipment place, in EquipmentPlace order: helm, left arm, right arm, gloves, torso, left ring, right ring, amulet, necklace, sash, boots.")]
        [SerializeField] private List<UiWidget> equipmentSlots = new List<UiWidget>();
        [Tooltip("The carried artefacts, in inventory order.")]
        [SerializeField] private List<UiWidget> inventorySlots = new List<UiWidget>();
        [SerializeField] private ArmySlotsView armySlots;

        [Header("Skills")]
        [SerializeField] private UiWidget inventoryTab;
        [SerializeField] private UiWidget skillsTab;
        [Tooltip("Covers the inventory while the skills tab is chosen.")]
        [SerializeField] private GameObject skillsPanel;
        [Tooltip("One row per skill the hero can know, in the order learned.")]
        [SerializeField] private List<UiWidget> skillRows = new List<UiWidget>();

        private bool isShowingSkills;

        public IReadOnlyList<UiWidget> EquipmentSlots => equipmentSlots;
        public IReadOnlyList<UiWidget> InventorySlots => inventorySlots;
        public IReadOnlyList<UiWidget> SkillRows => skillRows;
        public UiWidget SpellBookButton => spellBookButton;
        public bool IsShowingSkills => isShowingSkills;

        /// <summary>Shows the skills tab (true) or the inventory tab.</summary>
        public void ShowSkills(bool isShowing)
        {
            isShowingSkills = isShowing;
            Refresh();
        }

        public override void Refresh()
        {
            HeroDefinition hero = Catalog.Hero(Campaign.HeroId);
            ExperienceTable experience = Catalog.HeroPolicy.Experience;
            int level = Rules.Level(Campaign);
            long from = experience.Threshold(level);
            long to = experience.Threshold(level + 1);
            UiBinding.SetText(titleText, hero.Name);
            UiBinding.SetSprite(portraitImage, Context.HeroFullPortrait, Context.HeroTint);
            UiBinding.SetText(levelText, Catalog.ClassOf(hero.Id).Name.ToUpperInvariant() + " / LEVEL " + level + " / XP " + Campaign.Experience);
            UiBinding.SetProgress(experienceBarImage, to > from ? (Campaign.Experience - from) / (float)(to - from) : 1f);
            UiBinding.SetText(statsText, "ATTACK<pos=66%>" + Rules.Attack(Campaign) + "\nDEFENSE<pos=66%>" + Rules.Defense(Campaign)
                + "\nSPEED<pos=66%>+" + Rules.SpeedBonus(Campaign) + "\nMORALE<pos=66%>" + GameSession.MoraleText(Rules.Morale(Campaign)));
            UiBinding.SetText(magicText, "POWER<pos=70%>" + Rules.SpellPower(Campaign) + "\nKNOWLEDGE<pos=70%>" + Rules.Knowledge(Campaign)
                + "\nMANA<pos=70%>" + Campaign.Mana + "/" + Rules.MaximumMana(Campaign) + "\nLUCK<pos=70%>" + GameSession.MoraleText(Rules.Luck(Campaign)));
            UiBinding.SetButton(spellBookButton, Campaign.HasSpellBook ? "SPELL BOOK" : "NO BOOK", Campaign.HasSpellBook);
            // the chosen tab is framed in brackets; both stay clickable
            string skills = "SKILLS " + Campaign.Skills.Count + "/" + Catalog.HeroPolicy.MaximumSkills;
            UiBinding.SetButton(inventoryTab, isShowingSkills ? "INVENTORY" : "[ INVENTORY ]", true);
            UiBinding.SetButton(skillsTab, isShowingSkills ? "[ " + skills + " ]" : skills, true);
            UiBinding.SetVisible(skillsPanel, isShowingSkills);
            UiBinding.SetText(hintText, isShowingSkills ? "Skills grow as your hero gains levels." : InventoryHint());
            ShowEquipment();
            ShowInventory();
            ShowSkills();
            armySlots.Display(Campaign.IsHeroHome ? Campaign.Garrison : Campaign.Army, true);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(portraitImage, nameof(portraitImage));
            Require(levelText, nameof(levelText));
            Require(experienceBarImage, nameof(experienceBarImage));
            Require(statsText, nameof(statsText));
            Require(magicText, nameof(magicText));
            Require(hintText, nameof(hintText));
            Require(backButton, nameof(backButton));
            Require(spellBookButton, nameof(spellBookButton));
            Require(armySlots, nameof(armySlots));
            Require(inventoryTab, nameof(inventoryTab));
            Require(skillsTab, nameof(skillsTab));
            Require(skillsPanel, nameof(skillsPanel));
            if (equipmentSlots.Count != Equipment.PLACE_COUNT || equipmentSlots.Exists(slot => slot == null))
            {
                Require(null, nameof(equipmentSlots));
            }

            if (inventorySlots.Count == 0 || inventorySlots.Exists(slot => slot == null))
            {
                Require(null, nameof(inventorySlots));
            }

            if (skillRows.Count < Catalog.HeroPolicy.MaximumSkills || skillRows.Exists(row => row == null))
            {
                Require(null, nameof(skillRows));
            }
        }

        protected override void BindControls()
        {
            backButton.Bind(Session.Back);
            spellBookButton.Bind(Session.OpenSpellBook);
            inventoryTab.Bind(() => ShowSkills(false));
            skillsTab.Bind(() => ShowSkills(true));
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

        private string InventoryHint()
        {
            int hidden = Mathf.Max(0, Campaign.Inventory.Count - inventorySlots.Count);
            return Campaign.Inventory.Count == 0 ? "Nothing carried." : hidden > 0 ? "+" + hidden + " more carried" : "Click to wear";
        }

        private void ShowEquipment()
        {
            for (int i = 0; i < equipmentSlots.Count; i++)
            {
                string id = Campaign.Equipped((EquipmentPlace)i);
                ShowArtefact(equipmentSlots[i], id, PlaceNames[i]);
                BindArtefactInfo(equipmentSlots[i], id);
            }
        }

        private void ShowInventory()
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                string id = i < Campaign.Inventory.Count ? Campaign.Inventory[i] : null;
                ShowArtefact(inventorySlots[i], id, null);
                BindArtefactInfo(inventorySlots[i], id);
            }
        }

        /// <summary>A known skill shows its icon, level and what it does; the free slots stay empty.</summary>
        private void ShowSkills()
        {
            HeroPolicy policy = Catalog.HeroPolicy;
            for (int i = 0; i < skillRows.Count; i++)
            {
                UiWidget row = skillRows[i];
                bool isSlot = i < policy.MaximumSkills;
                UiBinding.SetVisible(row.gameObject, isSlot);
                if (!isSlot)
                {
                    continue;
                }

                if (i >= Campaign.Skills.Count)
                {
                    row.Display("FREE SLOT", string.Empty, string.Empty, null, Color.white, false);
                    row.BindInfo(null);
                    continue;
                }

                HeroSkill skill = Campaign.Skills[i];
                row.BindInfo(() => Context.Info.ShowSkill(skill.Skill, skill.Level));
                // a known skill stays lit (enabled, without an action); only the free slots are dimmed
                row.Display(MagicLabels.SkillTitle(policy, skill), MagicLabels.SkillEffect(policy, skill), string.Empty, Context.SkillIcon(skill.Skill),
                    Context.SkillTint(skill.Skill), true);
            }
        }

        private void BindArtefactInfo(UiWidget slot, string artefactId)
        {
            slot.BindInfo(artefactId == null ? (System.Action)null : () => Context.Info.ShowArtefact(artefactId));
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
