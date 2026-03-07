using UnityEngine;
using System.Collections.Generic;

// 单位类型枚举
public enum UnitType
{
    Player,
    Enemy,
    NPC
}

// 单位状态枚举
public enum UnitState
{
    Idle,       // 等待行动
    Moving,     // 移动中
    Attacking,  // 攻击中
    Dead        // 死亡
}

public class Unit : MonoBehaviour
{
    [Header("基础属性")]
    public string unitName;
    public UnitType unitType;
    public UnitState currentState = UnitState.Idle;

    [Header("战斗属性")]
    public int maxHP = 10;
    public int currentHP;
    public int attackPower = 3;
    public int moveRange = 3;      // 移动范围（格）
    public int attackRange = 1;     // 攻击范围（格，1为相邻）

    [Header("引用")]
    public Tile currentTile;         // 当前所在格子

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

    // 死亡
    void Die()
    {
        currentState = UnitState.Dead;
        // 从网格中移除
        if (currentTile != null)
        {
            currentTile.occupyingUnit = null;
        }
        TurnManager turnManager = FindObjectOfType<TurnManager>();
        if (turnManager != null)
        {
            turnManager.RemoveUnit(this);
        }
        gameObject.SetActive(false);
        Debug.Log($"{unitName} 死亡");
    }

    // 检查是否可攻击目标
    public bool CanAttack(Unit target)
    {
        if (target == null || target.currentHP <= 0) return false;

        // 计算距离（曼哈顿距离）
        int distance = Mathf.Abs(currentTile.gridPos.x - target.currentTile.gridPos.x) +
                      Mathf.Abs(currentTile.gridPos.y - target.currentTile.gridPos.y);

        return distance <= attackRange;
    }

    // 攻击目标
    public void Attack(Unit target)
    {
        if (currentHP <= 0 || currentState == UnitState.Dead)
        {
            Debug.LogWarning($"{unitName} 已经死亡，无法攻击！");
            return;
        }

        if (!CanAttack(target)) return;

        currentState = UnitState.Attacking;
        target.TakeDamage(attackPower);

        // 播放攻击动画（省略）
        // 攻击结束后恢复状态
        Invoke(nameof(FinishAction), 0.5f);
    }

    void FinishAction()
    {
        currentState = UnitState.Idle;
    }

    // 重置回合（每回合开始调用）
    public void NewTurn()
    {
        if (currentState != UnitState.Dead)
        {
            // 可以添加每回合恢复效果等
            currentState = UnitState.Idle;
        }
    }
}