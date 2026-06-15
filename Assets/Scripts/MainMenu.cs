using Game.RogueLike;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Game.UI.SaveSystem;

namespace Game.UI
{
    public class MainMenu : MonoBehaviour
    {
        [Header("Buttons")]
        public Button continueButton;      // 继续游戏按钮
        public GameObject settingsPanel;   // 设置面板

        private void Start()
        {
            // 检查是否存在存档，决定继续按钮是否可用
            bool hasSave = SaveSystem.HasSaveData();
            continueButton.interactable = hasSave;

            // 确保设置面板初始关闭
            settingsPanel.SetActive(false);
            /*GameProgress progress = SaveSystem.LoadProgress();
            if (progress.hasBeatenBoss)
            {
                Debug.Log($"欢迎回来！您已通关 {progress.clearedRuns} 次，最高种子：{progress.highestSeed}");
                // 可以显示在 UI 文本上，比如通关次数、解锁新难度按钮等
            }*/
        }

        // 开始新游戏
        public void StartNewGame()
        {
            SaveSystem.ClearSaveData();
            SceneManager.LoadScene("LoadingScene");
        }

        // 继续游戏
        public void ContinueGame()
        {
            SaveData save = SaveSystem.LoadGameData();
            if (save != null)
            {
                // 设置静态数据，让 RouteManager 在 GameScene 中自己加载
                RouteManager.LoadFromSave = true;
                RouteManager.PendingSaveData = save;
                SceneManager.LoadScene("GameScene");
            }
            else
            {
                Debug.LogWarning("没有存档，无法继续游戏！");
                continueButton.interactable = false;
            }
        }

        // 打开/关闭设置面板
        public void ToggleSettings(bool open)
        {
            settingsPanel.SetActive(open);
        }

        // 退出游戏
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;  // 编辑器模式下停止运行
#else
            Application.Quit();                               // 正式构建时退出应用
#endif
        }
    }
    public static class SaveSystem
    {
        // 存档标识键
        private const string SAVE_KEY = "GameSaveData";
        private const string PROGRESS_KEY = "GameProgress";

        // 检查是否存在存档
        public static bool HasSaveData()
        {
            return PlayerPrefs.HasKey(SAVE_KEY);
        }

        // 清除存档（开始新游戏时调用）
        public static void ClearSaveData()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            Debug.Log("存档已清除");
        }

        // 保存游戏数据
        public static void SaveGameData(SaveData data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        // 加载存档数据（返回 null 表示无存档）
        public static SaveData LoadGameData()
        {
            if (!HasSaveData()) return null;
            string json = PlayerPrefs.GetString(SAVE_KEY);
            return JsonUtility.FromJson<SaveData>(json);
        }
        public static void SaveProgress(GameProgress progress)
        {
            string json = JsonUtility.ToJson(progress);
            PlayerPrefs.SetString(PROGRESS_KEY, json);
            PlayerPrefs.Save();
        }

        public static GameProgress LoadProgress()
        {
            if (!PlayerPrefs.HasKey(PROGRESS_KEY))
                return new GameProgress(); // 返回默认值（未通关，次数0，种子0）

            string json = PlayerPrefs.GetString(PROGRESS_KEY);
            return JsonUtility.FromJson<GameProgress>(json);
        }

        public static void ClearProgress()
        {
            PlayerPrefs.DeleteKey(PROGRESS_KEY);
            Debug.Log("成就记录已清除");
        }
        // 存档数据结构
        [System.Serializable]
        public class SaveData
        {
            public int sceneIndex;
            public int playerLevel;

            // ===== 肉鸽进度 =====
            public int seed;                     // 地图种子
            public int currentNodeX;             // 当前节点X坐标
            public int currentNodeY;             // 当前节点Y坐标
            public NodeType currentNodeType;     // 当前节点类型
            public List<VisitedNodeData> visitedNodes; // 已访问节点列表
        }

        [System.Serializable]
        public class VisitedNodeData
        {
            public int x, y;
            public bool isVisited;
        }
        [System.Serializable]
        public class GameProgress
        {
            public bool hasBeatenBoss;      // 是否击败过 BOSS
            public int clearedRuns;         // 通关次数
            public int highestSeed;         // 通关时的最高种子
            public int deathCount;          // 总死亡次数（新增）
            public int farthestLayer;       // 到达的最远层数

            public GameProgress()
            {
                hasBeatenBoss = false;
                clearedRuns = 0;
                highestSeed = 0;
                deathCount = 0;
                farthestLayer = 0;
            }
        }
    }
}