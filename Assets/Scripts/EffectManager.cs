using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    void Awake()
    {
        SkillEventBus.OnSkillHit += OnSkillHit;
    }

    void OnDestroy()
    {
        SkillEventBus.OnSkillHit -= OnSkillHit;
    }

    void OnSkillHit(SkillEventArgs args)
    {
        // 根据技能数据决定播放哪个特效
        if (args.skillData.hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(args.skillData.hitEffectPrefab, args.hitPoint, Quaternion.identity);
            effect.transform.rotation = Quaternion.Euler(-90, 0, 0);
            Destroy(effect, 1.5f);
        }
        // 播放音效等
    }
}
