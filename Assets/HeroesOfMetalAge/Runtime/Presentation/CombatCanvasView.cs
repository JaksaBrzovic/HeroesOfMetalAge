using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The battle screen's UI: the active stack panel, the battle log and the actions: Defend and Pass, the spell book and
    /// the surrender to an enemy hero. During the Tactics phase the two actions become Start Battle and Next Stack. The board
    /// itself is drawn in the world (<see cref="BattlefieldView"/>) inside <see cref="BattleViewport"/>.
    /// </summary>
    public sealed class CombatCanvasView : CanvasScreenView
    {
        [Header("Active stack panel")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text actorText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private UiWidget defendButton;
        [SerializeField] private UiWidget passButton;
        [SerializeField] private UiWidget spellBookButton;
        [SerializeField] private UiWidget surrenderButton;

        [Header("Board")]
        [Tooltip("Screen area where the world camera draws the battlefield.")]
        [SerializeField] private RectTransform battleViewport;

        public RectTransform BattleViewport => battleViewport;
        public UiWidget SpellBookButton => spellBookButton;
        public UiWidget SurrenderButton => surrenderButton;

        public override void Refresh()
        {
            BattleEngine battle = Session.Battle;
            if (battle == null)
            {
                return;
            }

            Combatant active = battle.Active;
            UnitDefinition unit = Catalog.Unit(active.UnitId);
            bool canAct = active.IsPlayer && !Session.IsBusy;
            string round = battle.IsTacticsPhase ? "TACTICS" : "ROUND " + battle.Round;
            UiBinding.SetText(titleText, Session.Encounter.Name);
            UiBinding.SetText(turnText, round + " / " + (active.IsPlayer ? "YOUR TURN" : "ENEMY TURN") + " / " + unit.Name);
            UiBinding.SetText(actorText, unit.Name + " / " + active.Count);
            string effects = string.Join(", ", active.Effects.Select(effect => Catalog.Spell(effect.SpellId).Name.ToUpperInvariant() + " " + effect.RoundsLeft));
            UiBinding.SetText(statsText, "HP " + active.TotalHealth + " / ATK " + battle.AttackOf(active) + " / DEF " + battle.DefenseOf(active)
                + " / SPEED " + battle.Speed(active)
                + "\nDAMAGE " + unit.MinimumDamage + "-" + unit.MaximumDamage + " PER CREATURE / " + (unit.IsRanged ? "RANGED" : "MELEE")
                + "\n" + (effects.Length > 0 ? effects : "NO SPELLS ON THE STACK")
                + "\nHERO MANA " + battle.Player.Mana + " / LUCK " + GameSession.MoraleText(battle.Player.Luck));
            UiBinding.SetSprite(portraitImage, Context.UnitCard(active.UnitId), Context.UnitTint(active.UnitId, active.IsPlayer));
            UiBinding.SetText(logText, string.Join("\n", Session.BattleLog));
            UiBinding.SetButton(defendButton, battle.IsTacticsPhase ? "START BATTLE" : "DEFEND", canAct);
            UiBinding.SetButton(passButton, battle.IsTacticsPhase ? "NEXT STACK" : "PASS TURN", canAct);
            bool canCast = canAct && !battle.IsTacticsPhase && battle.Player.HasSpellBook && !battle.Player.HasCastThisRound;
            string spellCaption = !battle.Player.HasSpellBook ? "NO SPELL BOOK"
                : Session.TargetingSpellId != null ? "AIM " + Catalog.Spell(Session.TargetingSpellId).Name.ToUpperInvariant()
                : battle.Player.HasCastThisRound ? "SPELL CAST" : "SPELL BOOK [C]";
            UiBinding.SetButton(spellBookButton, spellCaption, canCast);
            bool hasCommander = Session.Encounter.Commander != null;
            UiBinding.SetButton(surrenderButton, hasCommander ? "SURRENDER " + Rules.SurrenderPrice(Campaign, battle.Survivors()) + " GOLD" : "NO SURRENDER",
                canAct && hasCommander);
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(turnText, nameof(turnText));
            Require(actorText, nameof(actorText));
            Require(statsText, nameof(statsText));
            Require(portraitImage, nameof(portraitImage));
            Require(logText, nameof(logText));
            Require(defendButton, nameof(defendButton));
            Require(passButton, nameof(passButton));
            Require(spellBookButton, nameof(spellBookButton));
            Require(surrenderButton, nameof(surrenderButton));
            Require(battleViewport, nameof(battleViewport));
        }

        protected override void BindControls()
        {
            defendButton.Bind(HandleDefend);
            passButton.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.Pass)));
            spellBookButton.Bind(Session.OpenSpellBook);
            surrenderButton.Bind(Session.Surrender);
        }

        // during the Tactics phase the defend button starts the battle
        private void HandleDefend()
        {
            bool isTactics = Session.Battle != null && Session.Battle.IsTacticsPhase;
            Session.BattleAction(new BattleAction(isTactics ? BattleActionKind.EndTactics : BattleActionKind.Defend));
        }
    }
}
