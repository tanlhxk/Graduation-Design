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
            // 同步 Animator 到 Idle，避免上一回合 Hit/SkillPlay 残留导致无法再次进入 Walk
            unit.SyncAnimatorToIdle();
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
        public List<SkillDataSO> skillData = new List<SkillDataSO>();

        [Header("引用")]
        public Tile currentTile;         // 当前所在格子

        [Header("动画")]
        public Animator animator;
        //定义动画参数的 Hash
        private int animIDSpeed;
        private int animIDHit;
        private int animIDDeath;
        private int animIDPlaySkill;
        private const int BaseLayerIndex = 0;
        private const int ActionLayerIndex = 1;
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

        public virtual void Awake()
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
                var originalController = animator.runtimeAnimatorController;
                if (originalController != null)
                {
                    overrideController = new AnimatorOverrideController(originalController);
                    animator.runtimeAnimatorController = overrideController;
                }
            }
            // 缓存参数 ID
            animIDSpeed = Animator.StringToHash("Speed");
            animIDHit = Animator.StringToHash("Hit");
            animIDDeath = Animator.StringToHash("Death");
            animIDPlaySkill = Animator.StringToHash("PlaySkill");
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
        public virtual void MoveTo(Tile targetTile)
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
            Debug.Log($"[AddSkill] 成功向 {unitName} 添加了技能 {skill.skillName}，当前技能数量: {skillData.Count}");
        }

        // 外部读取技能
        public List<SkillDataSO> GetUnitSkills()
        {
            return skillData;
        }

        // 1. 提供一个公共方法，根据索引返回技能数据
        public SkillDataSO GetSkillData(int index)
        {
            // 如果列表为空，返回 null
            if (skillData == null || skillData.Count == 0)
            {
                Debug.LogError($"{unitName} 没有配置任何技能！");
                return null;
            }

            // 如果索引为 0 且列表为空，或者索引超出范围
            // 注意：索引 0 通常代表普攻，如果列表里没有，应该返回 null 或者默认数据
            if (index < 0 || index >= skillData.Count)
            {
                Debug.LogError($"{unitName} 尝试访问技能索引 {index}，但技能列表只有 {skillData.Count} 个技能。");
                return null;
            }

            return skillData[index];
        }
        // 对外接口：攻击目标
        public void Attack(Unit target, int skillIndex)
        {
            if (CurrentStateEnum != UnitState.Idle) return;

            attackTarget = target;
            attackSkillIndex = skillIndex;

            currentSelectedSkillData = GetSkillData(skillIndex);

            if (currentSelectedSkillData == null)
            {
                Debug.LogError($"{unitName} 无法解析技能索引 {skillIndex}，攻击取消。");
                return;
            }

            ChangeState(UnitState.Attacking);
        }

        public void Attack(Unit target, SkillDataSO skillData)
        {
            if (CurrentStateEnum != UnitState.Idle) return;
            attackTarget = target;
            currentSelectedSkillData = skillData;
            ChangeState(UnitState.Attacking);
        }
        /// <summary>
        /// 攻击并等待结束（供 AI 协程调用）
        /// </summary>
        public IEnumerator AttackAndWait(Unit target, int skillIndex)
        {
            if (CurrentStateEnum != UnitState.Idle) yield break;

            attackTarget = target;
            attackSkillIndex = skillIndex;
            currentSelectedSkillData = GetSkillData(skillIndex);

            if (currentSelectedSkillData == null) yield break;

            ChangeState(UnitState.Attacking);
            while (CurrentStateEnum == UnitState.Attacking)
            {
                yield return null; // 等待一帧
            }
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
        public virtual void Die()
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

            // 增加对 skillData 本身的判空
            if (skillData == null)
            {
                Debug.LogError($"{unitName} 尝试播放攻击动画，但 skillData 为 null！");
                return;
            }

            // 检查动画组件和动画剪辑
            if (animator == null)
            {
                Debug.LogWarning($"{unitName} 缺少 Animator 组件");
                return;
            }

            // 检查技能动画是否为空
            if (skillData.skillAnimation == null)
            {
                Debug.LogError($"{unitName} 的技能 [{skillData.skillName}] 缺少动画剪辑！请检查 Resources 配置。");

                // 如果没有配置动画，直接播放默认攻击 Trigger（假设 Animator 中有默认的 Attack 状态）
                animator.SetTrigger("Attack");
                return;
            }
            // 确保 overrideController 已初始化
            if (overrideController == null)
            {
                // 尝试重新创建 overrideController
                var originalController = animator.runtimeAnimatorController;
                if (originalController != null)
                {
                    overrideController = new AnimatorOverrideController(originalController);
                    animator.runtimeAnimatorController = overrideController;
                }
                else
                {
                    Debug.LogError($"{unitName} 的 Animator 没有 RuntimeAnimatorController");
                    return;
                }
            }
            // 覆盖动画
            overrideController["SkillPlay"] = skillData.skillAnimation;

            animator.SetTrigger("PlaySkill");
        }
        // 强制将 Animator 各层复位到 Idle（逻辑状态与动画状态解耦时的同步入口）
        public void SyncAnimatorToIdle()
        {
            if (animator == null) return;

            animator.Play("Idle", BaseLayerIndex, 0f);
            animator.Play("Idle", ActionLayerIndex, 0f);
            animator.ResetTrigger(animIDHit);
            animator.ResetTrigger(animIDPlaySkill);
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
                // Hit 状态 WriteDefaultValues 会把 Speed 写回 0，移动中需立即恢复以便播完后回到 Walk
                if (CurrentStateEnum == UnitState.Moving)
                    SetMoveAnimation(moveSpeed);
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
        public virtual void NewTurn()
        {
            if (CurrentStateEnum != UnitState.Dead)
            {
                ChangeState(UnitState.Idle);
            }
        }
    }
}