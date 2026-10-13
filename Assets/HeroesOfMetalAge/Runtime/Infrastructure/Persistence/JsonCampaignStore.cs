using System;
using System.IO;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Persistence
{
    /// <summary>
    /// JSON campaign file. Writes go to a temporary file that atomically replaces the save (keeping a .bak), so a failed
    /// write never destroys the last valid save. Saves of earlier versions were made on an earlier map with other movement
    /// points; no hero position in them is valid any more, so they are rejected rather than migrated.
    /// </summary>
    public sealed class JsonCampaignStore : ISaveStore
    {
        private const string TEMPORARY_SUFFIX = ".tmp";
        private const string BACKUP_SUFFIX = ".bak";

        public JsonCampaignStore(string location)
        {
            Location = Path.GetFullPath(location);
            Exists = File.Exists(Location);
        }

        public bool Exists { get; private set; }
        public string Location { get; }

        /// <exception cref="InvalidDataException">The file has no header or another version than the current one.</exception>
        public CampaignSnapshot Read()
        {
            string json = File.ReadAllText(Location);
            VersionHeader header = JsonUtility.FromJson<VersionHeader>(json);
            if (header == null)
            {
                throw new InvalidDataException("Missing campaign header.");
            }

            if (header.version != CampaignSnapshot.CURRENT_VERSION)
            {
                throw new InvalidDataException("Unsupported campaign version: " + header.version);
            }

            return JsonUtility.FromJson<CampaignSnapshot>(json);
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

        // JsonUtility fills this record by field name; the name is the save format
#pragma warning disable CS0649
        [Serializable]
        private sealed class VersionHeader
        {
            public int version;
        }
#pragma warning restore CS0649
    }
}
