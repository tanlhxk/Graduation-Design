using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

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

// 技能类型枚举
public enum SkillType
{
    NormalAttack,   // 普攻
    BattleSkill,    // 战技
    Ultimate        // 终结技
}

// 技能数据容器
[System.Serializable]
public class SkillData
{
    public SkillType type;
    public string skillName;
    public int damageMultiplier;
    public int attackRange;
    public Sprite icon;
    public int coolDown_Rounds; // 冷却需要的“回合数”或“步数”

    // 构造函数
    public SkillData(SkillType t, string n, int dmg,int ar, int cd)
    {
        type = t;
        skillName = n;
        damageMultiplier = dmg;
        attackRange = ar;
        coolDown_Rounds = cd;
    }
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
    public int moveRange = 3;

    [Header("引用")]
    public Tile currentTile;         // 当前所在格子

    // 死亡
    public void Die()
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

    // 修改 CanAttack 以支持范围
    public List<Unit> GetAttackTargets(SkillData skill)
    {
        List<Unit> targets = new List<Unit>();
        SkillData useSkill = skill;

        // 获取当前单位位置
        Vector2Int myPos = currentTile.gridPos;

        // 遍历所有敌人（简化版：全图扫描，实际可用四叉树优化）
        foreach (Unit enemy in FindObjectsOfType<Unit>())
        {
            if (enemy.unitType == unitType) continue; // 跳过同阵营

            // 计算距离
            int distance = Mathf.Abs(myPos.x - enemy.currentTile.gridPos.x) +
                           Mathf.Abs(myPos.y - enemy.currentTile.gridPos.y);

            // 判定是否在技能范围内
            if (distance <= useSkill.attackRange)
            {
                targets.Add(enemy);
            }
        }
        return targets;
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