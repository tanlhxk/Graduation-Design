using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Map;
using Game.Camera;
using Game.Combat.AI;

namespace Game.Combat.Units
{
    public class EnemyUnit : Unit
    {
        private EnemyAI ai;
        public override void Awake()
        {
            base.Awake(); // 确保基类 Awake 执行
            ai = GetComponent<EnemyAI>();
            if (ai == null)
                ai = gameObject.AddComponent<EnemyAI>();
        }
        void Start()
        {
            // 确保基础血量初始化
            if (currentHP == 0) currentHP = maxHP;
        }
        public bool CanUseSkill(FriendlyUnit target, SkillDataSO skillData)
        {
            if (skillData == null) return false;

            // 这里可以写通用的距离判断逻辑
            int distance = GridManager.GetDistance(currentTile, target.currentTile);
            return distance <= skillData.skillRange;
        }
        // 攻击范围判断
        public bool CanAttack(FriendlyUnit target, int skillIndex)
        {
            if (target == null || target.currentHP <= 0) return false;

            // 计算曼哈顿距离
            int distance = Mathf.Abs(currentTile.gridPos.x - target.currentTile.gridPos.x) +
                          Mathf.Abs(currentTile.gridPos.y - target.currentTile.gridPos.y);

            // 如果没有技能数据，或者索引为0（普攻），使用单位的 attackRange
            // 否则使用技能的 attackRange
            int effectiveRange = attackRange;
            List<SkillDataSO> skillDataSO = GetUnitSkills();
            if (skillDataSO.Count > skillIndex && skillIndex > 0)
            {
                effectiveRange = skillDataSO[skillIndex].skillRange;
            }

            return distance <= effectiveRange;
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

            // 可以发送事件，让 CameraController 监听
            // 或者如果保持原样，判空即可
            if (CameraController.Instance != null)
                CameraController.Instance.ForcePosition(transform.position);

            // 执行伤害
            target.TakeDamage(damage);

            // 摄像机震动
            CameraShake camShake = UnityEngine.Camera.main.GetComponent<CameraShake>();
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
        public override void Die()
        {
            if (ai != null)
                ai.ChangeState(EnemyAI.AIState.Dead);
            base.Die();
        }
        public override void NewTurn()
        {
            base.NewTurn();
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null) ai.OnTurnStart();
        }
        public override void MoveTo(Tile targetTile)
        {
            // 先调用基类移动
            base.MoveTo(targetTile);
        }
    }
}