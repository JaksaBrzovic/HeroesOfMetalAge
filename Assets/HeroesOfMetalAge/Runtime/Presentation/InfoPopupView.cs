using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The info card shown while the right mouse button is held over a unit (on the battlefield, in an army slot, an army on
    /// the map), a spell, a skill or an artefact, in the manner of the classic hero games: the picture, the numbers and a
    /// line of lore. It never takes clicks; <see cref="GameHud"/> hides it when the button is released.
    /// </summary>
    public sealed class InfoPopupView : MonoBehaviour
    {
        private const float SCREEN_MARGIN = 16f;

        [SerializeField] private RectTransform panel;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text loreText;

        private CanvasViewContext context;

        public bool IsShowing => panel.gameObject.activeSelf;
        public string Title => titleText.text;
        /// <summary>The card itself, to check where it opened.</summary>
        public RectTransform Card => panel;

        private GameSession Session => context.Session;
        private DefinitionCatalog Catalog => Session.Catalog;
        private CampaignRules Rules => Session.Rules;

        /// <exception cref="InvalidOperationException">A reference is missing on the prefab.</exception>
        public void Initialize(CanvasViewContext viewContext)
        {
            if (panel == null || iconImage == null || titleText == null || subtitleText == null || statsText == null || bodyText == null || loreText == null)
            {
                throw new InvalidOperationException($"InfoPopupView on '{name}' has unassigned references. Assign them in the InfoPopup prefab.");
            }

            context = viewContext;
            Hide();
        }

        public void Hide()
        {
            UiBinding.SetVisible(panel.gameObject, false);
        }

        /// <summary>A stack on the battlefield: its numbers with its hero's and spells', the health of its top creature, its spells.</summary>
        /// <param name="screenPoint">Where the card is centred, in screen pixels; null for the pointer.</param>
        public void ShowCombatant(BattleEngine battle, Combatant combatant, Vector2? screenPoint = null)
        {
            UnitDefinition unit = Catalog.Unit(combatant.UnitId);
            BattleSide side = battle.Side(combatant.IsPlayer);
            int topHealth = combatant.TotalHealth - (combatant.Count - 1) * combatant.HealthPerUnit;
            string effects = combatant.Effects.Count == 0 ? "No spells on the stack."
                : "Spells: " + string.Join(", ", combatant.Effects.Select(effect => Catalog.Spell(effect.SpellId).Name + " (" + Rounds(effect.RoundsLeft) + ")")) + ".";
            string stats = Stats(unit, battle.AttackOf(combatant), battle.DefenseOf(combatant), battle.Speed(combatant))
                + "\nHEALTH LEFT<pos=55%>" + topHealth + " / " + unit.Health
                + "\nMORALE<pos=55%>" + GameSession.MoraleText(side.Morale) + "\nLUCK<pos=55%>" + GameSession.MoraleText(side.Luck);
            Show(context.UnitCard(unit.Id), context.UnitTint(unit.Id, combatant.IsPlayer), unit.Name, combatant.Count + " / " + (combatant.IsPlayer ? "YOURS" : "ENEMY"),
                stats, Abilities(unit) + "\n" + effects, unit.Lore, screenPoint);
        }

        /// <summary>A stack in an army: the player's numbers include the hero's attack and defense.</summary>
        public void ShowTroop(Troop troop, bool isPlayer)
        {
            UnitDefinition unit = Catalog.Unit(troop.UnitId);
            CombatPolicy combat = Catalog.Mission.Combat;
            int attack = unit.Attack + (isPlayer ? Rules.Attack(Session.Campaign) : combat.EnemyAttack);
            int defense = unit.Defense + (isPlayer ? Rules.Defense(Session.Campaign) : combat.EnemyDefense);
            int speed = unit.Speed + (isPlayer ? Rules.SpeedBonus(Session.Campaign) : 0);
            string stats = Stats(unit, attack, defense, speed) + "\nCOST<pos=55%>" + unit.Cost + " GOLD\nGROWTH<pos=55%>" + unit.WeeklyGrowth + " / WEEK";
            Show(context.UnitCard(unit.Id), context.UnitTint(unit.Id, isPlayer), unit.Name, troop.Count + " / " + (isPlayer ? "YOURS" : "ENEMY"), stats,
                Abilities(unit) + "\n" + unit.Description, unit.Lore);
        }

        /// <summary>An army on the adventure map: its stacks, its commander and what beating it brings.</summary>
        public void ShowEncounter(EncounterDefinition encounter, Vector2? screenPoint = null)
        {
            string army = string.Join("\n", encounter.Army.Select(troop => troop.Count + " " + Catalog.Unit(troop.UnitId).Name));
            string commander = encounter.Commander != null ? "Led by " + encounter.Commander.Name + ", who wields magic." : "No hero leads them.";
            string reward = encounter.IsHostile || encounter.Kind == EncounterKind.Town ? "Victory: " + encounter.RewardExperience + " XP, about " + encounter.RewardGold + " gold."
                : "They would join you.";
            Troop first = encounter.Army[0];
            Show(context.UnitCard(first.UnitId), context.UnitTint(first.UnitId, !encounter.IsHostile), encounter.Name,
                encounter.IsHostile ? "HOSTILE ARMY" : encounter.Kind == EncounterKind.Town ? "TOWN GARRISON" : "FRIENDLY ARMY", army, commander + "\n" + reward,
                encounter.Description, screenPoint);
        }

        /// <summary>A spell: its cost, what it does at the hero's spell power, whether it can be learned and cast now.</summary>
        public void ShowSpell(string spellId)
        {
            SpellDefinition spell = Catalog.Spell(spellId);
            CampaignState campaign = Session.Campaign;
            BattleEngine battle = Session.Screen == GameScreen.Battle ? Session.Battle : null;
            int power = battle != null ? battle.Player.SpellPower : Rules.SpellPower(campaign);
            int mana = battle != null ? battle.Player.Mana : campaign.Mana;
            string duration = spell.IsLasting ? Rounds(spell.Duration(power)) : "at once";
            string stats = "LEVEL<pos=55%>" + spell.Level + "\nMANA COST<pos=55%>" + spell.ManaCost + " (YOU HAVE " + mana + ")\nSPELL POWER<pos=55%>" + power
                + "\nDURATION<pos=55%>" + duration + "\nPRICE<pos=55%>" + spell.Price + " GOLD";
            string state = !campaign.KnowsSpell(spell.Id) ? (Rules.CanLearn(campaign, spell) ? "Not in your spell book yet." : "Needs more Wisdom to be learned.")
                : battle == null ? "In your spell book. Spells are cast in battle."
                : battle.CanCast(true, spell.Id, out string reason) ? "Ready to cast." : reason;
            Show(context.SpellIcon(spell.Id), context.SpellTint(spell.Id), spell.Name, "BATTLE SPELL", stats,
                Capitalized(MagicLabels.SpellDescription(spell, power)) + ".\n" + state, spell.Description);
        }

        /// <summary>A secondary skill: what it does at each level, the hero's level marked.</summary>
        public void ShowSkill(SecondarySkill skill, SkillLevel known)
        {
            HeroPolicy policy = Catalog.HeroPolicy;
            SkillDefinition definition = policy.Skill(skill);
            List<string> levels = new List<string>();
            foreach (SkillLevel level in new[] { SkillLevel.Basic, SkillLevel.Advanced, SkillLevel.Expert })
            {
                string line = level.ToString().ToUpperInvariant() + "<pos=38%>" + MagicLabels.SkillEffect(policy, new HeroSkill(skill, level));
                levels.Add(level == known ? "<color=#F0C878>" + line + "</color>" : line);
            }

            Show(context.SkillIcon(skill), context.SkillTint(skill), definition.Name, known == SkillLevel.None ? "NOT KNOWN" : known.ToString().ToUpperInvariant(),
                string.Join("\n", levels), string.Empty, definition.Description);
        }

        public void ShowArtefact(string artefactId)
        {
            ArtefactDefinition artefact = Catalog.Artefact(artefactId);
            Show(context.ArtefactIcon(artefactId), context.ArtefactTint(artefactId), artefact.Name, artefact.Rarity.ToString().ToUpperInvariant() + " " + artefact.Slot.ToString().ToUpperInvariant(),
                ArtefactLabels.Bonus(artefact).Replace(" / ", "\n"), string.Empty, artefact.Description);
        }

        private void Show(Sprite icon, Color tint, string title, string subtitle, string stats, string body, string lore, Vector2? screenPoint = null)
        {
            UiBinding.SetSprite(iconImage, icon, tint);
            UiBinding.SetText(titleText, title);
            UiBinding.SetText(subtitleText, subtitle);
            UiBinding.SetText(statsText, stats);
            UiBinding.SetText(bodyText, body);
            UiBinding.SetText(loreText, string.IsNullOrEmpty(lore) ? string.Empty : "<i>" + lore + "</i>");
            PlaceAt(screenPoint ?? (Vector2)Input.mousePosition);
            UiBinding.SetVisible(panel.gameObject, true);
        }

        /// <summary>Centres the card on a screen point, moved only as far as needed to stay on the screen.</summary>
        private void PlaceAt(Vector2 screenPoint)
        {
            RectTransform frame = (RectTransform)transform;
            Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screenPoint, eventCamera, out Vector2 local))
            {
                return;
            }

            // the card hangs from its top left corner, which is anchored to the frame's top left corner
            Rect area = frame.rect;
            Vector2 size = panel.rect.size;
            float x = local.x - area.xMin - size.x * 0.5f;
            float y = local.y - area.yMax + size.y * 0.5f;
            x = Mathf.Clamp(x, SCREEN_MARGIN, Mathf.Max(SCREEN_MARGIN, area.width - size.x - SCREEN_MARGIN));
            y = Mathf.Clamp(y, Mathf.Min(-SCREEN_MARGIN, -(area.height - size.y - SCREEN_MARGIN)), -SCREEN_MARGIN);
            Vector2 position = new Vector2(x, y);
            if (panel.anchoredPosition != position)
            {
                panel.anchoredPosition = position;
            }
        }

        private static string Stats(UnitDefinition unit, int attack, int defense, int speed)
        {
            return "ATTACK<pos=55%>" + unit.Attack + (attack != unit.Attack ? " (" + attack + ")" : string.Empty)
                + "\nDEFENSE<pos=55%>" + unit.Defense + (defense != unit.Defense ? " (" + defense + ")" : string.Empty)
                + "\nDAMAGE<pos=55%>" + unit.MinimumDamage + "-" + unit.MaximumDamage
                + "\nHEALTH<pos=55%>" + unit.Health
                + "\nSPEED<pos=55%>" + unit.Speed + (speed != unit.Speed ? " (" + speed + ")" : string.Empty);
        }

        private static string Abilities(UnitDefinition unit)
        {
            return unit.IsRanged ? "Shoots from afar without retaliation; half damage in melee." : "Melee fighter; strikes back once per round.";
        }

        private static string Rounds(int rounds)
        {
            return rounds + (rounds == 1 ? " round" : " rounds");
        }

        private static string Capitalized(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
