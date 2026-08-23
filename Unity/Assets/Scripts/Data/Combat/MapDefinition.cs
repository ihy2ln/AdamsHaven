using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// A lane-based battle map. Lanes are rows; columns run along each lane.
    ///
    /// Height is capped at 3 by design, not by balance preference: a fixed camera cannot
    /// rotate around tall terrain the way FFT's could, so front-lane height would
    /// permanently occlude back lanes.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Combat/Map", fileName = "Map_")]
    public class MapDefinition : ScriptableObject
    {
        public const int MaxHeight = 3;

        [Min(1)] public int laneCount = 3;
        [Min(1)] public int columnCount = 8;

        [Header("Presentation")]
        public Sprite backgroundSprite;

        [Tooltip("Packed hit-impact flipbook sheet + per-frame pixel rects (x,y,w,h), "
            + "parsed from the sheet's JSON sidecar by BattleAssetBuilder.")]
        public Sprite fxImpactSheet;
        public List<Vector4> fxImpactFrameRects = new();

        [Tooltip("Row-major: index = lane * columnCount + column.")]
        public List<TileData> tiles = new();

        [Header("Deployment")]
        [Tooltip("Tiles where player units may be placed during the free deployment phase.")]
        public List<Vector2Int> playerDeployTiles = new();

        public List<EnemyPlacement> enemies = new();

        [Header("Rules")]
        [Tooltip("Blocks the Flee action on this map (M19) -- the standard \"you can't "
            + "run from a boss\" rule. Defaults to false, so every map built before this "
            + "field existed still allows escape; no content sets it yet, and it's the "
            + "hook a future boss-phase system wants.")]
        public bool forbidEscape;

        public TileData GetTile(int lane, int column)
        {
            int i = lane * columnCount + column;
            return (i >= 0 && i < tiles.Count) ? tiles[i] : default;
        }

        public TileData GetTile(Vector2Int p) => GetTile(p.x, p.y);

        public bool InBounds(Vector2Int p) =>
            p.x >= 0 && p.x < laneCount && p.y >= 0 && p.y < columnCount;
    }

    [Serializable]
    public struct TileData
    {
        [Range(0, MapDefinition.MaxHeight)] public int height;
        public TerrainType terrain;
        public bool blocked;
    }

    [Serializable]
    public struct EnemyPlacement
    {
        public CharacterDefinition character;
        public TierDefinition tier;
        public Vector2Int position;
        public int level;
    }
}
