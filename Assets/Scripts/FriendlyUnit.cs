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

    public void Attack(EnemyUnit target, int skillIndex)
    {
        // 1. 状态检查
        if (target == null || currentHP <= 0 || currentState == UnitState.Dead)
        {
            Debug.LogWarning("攻击目标无效或单位无法行动");
            return;
        }

        // 2. 距离检查
        if (!CanAttack(target, skillIndex))
        {
            Debug.LogWarning($"{unitName} 无法攻击：距离过远");
            return;
        }

        // 3. 设置状态
        currentState = UnitState.Attacking;

        // 4. 计算伤害与特效
        int finalDamage = 0;
        string skillName = "普通攻击";

        // 逻辑：如果没技能或选中普攻，用基础攻击；否则用技能
        if (skillData.Count == 0 || skillIndex == 0 || skillIndex >= skillData.Count)
        {
            finalDamage = baseAttack;
            skillName = "普通攻击";
        }
        else
        {
            SkillData data = skillData[skillIndex];
            finalDamage = Mathf.RoundToInt(baseAttack * data.damageMultiplier);
            skillName = data.skillName;
            // 这里可以添加技能特效播放 logic...
        }

        Debug.Log($"{unitName} 使用 {skillName} 造成 {finalDamage} 点伤害");

        // 5. 执行打击
        target.TakeDamage(finalDamage);

        // 6. 视觉特效（摄像机震动）
        CameraShake camShake = Camera.main.GetComponent<CameraShake>();
        if (camShake != null)
        {
            StartCoroutine(camShake.Shake(0.1f, 0.1f));
        }

        // 7. 攻击后复原
        Invoke(nameof(FinishAction), 0.5f);
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

    public void Attack(EnemyUnit target)
    {
        // 1. 状态检查
        if (currentHP <= 0 || currentState == UnitState.Dead)
        {
            Debug.LogWarning($"{unitName} 已经死亡，无法攻击！");
            return;
        }
        if (!CanAttack(target, 1))
        {
            Debug.LogWarning($"{unitName} 无法攻击 {target.unitName}（距离过远或目标无效）");
            return;
        }

        // 2. 设置状态
        currentState = UnitState.Attacking;

        // 3. 计算伤害 (这里是你未来加“克制”逻辑的地方)
        // int damage = CalculateDamage(target); 
        int damage = baseAttack; // 简化版

        // 4. 触发受击
        target.TakeDamage(damage);

        // 5. 视觉与特效反馈 (关键修改点)
        // A. 摄像机震动 (调用你上传的 CameraShake)
        CameraShake camShake = Camera.main.GetComponent<CameraShake>();
        if (camShake != null)
        {
            StartCoroutine(camShake.Shake(0.1f, 0.1f));
        }

        // B. 播放攻击特效 (伪代码)
        // PlayAttackAnimation();
        // PlayHitEffectOnTarget(target.transform.position);

        // 6. 攻击后复原
        // 使用协程或 Invoke 来延迟恢复 Idle 状态，以便播放动画
        Invoke(nameof(FinishAction), 0.5f); // 0.5秒后恢复空闲
    }

    void FinishAction()
    {
        currentState = UnitState.Idle;
        // 通知回合系统结束
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnitFinishedAction(this);
        }
    }
}
