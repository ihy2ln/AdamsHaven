namespace Game.Town
{
    /// <summary>Deliberately the same three names as `Game.Battle.BattleRewards
    /// .MaterialKind` (Hide/Ore/Essence) -- Town doesn't reference Game.Battle (kept
    /// decoupled on purpose, see PROJECT-README's M30 entry), so this is a duplicate
    /// enum, not a shared one. FOUNDATION.md's own resource table names Battle stages
    /// as the actual source of "Town building materials" -- matching the vocabulary now
    /// means the real cross-scene transfer (once a save/session layer exists to carry
    /// it) won't need a conversion table, just a straight copy.</summary>
    public enum TownMaterialKind
    {
        Hide,
        Ore,
        Essence,
    }
}
