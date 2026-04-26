using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Tile", menuName = "WFC/Tile")]
public class TileSet : ScriptableObject
{
    [System.Serializable]
    public struct TileEntry
    {
        public TileType type;
        public TileBase tile;
    }

    public TileEntry[] entries;

    public TileBase GetTile(TileType type)
    {
        foreach (var entry in entries)
        {
            if (entry.type == type) return entry.tile;
        }
        return null;
    }
}
