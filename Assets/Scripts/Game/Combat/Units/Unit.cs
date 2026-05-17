using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using Game.Combat;
using Game.Map;
using Unity.VisualScripting;
using Game.Camera;

namespace Game.Combat.Units
{
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
        public void Enter(Unit unit)
        {
            // 进入空闲：确保速度为 0
            unit.SetMoveAnimation(0);
        }
        public void Update(Unit unit) { }
        public void Exit(Unit unit) { }
    }

    // 移动状态
    public class UnitMovingState : IUnitState
    {
        public void Enter(Unit unit)
        {
            // 启动移动协程，移动完成后自动切换回 Idle
            unit.SetMoveAnimation(unit.moveSpeed);
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
        public void Exit(Unit unit)
        {
            unit.SetMoveAnimation(0);
        }
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
            unit.PlayAttackAnimation(unit.currentSelectedSkillData);
            // 1. 执行伤害逻辑（可能导致敌人死亡，但不会立即胜利）
            unit.PerformAttackWithSkill();

            // 2. 获取技能的特效/动画时长（如果没有，默认0.5秒）
            float duration = unit.currentSelectedSkillData?.effectDuration ?? 0.5f;
            // 3. 等待特效播放完成
            yield return new WaitForSeconds(duration);
            unit.SetMoveAnimation(0);
            // 4. 特效结束，切换状态并通知回合管理器
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
            // 停止所有动作
            unit.SetMoveAnimation(0);

            // 如果有死亡动画，等待它播放完毕再销毁
            // 假设死亡动画时长为 2 秒
            unit.StartCoroutine(DestroyAfterAnimation(unit, 2f));
        }
        public void Update(Unit unit) { }
        public void Exit(Unit unit) { }
        private IEnumerator DestroyAfterAnimation(Unit unit, float delay)
        {
            yield return new WaitForSeconds(delay);
            unit.DieImmediate();
        }
    }

    public class Unit : MonoBehaviour
    {
        [Header("基础属性")]
        public string unitName;
        public UnitType unitType;
        public GameObject healthBarPrefab;

        [Header("战斗属性")]
        public int maxHP = 10;
        public int currentHP;
        public int moveRange = 3;
        public int baseAttack = 3;
        public float moveSpeed = 2f;
        public int attackRange = 1;     // 攻击范围（格，1为相邻）
        private List<SkillDataSO> skillData = new List<SkillDataSO>();

        [Header("引用")]
        public Tile currentTile;         // 当前所在格子

        [Header("动画")]
        public Animator animator;
        //定义动画参数的 Hash
        private int animIDSpeed;
        private int animIDHit;
        private int animIDDeath;
        private AnimatorOverrideController overrideController;

        // 静态列表，存储所有当前存活的单位
        public static List<Unit> AllUnits = new List<Unit>();

        public UnitState CurrentStateEnum { get; private set; }
        private IUnitState currentState;
        public SkillDataSO currentSelectedSkillData { get; private set; }

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
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator != null)
            {
                // 保存原始的 AnimatorController
                var originalController = animator.runtimeAnimatorController;
                // 创建 OverrideController 包裹原始控制器
                overrideController = new AnimatorOverrideController(originalController);
                // 将 OverrideController 赋值给 Animator
                animator.runtimeAnimatorController = overrideController;
            }
            // 缓存参数 ID
            animIDSpeed = Animator.StringToHash("Speed");
            animIDHit = Animator.StringToHash("Hit");
            animIDDeath = Animator.StringToHash("Death");
            // 初始状态设为 Idle
            ChangeState(UnitState.Idle);
            // 动态创建血条
            if (healthBarPrefab != null)
            {
                Debug.Log("创建");
                GameObject healthBarObj = Instantiate(healthBarPrefab, transform);
                HealthBar hpBar = healthBarObj.GetComponent<HealthBar>();
                hpBar.targetUnit = this;
                hpBar.slider.maxValue = maxHP;
                hpBar.slider.value = currentHP;
            }
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
        // 对外接口：攻击目标
        public void Attack(Unit target, int skillIndex)
        {
            if (CurrentStateEnum != UnitState.Idle) return;
            // 检查攻击范围（需要具体实现，此处简化）
            attackTarget = target;
            attackSkillIndex = skillIndex;
            ChangeState(UnitState.Attacking);
        }

        public void Attack(Unit target, SkillDataSO skillData)
        {
            if (CurrentStateEnum != UnitState.Idle) return;
            attackTarget = target;
            currentSelectedSkillData = skillData;
            ChangeState(UnitState.Attacking);
        }

        public virtual void PerformAttackWithSkill()
        {
            if (attackTarget == null || currentSelectedSkillData == null) return;
            ISkillEffect effect = SkillFactory.GetSkillEffect(currentSelectedSkillData.skillType);
            effect.Execute(this, attackTarget, currentSelectedSkillData);
        }

        // 实际执行攻击（由攻击状态调用）
        public virtual void PerformAttack()
        {
            // 此处暂时留空，具体由子类重写
            Debug.Log($"{unitName} 执行攻击");
        }

        public virtual void TakeDamage(int damage)
        {
            currentHP -= damage;

            // 1. 播放受击动画
            PlayHitAnimation();

            // 2. 如果死亡，播放死亡动画并进入 Dead 状态
            if (currentHP <= 0)
            {
                PlayDeathAnimation(); // 播放死亡动画
                Die(); // 进入死亡状态 (会调用 UnitDeadState)
            }
            else
            {
                // 3. 如果没死，触发屏幕震动等反馈
                CameraShake camShake = UnityEngine.Camera.main.GetComponent<CameraShake>();
                if (camShake != null)
                    camShake.Shake(0.1f, 0.1f);
            }
        }

        // 立即死亡（由死亡状态调用）
        public void DieImmediate()
        {
            StopAllCoroutines();  // 停止所有协程，包括移动/攻击
                                  // 从网格中移除
            if (currentTile != null)
                currentTile.occupyingUnit = null;
            HealthBar hpBar = GetComponentInChildren<HealthBar>();
            if (hpBar != null) Destroy(hpBar.gameObject);
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
            Debug.Log($"{unitName} 死亡");
            if (TurnManager.Instance != null && unitType == UnitType.Enemy)
            {
                TurnManager.Instance.OnEnemyDied(this as EnemyUnit);
            }
            Destroy(gameObject);
        }

        // 死亡（外部调用）
        public void Die()
        {
            if (CurrentStateEnum != UnitState.Dead)
                ChangeState(UnitState.Dead);
        }

        // 获取可攻击目标列表（使用缓存的 AllUnits）
        public List<Unit> GetAttackTargets(SkillDataSO skill)
        {
            List<Unit> targets = new List<Unit>();
            Vector2Int myPos = currentTile.gridPos;
            foreach (Unit unit in AllUnits)
            {
                if (unit.unitType == unitType) continue; // 跳过同阵营
                if (unit.currentHP <= 0) continue;       // 跳过已死亡
                int distance = Mathf.Abs(myPos.x - unit.currentTile.gridPos.x) +
                               Mathf.Abs(myPos.y - unit.currentTile.gridPos.y);
                if (distance <= skill.skillRange)
                {
                    targets.Add(unit);
                }
            }
            return targets;
        }
        public virtual void PlayAttackAnimation(SkillDataSO skillData)
        {
            // 播放音效
            /*if (skillData.hitSound != null)
            {
                AudioSource.PlayClipAtPoint(skillData.hitSound, transform.position);
            }*/

            if (animator == null || skillData.skillAnimation == null)
            {
                Debug.LogWarning("Missing animator or animation clip");
                return;
            }
            // 覆盖动画
            overrideController["SkillPlay"] = skillData.skillAnimation;

            animator.SetTrigger("PlaySkill");
        }
        //设置移动速度
        public void SetMoveAnimation(float speed)
        {
            if (animator != null)
            {
                animator.SetFloat(animIDSpeed, speed);
            }
        }

        //播放受击动画
        public virtual void PlayHitAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(animIDHit);
            }
        }

        //播放死亡动画
        public virtual void PlayDeathAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(animIDDeath);
            }
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
}