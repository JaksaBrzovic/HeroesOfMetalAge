using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Enemy turn: a commander first casts the first spell of its list that it can cast and that would do something, then the
    /// stack attacks the closest player stack (weakest, then lowest ID on ties) if possible, otherwise moves as close to it as
    /// the reach allows. Proposals go through the same validation as player actions.
    /// </summary>
    public sealed class BattleAi
    {
        private readonly DefinitionCatalog catalog;

        public BattleAi(DefinitionCatalog catalog)
        {
            this.catalog = catalog;
        }

        public BattleAction Choose(BattleEngine battle)
        {
            Combatant active = battle.Active;
            BattleAction? spell = ChooseSpell(battle, active.IsPlayer);
            if (spell.HasValue)
            {
                return spell.Value;
            }

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

        private BattleAction? ChooseSpell(BattleEngine battle, bool isPlayer)
        {
            BattleSide side = battle.Side(isPlayer);
            foreach (string spellId in side.Spells)
            {
                if (!battle.CanCast(isPlayer, spellId, out _))
                {
                    continue;
                }

                SpellDefinition spell = catalog.Spell(spellId);
                Combatant target = Target(battle, spell, isPlayer);
                if (target != null)
                {
                    return new BattleAction(BattleActionKind.CastSpell, target.Position, spellId);
                }

                if (!spell.NeedsTarget && IsWorthCasting(battle, spell, isPlayer))
                {
                    return new BattleAction(BattleActionKind.CastSpell, default, spellId);
                }
            }

            return null;
        }

        // damage goes to the strongest hostile stack, curses to the strongest one not yet under it, blessings to the
        // strongest own stack not yet under it, healing to the most wounded own stack
        private static Combatant Target(BattleEngine battle, SpellDefinition spell, bool isPlayer)
        {
            if (!spell.NeedsTarget)
            {
                return null;
            }

            bool isOnEnemies = spell.Target == SpellTarget.Enemy || spell.Target == SpellTarget.Area;
            IEnumerable<Combatant> candidates = battle.Combatants.Where(combatant => combatant.IsAlive && (combatant.IsPlayer != isPlayer) == isOnEnemies);
            if (spell.Effect == SpellEffect.Heal)
            {
                return candidates
                    .Where(combatant => combatant.Count * combatant.HealthPerUnit > combatant.TotalHealth)
                    .OrderByDescending(combatant => combatant.Count * combatant.HealthPerUnit - combatant.TotalHealth)
                    .ThenBy(combatant => combatant.Id)
                    .FirstOrDefault();
            }

            if (spell.IsLasting)
            {
                candidates = candidates.Where(combatant => !combatant.HasSpell(spell.Id));
            }

            return candidates.OrderByDescending(combatant => combatant.TotalHealth).ThenBy(combatant => combatant.Id).FirstOrDefault();
        }

        private static bool IsWorthCasting(BattleEngine battle, SpellDefinition spell, bool isPlayer)
        {
            switch (spell.Target)
            {
                case SpellTarget.AllAllies:
                    return battle.Combatants.Any(combatant => combatant.IsAlive && combatant.IsPlayer == isPlayer && !combatant.HasSpell(spell.Id));
                case SpellTarget.AllEnemies:
                    return battle.Combatants.Any(combatant => combatant.IsAlive && combatant.IsPlayer != isPlayer);
                default:
                    // a spell on every stack is cast only while the hostile army is the larger one
                    int own = battle.Combatants.Where(combatant => combatant.IsAlive && combatant.IsPlayer == isPlayer).Sum(combatant => combatant.TotalHealth);
                    int hostile = battle.Combatants.Where(combatant => combatant.IsAlive && combatant.IsPlayer != isPlayer).Sum(combatant => combatant.TotalHealth);
                    return hostile > own;
            }
        }
    }
}
