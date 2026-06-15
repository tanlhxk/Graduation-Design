using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.UI;
using Game.Core;
using Game.Map;
using UnityEngine.SceneManagement;
using Game.Combat.Units;
using static Game.UI.SaveSystem;
using System.Collections;
using Game.Camera;

namespace Game.RogueLike
{
    public class RouteManager : MonoBehaviour
    {
        public static RouteManager Instance;

        [Header("地图生成器")]
        public GridMapGenerator mapGenerator;

        public List<GridNode> AllRooms { get; private set; }
        public GridNode CurrentNode { get; private set; }
        public int seed;
        public int Seed => seed;
        public bool IsFixedSeed = false;
        private bool isGameEnding = false;
        public static bool LoadFromSave = false;
        public static SaveData PendingSaveData;
        private bool pendingResumeFromSave = false;
        private bool isEnteringNode = false;
        private bool isCombatActive = false;
        public bool CanOpenRouteMap => !isCombatActive;
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
            if (LoadFromSave && PendingSaveData != null)
            {
                if (mapGenerator == null)
                {
                    Debug.LogError("RouteManager 缺少 mapGenerator 引用，无法加载存档！");
                    return;
                }
                LoadSavedGame(PendingSaveData);
                LoadFromSave = false;
                PendingSaveData = null;
            }
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SaveAndExit();
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
            if (isCombatActive)
            {
                Debug.Log("战斗尚未结束，无法前往下一房间。");
                return false;
            }
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
            if (isEnteringNode) return;
            isEnteringNode = true;
            try
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
            finally
            {
                isEnteringNode = false;
            }
        }

        private void StartCombat(GridNode node)
        {
            isCombatActive = true;
            MapViewer.Instance?.CloseMap();
            CombatData.CurrentNodeType = node.nodeType;
            if (GameManager.Instance != null)
                GameManager.Instance.StartCombat(node.nodeType);
            else
                Debug.LogError("GameManager not found");
            UIManager.Instance.ShowBattleUI();
        }

        private void StartEvent(GridNode node)
        {
            ExitCombatRoom();
            Debug.Log($"进入事件房间: {node.gridPos}");
            OnRoomCleared();
        }
        private void OpenShop(GridNode node)
        {
            ExitCombatRoom();
            Debug.Log($"进入商店房间: {node.gridPos}");
            OnRoomCleared();
        }
        private void Rest()
        {
            ExitCombatRoom();
            Debug.Log("进入休息房间");
            OnRoomCleared();
        }
        private void GetTreasure()
        {
            ExitCombatRoom();
            Debug.Log("进入宝箱房间");
            OnRoomCleared();
        }

        private void ExitCombatRoom()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EndCombat();
        }

        public void OnRoomCleared()
        {
            isCombatActive = false;
            if (CurrentNode != null)
                CurrentNode.isVisited = true;

            if (CurrentNode != null && CurrentNode.nodeType == NodeType.Boss)
            {
                Debug.Log("游戏通关！返回主菜单...");
                EndGame(true);
                return;
            }
            ExitCombatRoom();
            MapViewer.Instance?.OpenMap();
            UIManager.Instance?.ShowRouteUI();
        }
        /// <summary>
        /// 游戏结束（失败或胜利）
        /// </summary>
        /// <param name="isVictory">true=击败 BOSS, false=玩家死亡</param>
        public void EndGame(bool isVictory)
        {
            if (isGameEnding) return;
            isGameEnding = true;

            GameProgress progress = SaveSystem.LoadProgress();

            if (isVictory)
            {
                // 胜利记录
                progress.hasBeatenBoss = true;
                progress.clearedRuns++;
                if (Seed > progress.highestSeed)
                    progress.highestSeed = Seed;
                Debug.Log($"胜利！通关次数：{progress.clearedRuns}，最高种子：{progress.highestSeed}");
            }
            else
            {
                // 死亡记录
                progress.deathCount++;

                // 记录最远层数（根据当前节点的层数，即 gridPos.y）
                int currentLayer = CurrentNode?.gridPos.y ?? 0;
                if (currentLayer > progress.farthestLayer)
                    progress.farthestLayer = currentLayer;
                Debug.Log($"玩家死亡！死亡次数：{progress.deathCount}，最远层数：{progress.farthestLayer}");
            }

            SaveSystem.SaveProgress(progress);

            // 清理资源（复用之前的清理方法）
            StartCoroutine(CleanupAndLoadMenu());
        }
        private IEnumerator CleanupAndLoadMenu()
        {
            // 清理所有单位
            ClearAllUnits();

            // 清理地图生成的障碍物实例
            if (GridManager.Instance != null)
                GridManager.Instance.ClearInstancedObstacles();

            // 停止所有可能残留的协程
            if (TurnManager.Instance != null)
                TurnManager.Instance.StopAllCoroutines();
            if (CameraController.Instance != null)
                Destroy(CameraController.Instance.gameObject);
            // 异步加载主菜单
            AsyncOperation async = SceneManager.LoadSceneAsync("Start");
            async.allowSceneActivation = true;
            yield return async;
        }
        private void ClearAllUnits()
        {
            Unit.AllUnits.Clear();
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.playerUnits.Clear();
                TurnManager.Instance.enemyUnits.Clear();
                TurnManager.Instance.allUnits.Clear();
                TurnManager.Instance.currentActiveUnit = null;
            }
            // 销毁场景中所有 Unit 物体
            foreach (var unit in FindObjectsOfType<Unit>(true))
                Destroy(unit.gameObject);
        }
        public void SaveCurrentGame()
        {
            if (CurrentNode == null) return;

            SaveData data = new SaveData();
            data.sceneIndex = 0; // 可忽略或存场景索引
            data.playerLevel = 1; // 根据需要从玩家单位获取等级

            // 保存肉鸽进度
            data.seed = seed;
            data.currentNodeX = CurrentNode.gridPos.x;
            data.currentNodeY = CurrentNode.gridPos.y;
            data.currentNodeType = CurrentNode.nodeType;

            // 保存已访问节点（用于恢复时标记）
            data.visitedNodes = new List<VisitedNodeData>();
            foreach (var node in AllRooms)
            {
                data.visitedNodes.Add(new VisitedNodeData
                {
                    x = node.gridPos.x,
                    y = node.gridPos.y,
                    isVisited = node.isVisited
                });
            }

            SaveSystem.SaveGameData(data);
        }
        public void LoadSavedGame(SaveData data)
        {
            seed = data.seed;
            // 重新生成地图（使用保存的种子）
            AllRooms = mapGenerator.GenerateMap(seed);

            // 恢复已访问状态
            foreach (var node in AllRooms)
            {
                var saved = data.visitedNodes.Find(v => v.x == node.gridPos.x && v.y == node.gridPos.y);
                if (saved != null)
                    node.isVisited = saved.isVisited;
            }

            // 找到当前节点
            CurrentNode = AllRooms.Find(n => n.gridPos.x == data.currentNodeX && n.gridPos.y == data.currentNodeY);
            if (CurrentNode == null) CurrentNode = AllRooms.Find(n => n.nodeType == NodeType.Start);

            pendingResumeFromSave = true;
        }
        public void ResumeFromSave()
        {
            if (pendingResumeFromSave)
            {
                pendingResumeFromSave = false;
                EnterNode(CurrentNode);
            }
        }
        public void StartCurrentNode()
        {
            if (CurrentNode != null)
                EnterNode(CurrentNode);
        }
        /// <summary>
        /// 游戏场景加载完成后进入当前节点（新游戏与读档统一入口）
        /// </summary>
        public void BeginAtCurrentNode()
        {
            if (CurrentNode == null) return;
            pendingResumeFromSave = false;
            EnterNode(CurrentNode);
        }
        public void SaveAndExit()
        {
            SaveCurrentGame();           // 保存进度
            StartCoroutine(ExitToMenu()); // 清理并加载主菜单
        }

        private IEnumerator ExitToMenu()
        {
            // 清理战斗单位、障碍物等
            ClearAllUnits();
            if (GridManager.Instance != null)
                GridManager.Instance.ClearInstancedObstacles();

            // 销毁相机避免AudioListener冲突
            if (CameraController.Instance != null)
                Destroy(CameraController.Instance.gameObject);

            // 销毁所有面向相机的物体
            foreach (var fc in FindObjectsOfType<FacingCamera>())
                Destroy(fc.gameObject);

            yield return null;
            SceneManager.LoadScene("Start");
        }
        // 兼容旧调用（可选）
        public void OnCombatVictory() => OnRoomCleared();
    }
}