using System.Collections.Generic;
using System.Linq;
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

    [Header("连通性后处理")]
    public bool enableConnectivityPostProcess = true;
    public List<Vector2Int> criticalPoints;      // 必须连通的关键点（如起点、出口）
    public List<TileType> walkableTypes;          // 哪些类型被视为可行走（用于连通性判断）
    public Vector2Int mapSize;

    public void GenerateAndBuildMap(int seed)
    {
        TileType[,] mapData = RunWFC(seed);   // WFC算法返回二维数组
        GridManager.Instance.BuildGridFromData(mapData);
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
    public TileType[,] RunWFC(int seed)
    {
        // 锁定随机种子
        Random.InitState(seed);
        // 初始化兼容性字典
        InitializeCompatibility();

        // 初始化波函数
        wave = new WFCCell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                wave[x, y] = new WFCCell(tileTypes);

        propagationQueue = new Queue<Vector2Int>();

        // 强制边界为障碍物
        for (int x = 0; x < width; x++)
        {
            ForceCollapseCell(x, 0, TileType.Obstacle);
            ForceCollapseCell(x, height - 1, TileType.Obstacle);
        }
        for (int y = 0; y < height; y++)
        {
            ForceCollapseCell(0, y, TileType.Obstacle);
            ForceCollapseCell(width - 1, y, TileType.Obstacle);
        }

        // 强制中心区域可走
        ForceCollapseCell(width / 2, height / 2, TileType.Walkable);
        ForceCollapseCell(1, 1, TileType.Walkable);

        // 主循环
        while (true)
        {
            Vector2Int? target = FindMinEntropyCell();
            if (target == null)
                break;

            CollapseCell(target.Value.x, target.Value.y);
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

        // 后处理：确保关键点连通（基于原始 Walkable/Obstacle/Exit）
        if (enableConnectivityPostProcess && criticalPoints.Count >= 2)
        {
            EnsureConnectivity(ref result);
        }
        RemoveSmallObstacleClusters(ref result);
        return result;
    }

    // 初始化兼容性规则
    private void InitializeCompatibility()
    {
        compatibleNeighbors = new Dictionary<TileType, HashSet<TileType>>();

        foreach (var type in tileTypes)
        {
            if (!compatibleNeighbors.ContainsKey(type))
                compatibleNeighbors[type] = new HashSet<TileType>();
        }

        foreach (var rule in adjacencyRules)
        {
            if (!compatibleNeighbors.ContainsKey(rule.type)) continue;
            compatibleNeighbors[rule.type].Clear();
            foreach (var neighbor in rule.allowedNeighbors)
                compatibleNeighbors[rule.type].Add(neighbor);
        }
    }

    // 找到熵最小的未坍缩格子
    private Vector2Int? FindMinEntropyCell()
    {
        int minEntropy = int.MaxValue;
        List<Vector2Int> candidates = new List<Vector2Int>();

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
                        candidates.Clear();
                        candidates.Add(new Vector2Int(x, y));
                    }
                    else if (entropy == minEntropy)
                        candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        if (candidates.Count == 0) return null;
        int randomIndex = Random.Range(0, candidates.Count);
        return candidates[randomIndex];
    }

    // 坍缩指定格子
    private void CollapseCell(int x, int y)
    {
        var cell = wave[x, y];
        TileType chosen = ChooseRandomType(cell.possibleTypes);
        cell.finalType = chosen;
        cell.possibleTypes = new List<TileType> { chosen };
        cell.collapsed = true;
        propagationQueue.Enqueue(new Vector2Int(x, y));
    }

    // 根据权重随机选择一个类型
    private TileType ChooseRandomType(List<TileType> candidates)
    {
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
        return candidates[0];
    }

    // 传播约束
    private bool Propagate()
    {
        while (propagationQueue.Count > 0)
        {
            Vector2Int pos = propagationQueue.Dequeue();
            CheckNeighbor(pos, pos.x + 1, pos.y);
            CheckNeighbor(pos, pos.x - 1, pos.y);
            CheckNeighbor(pos, pos.x, pos.y + 1);
            CheckNeighbor(pos, pos.x, pos.y - 1);
        }
        return true;
    }

    // 检查单个邻居并更新其可能类型
    private void CheckNeighbor(Vector2Int sourcePos, int nx, int ny)
    {
        if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            return;

        var sourceCell = wave[sourcePos.x, sourcePos.y];
        var neighborCell = wave[nx, ny];

        if (neighborCell.collapsed)
            return;

        List<TileType> sourcePossibilities = sourceCell.possibleTypes;
        List<TileType> newPossibilities = new List<TileType>();

        foreach (var neighborType in neighborCell.possibleTypes)
        {
            foreach (var sourceType in sourcePossibilities)
            {
                if (compatibleNeighbors[sourceType].Contains(neighborType))
                {
                    newPossibilities.Add(neighborType);
                    break;
                }
            }
        }

        if (newPossibilities.Count == 0)
        {
            Debug.LogError($"矛盾: 格子({nx},{ny}) 无合法类型");
            propagationQueue.Clear();
            return;
        }

        if (newPossibilities.Count < neighborCell.possibleTypes.Count)
        {
            neighborCell.possibleTypes = newPossibilities;
            propagationQueue.Enqueue(new Vector2Int(nx, ny));
        }
    }

    private void ForceCollapseCell(int x, int y, TileType type)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        var cell = wave[x, y];
        cell.finalType = type;
        cell.possibleTypes = new List<TileType> { type };
        cell.collapsed = true;
        propagationQueue.Enqueue(new Vector2Int(x, y));
    }

    // -------------------- 连通性后处理 --------------------
    private void EnsureConnectivity(ref TileType[,] map)
    {
        // 将关键点坐标限制在地图范围内
        List<Vector2Int> points = new List<Vector2Int>();
        foreach (var pt in criticalPoints)
        {
            if (pt.x >= 0 && pt.x < width && pt.y >= 0 && pt.y < height)
                points.Add(pt);
        }
        if (points.Count < 2) return;

        // 可行走类型集合（包括 Exit）
        HashSet<TileType> walkableSet = new HashSet<TileType>(walkableTypes);
        if (!walkableSet.Contains(TileType.Exit))
            walkableSet.Add(TileType.Exit); // 出口也视为可行走

        int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // BFS 从第一个关键点开始
            bool[,] visited = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Vector2Int start = points[0];
            if (IsWalkable(map[start.x, start.y], walkableSet))
            {
                visited[start.x, start.y] = true;
                queue.Enqueue(start);
            }

            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (var dir in dirs)
                {
                    Vector2Int neighbor = current + dir;
                    if (neighbor.x >= 0 && neighbor.x < width && neighbor.y >= 0 && neighbor.y < height &&
                        !visited[neighbor.x, neighbor.y] &&
                        IsWalkable(map[neighbor.x, neighbor.y], walkableSet))
                    {
                        visited[neighbor.x, neighbor.y] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // 检查所有关键点是否连通
            bool allConnected = true;
            List<Vector2Int> unconnected = new List<Vector2Int>();
            foreach (var pt in points)
            {
                if (!visited[pt.x, pt.y])
                {
                    allConnected = false;
                    unconnected.Add(pt);
                }
            }
            if (allConnected) break;

            // 打通未连通的关键点
            foreach (var pt in unconnected)
            {
                Vector2Int target = FindNearestVisited(pt, visited);
                if (target != new Vector2Int(-1, -1))
                {
                    List<Vector2Int> path = FindPathAStar(pt, target, map, walkableSet);
                    if (path != null)
                    {
                        foreach (var cell in path)
                        {
                            if (!IsWalkable(map[cell.x, cell.y], walkableSet))
                            {
                                TileType newType = ChooseTypeByNeighbors(cell, map, walkableSet);
                                map[cell.x, cell.y] = newType;
                            }
                        }
                    }
                }
            }
        }
    }

    private bool IsWalkable(TileType type, HashSet<TileType> walkableSet) => walkableSet.Contains(type);

    private Vector2Int FindNearestVisited(Vector2Int from, bool[,] visited)
    {
        int minDist = int.MaxValue;
        Vector2Int nearest = new Vector2Int(-1, -1);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y])
                {
                    int dist = Mathf.Abs(x - from.x) + Mathf.Abs(y - from.y);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = new Vector2Int(x, y);
                    }
                }
            }
        }
        return nearest;
    }

    private List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int goal, TileType[,] map, HashSet<TileType> walkableSet)
    {
        var openSet = new SimplePriorityQueue();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float>();
        Dictionary<Vector2Int, float> fScore = new Dictionary<Vector2Int, float>();

        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        openSet.Enqueue(start, fScore[start]);

        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var dir in dirs)
            {
                Vector2Int neighbor = current + dir;
                if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height)
                    continue;

                float moveCost = 1f;
                if (!IsWalkable(map[neighbor.x, neighbor.y], walkableSet))
                    moveCost += 10f;

                float tentativeG = gScore[current] + moveCost;
                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float f = tentativeG + Heuristic(neighbor, goal);
                    fScore[neighbor] = f;
                    if (!openSet.UnorderedItems.Any(item => item.position == neighbor))
                        openSet.Enqueue(neighbor, f);
                }
            }
        }
        return null;
    }

    private TileType ChooseTypeByNeighbors(Vector2Int pos, TileType[,] map, HashSet<TileType> walkableSet)
    {
        Dictionary<TileType, int> counts = new Dictionary<TileType, int>();
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
                               new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1) };

        foreach (var dir in dirs)
        {
            Vector2Int n = pos + dir;
            if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height)
            {
                TileType type = map[n.x, n.y];
                if (walkableSet.Contains(type))
                {
                    if (!counts.ContainsKey(type)) counts[type] = 0;
                    counts[type]++;
                }
            }
        }

        if (counts.Count > 0)
        {
            int maxCount = 0;
            TileType best = walkableTypes[0];
            foreach (var kv in counts)
            {
                if (kv.Value > maxCount)
                {
                    maxCount = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }
        return walkableTypes[0];
    }

    private float Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 移除所有大小小于 2x2 (即格子数 < 4) 的障碍物连通块，将其变为 Walkable。
    /// </summary>
    private void RemoveSmallObstacleClusters(ref TileType[,] map)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        bool[,] visited = new bool[w, h];

        // 四个方向（上下左右）用于连通性判断（四连通即可，障碍物块不需要对角连通）
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                // 只处理未访问的障碍物
                if (map[x, y] == TileType.Obstacle && !visited[x, y])
                {
                    // BFS 收集当前连通块
                    List<Vector2Int> cluster = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int cur = queue.Dequeue();
                        cluster.Add(cur);

                        foreach (var dir in dirs)
                        {
                            int nx = cur.x + dir.x;
                            int ny = cur.y + dir.y;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h &&
                                !visited[nx, ny] && map[nx, ny] == TileType.Obstacle)
                            {
                                visited[nx, ny] = true;
                                queue.Enqueue(new Vector2Int(nx, ny));
                            }
                        }
                    }

                    // 如果连通块大小 < 4（不足 2x2），则全部改为 Walkable
                    if (cluster.Count < 4)
                    {
                        foreach (var pos in cluster)
                        {
                            map[pos.x, pos.y] = TileType.Walkable;
                        }
                    }
                }
            }
        }

        // 可选：重新强制边界为 Obstacle，避免因移除小集群导致边界出现缺口
        for (int i = 0; i < w; i++)
        {
            map[i, 0] = TileType.Obstacle;
            map[i, h - 1] = TileType.Obstacle;
        }
        for (int j = 0; j < h; j++)
        {
            map[0, j] = TileType.Obstacle;
            map[w - 1, j] = TileType.Obstacle;
        }
    }
}

/// <summary>
/// 相邻规则序列化类（在Inspector中配置）
/// </summary>
[System.Serializable]
public class TileAdjacencyRule
{
    public TileType type;
    public List<TileType> allowedNeighbors;
}