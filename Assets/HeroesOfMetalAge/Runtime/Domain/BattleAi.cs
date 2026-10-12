using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Enemy turn: attack the closest player stack (weakest, then lowest ID on ties) if possible, otherwise move as close
    /// to it as the reach allows. Proposals go through the same validation as player actions.
    /// </summary>
    public sealed class BattleAi
    {
        public BattleAction Choose(BattleEngine battle)
        {
            Combatant active = battle.Active;
            Combatant target = battle.Combatants
                .Where(combatant => combatant.IsAlive && combatant.IsPlayer != active.IsPlayer)
                .OrderBy(combatant => HexGrid.Distance(active.Position, combatant.Position))
                .ThenBy(combatant => combatant.TotalHealth)
                .ThenBy(combatant => combatant.Id)
                .FirstOrDefault();
            if (target == null)
            {
                return new BattleAction(BattleActionKind.Pass);
            }

            if (battle.CanAttack(target))
            {
                return new BattleAction(BattleActionKind.MoveOrAttack, target.Position);
            }

            HexPosition closest = battle.ReachableCells()
                .OrderBy(cell => HexGrid.Distance(cell, target.Position))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .First();
            return new BattleAction(BattleActionKind.MoveOrAttack, closest);
        }
    }
}
