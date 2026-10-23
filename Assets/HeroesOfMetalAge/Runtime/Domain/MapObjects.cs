namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// What an adventure map object does when the hero enters its cell. Values are stored in the mission asset, so existing
    /// values never change; new kinds go at the end.
    /// </summary>
    public enum MapObjectKind
    {
        /// <summary>Gold lying on the ground; taken once.</summary>
        GoldPile,
        /// <summary>A chest the hero opens once, choosing its gold or its experience.</summary>
        TreasureChest,
        /// <summary>An artefact lying on the ground; taken once.</summary>
        Artefact,
        /// <summary>Raises the hero's attack for good, once.</summary>
        AttackShrine,
        /// <summary>Raises the hero's defense for good, once.</summary>
        DefenseShrine,
        /// <summary>Raises the army's morale until the next won battle.</summary>
        MoraleShrine,
        /// <summary>Pays gold once a week.</summary>
        Mill,
        /// <summary>Sells one kind of creature from its own stock, which grows every week.</summary>
        Dwelling,
        /// <summary>Someone who tells of a quest and hands over a key, once.</summary>
        QuestGiver,
        /// <summary>A tent that bars the way until the hero carries its key; then it opens and is gone.</summary>
        BorderGate,
        /// <summary>Someone held captive; freeing them is a goal and brings their gift.</summary>
        Prison,
        /// <summary>A place of lore; visiting it is a goal.</summary>
        Oracle,
        /// <summary>Leaves the mission once the required goals are done.</summary>
        ExitPortal
    }

    /// <summary>One authored object on the adventure map. Which values matter depends on <see cref="Kind"/>.</summary>
    public sealed class MapObjectDefinition
    {
        public MapObjectDefinition(string id, MapObjectKind kind, WorldPosition position, string name, string description, int gold, int experience,
            string artefactId, int attackBonus, int defenseBonus, int moraleBonus, string unitId, int weeklyGrowth, string keyId)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Name = name;
            Description = description;
            Gold = gold;
            Experience = experience;
            ArtefactId = string.IsNullOrEmpty(artefactId) ? null : artefactId;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            MoraleBonus = moraleBonus;
            UnitId = string.IsNullOrEmpty(unitId) ? null : unitId;
            WeeklyGrowth = weeklyGrowth;
            KeyId = string.IsNullOrEmpty(keyId) ? null : keyId;
        }

        public string Id { get; }
        public MapObjectKind Kind { get; }
        public WorldPosition Position { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>Gold pile: its gold; chest: the gold choice; mill: the weekly gold.</summary>
        public int Gold { get; }
        /// <summary>Chest: the experience choice.</summary>
        public int Experience { get; }
        /// <summary>Artefact pickup: the artefact; prison: the captive's gift. Null for none.</summary>
        public string ArtefactId { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int MoraleBonus { get; }
        /// <summary>Dwelling: the creature it sells.</summary>
        public string UnitId { get; }
        /// <summary>Dwelling: creatures added to its stock every week (also its stock at the start).</summary>
        public int WeeklyGrowth { get; }
        /// <summary>Quest giver: the key handed over; border gate: the key that opens it.</summary>
        public string KeyId { get; }

        /// <summary>
        /// Objects that stop a ride passing over them: what is taken or opened there (and then leaves the map). The hero rides
        /// over a lasting place (a shrine, a mill, a dwelling) and visits it only when it is where he rides to.
        /// </summary>
        public bool StopsPassage => IsRemovedWhenVisited;

        /// <summary>Taken or opened objects leave the map once visited.</summary>
        public bool IsRemovedWhenVisited => Kind == MapObjectKind.GoldPile || Kind == MapObjectKind.TreasureChest || Kind == MapObjectKind.Artefact
            || Kind == MapObjectKind.BorderGate || Kind == MapObjectKind.Prison;
    }

    /// <summary>How a mission goal is reached. Stored in the mission asset; new kinds go at the end.</summary>
    public enum ObjectiveKind
    {
        /// <summary>Win against the encounter named by the target ID.</summary>
        DefeatEncounter,
        /// <summary>Visit the map object named by the target ID (free the captive, consult the oracle).</summary>
        VisitObject,
        /// <summary>Carry or wear the artefact named by the target ID.</summary>
        OwnArtefact,
        /// <summary>Reach the hero level given by the value.</summary>
        ReachLevel
    }

    /// <summary>One goal of the mission. Its state is derived from the campaign, never stored.</summary>
    public sealed class ObjectiveDefinition
    {
        public ObjectiveDefinition(string id, string text, ObjectiveKind kind, string targetId, int value, bool isRequired)
        {
            Id = id;
            Text = text;
            Kind = kind;
            TargetId = targetId;
            Value = value;
            IsRequired = isRequired;
        }

        public string Id { get; }
        public string Text { get; }
        public ObjectiveKind Kind { get; }
        public string TargetId { get; }
        public int Value { get; }
        /// <summary>The mission can only be finished once every required goal is reached.</summary>
        public bool IsRequired { get; }
    }
}
