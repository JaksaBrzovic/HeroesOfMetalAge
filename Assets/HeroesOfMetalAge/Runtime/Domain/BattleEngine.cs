using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// One tactical battle: stacks, initiative, reach, spells and resolution. Actions are validated before anything changes;
    /// the turn order is fastest first, player before enemy on equal speed, then by stack ID. With Tactics the player first
    /// places the stacks in the columns nearest their edge, then the first round begins. Each side's hero may cast one spell
    /// per round during one of its stacks' turns; casting does not use up the stack's turn.
    /// </summary>
    public sealed class BattleEngine
    {
        private readonly DefinitionCatalog catalog;
        private readonly IRandomSource random;
        private readonly int heroSpeedBonus;
        private readonly List<MoraleEvent> moraleEvents = new List<MoraleEvent>();
        private readonly List<Combatant> combatants = new List<Combatant>();
        private readonly List<Combatant> turnOrder = new List<Combatant>();
        private readonly HashSet<HexPosition> reachable = new HashSet<HexPosition>();
        private int turnIndex;

        public BattleEngine(DefinitionCatalog catalog, IRandomSource random, CampaignState campaign, EncounterDefinition encounter)
        {
            this.catalog = catalog;
            this.random = random;
            CampaignRules rules = new CampaignRules(catalog);
            heroSpeedBonus = rules.SpeedBonus(campaign);
            Combatants = combatants.AsReadOnly();

            SideSkills playerSkills = new SideSkills(rules.SkillValue(campaign, SecondarySkill.Offense), rules.SkillValue(campaign, SecondarySkill.Archery),
                rules.SkillValue(campaign, SecondarySkill.Armorer), rules.SkillValue(campaign, SecondarySkill.Resistance),
                rules.SkillValue(campaign, SecondarySkill.Sorcery), (int)rules.SkillValue(campaign, SecondarySkill.Tactics));
            IEnumerable<string> playerSpells = campaign.HasSpellBook
                ? catalog.Spells.Where(spell => campaign.KnowsSpell(spell.Id)).Select(spell => spell.Id)
                : Enumerable.Empty<string>();
            Player = new BattleSide(true, catalog.Hero(campaign.HeroId).Name, rules.Attack(campaign), rules.Defense(campaign), rules.SpellPower(campaign),
                campaign.Mana, rules.Morale(campaign), rules.Luck(campaign), campaign.HasSpellBook, playerSpells, playerSkills);
            CommanderDefinition commander = encounter.Commander;
            IEnumerable<string> enemySpells = commander != null ? commander.SpellIds.Where(catalog.HasSpell) : Enumerable.Empty<string>();
            Enemy = new BattleSide(false, commander?.Name, Policy.EnemyAttack, Policy.EnemyDefense, commander?.SpellPower ?? 0, commander?.Mana ?? 0, 0, 0,
                commander != null, enemySpells, default);

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

            // the enemy has no Tactics, so the player's whole skill counts; the zone never reaches past the middle
            TacticsColumns = Math.Max(0, Math.Min(Policy.Width / 2, Player.Skills.TacticsColumns - Enemy.Skills.TacticsColumns));
            if (TacticsColumns > 0 && combatants.Exists(combatant => combatant.IsPlayer))
            {
                IsTacticsPhase = true;
                Active = combatants.First(combatant => combatant.IsPlayer);
                CalculateTacticsReach();
            }
            else
            {
                StartRound();
            }
        }

        public IReadOnlyList<Combatant> Combatants { get; }
        public Combatant Active { get; private set; }
        /// <summary>The round, from 1; 0 during the Tactics phase.</summary>
        public int Round { get; private set; }
        public BattleOutcome Outcome { get; private set; }
        public CombatPolicy Policy => catalog.Mission.Combat;
        public BattleSide Player { get; }
        public BattleSide Enemy { get; }
        /// <summary>The player's morale for this battle; the enemy fights at zero.</summary>
        public int PlayerMorale => Player.Morale;
        /// <summary>True while the player places the stacks before the first round (Tactics).</summary>
        public bool IsTacticsPhase { get; private set; }
        /// <summary>How many columns from the left edge the player may place stacks in during the Tactics phase.</summary>
        public int TacticsColumns { get; }

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
                        losses += Math.Max(0, combatant.StartCount - combatant.Count);
                    }
                }

                return losses;
            }
        }

        public BattleSide Side(bool isPlayer)
        {
            return isPlayer ? Player : Enemy;
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
            return Math.Max(1, catalog.Unit(combatant.UnitId).Speed + bonus + combatant.EffectAmount(SpellEffect.Speed));
        }

        /// <summary>The stack's attack: its unit's, its hero's and its spells'.</summary>
        public int AttackOf(Combatant combatant)
        {
            return catalog.Unit(combatant.UnitId).Attack + Side(combatant.IsPlayer).Attack + combatant.EffectAmount(SpellEffect.Attack);
        }

        public int DefenseOf(Combatant combatant)
        {
            return catalog.Unit(combatant.UnitId).Defense + Side(combatant.IsPlayer).Defense + combatant.EffectAmount(SpellEffect.Defense);
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

        /// <summary>Morale events since the last call (also the ones from setting up the battle), oldest first.</summary>
        public IReadOnlyList<MoraleEvent> TakeMoraleEvents()
        {
            List<MoraleEvent> events = new List<MoraleEvent>(moraleEvents);
            moraleEvents.Clear();
            return events.AsReadOnly();
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

        /// <summary>The creatures a side has lost so far, one entry per unit type in the order the stacks were placed.</summary>
        public IReadOnlyList<Troop> Casualties(bool isPlayer)
        {
            List<Troop> lost = new List<Troop>();
            foreach (Combatant combatant in combatants)
            {
                int count = combatant.StartCount - combatant.Count;
                if (combatant.IsPlayer != isPlayer || count <= 0)
                {
                    continue;
                }

                int index = lost.FindIndex(troop => troop.UnitId == combatant.UnitId);
                if (index < 0)
                {
                    lost.Add(new Troop(combatant.UnitId, count));
                }
                else
                {
                    lost[index] = new Troop(combatant.UnitId, lost[index].Count + count);
                }
            }

            return lost.AsReadOnly();
        }

        /// <summary>Ranged stacks attack from anywhere; melee stacks need to be adjacent or able to walk next to the target.</summary>
        public bool CanAttack(Combatant target)
        {
            if (IsTacticsPhase || target == null || target.IsPlayer == Active.IsPlayer)
            {
                return false;
            }

            return IsAdjacent(Active, target) || catalog.Unit(Active.UnitId).IsRanged || AttackPosition(target).HasValue;
        }

        /// <summary>True when the side's hero may cast the spell now; <paramref name="reason"/> says why not.</summary>
        public bool CanCast(bool isPlayer, string spellId, out string reason)
        {
            BattleSide side = Side(isPlayer);
            reason = Outcome != BattleOutcome.InProgress || Active == null || Active.IsPlayer != isPlayer ? "Spells are cast on your own turn."
                : !side.HasSpellBook ? "Your hero has no spell book."
                : IsTacticsPhase ? "Spells wait until the battle begins."
                : side.HasCastThisRound ? "Your hero has already cast a spell this round."
                : spellId == null || !catalog.HasSpell(spellId) || !side.Spells.Contains(spellId) ? "Your hero does not know that spell."
                : side.Mana < catalog.Spell(spellId).ManaCost ? "Not enough mana."
                : null;
            return reason == null;
        }

        /// <summary>True when the hex is a target the spell can be cast at by the given side.</summary>
        public bool IsValidSpellTarget(string spellId, bool isPlayerCaster, HexPosition target)
        {
            if (!catalog.HasSpell(spellId))
            {
                return false;
            }

            SpellDefinition spell = catalog.Spell(spellId);
            Combatant stack = At(target);
            switch (spell.Target)
            {
                case SpellTarget.Enemy:
                    return stack != null && stack.IsPlayer != isPlayerCaster;
                case SpellTarget.Ally:
                    return stack != null && stack.IsPlayer == isPlayerCaster;
                case SpellTarget.Area:
                    return target.X >= 0 && target.Y >= 0 && target.X < Policy.Width && target.Y < Policy.Height;
                default:
                    return true;
            }
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

            if (IsTacticsPhase)
            {
                return ExecuteTactics(action);
            }

            if (action.Kind == BattleActionKind.EndTactics)
            {
                return Rejected("The battle has already begun.");
            }

            if (action.Kind == BattleActionKind.CastSpell)
            {
                return Cast(action, isPlayer);
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
                // good morale: a stack that moved or struck may act once more this round
                bool hasActed = action.Kind == BattleActionKind.MoveOrAttack;
                if (hasActed && Active.IsAlive && !Active.HasMoraleBonus && RollsMorale(Active))
                {
                    Active.HasMoraleBonus = true;
                    moraleEvents.Add(new MoraleEvent(Active.Id, Active.UnitId, Active.IsPlayer, true));
                    CalculateReach();
                }
                else
                {
                    turnIndex++;
                    ActivateNext();
                }
            }

            return new BattleResolution(CommandResult.Ok(), hits, message);
        }

        #region TACTICS
        // during the Tactics phase a click on an own stack picks it, a click on a free highlighted hex moves it there
        private BattleResolution ExecuteTactics(BattleAction action)
        {
            string message = string.Empty;
            switch (action.Kind)
            {
                case BattleActionKind.MoveOrAttack:
                    Combatant there = At(action.Target);
                    if (there != null && there.IsPlayer)
                    {
                        Active = there;
                    }
                    else if (there == null && IsReachable(action.Target))
                    {
                        Active.Position = action.Target;
                        message = catalog.Unit(Active.UnitId).Name + " takes position.";
                    }
                    else
                    {
                        return Rejected("Place your stacks in the highlighted columns.");
                    }

                    break;

                case BattleActionKind.Pass:
                    List<Combatant> own = combatants.Where(combatant => combatant.IsPlayer && combatant.IsAlive).ToList();
                    Active = own[(own.IndexOf(Active) + 1) % own.Count];
                    break;

                case BattleActionKind.EndTactics:
                    IsTacticsPhase = false;
                    StartRound();
                    return new BattleResolution(CommandResult.Ok(), new List<DamageEvent>(), "The battle begins.");

                default:
                    return Rejected("Place your stacks, then start the battle.");
            }

            CalculateTacticsReach();
            return new BattleResolution(CommandResult.Ok(), new List<DamageEvent>(), message);
        }

        private void CalculateTacticsReach()
        {
            reachable.Clear();
            reachable.Add(Active.Position);
            for (int y = 0; y < Policy.Height; y++)
            {
                for (int x = 0; x < TacticsColumns; x++)
                {
                    HexPosition cell = new HexPosition(x, y);
                    if (At(cell) == null)
                    {
                        reachable.Add(cell);
                    }
                }
            }
        }
        #endregion

        #region SPELLS
        private BattleResolution Cast(BattleAction action, bool isPlayer)
        {
            if (!CanCast(isPlayer, action.SpellId, out string reason))
            {
                return Rejected(reason);
            }

            SpellDefinition spell = catalog.Spell(action.SpellId);
            if (spell.NeedsTarget && !IsValidSpellTarget(spell.Id, isPlayer, action.Target))
            {
                return Rejected("Choose a target for " + spell.Name + ".");
            }

            BattleSide side = Side(isPlayer);
            List<SpellHit> spellHits = new List<SpellHit>();
            foreach (Combatant target in SpellTargets(spell, isPlayer, action.Target))
            {
                spellHits.Add(Affect(spell, side, target));
            }

            side.Mana -= spell.ManaCost;
            side.HasCastThisRound = true;
            UpdateOutcome();
            if (Outcome == BattleOutcome.InProgress)
            {
                // a spell on the whole battlefield can kill the stack whose turn it is
                if (!Active.IsAlive)
                {
                    turnIndex++;
                    ActivateNext();
                }
                else
                {
                    CalculateReach();
                }
            }

            string caster = side.HeroName ?? (isPlayer ? "Your hero" : "The enemy");
            SpellEvent cast = new SpellEvent(spell.Id, isPlayer, caster, action.Target, spellHits);
            return new BattleResolution(CommandResult.Ok(), new List<DamageEvent>(), caster + " casts " + spell.Name + ".", cast);
        }

        private List<Combatant> SpellTargets(SpellDefinition spell, bool isPlayerCaster, HexPosition target)
        {
            List<Combatant> living = combatants.Where(combatant => combatant.IsAlive).ToList();
            switch (spell.Target)
            {
                case SpellTarget.Enemy:
                case SpellTarget.Ally:
                    return new List<Combatant> { At(target) };
                case SpellTarget.AllEnemies:
                    return living.FindAll(combatant => combatant.IsPlayer != isPlayerCaster);
                case SpellTarget.AllAllies:
                    return living.FindAll(combatant => combatant.IsPlayer == isPlayerCaster);
                case SpellTarget.Area:
                    HashSet<HexPosition> area = new HashSet<HexPosition>(HexGrid.Neighbors(target, Policy.Width, Policy.Height)) { target };
                    return living.FindAll(combatant => area.Contains(combatant.Position));
                default:
                    return living;
            }
        }

        /// <summary>
        /// What the spell does to one stack. A harmful spell from the other side may be resisted (the target side's
        /// Resistance); damage grows with the caster's Sorcery; healing never raises the dead.
        /// </summary>
        private SpellHit Affect(SpellDefinition spell, BattleSide caster, Combatant target)
        {
            BattleSide targetSide = Side(target.IsPlayer);
            bool isHostile = target.IsPlayer != caster.IsPlayer;
            // no roll without Resistance, so battles without it replay exactly as before
            if (spell.IsHarmful && isHostile && targetSide.Skills.Resistance > 0 && random.NextValue() < targetSide.Skills.Resistance)
            {
                return new SpellHit(target.Id, target.UnitId, target.IsPlayer, 0, 0, 0, true);
            }

            int amount = spell.Amount(caster.SpellPower);
            switch (spell.Effect)
            {
                case SpellEffect.Damage:
                    int damage = (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round(amount * (1d + caster.Skills.Sorcery), MidpointRounding.ToEven)));
                    int countBefore = target.Count;
                    target.TotalHealth = Math.Max(0, target.TotalHealth - damage);
                    return new SpellHit(target.Id, target.UnitId, target.IsPlayer, damage, countBefore - target.Count, 0, false);

                case SpellEffect.Heal:
                    int missing = target.Count * target.HealthPerUnit - target.TotalHealth;
                    int healed = Math.Max(0, Math.Min(amount, missing));
                    target.TotalHealth += healed;
                    return new SpellHit(target.Id, target.UnitId, target.IsPlayer, 0, 0, healed, false);

                default:
                    target.EffectData.RemoveAll(active => active.SpellId == spell.Id);
                    target.EffectData.Add(new ActiveSpell(spell.Id, spell.Effect, amount, spell.Duration(caster.SpellPower)));
                    return new SpellHit(target.Id, target.UnitId, target.IsPlayer, 0, 0, 0, false);
            }
        }

        // every lasting spell loses a round when a new round starts; the hero may cast again
        private void AgeSpells()
        {
            foreach (Combatant combatant in combatants)
            {
                for (int i = combatant.EffectData.Count - 1; i >= 0; i--)
                {
                    ActiveSpell active = combatant.EffectData[i];
                    if (active.RoundsLeft <= 1)
                    {
                        combatant.EffectData.RemoveAt(i);
                    }
                    else
                    {
                        combatant.EffectData[i] = new ActiveSpell(active.SpellId, active.Effect, active.Amount, active.RoundsLeft - 1);
                    }
                }
            }

            Player.HasCastThisRound = false;
            Enemy.HasCastThisRound = false;
        }
        #endregion

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
            if (Round > 0)
            {
                AgeSpells();
            }

            Round++;
            foreach (Combatant combatant in combatants)
            {
                combatant.HasRetaliated = false;
                combatant.IsDefending = false;
                combatant.HasMoraleBonus = false;
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
            // bad morale: the stack freezes and loses its turn, unless it is the last one still able to act this round
            if (MoraleOf(Active) < 0 && RollsMorale(Active) && HasAnotherLivingStack(Active))
            {
                moraleEvents.Add(new MoraleEvent(Active.Id, Active.UnitId, Active.IsPlayer, false));
                turnIndex++;
                ActivateNext();
                return;
            }

            CalculateReach();
        }

        private int MoraleOf(Combatant combatant)
        {
            return Side(combatant.IsPlayer).Morale;
        }

        // no roll at zero morale or chance, so battles without morale replay exactly as before
        private bool RollsMorale(Combatant combatant)
        {
            int morale = MoraleOf(combatant);
            if (morale == 0 || Policy.MoraleChance <= 0)
            {
                return false;
            }

            return random.NextValue() < Math.Abs(morale) * Policy.MoraleChance;
        }

        // no roll at zero luck or chance, so battles without luck replay exactly as before
        private int RollLuck(BattleSide side)
        {
            double chance = catalog.HeroPolicy.LuckChance;
            if (side.Luck == 0 || chance <= 0)
            {
                return 0;
            }

            return random.NextValue() < Math.Abs(side.Luck) * chance ? Math.Sign(side.Luck) : 0;
        }

        private bool HasAnotherLivingStack(Combatant combatant)
        {
            foreach (Combatant other in combatants)
            {
                if (other != combatant && other.IsAlive && other.IsPlayer == combatant.IsPlayer)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Applies one strike, which may miss; a surviving melee target strikes back once per round. Offense (melee) or
        /// Archery (shots) and good luck raise the damage, bad luck halves it, the target's Armorer and a shield spell
        /// (melee only) lower it, and bless or curse fix the damage roll.
        /// </summary>
        private void Strike(Combatant attacker, Combatant target, List<DamageEvent> hits, bool isRetaliation)
        {
            bool isMelee = IsAdjacent(attacker, target);
            BattleSide attackerSide = Side(attacker.IsPlayer);
            BattleSide targetSide = Side(target.IsPlayer);
            // no roll at a zero chance, so battles without misses replay exactly as before
            bool isMiss = Policy.MissChance > 0 && random.NextValue() < Policy.MissChance;
            int damage = 0;
            int luck = 0;
            if (!isMiss)
            {
                luck = RollLuck(attackerSide);
                int attack = attackerSide.Attack + attacker.EffectAmount(SpellEffect.Attack);
                int defense = targetSide.Defense + target.EffectAmount(SpellEffect.Defense);
                double shield = isMelee ? Math.Max(0, Math.Min(100, target.EffectAmount(SpellEffect.Shield))) / 100d : 0d;
                StrikeModifiers modifiers = new StrikeModifiers(isMelee ? attackerSide.Skills.Offense : attackerSide.Skills.Archery, targetSide.Skills.Armorer,
                    shield, luck, attacker.HasEffect(SpellEffect.Bless), attacker.HasEffect(SpellEffect.Curse));
                damage = DamageCalculator.Calculate(catalog.Unit(attacker.UnitId), attacker.Count, attack, catalog.Unit(target.UnitId), defense,
                    target.IsDefending, isMelee, random.NextValue(), Policy, modifiers);
            }

            int countBefore = target.Count;
            target.TotalHealth = Math.Max(0, target.TotalHealth - damage);
            hits.Add(new DamageEvent(attacker.Id, target.Id, attacker.UnitId, target.UnitId, damage, countBefore - target.Count, target.IsPlayer, isRetaliation,
                !isMelee, isMiss, luck));

            if (!isRetaliation && isMelee && target.IsAlive && !target.HasRetaliated)
            {
                target.HasRetaliated = true;
                Strike(target, attacker, hits, true);
            }
        }
    }
}
