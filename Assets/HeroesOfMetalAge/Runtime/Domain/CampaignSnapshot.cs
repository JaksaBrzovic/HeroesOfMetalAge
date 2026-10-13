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

    [Serializable]
    public sealed class CampaignSnapshot
    {
        public const int CURRENT_VERSION = 3;

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
        public string weaponId;
        public string charmId;
        public bool heroHome;
        public bool recruited;
        public List<StackSnapshot> army;
        public List<StackSnapshot> garrison;
        public List<StockSnapshot> stock;
        public List<string> inventory;
        public List<string> completed;
    }
}
