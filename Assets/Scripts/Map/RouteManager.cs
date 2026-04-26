using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RouteManager : MonoBehaviour
{
    public static RouteManager Instance;

    [Header("生成器")]
    public RouteMapGenerator mapGenerator;
    [SerializeField]public int seed;

    public List<List<RouteNode>> CurrentMap { get; private set; }
    public RouteNode CurrentNode { get; private set; }      // 当前所在的节点
    public RouteNode NextNode { get; private set; }         // 玩家选择的下一节点

    public int currentLayer;
    private int currentIndex;

    void Awake()
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
        //MainMenu.OnNewGameRequested += StartNewRunFromMenu;
    }
    /// <summary>
    /// 开始新的一轮肉鸽（生成新地图）
    /// </summary>
    public void StartNewRun()
    {
        seed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(seed);
        CurrentMap = mapGenerator.GenerateMap(seed);
        currentLayer = 0;
        currentIndex = 0;
        CurrentNode = CurrentMap[0][0];
        CurrentNode.isVisited = true;
    }

    /// <summary>
    /// 玩家点击选择了下一个节点
    /// </summary>
    public void SelectNextNode(RouteNode node)
    {
        // 校验是否合法：必须位于当前节点的连接列表中
        if (!CurrentNode.nextIndices.Contains(node.index))
        {
            Debug.LogWarning("无法连接到该节点");
            return;
        }

        NextNode = node;
        // 进入节点（战斗、事件等）
        EnterNode(NextNode);
    }

    private void EnterNode(RouteNode node)
    {
        node.isVisited = true;
        currentLayer = node.layer;
        currentIndex = node.index;

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
        }
    }

    private void StartCombat(RouteNode node)
    {
        // 通过 CombatData 静态类传递节点类型（或者直接调用 GameManager）
        CombatData.CurrentNodeType = node.nodeType;

        // 通知 GameManager 开始战斗
        if (GameManager.Instance != null)
            GameManager.Instance.StartCombat(node.nodeType);
        else
            Debug.LogError("GameManager 未找到！");

        // 隐藏路线选择UI，显示战斗HUD（由UIManager负责）
        UIManager.Instance.ShowBattleUI();
    }

    private void StartEvent(RouteNode node) { /* 打开事件UI */ }
    private void OpenShop(RouteNode node) { /* 打开商店UI */ }
    private void Rest() { /* 回复生命 */ }
    private void GetTreasure() { /* 获得随机宝物 */ }

    /// <summary>
    /// 战斗结束后调用（战斗胜利后）
    /// </summary>
    public void OnCombatVictory()
    {
        if (CurrentNode.nodeType == NodeType.Boss)
        {
            Debug.Log("游戏通关！");
            return;
        }

        // 获取下一层的所有节点（注意：只有 CurrentNode.nextIndices 中指定的节点才是可选的）
        List<RouteNode> nextLayerNodes = new List<RouteNode>();
        foreach (int idx in CurrentNode.nextIndices)
        {
            // 注意边界检查
            if (CurrentMap.Count > CurrentNode.layer + 1 && idx < CurrentMap[CurrentNode.layer + 1].Count)
                nextLayerNodes.Add(CurrentMap[CurrentNode.layer + 1][idx]);
        }

        /*// 显示路线选择UI，并且把可选节点列表传进去
        UIManager.Instance.ShowRouteUI();
        RouteMapUI routeMapUI = FindObjectOfType<RouteMapUI>();
        if (routeMapUI != null)
            routeMapUI.ShowNodes(nextLayerNodes, CurrentNode);*/
    }
    /// <summary>
    /// 开始当前节点（战斗、事件等），由场景加载完成后调用
    /// </summary>
    public void StartCurrentNode()
    {
        if (CurrentNode == null)
        {
            Debug.LogError("RouteManager: CurrentNode is null! Did you forget to call StartNewRun?");
            return;
        }
        Debug.Log($"RouteManager: 开始当前节点 {CurrentNode.nodeType} (层 {CurrentNode.layer}, 索引 {CurrentNode.index})");
        EnterNode(CurrentNode);
    }
}