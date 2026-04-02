using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

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
    public SkillData(SkillType t, string n, int dmg, int ar, int cd)
    {
        type = t;
        skillName = n;
        damageMultiplier = dmg;
        attackRange = ar;
        coolDown_Rounds = cd;
    }
}

// 状态接口
public interface IUnitState
{
    void Enter(Unit unit);
    void Update(Unit unit);
    void Exit(Unit unit);
}

// 空闲状态
public class UnitIdleState : IUnitState
{
    public void Enter(Unit unit) { }
    public void Update(Unit unit) { }
    public void Exit(Unit unit) { }
}

// 移动状态
public class UnitMovingState : IUnitState
{
    public void Enter(Unit unit)
    {
        // 启动移动协程，移动完成后自动切换回 Idle
        unit.StartCoroutine(MoveCoroutine(unit));
    }

    private IEnumerator MoveCoroutine(Unit unit)
    {
        // 注意：这里需要 unit.movementSystem 引用，请确保在 Unit 中已赋值
        yield return MovementSystem.Instance.MoveUnitAlongPath(unit, unit.currentPath);
        unit.ChangeState(UnitState.Idle);
        // 通知回合管理器该单位行动结束
        if (TurnManager.Instance != null && unit.currentHP > 0)
        {
            Debug.Log($"状态机完成行动，准备通知 TurnManager：{unit.unitName}");
            TurnManager.Instance.UnitFinishedAction(unit);
        }
    }

    public void Update(Unit unit) { }
    public void Exit(Unit unit) { }
}

// 攻击状态
public class UnitAttackingState : IUnitState
{
    public void Enter(Unit unit)
    {
        // 启动攻击协程，攻击完成后切换回 Idle
        unit.StartCoroutine(AttackCoroutine(unit));
    }

    private IEnumerator AttackCoroutine(Unit unit)
    {
        // 执行攻击逻辑（例如调用攻击方法，等待动画等）
        unit.PerformAttack(); // 需要在 Unit 中实现 PerformAttack
        yield return new WaitForSeconds(0.5f); // 模拟攻击动画时间
        unit.ChangeState(UnitState.Idle);
        if (TurnManager.Instance != null && unit.currentHP > 0)
        {
            Debug.Log($"状态机完成行动，准备通知 TurnManager：{unit.unitName}");
            TurnManager.Instance.UnitFinishedAction(unit);
        }
    }

    public void Update(Unit unit) { }
    public void Exit(Unit unit) { }
}

// 死亡状态
public class UnitDeadState : IUnitState
{
    public void Enter(Unit unit)
    {
        // 死亡时执行清理，如从网格移除、停用对象等
        unit.DieImmediate();
    }
    public void Update(Unit unit) { }
    public void Exit(Unit unit) { }
}

public class Unit : MonoBehaviour
{
    [Header("基础属性")]
    public string unitName;
    public UnitType unitType;

    [Header("战斗属性")]
    public int maxHP = 10;
    public int currentHP;
    public int moveRange = 3;
    public float moveSpeed = 2f;

    [Header("引用")]
    public Tile currentTile;         // 当前所在格子

    // 静态列表，存储所有当前存活的单位
    public static List<Unit> AllUnits = new List<Unit>();

    public UnitState CurrentStateEnum { get; private set; }
    private IUnitState currentState;

    // 状态对象字典，便于复用
    private Dictionary<UnitState, IUnitState> states = new Dictionary<UnitState, IUnitState>();

    // 当前移动路径（供移动状态使用）
    public List<Tile> currentPath;

    // 攻击目标及技能索引（供攻击状态使用）
    protected Unit attackTarget;   // 攻击目标
    protected int attackSkillIndex; // 使用的技能索引

    protected virtual void Awake()
    {
        // 在 Awake 中注册，确保生成时立即加入
        AllUnits.Add(this);

        // 初始化状态字典
        states[UnitState.Idle] = new UnitIdleState();
        states[UnitState.Moving] = new UnitMovingState();
        states[UnitState.Attacking] = new UnitAttackingState();
        states[UnitState.Dead] = new UnitDeadState();

        // 初始状态设为 Idle
        ChangeState(UnitState.Idle);
    }

    public void Update()
    {
        currentState?.Update(this);
    }

    public void ChangeState(UnitState newState)
    {
        if (currentState != null)
            currentState.Exit(this);
        CurrentStateEnum = newState;
        currentState = states[newState];
        currentState.Enter(this);
    }

    protected virtual void OnDestroy()
    {
        // 在对象销毁时从列表中移除
        AllUnits.Remove(this);
    }

    // 对外接口：移动到目标格子
    public void MoveTo(Tile targetTile)
    {
        if (CurrentStateEnum != UnitState.Idle) return;
        // 计算路径
        currentPath = MovementSystem.Instance.FindPath(this, currentTile, targetTile);
        // 限制移动范围不超过移动力
        if (currentPath.Count > moveRange + 1)
            currentPath = currentPath.GetRange(0, moveRange + 1);
        if (currentPath.Count > 1)
            ChangeState(UnitState.Moving);
    }

    // 对外接口：攻击目标
    public void Attack(Unit target, int skillIndex)
    {
        if (CurrentStateEnum != UnitState.Idle) return;
        // 检查攻击范围（需要具体实现，此处简化）
        // ...
        attackTarget = target;
        attackSkillIndex = skillIndex;
        ChangeState(UnitState.Attacking);
    }

    // 实际执行攻击（由攻击状态调用）
    public virtual void PerformAttack()
    {
        // 此处暂时留空，具体由子类重写
        Debug.Log($"{unitName} 执行攻击");
    }

    // 立即死亡（由死亡状态调用）
    public void DieImmediate()
    {
        StopAllCoroutines();  // 停止所有协程，包括移动/攻击
        // 从网格中移除
        if (currentTile != null)
            currentTile.occupyingUnit = null;

        TurnManager turnManager = TurnManager.Instance;
        if (turnManager != null)
        {
            turnManager.RemoveUnit(this);
            if (turnManager.currentActiveUnit == this)
            {
                Debug.Log($"单位 {unitName} 在行动中死亡，强制结束回合");
                turnManager.UnitFinishedAction(this);
            }

        }

        AllUnits.Remove(this);
        gameObject.SetActive(false);
        Debug.Log($"{unitName} 死亡");
    }

    // 死亡（外部调用）
    public void Die()
    {
        if (CurrentStateEnum != UnitState.Dead)
            ChangeState(UnitState.Dead);
    }

    // 获取可攻击目标列表（使用缓存的 AllUnits）
    public List<Unit> GetAttackTargets(SkillData skill)
    {
        List<Unit> targets = new List<Unit>();
        Vector2Int myPos = currentTile.gridPos;
        foreach (Unit unit in AllUnits)
        {
            if (unit.unitType == unitType) continue; // 跳过同阵营
            if (unit.currentHP <= 0) continue;       // 跳过已死亡
            int distance = Mathf.Abs(myPos.x - unit.currentTile.gridPos.x) +
                           Mathf.Abs(myPos.y - unit.currentTile.gridPos.y);
            if (distance <= skill.attackRange)
            {
                targets.Add(unit);
            }
        }
        return targets;
    }

    // 重置回合（每回合开始调用）
    public void NewTurn()
    {
        if (CurrentStateEnum != UnitState.Dead)
        {
            ChangeState(UnitState.Idle);
        }
    }
}