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

    public override void PerformAttack()
    {
        // 假设已在基类中这样做了，这里可以直接使用 attackTarget
        if (attackTarget == null) return;

        // 由于 EnemyUnit 的攻击目标是 FriendlyUnit，需要转换
        FriendlyUnit target = attackTarget as FriendlyUnit;
        if (target == null) return;

        // 计算伤害（可以根据技能索引扩展）
        int damage = baseAttack;

        // 可选：摄像机聚焦（改为事件触发，避免直接耦合）
        // 可以发送事件，让 CameraController 监听
        // 或者如果保持原样，判空即可
        if (CameraController.Instance != null)
            CameraController.Instance.ForcePosition(this.transform.position);

        // 执行伤害
        target.TakeDamage(damage);

        // 摄像机震动
        CameraShake camShake = Camera.main.GetComponent<CameraShake>();
        if (camShake != null)
            StartCoroutine(camShake.Shake(0.1f, 0.1f));

        // 注意：状态机中已经会在攻击动画后调用 UnitFinishedAction，这里无需再手动调用
    }
    public void Attack(FriendlyUnit target, int skillIndex = 0)
    {
        // 基类 Attack 会检查状态、设置攻击目标，并进入 Attacking 状态
        base.Attack(target, skillIndex);
    }
    public void Attack(FriendlyUnit target)
    {
        Attack(target, 0); // 默认使用普攻
    }
    /*void FinishAction()
    {
        currentState = UnitState.Idle;
        // 通知回合系统结束
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnitFinishedAction(this);
        }
    }*/
}
