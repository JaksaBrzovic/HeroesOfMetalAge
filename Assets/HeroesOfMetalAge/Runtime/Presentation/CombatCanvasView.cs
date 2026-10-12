using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The battle screen: the hex board, stack markers and the active stack panel. Clicking a hex or an enemy sends a
    /// move-or-attack action; the board only shows the result.
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
        [SerializeField] private UiWidget hexPrefab;
        [SerializeField] private RectTransform board;
        [Tooltip("Distance between neighbouring hex centres.")]
        [SerializeField] private Vector2 cellStride = new Vector2(96, 72);
        [Tooltip("Horizontal shift of odd rows.")]
        [SerializeField] private float rowOffset = 48;

        [Header("Units")]
        [SerializeField] private UiWidget unitPrefab;
        [SerializeField] private RectTransform playerUnits;
        [SerializeField] private RectTransform enemyUnits;
        [Tooltip("Markers saved in the prefab for previewing; reused for the first stacks of each battle.")]
        [SerializeField] private UiWidget[] authoredUnitMarkers;

        private readonly Dictionary<int, UiWidget> unitMarkers = new Dictionary<int, UiWidget>();
        private UiWidget[,] cells;
        private BattleEngine displayedBattle;

        public UiWidget UnitMarker(int combatantId)
        {
            return unitMarkers[combatantId];
        }

        public UiWidget Cell(HexPosition position)
        {
            return cells[position.X, position.Y];
        }

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

            if (displayedBattle != battle)
            {
                AssignUnitMarkers(battle);
            }

            RefreshCells(battle, active, canAct);
            RefreshUnitMarkers(battle, canAct);
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
            Require(hexPrefab, nameof(hexPrefab));
            Require(board, nameof(board));
            Require(unitPrefab, nameof(unitPrefab));
            Require(playerUnits, nameof(playerUnits));
            Require(enemyUnits, nameof(enemyUnits));
        }

        protected override void BindControls()
        {
            if (authoredUnitMarkers != null)
            {
                for (int i = 0; i < authoredUnitMarkers.Length; i++)
                {
                    unitMarkers.Add(i, authoredUnitMarkers[i]);
                }
            }

            defendButton.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.Defend)));
            passButton.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.Pass)));

            CombatPolicy policy = Catalog.Mission.Combat;
            cells = new UiWidget[policy.Width, policy.Height];
            for (int y = 0; y < policy.Height; y++)
            {
                for (int x = 0; x < policy.Width; x++)
                {
                    HexPosition position = new HexPosition(x, y);
                    UiWidget cell = Instantiate(hexPrefab, board);
                    cell.name = "Hex " + x + "," + y;
                    cell.Rect.anchoredPosition = HexOrigin(position);
                    cell.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.MoveOrAttack, position)));
                    cells[x, y] = cell;
                }
            }
        }

        /// <summary>A new battle reuses the markers by combatant ID; clicking a marker attacks that stack's current hex.</summary>
        private void AssignUnitMarkers(BattleEngine battle)
        {
            foreach (UiWidget previous in unitMarkers.Values)
            {
                previous.Unbind();
                previous.gameObject.SetActive(false);
            }

            foreach (Combatant combatant in battle.Combatants)
            {
                RectTransform parent = combatant.IsPlayer ? playerUnits : enemyUnits;
                if (!unitMarkers.TryGetValue(combatant.Id, out UiWidget marker))
                {
                    marker = Instantiate(unitPrefab, parent);
                    unitMarkers.Add(combatant.Id, marker);
                }

                marker.transform.SetParent(parent, false);
                int id = combatant.Id;
                marker.Bind(() => Session.BattleAction(new BattleAction(BattleActionKind.MoveOrAttack, Session.Battle.Combatants[id].Position)));
            }

            displayedBattle = battle;
        }

        private void RefreshCells(BattleEngine battle, Combatant active, bool canAct)
        {
            for (int y = 0; y < battle.Policy.Height; y++)
            {
                for (int x = 0; x < battle.Policy.Width; x++)
                {
                    HexPosition position = new HexPosition(x, y);
                    UiWidget cell = cells[x, y];
                    cell.SetBackground(HexColor(battle, active, position));
                    cell.Display(string.Empty, string.Empty, string.Empty, null, Color.white, canAct);
                }
            }
        }

        private Color HexColor(BattleEngine battle, Combatant active, HexPosition position)
        {
            if (battle.At(position) == active)
            {
                return Context.Theme.ActiveHexColor;
            }

            bool isReachable = active.IsPlayer && battle.IsReachable(position);
            return isReachable ? Context.Theme.ReachableHexColor : Context.Theme.HexColor;
        }

        private void RefreshUnitMarkers(BattleEngine battle, bool canAct)
        {
            foreach (Combatant combatant in battle.Combatants)
            {
                UiWidget marker = unitMarkers[combatant.Id];
                UiBinding.SetVisible(marker.gameObject, combatant.IsAlive);
                if (!combatant.IsAlive)
                {
                    continue;
                }

                marker.name = (combatant.IsPlayer ? "Player " : "Enemy ") + Catalog.Unit(combatant.UnitId).Name;
                marker.Rect.anchoredPosition = HexOrigin(combatant.Position);
                marker.Display(string.Empty, string.Empty, combatant.Count.ToString(), Context.UnitIcon(combatant.UnitId),
                    Context.UnitTint(combatant.UnitId, combatant.IsPlayer), canAct);
            }
        }

        private Vector2 HexOrigin(HexPosition position)
        {
            return new Vector2(position.X * cellStride.x + (position.Y % 2) * rowOffset, -position.Y * cellStride.y);
        }
    }
}
