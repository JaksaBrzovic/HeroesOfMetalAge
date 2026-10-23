using System.Collections.Generic;
using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Presentation
{
    public static class ArtefactLabels
    {
        /// <summary>"RARE / +2 ATTACK / +1 MORALE".</summary>
        public static string Describe(ArtefactDefinition artefact)
        {
            return artefact.Rarity.ToString().ToUpperInvariant() + " / " + Bonus(artefact);
        }

        /// <summary>The bonuses the artefact gives, zero ones left out: "+2 ATTACK / +1 MORALE".</summary>
        public static string Bonus(ArtefactDefinition artefact)
        {
            List<string> bonuses = new List<string>();
            Add(bonuses, artefact.AttackBonus, "ATTACK");
            Add(bonuses, artefact.DefenseBonus, "DEFENSE");
            Add(bonuses, artefact.SpeedBonus, "SPEED");
            Add(bonuses, artefact.MoraleBonus, "MORALE");
            return bonuses.Count > 0 ? string.Join(" / ", bonuses) : "NO BONUS";
        }

        private static void Add(List<string> bonuses, int value, string stat)
        {
            if (value != 0)
            {
                bonuses.Add((value > 0 ? "+" : string.Empty) + value + " " + stat);
            }
        }
    }
}
