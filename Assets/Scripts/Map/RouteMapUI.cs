using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RouteMapUI : MonoBehaviour
{
    public GameObject nodeButtonPrefab;
    public Transform nodesParent;

    public void ShowNodes(List<RouteNode> nextNodes, RouteNode currentNode)
    {
        // 清除旧的按钮
        foreach (Transform child in nodesParent) Destroy(child.gameObject);

        foreach (var node in nextNodes)
        {
            GameObject btnObj = Instantiate(nodeButtonPrefab, nodesParent);
            Button btn = btnObj.GetComponent<Button>();
            // 设置按钮图标、文字
            // 点击时调用 RouteManager.Instance.SelectNextNode(node);
            btn.onClick.AddListener(() => RouteManager.Instance.SelectNextNode(node));
        }
    }
}
