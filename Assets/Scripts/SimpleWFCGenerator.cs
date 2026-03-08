using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简单的波函数坍缩地图生成器
/// </summary>
public class SimpleWFCGenerator : MonoBehaviour
{
    [Header("地图尺寸")]
    public int width = 10;
    public int height = 10;

    [Header("图块类型（必须与GridManager的TileType一致）")]
    public List<TileType> tileTypes = new List<TileType>()
        { TileType.Walkable, TileType.Obstacle, TileType.Exit };

    [Header("每种类型的生成权重（顺序与tileTypes对应）")]
    public List<int> weights = new List<int>() { 20, 5, 1 };

    [Header("相邻规则")]
    public List<TileAdjacencyRule> adjacencyRules;


    public GridManager gridManager;
    public Vector2Int mapSize;

    public void GenerateAndBuildMap()
    {
        TileType[,] mapData = RunWFC();   // WFC算法返回二维数组
        gridManager.BuildGridFromData(mapData);
    }

    // 内部数据结构：类型兼容性查找表（双向）
    private Dictionary<TileType, HashSet<TileType>> compatibleNeighbors;

    // 波函数单元
    private class WFCCell
    {
        public List<TileType> possibleTypes;    // 当前可能的类型
        public bool collapsed;                   // 是否已坍缩
        public TileType finalType;                // 坍缩后的结果

        public WFCCell(List<TileType> allTypes)
        {
            possibleTypes = new List<TileType>(allTypes);
            collapsed = false;
        }
    }

    private WFCCell[,] wave;
    private Queue<Vector2Int> propagationQueue;

    /// <summary>
    /// 对外调用：运行WFC算法，返回二维类型数组
    /// </summary>
    public TileType[,] RunWFC()
    {
        // 初始化兼容性字典
        InitializeCompatibility();

        // 初始化波函数
        wave = new WFCCell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                wave[x, y] = new WFCCell(tileTypes);

        propagationQueue = new Queue<Vector2Int>();

        // 主循环
        while (true)
        {
            // 1. 观察：找到熵最小（可能类型最少）的未坍缩格子
            Vector2Int? target = FindMinEntropyCell();
            if (target == null)
                break; // 所有格子已坍缩

            // 2. 坍缩该格子
            CollapseCell(target.Value.x, target.Value.y);

            // 3. 传播约束（队列中已包含刚坍缩的格子）
            if (!Propagate())
            {
                Debug.LogError("WFC 矛盾发生，生成失败");
                return null;
            }
        }

        // 构建结果数组
        TileType[,] result = new TileType[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                result[x, y] = wave[x, y].finalType;

        return result;
    }

    // 初始化兼容性规则
    private void InitializeCompatibility()
    {
        compatibleNeighbors = new Dictionary<TileType, HashSet<TileType>>();

        // 为每个类型建立空集合
        foreach (var type in tileTypes)
            compatibleNeighbors[type] = new HashSet<TileType>();

        // 根据用户定义的规则填充
        foreach (var rule in adjacencyRules)
        {
            if (!compatibleNeighbors.ContainsKey(rule.type))
                compatibleNeighbors[rule.type] = new HashSet<TileType>();

            foreach (var neighbor in rule.allowedNeighbors)
            {
                compatibleNeighbors[rule.type].Add(neighbor);

                // 确保双向兼容（如果规则未显式包含反向，自动添加）
                if (!compatibleNeighbors.ContainsKey(neighbor))
                    compatibleNeighbors[neighbor] = new HashSet<TileType>();
                if (!compatibleNeighbors[neighbor].Contains(rule.type))
                    compatibleNeighbors[neighbor].Add(rule.type);
            }
        }
    }

    // 找到熵最小的未坍缩格子
    private Vector2Int? FindMinEntropyCell()
    {
        int minEntropy = int.MaxValue;
        Vector2Int? result = null;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = wave[x, y];
                if (!cell.collapsed)
                {
                    int entropy = cell.possibleTypes.Count;
                    if (entropy < minEntropy)
                    {
                        minEntropy = entropy;
                        result = new Vector2Int(x, y);
                    }
                }
            }
        }
        return result;
    }

    // 坍缩指定格子（随机选择一种类型）
    private void CollapseCell(int x, int y)
    {
        var cell = wave[x, y];
        TileType chosen = ChooseRandomType(cell.possibleTypes);
        cell.finalType = chosen;
        cell.possibleTypes = new List<TileType> { chosen };
        cell.collapsed = true;

        // 将当前格子加入传播队列
        propagationQueue.Enqueue(new Vector2Int(x, y));
    }

    // 根据权重随机选择一个类型
    private TileType ChooseRandomType(List<TileType> candidates)
    {
        // 计算权重总和
        int totalWeight = 0;
        foreach (var type in candidates)
        {
            int idx = tileTypes.IndexOf(type);
            totalWeight += weights[idx];
        }

        int random = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var type in candidates)
        {
            int idx = tileTypes.IndexOf(type);
            cumulative += weights[idx];
            if (random < cumulative)
                return type;
        }
        return candidates[0]; // 后备
    }

    // 传播约束（基于队列）
    private bool Propagate()
    {
        while (propagationQueue.Count > 0)
        {
            Vector2Int pos = propagationQueue.Dequeue();
            var cell = wave[pos.x, pos.y];

            // 检查四个方向
            CheckNeighbor(pos, pos.x + 1, pos.y); // 右
            CheckNeighbor(pos, pos.x - 1, pos.y); // 左
            CheckNeighbor(pos, pos.x, pos.y + 1); // 上
            CheckNeighbor(pos, pos.x, pos.y - 1); // 下
        }
        return true;
    }

    // 检查单个邻居并更新其可能类型
    private void CheckNeighbor(Vector2Int sourcePos, int nx, int ny)
    {
        // 边界检查
        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            return;

        var sourceCell = wave[sourcePos.x, sourcePos.y];
        var neighborCell = wave[nx, ny];

        // 如果邻居已坍缩，无需更新
        if (neighborCell.collapsed)
            return;

        // 获取源格子的所有可能类型（如果已坍缩则只有一种）
        List<TileType> sourcePossibilities = sourceCell.possibleTypes;

        // 计算邻居的新可能类型：必须与源格子的至少一种类型兼容
        List<TileType> newPossibilities = new List<TileType>();
        foreach (var neighborType in neighborCell.possibleTypes)
        {
            foreach (var sourceType in sourcePossibilities)
            {
                // 检查兼容性
                if (compatibleNeighbors[sourceType].Contains(neighborType))
                {
                    newPossibilities.Add(neighborType);
                    break; // 找到一个兼容即可
                }
            }
        }

        // 如果没有剩余类型，说明产生矛盾
        if (newPossibilities.Count == 0)
        {
            Debug.LogError($"矛盾: 格子({nx},{ny}) 无合法类型");
            propagationQueue.Clear();
            return;
        }

        // 如果可能类型减少，更新并重新加入队列
        if (newPossibilities.Count < neighborCell.possibleTypes.Count)
        {
            neighborCell.possibleTypes = newPossibilities;
            propagationQueue.Enqueue(new Vector2Int(nx, ny));
        }
    }
}

/// <summary>
/// 相邻规则序列化类（在Inspector中配置）
/// </summary>
[System.Serializable]
public class TileAdjacencyRule
{
    public TileType type;                     // 当前类型
    public List<TileType> allowedNeighbors;   // 允许相邻的类型列表
}