using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    public Vector2Int playerStartPos; // 生成地牢后设置这个值
    public Vector2Int EnemyStartPos;

    public void GenerateDungeon(Dictionary<Vector2Int, Tile> tileDict)
    {
        // ... 生成逻辑 ...
        List<KeyValuePair<Vector2Int, Tile>> entries = new List<KeyValuePair<Vector2Int, Tile>>(tileDict);

        while (entries.Count > 0)
        {
            int randomIndex = Random.Range(0, entries.Count);
            KeyValuePair<Vector2Int, Tile> randomEntry = entries[randomIndex];
            if (randomEntry.Value.type == TileType.Walkable && randomEntry.Value.occupyingUnit == null)
            {
                EnemyStartPos = randomEntry.Key;
                //playerStartPos = startRoomGridPos; // 记录起点
                playerStartPos = new Vector2Int(0, 0);
                return;
            }
        }
    }
}
