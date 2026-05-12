using System.Collections.Generic;
using UnityEngine;
using Game.RogueLike;

namespace Game.RogueLike
{
    [System.Serializable]
    public class RouteNode
    {
        public int layer;              // 层数（从0开始）
        public int index;              // 在当前层的索引
        public NodeType nodeType;
        public Vector2Int position;    // 在UI上的位置（用于绘制连线）
        public List<int> nextIndices;  // 下一层的节点索引列表（连接关系）
        public bool isVisited;
        public bool isLocked;          // 是否已无法访问（例如分支被跳过）

        public RouteNode(int layer, int index, NodeType type)
        {
            this.layer = layer;
            this.index = index;
            this.nodeType = type;
            nextIndices = new List<int>();
            isVisited = false;
            isLocked = false;
        }
    }
    public class RouteMapGenerator : MonoBehaviour
    {
        [Header("路线配置")]
        public int totalLayers = 5;          // 总层数（不含BOSS层）
        public int minNodesPerLayer = 2;
        public int maxNodesPerLayer = 4;
        public float branchProbability = 0.6f; // 分支概率（连接多个下一层节点的概率）

        [Header("节点类型权重")]
        public int[] combatWeight = { 70 };
        public int[] eliteWeight = { 20 };
        public int[] eventWeight = { 30 };
        public int[] shopWeight = { 15 };
        public int[] restWeight = { 10 };
        public int[] treasureWeight = { 5 };

        /// <summary>
        /// 生成一张随机路线图
        /// </summary>
        public List<List<RouteNode>> GenerateMap(int seed)
        {
            List<List<RouteNode>> layers = new List<List<RouteNode>>();

            // 生成每一层的节点数量
            int[] nodesPerLayer = new int[totalLayers + 1]; // 最后一层为BOSS层
            for (int i = 0; i < totalLayers; i++)
            {
                if (i == 0) nodesPerLayer[i] = 1;
                else nodesPerLayer[i] = Random.Range(minNodesPerLayer, maxNodesPerLayer + 1);
            }
            nodesPerLayer[totalLayers] = 1; // BOSS层只有一个节点

            // 创建节点
            for (int layer = 0; layer <= totalLayers; layer++)
            {
                List<RouteNode> currentLayer = new List<RouteNode>();
                for (int idx = 0; idx < nodesPerLayer[layer]; idx++)
                {
                    NodeType type;
                    if (layer == totalLayers)
                        type = NodeType.Boss;
                    else
                        type = GetRandomNodeType(layer);
                    RouteNode node = new RouteNode(layer, idx, type);
                    currentLayer.Add(node);
                }
                layers.Add(currentLayer);
            }

            // 建立连接（从上一层到下一层）
            for (int layer = 0; layer < totalLayers; layer++)
            {
                List<RouteNode> current = layers[layer];
                List<RouteNode> next = layers[layer + 1];

                // 为每个当前节点分配下一层的连接
                for (int i = 0; i < current.Count; i++)
                {
                    // 确定能连接到下一层的哪些索引
                    List<int> candidates = new List<int>();
                    // 通常连接到同一列或相邻列
                    int startIdx = Mathf.Max(0, i - 1);
                    int endIdx = Mathf.Min(next.Count - 1, i + 1);
                    for (int j = startIdx; j <= endIdx; j++)
                        candidates.Add(j);

                    // 随机决定连接数量（1 或 2）
                    int connectCount = Random.value < branchProbability ? 2 : 1;
                    connectCount = Mathf.Min(connectCount, candidates.Count);
                    // 随机挑选
                    for (int c = 0; c < connectCount; c++)
                    {
                        int idx = Random.Range(0, candidates.Count);
                        current[i].nextIndices.Add(candidates[idx]);
                        candidates.RemoveAt(idx);
                    }
                    // 去重（如果有重复）
                    current[i].nextIndices = new List<int>(new HashSet<int>(current[i].nextIndices));
                }
            }
            LayoutNodes(layers);
            return layers;
        }

        private NodeType GetRandomNodeType(int layer)
        {
            if (layer == 0) return NodeType.Combat;
            // 精英和BOSS只在后期出现，可简单规则
            float eliteChance = layer >= totalLayers - 2 ? 0.3f : 0.1f;
            // 简单按权重随机，你可以扩展更复杂的逻辑
            float rand = Random.value;
            if (rand < 0.5f) return NodeType.Combat;
            if (rand < 0.7f) return NodeType.Event;
            if (rand < 0.8f) return NodeType.Shop;
            if (rand < 0.9f) return NodeType.Rest;
            return NodeType.EliteCombat;
        }
        private void LayoutNodes(List<List<RouteNode>> layers)
        {
            float startX = 100f;
            float startY = 0f;
            float xSpacing = 180f;
            float ySpacing = 120f;

            for (int i = 0; i < layers.Count; i++)
            {
                int nodeCount = layers[i].Count;
                float totalHeight = (nodeCount - 1) * ySpacing;
                float startYThisLayer = startY - totalHeight * 0.5f;
                for (int j = 0; j < nodeCount; j++)
                {
                    float x = startX + i * xSpacing;
                    float y = startYThisLayer + j * ySpacing;
                    layers[i][j].position = new Vector2Int((int)x, (int)y);
                }
            }
        }
    }
}