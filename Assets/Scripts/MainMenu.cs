using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    }

    // 开始新游戏
    public void StartNewGame()
    {
        // 清除旧存档（可选）
        SaveSystem.ClearSaveData();

        // 加载游戏主场景（需要先在 Build Settings 中添加）
        SceneManager.LoadScene("GameScene");
    }

    // 继续游戏
    public void ContinueGame()
    {
        if (SaveSystem.HasSaveData())
        {
            // 加载存档中保存的场景（示例中默认加载 GameScene）
            SceneManager.LoadScene("GameScene");
            // 实际项目中，进入场景后需要通过事件或静态变量告知加载存档数据
            // 例如：GameManager.LoadFromSave = true;
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
    public static void SaveGame(int sceneIndex, int playerLevel)
    {
        SaveData data = new SaveData(sceneIndex, playerLevel);
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    // 加载存档数据（返回 null 表示无存档）
    public static SaveData LoadSaveData()
    {
        if (!HasSaveData()) return null;
        string json = PlayerPrefs.GetString(SAVE_KEY);
        return JsonUtility.FromJson<SaveData>(json);
    }

    // 存档数据结构
    [System.Serializable]
    public class SaveData
    {
        public int sceneIndex;
        public int playerLevel;

        public SaveData(int sceneIndex, int playerLevel)
        {
            this.sceneIndex = sceneIndex;
            this.playerLevel = playerLevel;
        }
    }
}