using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using MLin.Audio;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// Checks authored databases before they become the gameplay catalog. Every failure throws an
    /// <see cref="InvalidOperationException"/> naming the offending entry, so a broken asset fails at startup with a clear message.
    /// </summary>
    internal static class ContentValidator
    {
        private const int MAX_STAT = 10000;
        private const int MAX_DAMAGE = 100000;
        private const int MAX_SPEED = 100;
        private const int MAX_MOVEMENT = 100000;
        private const int MAX_MAP_CELLS = 512;

        public static void ValidateReferences(UnitDatabaseScriptableObject units, ArtefactDatabaseScriptableObject artefacts,
            HeroDataBaseScriptableObject heroes, AudioDatabaseScriptableObject audio, MissionSettingsScriptableObject mission)
        {
            Require(units != null && artefacts != null && heroes != null && audio != null && mission != null, "Missing database reference on Bootstrap.");
            Require(AllPresent(units.Units) && AllPresent(artefacts.Artefacts) && AllPresent(heroes.Heroes)
                && AllPresent(mission.Encounters) && mission.RecruitmentUnitIds != null,
                "A database contains a missing list or entry.");
            Require(!string.IsNullOrWhiteSpace(mission.Id) && !string.IsNullOrWhiteSpace(mission.ClanId) && mission.ClanId != CampaignState.PLAYER_OWNER_ID,
                "Invalid mission/clan ID.");

            RequireUniqueIds(units.Units.Select(entry => entry.Id), "unit");
            RequireUniqueIds(artefacts.Artefacts.Select(entry => entry.Id), "artefact");
            RequireUniqueIds(heroes.Heroes.Select(entry => entry.Id), "hero");
            RequireUniqueIds(mission.Encounters.Select(entry => entry.Id), "encounter");
        }

        public static void ValidateUnits(UnitDatabaseScriptableObject units)
        {
            foreach (UnitEntry unit in units.Units)
            {
                bool isValid = InRange(unit.Health, 1, MAX_STAT)
                    && unit.MinimumDamage > 0
                    && unit.MaximumDamage >= unit.MinimumDamage
                    && unit.MaximumDamage <= MAX_DAMAGE
                    && InRange(unit.Attack, 0, MAX_STAT)
                    && InRange(unit.Defense, 0, MAX_STAT)
                    && InRange(unit.Speed, 1, MAX_SPEED)
                    && unit.RecruitmentCost > 0
                    && InRange(unit.WeeklyGrowth, 0, CampaignRules.MAXIMUM_STACK_COUNT);
                Require(isValid, "Invalid stats for unit " + unit.Id);
                Require(unit.Voice == null || unit.Voice.HasClips, "Voice without clips for unit " + unit.Id);
            }
        }

        public static void ValidateArtefacts(ArtefactDatabaseScriptableObject artefacts)
        {
            foreach (ArtefactEntry artefact in artefacts.Artefacts)
            {
                bool isValid = Enum.IsDefined(typeof(EquipmentSlot), artefact.Slot)
                    && Enum.IsDefined(typeof(ArtefactRarity), artefact.Rarity)
                    && InRange(artefact.AttackBonus, 0, MAX_STAT)
                    && InRange(artefact.DefenseBonus, 0, MAX_STAT)
                    && InRange(artefact.SpeedBonus, 0, MAX_SPEED)
                    && InRange(artefact.MoraleBonus, -CampaignRules.MAXIMUM_MORALE, CampaignRules.MAXIMUM_MORALE);
                Require(isValid, "Invalid artefact " + artefact.Id);
            }
        }

        public static void ValidateHeroes(HeroDataBaseScriptableObject heroes, HashSet<string> unitIds, int armySlots)
        {
            foreach (HeroEntry hero in heroes.Heroes)
            {
                bool isValid = InRange(hero.BaseAttack, 0, MAX_STAT)
                    && InRange(hero.BaseDefense, 0, MAX_STAT)
                    && InRange(hero.DailyMovement, 1, MAX_MOVEMENT)
                    && hero.ExperiencePerLevel > 0
                    && InRange(hero.AttackPerLevel, 0, MAX_STAT)
                    && hero.DefenseLevelInterval > 0;
                Require(isValid, "Invalid hero " + hero.Id);
                ValidateArmy(hero.StartingArmy, unitIds, armySlots, "hero " + hero.Id);
            }
        }

        public static void ValidateAudio(AudioDatabaseScriptableObject audio)
        {
            Require(audio.Settings != null, "The audio database has no Audio System Settings assigned.");
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                SoundDefinition sound = audio.Sound(cue);
                Require(sound != null && sound.HasClips, "Missing sound or clips for cue " + cue);
            }

            foreach (MusicLayer layer in Enum.GetValues(typeof(MusicLayer)))
            {
                SoundDefinition sound = audio.Layer(layer);
                Require(sound != null && sound.HasClips && sound.Loop, "Music layer must have looping clips: " + layer);
            }

            foreach (MusicTrack track in new[] { MusicTrack.Battle, MusicTrack.Victory, MusicTrack.Defeat })
            {
                SoundDefinition sound = audio.Music(track);
                Require(sound != null && sound.HasClips, "Missing music or clips for track " + track);
            }

            // it plays for as long as the victory and rewards screens stay open
            Require(audio.Music(MusicTrack.Victory).Loop, "The victory music must loop.");
            Require(audio.RidingLoop != null && audio.RidingLoop.HasClips && audio.RidingLoop.Loop, "The riding loop must have looping clips.");
        }

        public static void ValidateMission(MissionSettingsScriptableObject mission, HeroDataBaseScriptableObject heroes, HashSet<string> unitIds)
        {
            TerrainDatabaseScriptableObject terrain = mission.Terrain;
            Require(terrain != null && AllPresent(terrain.Terrains), "The mission has no terrain database.");
            Require(terrain.Terrains.Select(entry => entry.Terrain).Distinct().Count() == terrain.Terrains.Count, "Duplicate terrain types.");
            Require(terrain.Terrains.Any(entry => entry.IsPassable && entry.Terrain != TerrainType.Blocked), "No terrain is passable.");
            Require(terrain.Terrains.All(entry => entry.MovementCost > 0), "Terrain movement costs must be positive.");

            AdventureMapAuthoring map = mission.Map;
            Require(map != null, "The mission has no adventure map prefab.");
            Require(map.TerrainLogic != null && map.Background != null && map.MapImage != null,
                "Map '" + map.name + "' needs its background renderer, map image and terrain logic layer.");
            Require(map.IsBuilt, "Map '" + map.name + "' changed since its last rebuild: press Rebuild on the map prefab.");
            Require(InRange(map.Width, 4, MAX_MAP_CELLS) && InRange(map.Height, 4, MAX_MAP_CELLS),
                "Map '" + map.name + "' must be 4.." + MAX_MAP_CELLS + " cells on each side.");

            bool isValidRules = InRange(mission.ArmySlots, 1, 7)
                && mission.DaysPerWeek > 0
                && mission.StartingGold >= 0
                && mission.OwnedRecruitmentMultiplier > 0
                && mission.OwnedRecruitmentMultiplier <= 1;
            Require(isValidRules, "Invalid mission rules.");
            Require(heroes.Heroes.Any(hero => hero.Id == mission.HeroId), "Unknown mission hero.");

            bool isValidRecruitment = mission.RecruitmentUnitIds.Count > 0
                && mission.RecruitmentUnitIds.All(unitIds.Contains)
                && mission.RecruitmentUnitIds.Distinct().Count() == mission.RecruitmentUnitIds.Count;
            Require(isValidRecruitment, "Invalid recruitment catalog.");

            foreach (EncounterEntry encounter in mission.Encounters)
            {
                bool isInside = InRange(encounter.Position.x, 0, map.Width - 1) && InRange(encounter.Position.y, 0, map.Height - 1);
                Require(isInside, "Encounter outside map: " + encounter.Id);
                ValidateArmy(encounter.Army, unitIds, mission.ArmySlots, "encounter " + encounter.Id);
                // every battle pays some gold; joining a friendly encounter is not a battle
                bool isValidReward = encounter.RewardExperience >= 0
                    && (encounter.Kind == EncounterKind.Friendly ? encounter.RewardGold >= 0 : encounter.RewardGold > 0);
                Require(isValidReward, "Invalid reward: " + encounter.Id);
            }

            // armies of every kind may repeat, but a mission has exactly one town
            Require(mission.Encounters.Count(encounter => encounter.Kind == EncounterKind.Town) == 1, "A mission requires exactly one town.");

            Require(mission.Encounters.Select(encounter => encounter.Position).Distinct().Count() == mission.Encounters.Count, "Overlapping encounters.");
            ValidateCombat(mission);
            ValidateRewards(mission);
            // the map music clip may be a local file kept out of version control; without it the layers play instead
            Require(mission.MapMusic == null || mission.MapMusic.Loop, "The mission's map music must loop.");
            Require(mission.TownMusic == null || (mission.TownMusic.HasClips && mission.TownMusic.Loop), "The mission's town music must have looping clips.");
        }

        /// <summary>Map objects and goals: unique IDs, inside the map, free cells and known references for each kind.</summary>
        public static void ValidateMapObjects(MissionSettingsScriptableObject mission, HashSet<string> unitIds, HashSet<string> artefactIds)
        {
            AdventureMapAuthoring map = mission.Map;
            Require(AllPresent(mission.MapObjects) && AllPresent(mission.Objectives), "The mission contains a missing map object or goal.");
            if (mission.MapObjects.Count > 0)
            {
                RequireUniqueIds(mission.MapObjects.Select(entry => entry.Id), "map object");
            }

            HashSet<string> encounterIds = new HashSet<string>(mission.Encounters.Select(entry => entry.Id));
            HashSet<Vector2Int> taken = new HashSet<Vector2Int>(mission.Encounters.Select(entry => entry.Position));
            HashSet<string> keys = new HashSet<string>();
            foreach (MapObjectEntry mapObject in mission.MapObjects)
            {
                string name = "map object " + mapObject.Id;
                Require(Enum.IsDefined(typeof(MapObjectKind), mapObject.Kind), "Unknown kind of " + name);
                Require(InRange(mapObject.Position.x, 0, map.Width - 1) && InRange(mapObject.Position.y, 0, map.Height - 1), name + " is outside the map.");
                Require(taken.Add(mapObject.Position), name + " shares its cell with another site.");
                Require(!encounterIds.Contains(mapObject.Id), name + " uses an encounter's ID.");
                Require(string.IsNullOrEmpty(mapObject.ArtefactId) || artefactIds.Contains(mapObject.ArtefactId), name + " gives an unknown artefact.");
                switch (mapObject.Kind)
                {
                    case MapObjectKind.GoldPile:
                    case MapObjectKind.Mill:
                        Require(mapObject.Gold > 0, name + " needs gold.");
                        break;
                    case MapObjectKind.TreasureChest:
                        Require(mapObject.Gold > 0 && mapObject.Experience > 0, name + " needs both gold and experience to choose from.");
                        break;
                    case MapObjectKind.Artefact:
                        Require(!string.IsNullOrEmpty(mapObject.ArtefactId), name + " needs its artefact.");
                        break;
                    case MapObjectKind.AttackShrine:
                        Require(mapObject.AttackBonus > 0, name + " needs an attack bonus.");
                        break;
                    case MapObjectKind.DefenseShrine:
                        Require(mapObject.DefenseBonus > 0, name + " needs a defense bonus.");
                        break;
                    case MapObjectKind.MoraleShrine:
                        Require(mapObject.MoraleBonus > 0, name + " needs a morale bonus.");
                        break;
                    case MapObjectKind.Dwelling:
                        Require(unitIds.Contains(mapObject.UnitId) && mapObject.WeeklyGrowth > 0, name + " needs a known unit and weekly growth.");
                        break;
                    case MapObjectKind.QuestGiver:
                        if (!string.IsNullOrEmpty(mapObject.KeyId))
                        {
                            keys.Add(mapObject.KeyId);
                        }

                        break;
                }
            }

            foreach (MapObjectEntry gate in mission.MapObjects.Where(entry => entry.Kind == MapObjectKind.BorderGate))
            {
                Require(!string.IsNullOrEmpty(gate.KeyId) && keys.Contains(gate.KeyId), "Border gate " + gate.Id + " needs a key some quest giver hands over.");
            }

            HashSet<string> objectIds = new HashSet<string>(mission.MapObjects.Select(entry => entry.Id));
            if (mission.Objectives.Count > 0)
            {
                RequireUniqueIds(mission.Objectives.Select(entry => entry.Id), "goal");
            }

            foreach (ObjectiveEntry objective in mission.Objectives)
            {
                bool isValid = !string.IsNullOrWhiteSpace(objective.Text) && (objective.Kind == ObjectiveKind.DefeatEncounter ? encounterIds.Contains(objective.TargetId)
                    : objective.Kind == ObjectiveKind.VisitObject ? objectIds.Contains(objective.TargetId)
                    : objective.Kind == ObjectiveKind.OwnArtefact ? artefactIds.Contains(objective.TargetId)
                    : objective.Value > 1);
                Require(isValid, "Invalid goal " + objective.Id);
            }
        }

        public static void ValidateMap(MissionDefinition definition)
        {
            WorldMap map = new WorldMap(definition);
            Require(map.IsWalkable(definition.Start), "Starting position is blocked.");
            Require(definition.Encounters.All(encounter => map.IsWalkable(encounter.Position)), "An encounter is on blocked terrain.");
            foreach (MapObjectDefinition mapObject in definition.Objects)
            {
                Require(map.IsWalkable(mapObject.Position), "Map object " + mapObject.Id + " is on blocked terrain.");
            }
        }

        public static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Content validation: " + message);
            }
        }

        private static void ValidateCombat(MissionSettingsScriptableObject mission)
        {
            bool isValid = InRange(mission.CombatWidth, 6, 10)
                && mission.CombatHeight >= mission.ArmySlots
                && mission.CombatHeight <= 7
                && mission.DefendDamageMultiplier > 0
                && mission.DefendDamageMultiplier <= 1
                && mission.RangedMeleeDamageMultiplier > 0
                && mission.RangedMeleeDamageMultiplier <= 1
                && mission.MaximumDefenseDelta * mission.DefenseDeltaMultiplier < 1
                && mission.MissChance >= 0
                && mission.MissChance < 1
                && mission.TravelStepSeconds > 0
                && mission.EnemyTurnSeconds > 0
                && mission.BattleActionSeconds >= 0
                && mission.BattleResultSeconds >= 0;
            Require(isValid, "Invalid combat/pacing settings.");
        }

        private static void ValidateRewards(MissionSettingsScriptableObject mission)
        {
            bool isValid = mission.GoldVariation >= 0
                && mission.GoldVariation < 1
                && mission.ArtefactChance >= 0
                && mission.ArtefactChance <= 1
                && mission.RareChance >= 0
                && mission.LegendaryChance >= 0
                && mission.EpicChance >= 0
                && mission.RareChance + mission.LegendaryChance + mission.EpicChance <= 1
                && mission.MoraleChance >= 0
                && mission.MoraleChance * CampaignRules.MAXIMUM_MORALE < 1
                && mission.LegendaryGuaranteeVictories >= 0;
            Require(isValid, "Invalid battle reward settings.");
        }

        private static void ValidateArmy(IReadOnlyList<ArmyEntry> army, HashSet<string> unitIds, int slots, string context)
        {
            bool isValid = army != null
                && army.Count > 0
                && army.Count <= slots
                && army.All(troop => troop != null && unitIds.Contains(troop.UnitId) && InRange(troop.Count, 1, CampaignRules.MAXIMUM_STACK_COUNT))
                && army.Select(troop => troop.UnitId).Distinct().Count() == army.Count;
            Require(isValid, "Invalid army for " + context);
        }

        private static void RequireUniqueIds(IEnumerable<string> ids, string context)
        {
            string[] values = ids.ToArray();
            bool isValid = values.Length > 0
                && values.All(id => !string.IsNullOrWhiteSpace(id))
                && values.Distinct(StringComparer.Ordinal).Count() == values.Length;
            Require(isValid, "Invalid/duplicate " + context + " IDs.");
        }

        private static bool AllPresent<T>(IReadOnlyList<T> entries)
            where T : class
        {
            return entries != null && entries.All(entry => entry != null);
        }

        private static bool InRange(int value, int minimum, int maximum)
        {
            return value >= minimum && value <= maximum;
        }
    }
}
