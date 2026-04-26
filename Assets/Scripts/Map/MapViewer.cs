using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapViewer : MonoBehaviour
{
    public static MapViewer Instance;
    [Header("UI组件")]
    public GameObject nodeButtonPrefab;
    public GameObject linePrefab;
    public RectTransform mapContent;
    public Transform Background;
    public ScrollRect scrollRect;
    [SerializeField]private MapSetImage mapSetImage;

    private Dictionary<RouteNode, Button> nodeButtons = new Dictionary<RouteNode, Button>();
    private bool isOpen = false;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))   // 按M键打开/关闭小地图
        {
            if (isOpen) CloseMap();
            else OpenMap();
        }
    }
    public void OpenMap()
    {
        if (RouteManager.Instance == null || RouteManager.Instance.CurrentMap == null) return;
        RefreshMapUI(RouteManager.Instance.CurrentMap);
        Background.gameObject.SetActive(true);
        isOpen = true;

        if (scrollRect != null) scrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);
    }

    public void CloseMap()
    {
        Background.gameObject.SetActive(false);
        isOpen = false;
    }

    private void RefreshMapUI(List<List<RouteNode>> layers)
    {
        // 清除旧内容
        foreach (Transform child in mapContent) Destroy(child.gameObject);
        nodeButtons.Clear();

        // 1. 生成所有节点按钮
        foreach (var layer in layers)
            foreach (var node in layer)
                CreateNodeButton(node);

        // 2. 生成连线
        for (int i = 0; i < layers.Count - 1; i++)
            foreach (var node in layers[i])
                foreach (int nextIdx in node.nextIndices)
                    if (nextIdx < layers[i + 1].Count)
                        CreateLine(node.position, layers[i + 1][nextIdx].position);
    }

    private void CreateNodeButton(RouteNode node)
    {
        GameObject btnObj = Instantiate(nodeButtonPrefab, mapContent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(node.position.x, node.position.y);

        // 设置图标/颜色（根据节点类型和访问状态）
        Image img = btnObj.GetComponent<Image>();
        img.sprite = mapSetImage.GetSprite(node.nodeType);
        Button btn = btnObj.GetComponent<Button>();
        bool interactable = IsNodeSelectable(node);
        btn.interactable = interactable;
        btn.onClick.AddListener(() => OnNodeClicked(node));
        nodeButtons[node] = btn;
    }

    private bool IsNodeSelectable(RouteNode node)
    {
        if (node.isVisited) return false;
        RouteNode current = RouteManager.Instance.CurrentNode;
        if (current == null) return false;
        // 只能选择当前节点直接相连的下一层节点
        return current.nextIndices.Contains(node.index) && node.layer == current.layer + 1;
    }

    private void OnNodeClicked(RouteNode node)
    {
        if (!IsNodeSelectable(node)) return;
        RouteManager.Instance.SelectNextNode(node);
        CloseMap(); // 选择后关闭地图
    }

    private void CreateLine(Vector2 start, Vector2 end)
    {
        GameObject lineObj = Instantiate(linePrefab, mapContent);
        RectTransform rect = lineObj.GetComponent<RectTransform>();
        Vector2 dir = (end - start).normalized;
        float distance = Vector2.Distance(start, end);
        rect.sizeDelta = new Vector2(distance, 3f);   // 线宽3像素
        rect.anchoredPosition = start + dir * distance * 0.5f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.rotation = Quaternion.Euler(0, 0, angle);
    }
    private void AdjustContentSize(Vector2 minPos, Vector2 maxPos)
    {
        // 获取节点按钮的尺寸（假设预制体宽100，高100，可以根据实际调整）
        float nodeWidth = 100f;
        float nodeHeight = 100f;

        float width = maxPos.x - minPos.x + nodeWidth;
        float height = maxPos.y - minPos.y + nodeHeight;

        mapContent.sizeDelta = new Vector2(width, height);

        // 调整 Content 的锚点位置，使得地图左上角对齐到世界原点（方便滚动）
        // 常见做法：将 Content 锚点设为 (0,1) 左上角，然后设置 anchoredPosition
        mapContent.anchoredPosition = new Vector2(-minPos.x + nodeWidth / 2, -maxPos.y + nodeHeight / 2);
        // 上述公式根据锚点不同会有变化，如果锚点在中心，则需要重新计算，更简单的方法是保持锚点在中心，
        // 然后通过设置 anchoredPosition 使内容居中。此处不深入，可以根据实际调试。
        // 实际项目中可以直接用 ScrollRect 并手动设置 normalizedPosition 为 (0.5f, 0.5f) 让内容居中。
    }
}