using System;
using System.Collections.Generic;

namespace HeroesOfMetalAge.Domain
{
    // Detached, versioned save records. The public camelCase fields are the JSON save format read by JsonUtility;
    // renaming one is a save-format change that needs a migration.

    [Serializable]
    public sealed class StackSnapshot
    {
        public string unitId;
        public int count;
    }

    [Serializable]
    public sealed class StockSnapshot
    {
        public string unitId;
        public int count;
    }

    /// <summary>A count by stable ID: a dwelling's stock or the week a mill last paid out.</summary>
    [Serializable]
    public sealed class CountSnapshot
    {
        public string id;
        public int count;
    }

    /// <summary>A secondary skill by its <see cref="SecondarySkill"/> name and its <see cref="SkillLevel"/> value.</summary>
    [Serializable]
    public sealed class SkillSnapshot
    {
        public string skill;
        public int level;
    }

    [Serializable]
    public sealed class CampaignSnapshot
    {
        // version 5: secondary skills, level-ups, primary stats, luck, the spell book and mana
        public const int CURRENT_VERSION = 5;
        /// <summary>Saves of this version load with the hero's starting skills and every level-up still to be granted.</summary>
        public const int OLDEST_MIGRATED_VERSION = 4;

        public int version;
        public int day;
        public int gold;
        public int experience;
        public int movement;
        public int x;
        public int y;
        public string missionId;
        public string heroId;
        public string townOwnerId;
        public bool heroHome;
        public bool recruited;
        public int morale;
        /// <summary>
        /// Explored cells as packed bits in Base64 (see <see cref="ExploredMap"/>). Saves made before the fog of war have none
        /// and load with only the hero's surroundings explored.
        /// </summary>
        public string explored;
        /// <summary>One artefact ID per equipment place in <see cref="EquipmentPlace"/> order, empty where nothing is worn.</summary>
        public List<string> equipped;
        public List<string> visited;
        public List<string> keys;
        public List<CountSnapshot> dwellings;
        public List<CountSnapshot> mills;
        public List<StackSnapshot> army;
        public List<StackSnapshot> garrison;
        public List<StockSnapshot> stock;
        public List<string> inventory;
        public List<string> completed;
        /// <summary>Luck from map objects until the next won battle.</summary>
        public int luck;
        /// <summary>Primary stats won on level-ups, in <see cref="PrimaryStat"/> order.</summary>
        public List<int> primaryGains;
        public List<SkillSnapshot> skills;
        public int levelsGranted;
        public int levelsWithoutWisdom;
        /// <summary>The level of the level-up waiting for a choice; 0 for none.</summary>
        public int pendingLevel;
        public string pendingStat;
        public List<SkillSnapshot> pendingSkills;
        public bool spellBook;
        public List<string> spells;
        public int mana;
    }
}
