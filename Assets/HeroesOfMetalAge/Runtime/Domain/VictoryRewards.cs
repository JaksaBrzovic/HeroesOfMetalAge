using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>The rolled spoils of one won battle: always some gold, none or one artefact.</summary>
    public sealed class VictoryRewards
    {
        public VictoryRewards(int gold, IEnumerable<string> artefactIds, bool hasLegendary)
        {
            Gold = gold;
            ArtefactIds = artefactIds.ToList().AsReadOnly();
            HasLegendary = hasLegendary;
        }

        public int Gold { get; }
        public IReadOnlyList<string> ArtefactIds { get; }
        /// <summary>True when one of the artefacts is legendary.</summary>
        public bool HasLegendary { get; }
    }
}
