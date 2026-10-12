using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>Validates the authored databases and projects them into the read-only gameplay catalog.</summary>
    public static class ContentLoader
    {
        // the serialized float 0.55 is normalized before currency rounding, so 70 * 0.55 stays 38.5 and not 38.50000001
        private const int PRICE_MULTIPLIER_DECIMALS = 6;

        /// <exception cref="InvalidOperationException">Any authored value is missing or out of range.</exception>
        public static DefinitionCatalog Load(UnitDatabaseScriptableObject units, ArtefactDatabaseScriptableObject artefacts,
            HeroDataBaseScriptableObject heroes, AudioDatabaseScriptableObject audio, MissionSettingsScriptableObject mission)
        {
            ContentValidator.ValidateReferences(units, artefacts, heroes, audio, mission);
            HashSet<string> unitIds = new HashSet<string>(units.Units.Select(entry => entry.Id));
            HashSet<string> artefactIds = new HashSet<string>(artefacts.Artefacts.Select(entry => entry.Id));

            ContentValidator.ValidateUnits(units);
            ContentValidator.ValidateArtefacts(artefacts);
            ContentValidator.ValidateHeroes(heroes, unitIds, mission.ArmySlots);
            ContentValidator.ValidateAudio(audio);
            ContentValidator.ValidateMission(mission, heroes, unitIds, artefactIds);

            MissionDefinition definition = ToDefinition(mission);
            ContentValidator.ValidateMap(definition);
            return new DefinitionCatalog(
                units.Units.Select(entry => entry.ToDefinition()),
                artefacts.Artefacts.Select(entry => entry.ToDefinition()),
                heroes.Heroes.Select(entry => entry.ToDefinition()),
                definition);
        }

        private static MissionDefinition ToDefinition(MissionSettingsScriptableObject mission)
        {
            CombatPolicy combat = new CombatPolicy(mission.CombatWidth, mission.CombatHeight, mission.EnemyAttack, mission.EnemyDefense,
                mission.DefendDamageMultiplier, mission.RangedMeleeDamageMultiplier, mission.AttackDeltaMultiplier, mission.DefenseDeltaMultiplier,
                mission.MaximumAttackDelta, mission.MaximumDefenseDelta);
            TerrainType[] terrain = mission.TerrainRows.SelectMany(row => row.Cells).ToArray();
            WorldPosition start = new WorldPosition(mission.StartingPosition.x, mission.StartingPosition.y);
            double ownedPriceMultiplier = Math.Round(mission.OwnedRecruitmentMultiplier, PRICE_MULTIPLIER_DECIMALS);

            return new MissionDefinition(mission.Id, mission.DisplayName, mission.Description, mission.HeroId, mission.ClanId, mission.ClanName,
                mission.ClanHeroName, mission.StartingGold, mission.ArmySlots, mission.DaysPerWeek, mission.ForestMovementCost,
                mission.GroundMovementCost, ownedPriceMultiplier, mission.MapWidth, mission.MapHeight, start, terrain, combat,
                mission.Encounters.Select(entry => entry.ToDefinition()), mission.RecruitmentUnitIds);
        }
    }
}
