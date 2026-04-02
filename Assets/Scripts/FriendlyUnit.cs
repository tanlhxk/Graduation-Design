using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FriendlyUnit : Unit
{
    [Header("战斗属性")]
    public int baseAttack = 3;
    public int attackRange = 1;     // 攻击范围（格，1为相邻）
    private List<SkillData> skillData = new List<SkillData>();

    void Start()
    {
        currentHP = maxHP;
    }

    // 受伤
    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        Debug.Log($"{unitName} 受到 {damage} 点伤害，剩余 HP: {currentHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void AddSkill(SkillType type, string name, int damage, int attackRange, int cd)
    {
        skillData.Add(new SkillData(type, name, damage, attackRange, cd));
    }

    // 外部读取技能
    public List<SkillData> GetSkills()
    {
        return skillData;
    }

    // 1. 提供一个公共方法，根据索引返回技能数据
    public SkillData GetSkillData(int index)
    {
        return skillData[index];
    }

    public override void PerformAttack()
    {
        if (attackTarget == null) return;

        // 目标一定是 EnemyUnit
        EnemyUnit target = attackTarget as EnemyUnit;
        if (target == null) return;

        int finalDamage = 0;
        string skillName = "普通攻击";

        // 根据技能索引计算伤害
        if (skillData.Count == 0 || attackSkillIndex == 0 || attackSkillIndex >= skillData.Count)
        {
            finalDamage = baseAttack;
            skillName = "普通攻击";
        }
        else
        {
            SkillData data = skillData[attackSkillIndex];
            finalDamage = Mathf.RoundToInt(baseAttack * data.damageMultiplier);
            skillName = data.skillName;
            // 可在此添加技能特效
        }

        Debug.Log($"{unitName} 使用 {skillName} 造成 {finalDamage} 点伤害");
        target.TakeDamage(finalDamage);

        // 摄像机震动
        CameraShake camShake = Camera.main.GetComponent<CameraShake>();
        if (camShake != null)
            StartCoroutine(camShake.Shake(0.1f, 0.1f));

        // 状态机中已经会调用 UnitFinishedAction，无需额外处理
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
            effectiveRange = skillData[skillIndex].attackRange;
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
}
