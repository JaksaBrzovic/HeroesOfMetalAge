using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using MLin.Audio;

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

        public static void ValidateReferences(UnitDatabaseScriptableObject units, ArtefactDatabaseScriptableObject artefacts,
            HeroDataBaseScriptableObject heroes, AudioDatabaseScriptableObject audio, MissionSettingsScriptableObject mission)
        {
            Require(units != null && artefacts != null && heroes != null && audio != null && mission != null, "Missing database reference on Bootstrap.");
            Require(AllPresent(units.Units) && AllPresent(artefacts.Artefacts) && AllPresent(heroes.Heroes)
                && AllPresent(mission.Encounters) && mission.TerrainRows != null && mission.RecruitmentUnitIds != null,
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
                    && InRange(artefact.AttackBonus, 0, MAX_STAT)
                    && InRange(artefact.DefenseBonus, 0, MAX_STAT)
                    && InRange(artefact.SpeedBonus, 0, MAX_SPEED);
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
        }

        public static void ValidateMission(MissionSettingsScriptableObject mission, HeroDataBaseScriptableObject heroes, HashSet<string> unitIds,
            HashSet<string> artefactIds)
        {
            Require(mission.TerrainRows.Count == mission.MapHeight && mission.TerrainRows.All(row => row != null && row.Cells != null && row.Cells.Count == mission.MapWidth),
                "Terrain dimensions do not match the map.");

            bool isValidRules = InRange(mission.MapWidth, 4, 18)
                && InRange(mission.MapHeight, 4, 11)
                && InRange(mission.ArmySlots, 1, 7)
                && mission.DaysPerWeek > 0
                && mission.StartingGold >= 0
                && mission.ForestMovementCost > 0
                && mission.GroundMovementCost > 0
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
                bool isInside = InRange(encounter.Position.x, 0, mission.MapWidth - 1) && InRange(encounter.Position.y, 0, mission.MapHeight - 1);
                Require(isInside, "Encounter outside map: " + encounter.Id);
                ValidateArmy(encounter.Army, unitIds, mission.ArmySlots, "encounter " + encounter.Id);
                bool isValidReward = encounter.RewardGold >= 0
                    && encounter.RewardExperience >= 0
                    && (string.IsNullOrEmpty(encounter.RewardArtefactId) || artefactIds.Contains(encounter.RewardArtefactId));
                Require(isValidReward, "Invalid reward: " + encounter.Id);
            }

            foreach (EncounterKind kind in Enum.GetValues(typeof(EncounterKind)))
            {
                Require(mission.Encounters.Count(encounter => encounter.Kind == kind) == 1, "First mission requires one " + kind + " encounter.");
            }

            Require(mission.Encounters.Select(encounter => encounter.Position).Distinct().Count() == mission.Encounters.Count, "Overlapping encounters.");
            ValidateCombat(mission);
        }

        public static void ValidateMap(MissionDefinition definition)
        {
            WorldMap map = new WorldMap(definition);
            Require(map.IsWalkable(definition.Start), "Starting position is blocked.");
            Require(definition.Encounters.All(encounter => map.IsWalkable(encounter.Position)), "An encounter is on blocked terrain.");
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
                && mission.TravelStepSeconds > 0
                && mission.EnemyTurnSeconds > 0;
            Require(isValid, "Invalid combat/pacing settings.");
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
