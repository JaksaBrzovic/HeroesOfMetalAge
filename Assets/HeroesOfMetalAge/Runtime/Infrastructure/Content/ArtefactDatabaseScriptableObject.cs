using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>Authored artefact definitions; validated and projected into the gameplay catalog at startup.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Artefact Database")]
    public sealed class ArtefactDatabaseScriptableObject : ScriptableObject
    {
        [SerializeField] private List<ArtefactEntry> artefacts = new List<ArtefactEntry>();

        public IReadOnlyList<ArtefactEntry> Artefacts => artefacts;
    }
}
