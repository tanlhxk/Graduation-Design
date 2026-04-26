using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    [Header("加载设置")]
    public string gameSceneName = "GameScene";
    public float minLoadingTime = 1f; // 最短加载时间（避免闪屏）

    private int currentSeed;
    private void Start()
    {
        // 开始加载流程
        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        float startTime = Time.time;
        // 确保 RouteManager 存在并完整
        RouteManager routeManager = RouteManager.Instance;
        if (routeManager == null)
        {
            GameObject routeObj = new GameObject("RouteManager");
            routeManager = routeObj.AddComponent<RouteManager>();
            RouteMapGenerator generator = routeObj.AddComponent<RouteMapGenerator>();
            generator.totalLayers = 5;
            generator.minNodesPerLayer = 2;
            generator.maxNodesPerLayer = 4;
            generator.branchProbability = 0.6f;
            routeManager.mapGenerator = generator;
            DontDestroyOnLoad(routeObj);
        }
        else if (routeManager.mapGenerator == null)
        {
            Debug.LogError("RouteManager 缺少 mapGenerator，无法生成地图");
            yield break;
        }

        // 生成地图（不进入节点）
        routeManager.StartNewRun();
        Debug.Log($"地图已生成，起始节点：{routeManager.CurrentNode.nodeType}");

        // 加载游戏场景（直接激活，不等待后续逻辑）
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        asyncLoad.allowSceneActivation = true;

        // 可选：等待场景加载完成（但不再做额外工作）
        yield return asyncLoad;
        // LoadingScene 会自动卸载，协程结束
    }
}