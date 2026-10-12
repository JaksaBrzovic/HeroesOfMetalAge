namespace HeroesOfMetalAge.Domain
{
    /// <summary>A stack of identical creatures, immutable; changes produce a new stack.</summary>
    public sealed class Troop
    {
        public Troop(string unitId, int count)
        {
            UnitId = unitId;
            Count = count;
        }

        public string UnitId { get; }
        public int Count { get; }
    }
}
