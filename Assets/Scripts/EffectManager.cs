using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        SkillEventBus.OnSkillHit += OnSkillHit;
    }

    void OnDestroy()
    {
        SkillEventBus.OnSkillHit -= OnSkillHit;
    }

    void OnSkillHit(SkillEventArgs args)
    {
        if (args.skillData.hitEffectPrefab == null) return;

        // 获取特效持续时间，优先使用技能配置，否则使用预制体上的 ParticleSystem 时长，最后默认 1.5f
        float duration = args.skillData.effectDuration;
        if (duration <= 0)
        {
            // 尝试从预制体上的 ParticleSystem 获取主循环时长
            var ps = args.skillData.hitEffectPrefab.GetComponent<ParticleSystem>();
            if (ps != null)
                duration = ps.main.duration;
            else
                duration = 1.5f;
        }

        // 从对象池获取或实例化特效
        GameObject effect = EffectPool.Instance.Get(args.skillData.hitEffectPrefab);
        effect.transform.position = args.hitPoint;
        effect.transform.rotation = Quaternion.Euler(-90, 0, 0);
        effect.SetActive(true);

        // 自动回收
        StartCoroutine(RecycleAfter(effect, duration));
    }

    IEnumerator RecycleAfter(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        EffectPool.Instance.Recycle(effect);
    }
}