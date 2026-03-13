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


    public GridManager gridManager;
    public Vector2Int mapSize;

    public void GenerateAndBuildMap(int seed)
    {
        TileType[,] mapData = RunWFC(seed);   // WFC算法返回二维数组
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

        for (int x = 0; x < width; x++)
        {
            ForceCollapseCell(x, 0, TileType.Obstacle); // 底边
            ForceCollapseCell(x, height - 1, TileType.Obstacle); // 顶边
        }
        for (int y = 0; y < height; y++)
        {
            ForceCollapseCell(0, y, TileType.Obstacle); // 左边
            ForceCollapseCell(width - 1, y, TileType.Obstacle); // 右边
        }

        // 强制中心区域是可走的，确保有空间生成
        ForceCollapseCell(width / 2, height / 2, TileType.Walkable);
        ForceCollapseCell(1, 1, TileType.Walkable); // 确保起点附近可走

        // 强制出口（保持不变）
        //ForceCollapseCell(width - 2, height - 2, TileType.Exit);

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

        // 后处理：确保关键点连通
        if (enableConnectivityPostProcess && criticalPoints.Count >= 2)
        {
            EnsureConnectivity(ref result);
        }
        // 遍历地图，移除孤立的障碍物
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                TileType currentType = result[x, y];

                // 如果当前格子是障碍物
                if (currentType == TileType.Obstacle)
                {
                    bool isIsolated = true;

                    // 检查上下左右四个邻居
                    Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                    foreach (var dir in directions)
                    {
                        Vector2Int neighborPos = new Vector2Int(x + dir.x, y + dir.y);
                        TileType neighborType = result[neighborPos.x, neighborPos.y];

                        // 如果有一个邻居也是障碍物，那它就不是孤立的
                        if (neighborType == TileType.Obstacle)
                        {
                            isIsolated = false;
                            break;
                        }
                    }

                    // 如果它是孤立的，把它变成可行走（或者根据权重随机选择）
                    if (isIsolated)
                    {
                        if (isIsolated && Random.value > 0.2f)
                        {
                            result[x, y] = TileType.Walkable;
                        }
                        // 根据周围环境选择类型（比如周围都是草地，这里也变草地）
                        // result[x, y] = ChooseTypeByNeighbors(new Vector2Int(x, y), result, walkableTypes); 
                    }
                }
            }
        }
        return result;
    }

    // 初始化兼容性规则
    private void InitializeCompatibility()
    {
        compatibleNeighbors = new Dictionary<TileType, HashSet<TileType>>();

        // 先初始化所有类型，允许为空集合
        foreach (var type in tileTypes)
        {
            if (!compatibleNeighbors.ContainsKey(type))
            {
                compatibleNeighbors[type] = new HashSet<TileType>();
            }
        }

        // 只添加你显式定义的规则
        foreach (var rule in adjacencyRules)
        {
            if (!compatibleNeighbors.ContainsKey(rule.type)) continue;

            // 清空旧规则，只添加新的
            compatibleNeighbors[rule.type].Clear();
            foreach (var neighbor in rule.allowedNeighbors)
            {
                compatibleNeighbors[rule.type].Add(neighbor);
            }
        }
    }

    // 找到熵最小的未坍缩格子
    private Vector2Int? FindMinEntropyCell()
    {
        int minEntropy = int.MaxValue;
        List<Vector2Int> candidates = new List<Vector2Int>(); // 储存所有最低熵的格子

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
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        if (candidates.Count == 0) return null;

        // 从所有“最低熵”的格子中随机选一个，而不是总选第一个
        int randomIndex = Random.Range(0, candidates.Count);
        return candidates[randomIndex];
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

    private void ForceCollapseCell(int x, int y, TileType type)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        var cell = wave[x, y];
        cell.finalType = type;
        cell.possibleTypes = new List<TileType> { type };
        cell.collapsed = true;
        propagationQueue.Enqueue(new Vector2Int(x, y));
    }

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

        // 建立可行走类型的哈希集合，便于快速判断
        HashSet<TileType> walkableSet = new HashSet<TileType>(walkableTypes);

        int maxAttempts = 10; // 防止无限循环
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 从第一个关键点开始进行 BFS，标记所有可达的可行走格子
            bool[,] visited = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            Vector2Int start = points[0];
            if (IsWalkable(map[start.x, start.y], walkableSet))
            {
                visited[start.x, start.y] = true;
                queue.Enqueue(start);
            }

            // 四个方向
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

            // 检查所有关键点是否都被访问到
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

            if (allConnected)
                break; // 全部连通，退出循环

            // 修复：将每个未连通的关键点与已连通区域打通
            foreach (var pt in unconnected)
            {
                // 寻找从 pt 到任意已访问格子的最短路径（这里使用简单的曼哈顿路径）
                Vector2Int target = FindNearestVisited(pt, visited);
                if (target != new Vector2Int(-1, -1))
                {
                    // 打通从 pt 到 target 的直线路径（可用 Bresenham 画线算法）
                    List<Vector2Int> path = FindPathAStar(pt, target, map, walkableSet);
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

    // 判断格子是否可行走（根据类型）
    private bool IsWalkable(TileType type, HashSet<TileType> walkableSet)
    {
        return walkableSet.Contains(type);
    }

    // 寻找离给定点最近的已访问格子（曼哈顿距离最小）
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
        // 节点优先级队列（按 f = g + h 排序）
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

                // 计算移动到 neighbor 的代价
                float moveCost = 1f; // 基础移动代价
                                     // 如果 neighbor 原本不可行走，增加额外代价
                if (!IsWalkable(map[neighbor.x, neighbor.y], walkableSet))
                    moveCost += 10f; // 高代价，鼓励绕行

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
        return null; // 无路径
    }

    // 统计邻居类型并选择最常见的可行走类型
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
                    if (!counts.ContainsKey(type))
                        counts[type] = 0;
                    counts[type]++;
                }
            }
        }

        // 如果有计数，返回计数最高的类型；否则返回默认可行走类型
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

    private float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

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

    /*
    // 生成从 start 到 end 的曼哈顿路径（先走 x 方向，再走 y 方向）
    private List<Vector2Int> GetManhattanPath(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        int x = start.x;
        int y = start.y;
        path.Add(new Vector2Int(x, y));

        // 水平移动
        while (x != end.x)
        {
            x += (x < end.x) ? 1 : -1;
            path.Add(new Vector2Int(x, y));
        }
        // 垂直移动
        while (y != end.y)
        {
            y += (y < end.y) ? 1 : -1;
            path.Add(new Vector2Int(x, y));
        }
        return path;
    }*/
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