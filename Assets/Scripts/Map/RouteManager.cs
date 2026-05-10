using System.Collections.Generic;
using UnityEngine;

public class RouteManager : MonoBehaviour
{
    public static RouteManager Instance;

    [Header("地图生成器")]
    public GridMapGenerator mapGenerator;

    public List<GridNode> AllRooms { get; private set; }
    public GridNode CurrentNode { get; private set; }
    public int seed;
    public int Seed => seed;
    public bool IsFixedSeed=false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartNewRun()
    {
        if (!IsFixedSeed)
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }
        AllRooms = mapGenerator.GenerateMap(seed);
        // 找到起点房间（类型为 Start）
        CurrentNode = AllRooms.Find(r => r.nodeType == NodeType.Start);
        if (CurrentNode == null)
        {
            Debug.LogError("地图中没有起点房间！");
            return;
        }
        CurrentNode.isVisited = true;
    }

    /// <summary>
    /// 移动玩家到相邻房间（通过坐标）
    /// </summary>
    public bool MoveToNode(Vector2Int targetPos)
    {
        GridNode targetNode = mapGenerator.RoomDict.ContainsKey(targetPos) ? mapGenerator.RoomDict[targetPos] : null;
        if (targetNode == null) return false;
        // 检查是否相邻（通过 CurrentNode.neighbors 包含 targetPos）
        if (!CurrentNode.neighbors.Contains(targetPos)) return false;
        // 可选：如果房间已访问且是战斗类型，不再触发战斗（做空房间处理）
        CurrentNode.isVisited = true;
        CurrentNode = targetNode;
        EnterNode(CurrentNode);
        return true;
    }

    public void EnterNode(GridNode node)
    {
        // 如果是战斗且已经打过，转为空房间
        if (node.isVisited && (node.nodeType == NodeType.Combat || node.nodeType == NodeType.EliteCombat))
        {
            Debug.Log("重复进入战斗房间，不再战斗");
            OnRoomCleared();
            return;
        }

        switch (node.nodeType)
        {
            case NodeType.Combat:
            case NodeType.EliteCombat:
            case NodeType.Boss:
                StartCombat(node);
                break;
            case NodeType.Event:
                StartEvent(node);
                break;
            case NodeType.Shop:
                OpenShop(node);
                break;
            case NodeType.Rest:
                Rest();
                break;
            case NodeType.Treasure:
                GetTreasure();
                break;
            case NodeType.Start:
                OnRoomCleared(); // 起点直接完事
                break;
        }
    }

    private void StartCombat(GridNode node)
    {
        CombatData.CurrentNodeType = node.nodeType;
        if (GameManager.Instance != null)
            GameManager.Instance.StartCombat(node.nodeType);
        else
            Debug.LogError("GameManager not found");
        UIManager.Instance.ShowBattleUI();
    }

    private void StartEvent(GridNode node) { /* 事件逻辑 */ }
    private void OpenShop(GridNode node) { /* 商店逻辑 */ }
    private void Rest() { /* 回复 */ }
    private void GetTreasure() { /* 宝物 */ }

    public void OnRoomCleared()
    {
        if (CurrentNode.nodeType == NodeType.Boss)
        {
            Debug.Log("游戏通关！");
            return;
        }
        // 显示地图UI，让玩家选择相邻房间
        MapViewer.Instance.OpenMap();
        UIManager.Instance.ShowRouteUI();
    }

    public void StartCurrentNode()
    {
        if (CurrentNode != null)
            EnterNode(CurrentNode);
    }

    // 兼容旧调用（可选）
    public void OnCombatVictory() => OnRoomCleared();
}