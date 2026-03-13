using UnityEngine;
using System.Collections.Generic;

// 格子类型枚举
public enum TileType
{
    Walkable,    // 可行走
    Obstacle,    // 障碍物（墙、陷阱）
    Wallside,
    Unit,        // 被单位占据
    Exit         // 出口/楼梯
}

// 单个格子数据
[System.Serializable]
public class Tile
{
    public Vector2Int gridPos;      // 网格坐标
    public TileType type;            // 格子类型
    public Unit occupyingUnit;       // 占据的单位（如果有）
    public Vector3 worldPos;         // 世界坐标位置
    public GameObject unitObj;

    public Tile(int x, int y, TileType tileType)
    {
        gridPos = new Vector2Int(x, y);
        type = tileType;
        occupyingUnit = null;
    }

    public bool IsWalkable()
    {
        return type == TileType.Walkable && occupyingUnit == null;
    }
    internal T GetComponent<T>()
    {
        return unitObj.GetComponent<T>();
    }
}
[CreateAssetMenu(fileName = "TileSet", menuName = "WFC/TileSet")]
public class TileSet : ScriptableObject
{
    [System.Serializable]
    public struct TileEntry
    {
        public TileType type;
        public GameObject prefab;
        //public Color color;  // 可选，用于调试
    }

    public TileEntry[] entries;

    public GameObject GetPrefab(TileType type)
    {
        foreach (var entry in entries)
        {
            if (entry.type == type) return entry.prefab;
        }
        return null;
    }
}
// 网格管理器
public class GridManager : MonoBehaviour
{
    [Header("网格设置")]
    [SerializeField] private float cellSize = 1f;          // 格子大小（保持不变）
    [SerializeField] private GameObject tilePrefab;        // 默认格子预制体
    [SerializeField] private TileSet tileSet;              // 图块集（包含各类型对应的预制体/颜色等）

    private int width;
    private int height;
    private Tile[,] grid;
    public static Dictionary<Vector2Int, Tile> tileDict;   // 快速查找字典

    void Awake()
    {
        //GenerateGrid();
    }

    /*
    // 程序化生成网格（与你的PCG系统结合）
    void GenerateGrid()
    {
        grid = new Tile[width, height];
        tileDict = new Dictionary<Vector2Int, Tile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 随机生成格子类型（简化版）
                TileType type = Random.Range(0, 10) < 8 ? TileType.Walkable : TileType.Obstacle;

                Tile tile = new Tile(x, y, type);
                grid[x, y] = tile;
                tileDict[new Vector2Int(x, y)] = tile;

                // 创建可视化格子
                CreateTileVisual(x, y, type, tile);
            }
        }
    }*/

    /// <summary>
    /// 从外部数据构建网格（由WFC生成器调用）
    /// </summary>
    public void BuildGridFromData(TileType[,] mapData)
    {
        width = mapData.GetLength(0);
        height = mapData.GetLength(1);

        grid = new Tile[width, height];
        tileDict = new Dictionary<Vector2Int, Tile>();

        // 清除旧的子物体（如果重新生成地图）
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType type = mapData[x, y];
                Tile tile = new Tile(x, y, type);
                grid[x, y] = tile;
                tileDict[new Vector2Int(x, y)] = tile;

                // 根据类型创建可视化
                CreateTileVisual(x, y, type, tile);
            }
        }

        Debug.Log($"Grid built with size {width}x{height}");
    }
    // 世界坐标转网格坐标
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int y = Mathf.FloorToInt(worldPos.y / cellSize);
        return new Vector2Int(x, y);
    }

    // 网格坐标转世界坐标
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x * cellSize + cellSize / 2,
                          gridPos.y * cellSize + cellSize / 2, 0);
    }

    // 获取指定位置的格子
    public Tile GetTile(Vector2Int gridPos)
    {
        if (gridPos.x < 0 || gridPos.x >= width || gridPos.y < 0 || gridPos.y >= height)
            return null;
        return grid[gridPos.x, gridPos.y];
    }

    // 设置格子占据单位
    public void SetUnitOnTile(Unit unit, Vector2Int gridPos)
    {
        // 先清除原位置的单位
        if (unit.currentTile != null)
        {
            unit.currentTile.occupyingUnit = null;
        }

        // 设置新位置
        Tile newTile = GetTile(gridPos);
        if (newTile != null)
        {
            newTile.occupyingUnit = unit;
            unit.currentTile = newTile;
            unit.transform.position = GridToWorld(gridPos);
        }
    }

    // 可视化格子
    private void CreateTileVisual(int x, int y, TileType type, Tile tile)
    {
        Vector3 worldPos = GridToWorld(new Vector2Int(x, y));
        GameObject tileObj;

        // 如果TileSet中定义了特定类型的预制体，则使用它；否则使用默认预制体
        if (tileSet != null && tileSet.GetPrefab(type) != null)
        {
            tileObj = Instantiate(tileSet.GetPrefab(type), worldPos, Quaternion.identity, transform);
        }
        else
        {
            tileObj = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
        }

        tileObj.name = $"Tile_{x}_{y}";
        tile.unitObj = tileObj;          // 注意：unitObj字段命名可能不合适，可改为visualObj

        // 可选：根据类型调整颜色（如果没有预制体就用颜色区分）
        if (tileSet == null)
        {
            SpriteRenderer renderer = tileObj.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                switch (type)
                {
                    case TileType.Walkable:
                        renderer.color = new Color(0.8f, 0.8f, 0.8f);
                        break;
                    case TileType.Obstacle:
                        renderer.color = new Color(0.3f, 0.3f, 0.3f);
                        break;
                    case TileType.Exit:
                        renderer.color = Color.green;
                        break;
                }
            }
        }
    }
}
