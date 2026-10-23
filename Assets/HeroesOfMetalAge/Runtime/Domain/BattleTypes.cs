using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    public enum BattleOutcome
    {
        InProgress,
        Victory,
        Defeat
    }

    public enum BattleActionKind
    {
        MoveOrAttack,
        Defend,
        Pass,
        /// <summary>The side's hero casts <see cref="BattleAction.SpellId"/>; the acting stack still has its turn.</summary>
        CastSpell,
        /// <summary>Ends the Tactics phase: the placed stacks start the first round.</summary>
        EndTactics
    }

    public readonly struct BattleAction
    {
        public BattleAction(BattleActionKind kind, HexPosition target = default, string spellId = null)
        {
            Kind = kind;
            Target = target;
            SpellId = spellId;
        }

        public BattleActionKind Kind { get; }
        /// <summary>Destination hex, the hex of the stack to attack, or the spell's target hex.</summary>
        public HexPosition Target { get; }
        /// <summary>The spell to cast; null for other actions.</summary>
        public string SpellId { get; }
    }

    /// <summary>One resolved strike (a hit or a miss), published after the command succeeded.</summary>
    public readonly struct DamageEvent
    {
        public DamageEvent(int attackerCombatantId, int targetCombatantId, string attackerId, string targetId, int damage, int casualties,
            bool isReceivedByPlayer, bool isRetaliation, bool isRanged, bool isMiss, int luck = 0)
        {
            AttackerCombatantId = attackerCombatantId;
            TargetCombatantId = targetCombatantId;
            AttackerId = attackerId;
            TargetId = targetId;
            Damage = damage;
            Casualties = casualties;
            IsReceivedByPlayer = isReceivedByPlayer;
            IsRetaliation = isRetaliation;
            IsRanged = isRanged;
            IsMiss = isMiss;
            Luck = luck;
        }

        /// <summary>The stacks involved, so presentation can animate the right ones when both sides field the same unit.</summary>
        public int AttackerCombatantId { get; }
        public int TargetCombatantId { get; }
        /// <summary>Unit definition IDs.</summary>
        public string AttackerId { get; }
        public string TargetId { get; }
        public int Damage { get; }
        public int Casualties { get; }
        /// <summary>True when the player's stack was hit.</summary>
        public bool IsReceivedByPlayer { get; }
        public bool IsRetaliation { get; }
        /// <summary>True for a shot from a distance; a ranged unit fighting next to its target strikes in melee.</summary>
        public bool IsRanged { get; }
        /// <summary>The strike missed: no damage and no casualties.</summary>
        public bool IsMiss { get; }
        /// <summary>1 when good luck doubled the damage, -1 when bad luck halved it, 0 otherwise.</summary>
        public int Luck { get; }
    }

    /// <summary>A stack's morale showed: with good morale it acts again, with bad morale it froze and lost its turn.</summary>
    public readonly struct MoraleEvent
    {
        public MoraleEvent(int combatantId, string unitId, bool isPlayer, bool isGood)
        {
            CombatantId = combatantId;
            UnitId = unitId;
            IsPlayer = isPlayer;
            IsGood = isGood;
        }

        public int CombatantId { get; }
        public string UnitId { get; }
        public bool IsPlayer { get; }
        public bool IsGood { get; }
    }

    /// <summary>What a spell did to one stack.</summary>
    public readonly struct SpellHit
    {
        public SpellHit(int combatantId, string unitId, bool isPlayer, int damage, int casualties, int healed, bool isResisted)
        {
            CombatantId = combatantId;
            UnitId = unitId;
            IsPlayer = isPlayer;
            Damage = damage;
            Casualties = casualties;
            Healed = healed;
            IsResisted = isResisted;
        }

        public int CombatantId { get; }
        public string UnitId { get; }
        /// <summary>True when the stack is the player's.</summary>
        public bool IsPlayer { get; }
        public int Damage { get; }
        public int Casualties { get; }
        public int Healed { get; }
        /// <summary>Resistance let the spell fail against this stack.</summary>
        public bool IsResisted { get; }
    }

    /// <summary>A spell cast by one side's hero and what it did to each stack it reached.</summary>
    public sealed class SpellEvent
    {
        internal SpellEvent(string spellId, bool isPlayerCaster, string casterName, HexPosition target, IEnumerable<SpellHit> hits)
        {
            SpellId = spellId;
            IsPlayerCaster = isPlayerCaster;
            CasterName = casterName;
            Target = target;
            Hits = hits.ToList().AsReadOnly();
        }

        public string SpellId { get; }
        public bool IsPlayerCaster { get; }
        public string CasterName { get; }
        /// <summary>The chosen hex for a targeted spell.</summary>
        public HexPosition Target { get; }
        public IReadOnlyList<SpellHit> Hits { get; }
    }

    /// <summary>Result of one battle action: the command result and every hit in resolution order.</summary>
    public sealed class BattleResolution
    {
        internal BattleResolution(CommandResult result, List<DamageEvent> hits, string message, SpellEvent spell = null)
        {
            Result = result;
            Hits = hits.AsReadOnly();
            Message = message;
            Spell = spell;
        }

        public CommandResult Result { get; }
        public IReadOnlyList<DamageEvent> Hits { get; }
        /// <summary>Battle log line for non-attack actions; empty for attacks, which are logged per hit.</summary>
        public string Message { get; }
        /// <summary>The spell cast by this action, or null.</summary>
        public SpellEvent Spell { get; }
    }
}
