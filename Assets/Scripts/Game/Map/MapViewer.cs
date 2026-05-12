using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Game.RogueLike
{
    public class MapViewer : MonoBehaviour
    {
        public static MapViewer Instance;

        public GameObject nodeButtonPrefab;
        public GameObject linePrefab;
        public RectTransform mapContent;
        public Transform Background;
        [SerializeField] private MapSetImage mapSetImage;

        private Dictionary<Vector2Int, Button> nodeButtons = new Dictionary<Vector2Int, Button>();
        private bool isOpen = false;
        private Vector2Int currentPlayerPos;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (isOpen) CloseMap();
                else OpenMap();
            }
        }

        public void OpenMap()
        {
            if (RouteManager.Instance == null || RouteManager.Instance.AllRooms == null) return;
            RefreshMapUI();
            Background.gameObject.SetActive(true);
            isOpen = true;
        }

        public void CloseMap()
        {
            Background.gameObject.SetActive(false);
            isOpen = false;
        }

        public void RefreshMap(Vector2Int currentPos)
        {
            if (!isOpen) return;
            RefreshMapUI();
        }

        private void RefreshMapUI()
        {
            // 清除旧内容
            foreach (Transform child in mapContent) Destroy(child.gameObject);
            nodeButtons.Clear();

            var allRooms = RouteManager.Instance.AllRooms;
            var currentNode = RouteManager.Instance.CurrentNode;

            // 计算布局范围
            float nodeWidth = 100f;
            float nodeHeight = 100f;
            Vector2 minPos = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maxPos = new Vector2(float.MinValue, float.MinValue);
            foreach (var node in allRooms)
            {
                Vector2 pos = new Vector2(node.gridPos.x * nodeWidth, node.gridPos.y * nodeHeight);
                minPos = Vector2.Min(minPos, pos);
                maxPos = Vector2.Max(maxPos, pos);
            }

            // 偏移使地图居中
            Vector2 offset = new Vector2(-(minPos.x + maxPos.x) / 2f, -(minPos.y + maxPos.y) / 2f);

            // 先画连线
            foreach (var node in allRooms)
            {
                Vector2 fromPos = new Vector2(node.gridPos.x * nodeWidth, node.gridPos.y * nodeHeight) + offset;
                foreach (var neighborPos in node.neighbors)
                {
                    // 只画一次（避免重复，约定只画 from 到 to 且 to 的坐标大于 from 的条件，但简单起见可以画两次也没事，但会重叠）
                    // 更好的方法：用 HashSet 记录已画边
                    Vector2 toPos = new Vector2(neighborPos.x * nodeWidth, neighborPos.y * nodeHeight) + offset;
                    CreateLine(fromPos, toPos);
                }
            }

            // 再画按钮
            foreach (var node in allRooms)
            {
                Vector2 anchoredPos = new Vector2(node.gridPos.x * nodeWidth, node.gridPos.y * nodeHeight) + offset;
                GameObject btnObj = Instantiate(nodeButtonPrefab, mapContent);
                RectTransform rt = btnObj.GetComponent<RectTransform>();
                rt.anchoredPosition = anchoredPos;

                Image img = btnObj.GetComponent<Image>();
                img.sprite = mapSetImage.GetSprite(node.nodeType);
                Button btn = btnObj.GetComponent<Button>();

                // 可交互条件：相邻且未被访问？或者只要相邻就可移动
                bool isAdjacent = currentNode != null && currentNode.neighbors.Contains(node.gridPos);
                btn.interactable = isAdjacent;
                btn.onClick.AddListener(() => OnNodeClicked(node.gridPos));

                nodeButtons[node.gridPos] = btn;

                // 当前所在房间高亮
                if (currentNode != null && node.gridPos == currentNode.gridPos)
                {
                    var outline = btnObj.GetComponent<Outline>();
                    if (outline != null) outline.enabled = true;
                }
            }
        }

        private void CreateLine(Vector2 start, Vector2 end)
        {
            GameObject lineObj = Instantiate(linePrefab, mapContent);
            RectTransform rect = lineObj.GetComponent<RectTransform>();
            Vector2 dir = (end - start).normalized;
            float distance = Vector2.Distance(start, end);
            rect.sizeDelta = new Vector2(distance, 3f);
            rect.anchoredPosition = start + dir * distance * 0.5f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rect.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void OnNodeClicked(Vector2Int pos)
        {
            if (RouteManager.Instance.MoveToNode(pos))
            {
                CloseMap(); // 移动后关闭地图，进入房间内容
            }
        }
    }
}