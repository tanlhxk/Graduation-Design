using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using DG.Tweening;

public class MovementSystem : MonoBehaviour
{
    public static MovementSystem Instance { get; private set; }
    public TurnManager turnManager;

    [Header("高亮材质")]
    public Color moveRangeColor = Color.blue;
    public Color attackRangeColor = Color.red;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // 计算可移动范围（广度优先搜索）
    public List<Tile> GetMoveableTiles(Unit unit , int moveRange)
    {
        if (unit == null || unit.currentTile == null) return new List<Tile>();

        List<Tile> result = new List<Tile>();
        Queue<Tile> queue = new Queue<Tile>();
        Dictionary<Tile, int> distanceMap = new Dictionary<Tile, int>();

        // 从当前格子开始
        Tile startTile = unit.currentTile;
        queue.Enqueue(startTile);
        distanceMap[startTile] = 0;

        // BFS遍历
        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();
            int currentDist = distanceMap[current];

            // 如果在移动范围内，加入结果
            if (currentDist <= moveRange)
            {
                // 当前格子可以移动（除非被其他单位占据）
                if (current == startTile || current.IsWalkable())
                {
                    result.Add(current);
                }
            }

            // 如果已经达到最大移动距离，不再扩展
            if (currentDist >= moveRange)
                continue;

            // 检查四个方向的邻居
            Vector2Int[] directions = {
                new Vector2Int(0, 1),   // 上
                new Vector2Int(1, 0),   // 右
                new Vector2Int(0, -1),  // 下
                new Vector2Int(-1, 0)   // 左
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = current.gridPos + dir;
                Tile neighbor = GridManager.Instance.GetTile(neighborPos);

                if (neighbor == null) continue;

                // 如果邻居是障碍物且不是单位当前所在位置，跳过
                if (neighbor.type == TileType.Obstacle && neighbor != startTile)
                    continue;

                // 如果邻居被其他单位占据且不是起点，跳过
                if (neighbor.occupyingUnit != null && neighbor != startTile)
                    continue;

                // 如果还没访问过
                if (!distanceMap.ContainsKey(neighbor))
                {
                    distanceMap[neighbor] = currentDist + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return result;
    }

    //寻路（A*算法简化版）
    public List<Tile> FindPath(Unit unit, Tile startTile, Tile targetTile)
    {
        if (startTile == null || targetTile == null) return new List<Tile>();

        // 使用A*算法寻路
        List<Tile> openSet = new List<Tile> { startTile };
        Dictionary<Tile, Tile> cameFrom = new Dictionary<Tile, Tile>();
        Dictionary<Tile, int> gScore = new Dictionary<Tile, int>();
        Dictionary<Tile, int> fScore = new Dictionary<Tile, int>();

        gScore[startTile] = 0;
        fScore[startTile] = Heuristic(startTile, targetTile);

        while (openSet.Count > 0)
        {
            // 获取fScore最小的节点
            Tile current = openSet.OrderBy(t => fScore.ContainsKey(t) ? fScore[t] : int.MaxValue).First();

            if (current == targetTile)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);

            // 检查四个方向
            Vector2Int[] directions = {
                new Vector2Int(0, 1), new Vector2Int(1, 0),
                new Vector2Int(0, -1), new Vector2Int(-1, 0)
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = current.gridPos + dir;
                Tile neighbor = GridManager.Instance.GetTile(neighborPos);

                if (neighbor == null) continue;

                // 跳过障碍物
                if (neighbor.type == TileType.Obstacle && neighbor != startTile)
                    continue;

                // 计算到邻居的距离
                int tentativeGScore = gScore[current] + 1;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, targetTile);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return new List<Tile>(); // 无路径
    }

    // 启发函数（曼哈顿距离）
    int Heuristic(Tile a, Tile b)
    {
        return Mathf.Abs(a.gridPos.x - b.gridPos.x) + Mathf.Abs(a.gridPos.y - b.gridPos.y);
    }

    // 重建路径
    List<Tile> ReconstructPath(Dictionary<Tile, Tile> cameFrom, Tile current)
    {
        List<Tile> path = new List<Tile> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    // 移动执行
    public IEnumerator MoveUnitAlongPath(Unit unit, List<Tile> path)
    {
        // 检查路径有效性
        if (path == null || path.Count < 2)
            yield break;   // 无需移动，直接退出

        // 注意：此时 unit.CurrentStateEnum 应为 Moving（由状态机保证）
        // 遍历路径中的每一个目标格子（跳过起点）
        for (int i = 1; i < path.Count; i++)
        {
            Tile nextTile = path[i];
            Vector3 targetPos = GridManager.Instance.GridToWorld(nextTile.gridPos);

            // 计算移动耗时
            float duration = Vector3.Distance(unit.transform.position, targetPos) / unit.moveSpeed;

            // 执行 DOTween 移动并等待完成
            unit.transform.DOKill(); // 清除可能残留的动画
            Tween moveTween = unit.transform.DOMove(targetPos, duration).SetEase(Ease.OutSine);
            yield return moveTween.WaitForCompletion();

            // 修正位置（防止浮点数误差）
            unit.transform.position = targetPos;

            // 更新网格管理器中的单位占据信息
            GridManager.Instance.SetUnitOnTile(unit, nextTile.gridPos);
        }

        // 移动完成，协程自然结束。状态机会在 UnitMovingState 中处理后续的切换状态和回合通知
    }

    // 高亮可移动范围
    public void HighlightMoveRange(List<Tile> tiles)
    {
        foreach (var tile in tiles)
        {
            // 设置高亮材质（简化：直接改变颜色）
            SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = moveRangeColor;
            }
        }
    }

    // 清除高亮
    public void ClearHighlights(Dictionary<Vector2Int, Tile> tileDict)
    {
        // 恢复所有格子颜色
        // 需要遍历所有格子恢复原始颜色
        foreach (var tile in tileDict.Values)
        {
            SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
            if (renderer != null && tile.type == TileType.Walkable)
            {
                renderer.color = Color.white;
            }
        }
    }
}