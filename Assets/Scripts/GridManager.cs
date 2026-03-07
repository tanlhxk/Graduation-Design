using UnityEngine;
using System.Collections.Generic;

// 格子类型枚举
public enum TileType
{
    Walkable,    // 可行走
    Obstacle,    // 障碍物（墙、陷阱）
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

// 网格管理器
public class GridManager : MonoBehaviour
{
    [Header("网格设置")]
    [SerializeField] private int width = 10;      // 网格宽度
    [SerializeField] private int height = 10;     // 网格高度
    [SerializeField] private float cellSize = 1f; // 格子大小
    [SerializeField] private GameObject tilePrefab; // 格子预制体

    private Tile[,] grid;                          // 二维网格数组
    public static Dictionary<Vector2Int, Tile> tileDict; // 快速查找字典

    void Awake()
    {
        GenerateGrid();
    }

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
    void CreateTileVisual(int x, int y, TileType type,Tile tile)
    {
        GameObject tileObj = Instantiate(tilePrefab, GridToWorld(new Vector2Int(x, y)), Quaternion.identity, transform);
        tileObj.name = $"Tile_{x}_{y}";
        tile.unitObj = tileObj;

        // 根据类型设置颜色
        SpriteRenderer renderer = tileObj.GetComponent<SpriteRenderer>();
        switch (type)
        {
            case TileType.Walkable:
                renderer.color = new Color(0.8f, 0.8f, 0.8f);
                break;
            case TileType.Obstacle:
                renderer.color = new Color(0.3f, 0.3f, 0.3f);
                break;
        }
    }
}
