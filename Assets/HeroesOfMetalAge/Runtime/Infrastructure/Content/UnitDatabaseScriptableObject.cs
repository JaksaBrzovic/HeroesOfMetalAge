using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>Authored creature definitions; validated and projected into the gameplay catalog at startup.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Unit Database")]
    public sealed class UnitDatabaseScriptableObject : ScriptableObject
    {
        [SerializeField] private List<UnitEntry> units = new List<UnitEntry>();

        public IReadOnlyList<UnitEntry> Units => units;
    }
}
