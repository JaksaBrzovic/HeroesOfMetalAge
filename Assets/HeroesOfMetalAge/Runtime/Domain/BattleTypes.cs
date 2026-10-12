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

    /// <summary>One resolved hit, published after the command succeeded.</summary>
    public readonly struct DamageEvent
    {
        public DamageEvent(int attackerCombatantId, int targetCombatantId, string attackerId, string targetId, int damage, int casualties,
            bool isReceivedByPlayer, bool isRetaliation)
        {
            AttackerCombatantId = attackerCombatantId;
            TargetCombatantId = targetCombatantId;
            AttackerId = attackerId;
            TargetId = targetId;
            Damage = damage;
            Casualties = casualties;
            IsReceivedByPlayer = isReceivedByPlayer;
            IsRetaliation = isRetaliation;
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
