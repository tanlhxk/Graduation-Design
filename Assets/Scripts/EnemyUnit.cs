using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyUnit : Unit
{
    [Header("战斗属性")]
    public int baseAttack = 3;
    public int attackRange = 1;     // 攻击范围（格，1为相邻）
    //private List<SkillData> skillData = new List<SkillData>();

    void Start()
    {
        // 确保基础血量初始化
        if (currentHP == 0) currentHP = maxHP;
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

    // 攻击范围判断
    public bool CanAttack(Unit target, int skillIndex)
    {
        if (target == null || target.currentHP <= 0) return false;

        int distance = Mathf.Abs(currentTile.gridPos.x - target.currentTile.gridPos.x) +
                      Mathf.Abs(currentTile.gridPos.y - target.currentTile.gridPos.y);

        return distance <= attackRange;
    }
    public void Attack(FriendlyUnit target, int skillIndex = 0)
    {
        // 1. 状态检查
        if (target == null || currentHP <= 0 || currentState == UnitState.Dead)
        {
            return;
        }

        // 2. 距离检查
        if (!CanAttack(target, skillIndex))
        {
            return;
        }

        // 3. 设置状态
        currentState = UnitState.Attacking;

        // 4. 计算伤害（敌人暂时不处理复杂技能系数，直接用 baseAttack）
        // 如果未来需要敌人技能，可以在这里扩展
        int damage = baseAttack;

        // 5. 执行打击
        target.TakeDamage(damage);

        // 6. 视觉特效
        CameraShake camShake = Camera.main.GetComponent<CameraShake>();
        if (camShake != null)
        {
            StartCoroutine(camShake.Shake(0.1f, 0.1f));
        }

        // 7. 攻击后复原
        Invoke(nameof(FinishAction), 0.5f);
    }
    public void Attack(FriendlyUnit target)
    {
        Attack(target, 0); // 默认使用普攻
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
