namespace HeroesOfMetalAge.Domain
{
    /// <summary>A stack on the battlefield. Health is pooled; the creature count follows from it.</summary>
    public sealed class Combatant
    {
        internal Combatant(int id, Troop troop, UnitDefinition definition, bool isPlayer, HexPosition position)
        {
            Id = id;
            UnitId = troop.UnitId;
            IsPlayer = isPlayer;
            StartCount = troop.Count;
            HealthPerUnit = definition.Health;
            TotalHealth = checked(troop.Count * definition.Health);
            Position = position;
        }

        public int Id { get; }
        public string UnitId { get; }
        public bool IsPlayer { get; }
        public int StartCount { get; }
        public int HealthPerUnit { get; }
        public int TotalHealth { get; internal set; }
        public HexPosition Position { get; internal set; }
        public bool IsDefending { get; internal set; }
        /// <summary>Each stack retaliates at most once per round.</summary>
        public bool HasRetaliated { get; internal set; }
        /// <summary>Living creatures; a partly damaged creature still counts.</summary>
        public int Count => TotalHealth <= 0 ? 0 : (TotalHealth - 1) / HealthPerUnit + 1;
        public bool IsAlive => TotalHealth > 0;
    }
}
