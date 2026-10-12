using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Persistence
{
    /// <summary>
    /// JSON campaign file. Writes go to a temporary file that atomically replaces the save (keeping a .bak), so a failed
    /// write never destroys the last valid save. Version 1 saves are migrated on read.
    /// </summary>
    public sealed class JsonCampaignStore : ISaveStore
    {
        private const int LEGACY_VERSION = 1;
        private const string TEMPORARY_SUFFIX = ".tmp";
        private const string BACKUP_SUFFIX = ".bak";
        private const string LEGACY_MISSION_ID = "ashen-pass";
        private const string LEGACY_HERO_ID = "kael";
        private const string LEGACY_CLAN_ID = "ashen-clan";
        private const int LEGACY_STOCK_LENGTH = 3;

        public JsonCampaignStore(string location)
        {
            Location = Path.GetFullPath(location);
            Exists = File.Exists(Location);
        }

        public bool Exists { get; private set; }
        public string Location { get; }

        /// <exception cref="InvalidDataException">The file has no header, an unknown version or incomplete legacy data.</exception>
        public CampaignSnapshot Read()
        {
            string json = File.ReadAllText(Location);
            VersionHeader header = JsonUtility.FromJson<VersionHeader>(json);
            if (header == null)
            {
                throw new InvalidDataException("Missing campaign header.");
            }

            if (header.version == CampaignSnapshot.CURRENT_VERSION)
            {
                return JsonUtility.FromJson<CampaignSnapshot>(json);
            }

            if (header.version == LEGACY_VERSION)
            {
                return Migrate(JsonUtility.FromJson<LegacyCampaign>(json));
            }

            throw new InvalidDataException("Unsupported campaign version: " + header.version);
        }

        public void Write(CampaignSnapshot snapshot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Location));
            string temporary = Location + TEMPORARY_SUFFIX;
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(snapshot, true));
                if (File.Exists(Location))
                {
                    File.Replace(temporary, Location, Location + BACKUP_SUFFIX);
                }
                else
                {
                    File.Move(temporary, Location);
                }

                Exists = true;
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static CampaignSnapshot Migrate(LegacyCampaign old)
        {
            bool isComplete = old != null && old.army != null && old.garrison != null && old.bag != null && old.stock != null
                && old.stock.Length == LEGACY_STOCK_LENGTH;
            if (!isComplete)
            {
                throw new InvalidDataException("Incomplete version 1 campaign.");
            }

            return new CampaignSnapshot
            {
                version = CampaignSnapshot.CURRENT_VERSION,
                missionId = LEGACY_MISSION_ID,
                heroId = LEGACY_HERO_ID,
                day = old.day,
                gold = old.gold,
                experience = old.xp,
                x = old.x,
                y = old.y,
                movement = old.movement,
                townOwnerId = old.townOwned ? CampaignState.PLAYER_OWNER_ID : LEGACY_CLAN_ID,
                heroHome = old.heroHome,
                recruited = old.recruited,
                weaponId = LegacyArtefact(old.weapon),
                charmId = LegacyArtefact(old.charm),
                army = old.army.Select(MigrateStack).ToList(),
                garrison = old.garrison.Select(MigrateStack).ToList(),
                inventory = old.bag.Select(LegacyArtefact).ToList(),
                completed = LegacyCompleted(old),
                stock = Enumerable.Range(0, LEGACY_STOCK_LENGTH)
                    .Select(index => new StockSnapshot { unitId = LegacyUnit(index), count = old.stock[index] })
                    .ToList()
            };
        }

        private static StackSnapshot MigrateStack(LegacyTroop troop)
        {
            return new StackSnapshot { unitId = LegacyUnit(troop.kind), count = troop.count };
        }

        private static List<string> LegacyCompleted(LegacyCampaign old)
        {
            List<string> completed = new List<string>();
            if (old.joined)
            {
                completed.Add("friendly");
            }

            if (old.sentinelDead)
            {
                completed.Add("sentinel");
            }

            if (old.bossDead)
            {
                completed.Add("boss");
            }

            if (old.townOwned)
            {
                completed.Add("town");
            }

            return completed;
        }

        // version 1 stored array indexes; these maps are fixed by that format and must never change
        private static string LegacyUnit(int index)
        {
            switch (index)
            {
                case 0:
                    return "ironbound";
                case 1:
                    return "ash-archer";
                case 2:
                    return "doom-rider";
                default:
                    throw new InvalidDataException("Unknown legacy unit index.");
            }
        }

        private static string LegacyArtefact(int index)
        {
            switch (index)
            {
                case -1:
                    return null;
                case 0:
                    return "fang-of-thunder";
                case 1:
                    return "ashen-ward";
                case 2:
                    return "warborn-sigil";
                default:
                    throw new InvalidDataException("Unknown legacy artefact index.");
            }
        }

        // JsonUtility fills these records by field name; the names are the save format
#pragma warning disable CS0649
        [Serializable]
        private sealed class VersionHeader
        {
            public int version;
        }

        [Serializable]
        private sealed class LegacyTroop
        {
            public int kind;
            public int count;
        }

        [Serializable]
        private sealed class LegacyCampaign
        {
            public int version;
            public int day;
            public int gold;
            public int xp;
            public int x;
            public int y;
            public int movement;
            public bool joined;
            public bool sentinelDead;
            public bool bossDead;
            public bool townOwned;
            public bool heroHome;
            public bool recruited;
            public List<LegacyTroop> army;
            public List<LegacyTroop> garrison;
            public int[] stock;
            public List<int> bag;
            public int weapon;
            public int charm;
        }
#pragma warning restore CS0649
    }
}
