using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Shared wording for artefact cards on the hero and rewards screens.</summary>
    public static class ArtefactLabels
    {
        /// <summary>"RARE / +2 ATTACK / +0 DEFENSE / +0 SPEED".</summary>
        public static string Describe(ArtefactDefinition artefact)
        {
            return artefact.Rarity.ToString().ToUpperInvariant() + " / " + Bonus(artefact);
        }

        public static string Bonus(ArtefactDefinition artefact)
        {
            return "+" + artefact.AttackBonus + " ATTACK / +" + artefact.DefenseBonus + " DEFENSE / +" + artefact.SpeedBonus + " SPEED";
        }
    }
}
