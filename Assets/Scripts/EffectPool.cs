using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectPool : MonoBehaviour
{
    public static EffectPool Instance;

    private Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
    private Dictionary<GameObject, GameObject> prefabMap = new Dictionary<GameObject, GameObject>(); // 实例 -> 预制体

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public GameObject Get(GameObject prefab)
    {
        if (!pool.ContainsKey(prefab) || pool[prefab].Count == 0)
        {
            // 创建新实例
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            prefabMap[obj] = prefab;
            return obj;
        }
        GameObject pooled = pool[prefab].Pop();
        prefabMap[pooled] = prefab;
        return pooled;
    }

    public void Recycle(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        if (prefabMap.TryGetValue(obj, out GameObject prefab))
        {
            if (!pool.ContainsKey(prefab))
                pool[prefab] = new Stack<GameObject>();
            pool[prefab].Push(obj);
        }
        else
        {
            // 未记录预制体，直接销毁
            Destroy(obj);
        }
    }

    // 可选：清理所有池（场景切换时调用）
    public void Clear()
    {
        foreach (var stack in pool.Values)
        {
            foreach (var obj in stack)
                Destroy(obj);
        }
        pool.Clear();
        prefabMap.Clear();
    }
}
