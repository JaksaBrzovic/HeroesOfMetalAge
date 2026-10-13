using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// One tactical battle: stacks, initiative, reach and resolution. Actions are validated before anything changes; the
    /// turn order is fastest first, player before enemy on equal speed, then by stack ID.
    /// </summary>
    public sealed class BattleEngine
    {
        private readonly DefinitionCatalog catalog;
        private readonly IRandomSource random;
        private readonly int heroAttack;
        private readonly int heroDefense;
        private readonly int heroSpeedBonus;
        private readonly List<Combatant> combatants = new List<Combatant>();
        private readonly List<Combatant> turnOrder = new List<Combatant>();
        private readonly HashSet<HexPosition> reachable = new HashSet<HexPosition>();
        private int turnIndex;

        public BattleEngine(DefinitionCatalog catalog, IRandomSource random, CampaignState campaign, EncounterDefinition encounter)
        {
            this.catalog = catalog;
            this.random = random;
            CampaignRules rules = new CampaignRules(catalog);
            heroAttack = rules.Attack(campaign);
            heroDefense = rules.Defense(campaign);
            heroSpeedBonus = rules.SpeedBonus(campaign);
            Combatants = combatants.AsReadOnly();

            for (int i = 0; i < campaign.Army.Count; i++)
            {
                AddCombatant(campaign.Army[i], true, new HexPosition(1, i));
            }

            // small enemy armies spread over every other row so they do not form a wall
            for (int i = 0; i < encounter.Army.Count; i++)
            {
                int row = encounter.Army.Count <= 3 ? 1 + i * 2 : i;
                AddCombatant(encounter.Army[i], false, new HexPosition(Policy.Width - 2, row));
            }

            StartRound();
        }

        public IReadOnlyList<Combatant> Combatants { get; }
        public Combatant Active { get; private set; }
        public int Round { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public CombatPolicy Policy => catalog.Mission.Combat;

        /// <summary>Creatures the player has lost so far.</summary>
        public int Losses
        {
            get
            {
                int losses = 0;
                foreach (Combatant combatant in combatants)
                {
                    if (combatant.IsPlayer)
                    {
                        losses += combatant.StartCount - combatant.Count;
                    }
                }

                return losses;
            }
        }

        /// <summary>The living stack on a hex, or null.</summary>
        public Combatant At(HexPosition position)
        {
            foreach (Combatant combatant in combatants)
            {
                if (combatant.IsAlive && combatant.Position == position)
                {
                    return combatant;
                }
            }

            return null;
        }

        public int Speed(Combatant combatant)
        {
            int bonus = combatant.IsPlayer ? heroSpeedBonus : 0;
            return Math.Max(1, catalog.Unit(combatant.UnitId).Speed + bonus);
        }

        public bool IsReachable(HexPosition position)
        {
            return reachable.Contains(position);
        }

        /// <summary>Hexes the active stack can move to, ordered by row then column.</summary>
        public IReadOnlyList<HexPosition> ReachableCells()
        {
            return reachable.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToList().AsReadOnly();
        }

        public bool IsAdjacent(Combatant a, Combatant b)
        {
            return HexGrid.Distance(a.Position, b.Position) == 1;
        }

        public IReadOnlyList<Troop> Survivors()
        {
            List<Troop> survivors = new List<Troop>();
            foreach (Combatant combatant in combatants)
            {
                if (combatant.IsPlayer && combatant.IsAlive)
                {
                    survivors.Add(new Troop(combatant.UnitId, combatant.Count));
                }
            }

            return survivors.AsReadOnly();
        }

        /// <summary>Ranged stacks attack from anywhere; melee stacks need to be adjacent or able to walk next to the target.</summary>
        public bool CanAttack(Combatant target)
        {
            if (target == null || target.IsPlayer == Active.IsPlayer)
            {
                return false;
            }

            return IsAdjacent(Active, target) || catalog.Unit(Active.UnitId).IsRanged || AttackPosition(target).HasValue;
        }

        public BattleResolution Execute(BattleAction action, bool isPlayer)
        {
            if (!Enum.IsDefined(typeof(BattleActionKind), action.Kind))
            {
                return Rejected("Unknown battle action.");
            }

            if (Outcome != BattleOutcome.InProgress || Active == null || Active.IsPlayer != isPlayer)
            {
                return Rejected("It is not this army's turn.");
            }

            List<DamageEvent> hits = new List<DamageEvent>();
            string message;
            switch (action.Kind)
            {
                case BattleActionKind.Defend:
                    Active.IsDefending = true;
                    message = catalog.Unit(Active.UnitId).Name + " defends.";
                    break;

                case BattleActionKind.Pass:
                    message = "Turn passed.";
                    break;

                default:
                    BattleResolution rejection = MoveOrAttack(action.Target, hits, out message);
                    if (rejection != null)
                    {
                        return rejection;
                    }
                    break;
            }

            UpdateOutcome();
            if (Outcome == BattleOutcome.InProgress)
            {
                turnIndex++;
                ActivateNext();
            }

            return new BattleResolution(CommandResult.Ok(), hits, message);
        }

        /// <returns>A rejection, or null when the action was applied.</returns>
        private BattleResolution MoveOrAttack(HexPosition target, List<DamageEvent> hits, out string message)
        {
            message = string.Empty;
            Combatant enemy = At(target);
            if (enemy == null)
            {
                if (!IsReachable(target))
                {
                    return Rejected("This hex is not reachable.");
                }

                Active.Position = target;
                message = catalog.Unit(Active.UnitId).Name + " advances.";
                return null;
            }

            if (!CanAttack(enemy))
            {
                return Rejected("Enemy is beyond your movement range.");
            }

            if (!IsAdjacent(Active, enemy) && !catalog.Unit(Active.UnitId).IsRanged)
            {
                Active.Position = AttackPosition(enemy).Value;
            }

            Strike(Active, enemy, hits, false);
            return null;
        }

        private void AddCombatant(Troop troop, bool isPlayer, HexPosition position)
        {
            combatants.Add(new Combatant(combatants.Count, troop, catalog.Unit(troop.UnitId), isPlayer, position));
        }

        private void StartRound()
        {
            Round++;
            foreach (Combatant combatant in combatants)
            {
                combatant.HasRetaliated = false;
                combatant.IsDefending = false;
            }

            turnOrder.Clear();
            turnOrder.AddRange(combatants
                .Where(combatant => combatant.IsAlive)
                .OrderByDescending(Speed)
                .ThenByDescending(combatant => combatant.IsPlayer)
                .ThenBy(combatant => combatant.Id));
            turnIndex = 0;
            ActivateNext();
        }

        private void ActivateNext()
        {
            while (turnIndex < turnOrder.Count && !turnOrder[turnIndex].IsAlive)
            {
                turnIndex++;
            }

            if (turnIndex >= turnOrder.Count)
            {
                StartRound();
                return;
            }

            Active = turnOrder[turnIndex];
            Active.IsDefending = false;
            CalculateReach();
        }

        private void CalculateReach()
        {
            reachable.Clear();
            reachable.Add(Active.Position);
            Dictionary<HexPosition, int> distance = new Dictionary<HexPosition, int> { [Active.Position] = 0 };
            Queue<HexPosition> queue = new Queue<HexPosition>();
            queue.Enqueue(Active.Position);
            int speed = Speed(Active);
            while (queue.Count > 0)
            {
                HexPosition cell = queue.Dequeue();
                if (distance[cell] >= speed)
                {
                    continue;
                }

                foreach (HexPosition neighbor in HexGrid.Neighbors(cell, Policy.Width, Policy.Height))
                {
                    if (distance.ContainsKey(neighbor) || At(neighbor) != null)
                    {
                        continue;
                    }

                    distance[neighbor] = distance[cell] + 1;
                    reachable.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        /// <summary>Closest free reachable hex next to the target, ties broken by row then column.</summary>
        private HexPosition? AttackPosition(Combatant target)
        {
            HexPosition? best = null;
            int bestDistance = int.MaxValue;
            foreach (HexPosition cell in HexGrid.Neighbors(target.Position, Policy.Width, Policy.Height))
            {
                if (!IsReachable(cell) || At(cell) != null)
                {
                    continue;
                }

                int distance = HexGrid.Distance(Active.Position, cell);
                bool isBetter = best == null
                    || distance < bestDistance
                    || (distance == bestDistance && (cell.Y < best.Value.Y || (cell.Y == best.Value.Y && cell.X < best.Value.X)));
                if (isBetter)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void UpdateOutcome()
        {
            bool hasPlayerStacks = false;
            bool hasEnemyStacks = false;
            foreach (Combatant combatant in combatants)
            {
                if (!combatant.IsAlive)
                {
                    continue;
                }

                hasPlayerStacks |= combatant.IsPlayer;
                hasEnemyStacks |= !combatant.IsPlayer;
            }

            Outcome = !hasPlayerStacks ? BattleOutcome.Defeat : !hasEnemyStacks ? BattleOutcome.Victory : BattleOutcome.InProgress;
        }

        private BattleResolution Rejected(string error)
        {
            return new BattleResolution(CommandResult.Reject(error), new List<DamageEvent>(), string.Empty);
        }

        /// <summary>Applies one strike, which may miss; a surviving melee target strikes back once per round.</summary>
        private void Strike(Combatant attacker, Combatant target, List<DamageEvent> hits, bool isRetaliation)
        {
            bool isMelee = IsAdjacent(attacker, target);
            // no roll at a zero chance, so battles without misses replay exactly as before
            bool isMiss = Policy.MissChance > 0 && random.NextValue() < Policy.MissChance;
            int damage = 0;
            if (!isMiss)
            {
                int attack = attacker.IsPlayer ? heroAttack : Policy.EnemyAttack;
                int defense = target.IsPlayer ? heroDefense : Policy.EnemyDefense;
                damage = DamageCalculator.Calculate(catalog.Unit(attacker.UnitId), attacker.Count, attack, catalog.Unit(target.UnitId), defense,
                    target.IsDefending, isMelee, random.NextValue(), Policy);
            }

            int countBefore = target.Count;
            target.TotalHealth = Math.Max(0, target.TotalHealth - damage);
            hits.Add(new DamageEvent(attacker.Id, target.Id, attacker.UnitId, target.UnitId, damage, countBefore - target.Count, target.IsPlayer, isRetaliation,
                !isMelee, isMiss));

            if (!isRetaliation && isMelee && target.IsAlive && !target.HasRetaliated)
            {
                target.HasRetaliated = true;
                Strike(target, attacker, hits, true);
            }
        }
    }
}
