using UnityEngine;
using UnityEngine.Events;

// 技能类型枚举
public enum SkillType
{
    NormalAttack,
    BattleSkill,
    Ultimate,
    BuffSkill,
    HealingSkill
}

// 技能效果接口 (核心解耦点)
public interface ISkillEffect
{
    void Execute(Unit caster, Unit target, SkillDataSO skillData);
}

// 技能数据资产 (ScriptableObject)
[CreateAssetMenu(fileName = "Skill", menuName = "Data/Skill")]
public class SkillDataSO : ScriptableObject
{
    public string skillName;
    public SkillType skillType;
    public Sprite icon;
    [TextArea] public string description;

    // 数值配置
    public float damageMultiplier = 1f; // 伤害倍率
    public int skillRange = 1;          // 技能射程
    public int cooldown = 0;            // 冷却

    public SkillDataSO(SkillType type, string name, float damageMultiplier, int range, int cd)
    {
        skillType = type;
        skillName = name;
        this.damageMultiplier = damageMultiplier;
        skillRange = range;
        cooldown = cd;
    }

    // 效果引用 (这里可以挂载具体的 MonoBehaviour 脚本或 ScriptableObject)
    // 为了简单演示，我们暂时用代码逻辑代替引用
}

// DamageEffect.cs
public class DamageEffect : ISkillEffect
{
    public void Execute(Unit caster, Unit target, SkillDataSO skillData)
    {
        // 1. 计算伤害
        int baseDamage = caster.baseAttack;
        int finalDamage = Mathf.RoundToInt(baseDamage * skillData.damageMultiplier);

        // 2. 应用伤害
        target.TakeDamage(finalDamage);

        // 3. 视觉/音效反馈 (这里可以扩展)
        Debug.Log($"{caster.unitName} 使用 {skillData.skillName} 造成 {finalDamage} 伤害!");
    }
}

public class HealingEffect : ISkillEffect
{
    public void Execute(Unit caster, Unit target, SkillDataSO skillData)
    {
        // 治疗逻辑：治疗量基于施法者的攻击力
        int healAmount = Mathf.RoundToInt(caster.baseAttack * skillData.damageMultiplier);
        //caster.Heal(healAmount); // 需要在 Unit 类中添加 Heal 方法
        Debug.Log($"{caster.unitName} 恢复了 {healAmount} 生命值!");
    }
}

public class UltimateEffect : ISkillEffect
{
    public void Execute(Unit caster, Unit target, SkillDataSO skillData)
    {
        // 这里可以实现大范围伤害、击退、特效播放等
        int baseDamage = caster.baseAttack;
        int finalDamage = Mathf.RoundToInt(baseDamage * skillData.damageMultiplier * 2f); // 终结技通常更强
        target.TakeDamage(finalDamage);

        // 播放终结技特效 (伪代码)
        // ParticleSystemManager.Play("UltimateExplosion", target.transform.position);

        Debug.Log($"{caster.unitName} 释放了终结技! 对 {target.unitName} 造成了巨大伤害!");
    }
}

public static class SkillFactory
{
    public static ISkillEffect GetSkillEffect(SkillType type)
    {
        switch (type)
        {
            case SkillType.NormalAttack:
            case SkillType.BattleSkill:
                return new DamageEffect();
            case SkillType.Ultimate:
                return new UltimateEffect();
            case SkillType.HealingSkill:
                return new HealingEffect();
            // 添加更多类型...
            default:
                return new DamageEffect(); // 默认返回伤害
        }
    }
}