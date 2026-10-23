using System.Collections.Generic;

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
        Pass
    }

    public readonly struct BattleAction
    {
        public BattleAction(BattleActionKind kind, HexPosition target = default)
        {
            Kind = kind;
            Target = target;
        }

        public BattleActionKind Kind { get; }
        /// <summary>Destination hex, or the hex of the stack to attack.</summary>
        public HexPosition Target { get; }
    }

    /// <summary>One resolved strike (a hit or a miss), published after the command succeeded.</summary>
    public readonly struct DamageEvent
    {
        public DamageEvent(int attackerCombatantId, int targetCombatantId, string attackerId, string targetId, int damage, int casualties,
            bool isReceivedByPlayer, bool isRetaliation, bool isRanged, bool isMiss)
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

    /// <summary>Result of one battle action: the command result and every hit in resolution order.</summary>
    public sealed class BattleResolution
    {
        internal BattleResolution(CommandResult result, List<DamageEvent> hits, string message)
        {
            Result = result;
            Hits = hits.AsReadOnly();
            Message = message;
        }

        public CommandResult Result { get; }
        public IReadOnlyList<DamageEvent> Hits { get; }
        /// <summary>Battle log line for non-attack actions; empty for attacks, which are logged per hit.</summary>
        public string Message { get; }
    }
}
