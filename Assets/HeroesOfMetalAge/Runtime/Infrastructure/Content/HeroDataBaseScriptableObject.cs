using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>Authored hero definitions; validated and projected into the gameplay catalog at startup.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Hero Database")]
    public sealed class HeroDataBaseScriptableObject : ScriptableObject
    {
        [SerializeField] private List<HeroEntry> heroes = new List<HeroEntry>();

        public IReadOnlyList<HeroEntry> Heroes => heroes;
    }
}
