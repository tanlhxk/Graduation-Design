using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

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
}
[CreateAssetMenu(fileName = "TileSet", menuName = "WFC/TileSet")]
public class TileSet : ScriptableObject
{
    [System.Serializable]
    public struct TileEntry
    {
        public TileType type;
        public TileBase tile;   // 改为 TileBase
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
// 网格管理器
public class GridManager : MonoBehaviour
{
    public static GridManager Instance;
    [Header("网格设置")]
    [SerializeField] private float cellSize = 1f;          // 格子大小（保持不变）
    [SerializeField] private GameObject tilePrefab;        // 默认格子预制体
    [Header("Tilemap设置")]
    [SerializeField] private Tilemap tilemap;          // 引用场景中的 Tilemap
    [SerializeField] private TileSet tileSet;          // 仍然使用 TileSet，但里面改为存储 Tile 而不是 Prefab
    [Header("Billboard障碍物配置")]
    public List<BillboardObstacleConfig> billboardObstacles;

    private int width;
    private int height;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    private Tile[,] grid;
    public static Dictionary<Vector2Int, Tile> tileDict;   // 快速查找字典
    private List<GameObject> instancedObstacles = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        //GenerateGrid();
    }

    void ClearInstancedObstacles()
    {
        foreach (var obj in instancedObstacles)
            if (obj != null) DestroyImmediate(obj);
        instancedObstacles.Clear();
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
        tilemap.ClearAllTiles();
        grid = new Tile[width, height];
        tileDict = new Dictionary<Vector2Int, Tile>();

        // 清除已存在的实例化障碍物（如果重新生成地图）
        ClearInstancedObstacles();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType type = mapData[x, y];
                Tile tile = new Tile(x, y, type);
                grid[x, y] = tile;
                tileDict[new Vector2Int(x, y)] = tile;

                Vector3 worldPos = tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
                tile.worldPos = worldPos;

                // 检查是否需要实例化 Billboard 障碍物
                BillboardObstacleConfig? config = GetBillboardConfig(type);
                if (config.HasValue)
                {
                    // 实例化预制体
                    GameObject obj = Instantiate(config.Value.prefab[Random.Range(0, config.Value.prefab.Length)], worldPos + Vector3.up * config.Value.yOffset, Quaternion.identity, transform);
                    // 添加 Billboard 脚本（如果预制体本身没有）
                    if (obj.GetComponent<FacingCamera>() == null)
                        obj.AddComponent<FacingCamera>();
                    // 记录实例，以便重新生成时清除
                    instancedObstacles.Add(obj);
                    TileBase groundTile = tileSet.GetTile(TileType.Walkable); // 使用地面瓦片
                    if (groundTile != null)
                        tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
                }
                else
                {
                    // 正常设置 Tilemap 瓦片
                    TileBase tileBase = tileSet.GetTile(type);
                    if (tileBase != null)
                        tilemap.SetTile(new Vector3Int(x, y, 0), tileBase);
                }
            }
        }
        tilemap.CompressBounds();
    }
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return tilemap.GetCellCenterWorld(new Vector3Int(gridPos.x, gridPos.y, 0));
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3Int cellPos = tilemap.WorldToCell(worldPos);
        return new Vector2Int(cellPos.x, cellPos.y);
    }

    public static int GetDistance(Tile tileA, Tile tileB)
    {
        // 这里使用曼哈顿距离作为例子，如果是斜向移动，可以用 Mathf.Max
        int dx = Mathf.Abs(tileA.gridPos.x - tileB.gridPos.x);
        int dy = Mathf.Abs(tileA.gridPos.y - tileB.gridPos.y);
        return dx + dy; // 曼哈顿距离
                        // 如果是八方向移动，通常用：return Mathf.Max(dx, dy);
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

    public void SetTileColor(Vector2Int gridPos, Color color)
    {
        Vector3Int cellPos = new Vector3Int(gridPos.x, gridPos.y, 0);
        tilemap.SetTileFlags(cellPos, TileFlags.None);
        tilemap.SetColor(cellPos, color);
    }

    public void ResetTileColor(Vector2Int gridPos)
    {
        Vector3Int cellPos = new Vector3Int(gridPos.x, gridPos.y, 0);
        tilemap.SetColor(cellPos, Color.white);
    }
    private BillboardObstacleConfig? GetBillboardConfig(TileType type)
    {
        foreach (var config in billboardObstacles)
        {
            if (config.type == type)
                return config;
        }
        return null;
    }
    /*// 可视化格子
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
    }*/
}
