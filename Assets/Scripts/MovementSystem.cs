using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using DG.Tweening;

public class MovementSystem : MonoBehaviour
{
    public GridManager gridManager;
    public TurnManager turnManager;

    [Header("高亮材质")]
    public Color moveRangeColor = Color.blue;
    public Color attackRangeColor = Color.red;

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
                if(gridManager==null)
                {
                    Debug.Log("未找到");
                }
                Tile neighbor = gridManager.GetTile(neighborPos);

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
                Tile neighbor = gridManager.GetTile(neighborPos);

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
        if (path == null || path.Count < 2)
        {
            // 移动结束后通知回合系统
            turnManager.UnitFinishedAction(unit);
            yield break;
        }

        unit.currentState = UnitState.Moving;

        // 遍历路径中的每一个目标格子（跳过起点）
        for (int i = 1; i < path.Count; i++)
        {
            Tile nextTile = path[i];
            Vector3 targetPos = gridManager.GridToWorld(nextTile.gridPos);

            // --- 关键点：使用 DOTween 移动，并等待完成 ---

            // 1. 记录当前时间，用于计算移动耗时
            float journeyLength = Vector3.Distance(unit.transform.position, targetPos);
            float duration = journeyLength / unit.moveSpeed; // 建议在 Unit 类中添加 moveSpeed 属性，默认 5f
                                                             // 如果没有 moveSpeed，就用固定时间，例如 0.2f

            // 2. 执行 DOTween 移动
            // DOKill() 确保没有残留的动画干扰
            unit.transform.DOKill();

            // DOMove 移动到目标点
            // SetEase(Ease.OutSine) 让移动在结束时稍微减速，手感更自然
            Tween moveTween = unit.transform.DOMove(targetPos, duration)
                .SetEase(Ease.OutSine);

            // 3. 等待 DOTween 动画完成
            // 这里的 yield return 是等待 DOTween 结束，而不是等待帧
            yield return moveTween.WaitForCompletion();

            // --- DOTween 动画结束后，执行逻辑 ---

            // 4. 强制修正位置（防止浮点数误差）
            unit.transform.position = targetPos;

            // 5. 更新网格管理器中的单位占据信息
            // 注意：这里必须在每一格移动后都更新，否则寻路会认为单位还在原地
            gridManager.SetUnitOnTile(unit, nextTile.gridPos);

            // 6. 可选：添加脚步音效
            // AudioManager.Play("Footstep");
        }

        // --- 整个路径走完 ---
        unit.currentState = UnitState.Idle;

        // 通知回合系统该单位行动结束
        // 注意：在回合制中，通常移动结束后回合就结束了，或者进入待机状态
        turnManager.UnitFinishedAction(unit);
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