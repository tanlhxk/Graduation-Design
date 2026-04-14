using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FriendlyUnit : Unit
{
    [Header("战斗属性")]
    public int attackRange = 1;
    private List<SkillDataSO> skillData = new List<SkillDataSO>();

    void Start()
    {
        currentHP = maxHP;
    }

    // 受伤
    public override void TakeDamage(int damage)
    {
        currentHP -= damage;
        Debug.Log($"{unitName} 受到 {damage} 点伤害，剩余 HP: {currentHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void AddSkill(SkillDataSO skill)
    {
        if (skill != null && !skillData.Contains(skill))
            skillData.Add(skill);
    }

    // 外部读取技能
    public List<SkillDataSO> GetUnitSkills()
    {
        return skillData;
    }

    // 1. 提供一个公共方法，根据索引返回技能数据
    public SkillDataSO GetSkillData(int index)
    {
        return skillData[index];
    }

    public bool CanUseSkill(EnemyUnit target, SkillDataSO skillData)
    {
        if (skillData == null) return false;

        // 这里可以写通用的距离判断逻辑
        // 例如：计算格子距离是否 <= 技能射程
        int distance = GridManager.GetDistance(this.currentTile, target.currentTile);
        return distance <= skillData.skillRange;
    }

    public override void PerformAttack()
    {
        if (attackTarget == null) return;

        // 假设基类里有 attackSkillIndex 字段记录当前使用的技能ID
        SkillDataSO skillData = GetSkillData(attackSkillIndex);
        if (skillData == null) return;

        // 执行技能 (核心：通过接口调用)
        // 这里可以使用一个 SkillFactory 来根据 skillData.type 获取对应的 ISkillEffect 实例
        ISkillEffect effect = SkillFactory.GetSkillEffect(skillData.skillType);
        effect.Execute(this, attackTarget, skillData);

        // 通用的战斗反馈 (这部分可以保留在 Unit 中)
        CameraShake camShake = Camera.main.GetComponent<CameraShake>();
        if (camShake != null)
            StartCoroutine(camShake.Shake(0.1f, 0.1f));

        // 基类逻辑：通知回合结束等
        base.PerformAttack();
    }

    public void Attack(EnemyUnit target, int skillIndex = 0)
    {
        base.Attack(target, skillIndex);
    }

    public bool CanAttack(EnemyUnit target, int skillIndex)
    {
        if (target == null || target.currentHP <= 0) return false;

        // 计算曼哈顿距离
        int distance = Mathf.Abs(currentTile.gridPos.x - target.currentTile.gridPos.x) +
                      Mathf.Abs(currentTile.gridPos.y - target.currentTile.gridPos.y);

        // 如果没有技能数据，或者索引为0（普攻），使用单位的 attackRange
        // 否则使用技能的 attackRange
        int effectiveRange = attackRange;
        if (skillData.Count > skillIndex && skillIndex > 0)
        {
            effectiveRange = skillData[skillIndex].skillRange;
        }

        return distance <= effectiveRange;
    }
    public bool AttackRange(Tile tile)
    {
        if (tile == null) return false;
        int ar = Mathf.Abs(currentTile.gridPos.x - tile.gridPos.x) +
                      Mathf.Abs(currentTile.gridPos.y - tile.gridPos.y);
        return ar <= attackRange;
    }

    public void ExecuteSkill(EnemyUnit target, SkillDataSO skillData)
    {
        // 1. 获取技能效果工厂
        ISkillEffect effect = SkillFactory.GetSkillEffect(skillData.skillType);

        // 2. 执行效果
        effect.Execute(this, target, skillData);

        // 3. 播放特效/动画
        // ...
    }
}
