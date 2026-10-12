using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The battle screen's UI: the active stack panel, the battle log and the Defend and Pass actions. The board itself is
    /// drawn in the world (<see cref="BattlefieldView"/>) inside <see cref="BattleViewport"/>.
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

        [Header("Board")]
        [Tooltip("Screen area where the world camera draws the battlefield.")]
        [SerializeField] private RectTransform battleViewport;

        public RectTransform BattleViewport => battleViewport;

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
            UiBinding.SetText(titleText, Session.Encounter.Name);
            UiBinding.SetText(turnText, "ROUND " + battle.Round + " / " + (active.IsPlayer ? "YOUR TURN" : "ENEMY TURN") + " / " + unit.Name);
            UiBinding.SetText(actorText, unit.Name + " / " + active.Count);
            UiBinding.SetText(statsText, "HP " + active.TotalHealth + " / SPEED " + battle.Speed(active)
                + "\nDAMAGE " + unit.MinimumDamage + "-" + unit.MaximumDamage + " PER CREATURE\n"
                + (unit.IsRanged ? "RANGED ATTACK" : "MELEE / ONE RETALIATION PER ROUND"));
            UiBinding.SetSprite(portraitImage, Context.UnitIcon(active.UnitId), Context.UnitTint(active.UnitId, active.IsPlayer));
            UiBinding.SetText(logText, string.Join("\n", Session.BattleLog));
            UiBinding.SetButton(defendButton, "DEFEND", canAct);
            UiBinding.SetButton(passButton, "PASS TURN", canAct);
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
            Require(battleViewport, nameof(battleViewport));
        }

        protected override void BindControls()
        {
            defendButton.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.Defend)));
            passButton.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.Pass)));
        }
    }
}
