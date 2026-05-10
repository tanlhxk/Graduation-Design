using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum NodeType
{
    Start,       // 起点
    Combat,
    EliteCombat,
    Event,
    Shop,
    Rest,
    Treasure,
    Boss,
    Empty        // 空房间
}
[System.Serializable]
public class GridNode
{
    public Vector2Int gridPos;
    public NodeType nodeType;
    public bool isVisited;
    public bool isLocked;
    public List<Vector2Int> neighbors;

    public GridNode(int x, int y, NodeType type)
    {
        gridPos = new Vector2Int(x, y);
        nodeType = type;
        isVisited = false;
        isLocked = false;
        neighbors = new List<Vector2Int>();
    }
}
public class GridMapGenerator : MonoBehaviour
{
    [Header("地图尺寸")]
    public int width = 8;
    public int height = 6;

    [Header("房间数量范围")]
    public int minRooms = 8;
    public int maxRooms = 15;

    [Header("房间类型权重（总和100）")]
    public int combatWeight = 40;
    public int eliteWeight = 10;
    public int eventWeight = 15;
    public int shopWeight = 10;
    public int restWeight = 10;
    public int treasureWeight = 5;

    [Header("额外连接概率（增加环路）")]
    public float extraEdgeProbability = 0.2f;

    [Header("强制房间")]
    public bool forceStartAtCorner = true;
    public bool forceBossAtOppositeCorner = true;

    // 输出
    public List<GridNode> AllRooms { get; private set; }
    public Dictionary<Vector2Int, GridNode> RoomDict { get; private set; }

    /// <summary>
    /// 生成地图，返回房间列表，每个房间的邻居只包含相邻（上下左右）的房间
    /// </summary>
    public List<GridNode> GenerateMap(int seed)
    {
        Random.InitState(seed);
        AllRooms = new List<GridNode>();
        RoomDict = new Dictionary<Vector2Int, GridNode>();

        // 1. 生成房间位置（连通，包含起点）
        HashSet<Vector2Int> roomPositions = GenerateConnectedRoomPositions();

        // 2. 确定起点和Boss位置
        Vector2Int startPos = forceStartAtCorner ? new Vector2Int(0, 0) : /* 实际上起点已经在positions中，我们主动找出 */ roomPositions.First();
        // 从非起点的房间中随机选一个作为Boss
        var nonStartPositions = roomPositions.Where(p => p != startPos).ToList();
        if (nonStartPositions.Count == 0)
        {
            Debug.LogError("没有足够房间放置Boss！");
            return null;
        }
        //Vector2Int bossPos = nonStartPositions[Random.Range(0, nonStartPositions.Count)];//随机boss房
        Vector2Int bossPos = nonStartPositions.OrderByDescending(p => Mathf.Abs(p.x - startPos.x) + Mathf.Abs(p.y - startPos.y)).First();//最远boss房

        // 3. 创建节点，根据是否为起点/Boss分配类型
        foreach (var pos in roomPositions)
        {
            NodeType type;
            if (pos == startPos)
                type = NodeType.Start;
            else if (pos == bossPos)
                type = NodeType.Boss;
            else
                type = GetRandomNodeType();  // 使用随机权重方法

            GridNode node = new GridNode(pos.x, pos.y, type);
            AllRooms.Add(node);
            RoomDict[pos] = node;
        }

        // 4. 构建相邻房间之间的边（上下左右）
        List<Edge> adjacentEdges = BuildAdjacentEdges();

        // 5. 生成最小生成树 + 额外边（保证连通且美观）
        List<Edge> finalEdges = KruskalMST(adjacentEdges);
        AddExtraEdges(adjacentEdges, finalEdges);

        // 6. 根据最终边集设置邻居列表
        foreach (var node in AllRooms)
            node.neighbors.Clear();

        foreach (var edge in finalEdges)
        {
            edge.from.neighbors.Add(edge.to.gridPos);
            edge.to.neighbors.Add(edge.from.gridPos);
        }

        // 去重
        foreach (var node in AllRooms)
            node.neighbors = node.neighbors.Distinct().ToList();

        return AllRooms;
    }

    /// <summary>
    /// 使用 Prim 算法生成连通的房间位置集合（保证所有房间可达）
    /// </summary>
    private HashSet<Vector2Int> GenerateConnectedRoomPositions()
    {
        HashSet<Vector2Int> positions = new HashSet<Vector2Int>();

        // 确定起点位置（仍然可以固定在角落或随机）
        Vector2Int startPos = forceStartAtCorner ? new Vector2Int(0, 0) : new Vector2Int(Random.Range(0, width), Random.Range(0, height));
        positions.Add(startPos);

        int targetCount = Random.Range(minRooms, maxRooms + 1);
        int maxAttempts = 2000;
        int attempts = 0;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (positions.Count < targetCount && attempts < maxAttempts)
        {
            // 随机选择一个现有房间作为扩展起点
            var roomList = positions.ToList();
            var baseRoom = roomList[Random.Range(0, roomList.Count)];

            // 随机尝试一个方向
            Vector2Int dir = dirs[Random.Range(0, dirs.Length)];
            Vector2Int newPos = baseRoom + dir;

            if (newPos.x >= 0 && newPos.x < width && newPos.y >= 0 && newPos.y < height && !positions.Contains(newPos))
            {
                positions.Add(newPos);
            }

            attempts++;
        }

        // 如果房间数仍不足，尝试直接添加邻居填充（兜底）
        if (positions.Count < targetCount)
        {
            foreach (var room in positions.ToList())
            {
                foreach (var dir in dirs)
                {
                    Vector2Int candidate = room + dir;
                    if (candidate.x >= 0 && candidate.x < width && candidate.y >= 0 && candidate.y < height && !positions.Contains(candidate))
                    {
                        positions.Add(candidate);
                        if (positions.Count >= targetCount) break;
                    }
                }
                if (positions.Count >= targetCount) break;
            }
        }

        return positions;
    }
    private NodeType GetRandomNodeType()
    {
        int total = combatWeight + eliteWeight + eventWeight + shopWeight + restWeight + treasureWeight;
        int rand = Random.Range(0, total);
        if (rand < combatWeight) return NodeType.Combat;
        rand -= combatWeight;
        if (rand < eliteWeight) return NodeType.EliteCombat;
        rand -= eliteWeight;
        if (rand < eventWeight) return NodeType.Event;
        rand -= eventWeight;
        if (rand < shopWeight) return NodeType.Shop;
        rand -= shopWeight;
        if (rand < restWeight) return NodeType.Rest;
        return NodeType.Treasure;
    }

    /// <summary>
    /// 构建所有相邻房间之间的边（曼哈顿距离 == 1）
    /// </summary>
    private List<Edge> BuildAdjacentEdges()
    {
        List<Edge> edges = new List<Edge>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var node in AllRooms)
        {
            foreach (var dir in dirs)
            {
                Vector2Int neighborPos = node.gridPos + dir;
                if (RoomDict.TryGetValue(neighborPos, out GridNode neighbor))
                {
                    // 避免重复添加同一条边（例如无向图只加一次）
                    if (node.gridPos.GetHashCode() < neighborPos.GetHashCode())
                    {
                        edges.Add(new Edge { from = node, to = neighbor, weight = 1 });
                    }
                }
            }
        }
        return edges;
    }

    private List<Edge> KruskalMST(List<Edge> edges)
    {
        if (edges.Count == 0) return new List<Edge>();
        var sortedEdges = edges.OrderBy(e => e.weight).ToList();
        Dictionary<GridNode, GridNode> parent = new Dictionary<GridNode, GridNode>();
        foreach (var node in AllRooms)
            parent[node] = node;

        List<Edge> mst = new List<Edge>();
        foreach (var edge in sortedEdges)
        {
            GridNode rootA = Find(parent, edge.from);
            GridNode rootB = Find(parent, edge.to);
            if (rootA != rootB)
            {
                parent[rootA] = rootB;
                mst.Add(edge);
            }
        }

        // 由于房间生成时已保证连通，这里理论上 mst.Count == AllRooms.Count - 1
        if (mst.Count != AllRooms.Count - 1)
        {
            Debug.LogError("MST 失败：房间图不连通，请调整房间数量或地图尺寸！");
        }
        return mst;
    }

    private GridNode Find(Dictionary<GridNode, GridNode> parent, GridNode node)
    {
        if (parent[node] != node)
            parent[node] = Find(parent, parent[node]);
        return parent[node];
    }
    private void AddExtraEdges(List<Edge> allEdges, List<Edge> mstEdges)
    {
        foreach (var edge in allEdges)
        {
            if (!mstEdges.Contains(edge) && Random.value < extraEdgeProbability)
            {
                mstEdges.Add(edge);
            }
        }
    }

    private struct Edge
    {
        public GridNode from, to;
        public int weight;
    }
}