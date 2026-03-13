using System;
using System.Collections.Generic;
using UnityEngine;

public class SimplePriorityQueue
{
    private List<(Vector2Int position, float priority)> elements = new List<(Vector2Int, float)>();

    public int Count => elements.Count;

    // 入队
    public void Enqueue(Vector2Int pos, float priority)
    {
        elements.Add((pos, priority));
    }

    // 出队 (返回优先级最小的)
    public Vector2Int Dequeue()
    {
        int bestIndex = 0;
        for (int i = 1; i < elements.Count; i++)
        {
            if (elements[i].priority < elements[bestIndex].priority)
            {
                bestIndex = i;
            }
        }
        var best = elements[bestIndex];
        elements.RemoveAt(bestIndex);
        return best.position;
    }

    // 检查是否包含某个位置
    public bool Contains(Vector2Int pos)
    {
        return elements.Exists(e => e.position == pos);
    }

    // 更新某个位置的优先级 (如果需要)
    public void Update(Vector2Int pos, float newPriority)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].position == pos)
            {
                elements[i] = (pos, newPriority);
                break;
            }
        }
    }

    public List<(Vector2Int position, float priority)> UnorderedItems
    {
        get { return elements; } // 直接返回内部列表，不进行排序
    }
}