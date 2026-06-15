using UnityEngine;
using System.Collections;
using Game.Combat.Units;
using Game.Map;
using Game.Core;
using System.Collections.Generic;
using static UnityEngine.GraphicsBuffer;

namespace Game.Combat.AI
{
    /// <summary>
    /// 敌人AI状态机
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        [Header("AI 参数")]
        public float patrolRadius = 5f;           // 巡逻半径（格）
        public float chaseDistance = 5f;          // 追击触发距离
        public float attackDistance = 1f;         // 攻击距离

        [Header("状态定时")]
        public float idleDuration = 1f;           // 空闲后等待时间（秒）
        public float patrolWaitTime = 0.5f;       // 巡逻点停留时间

        // 组件引用
        private EnemyUnit enemyUnit;
        private MovementSystem movementSystem;
        private TurnManager turnManager;

        // 状态机
        public AIState currentAIState { get; private set; }
        private Vector2Int patrolTargetPos;       // 巡逻目标点
        private float stateTimer;                 // 状态计时器

        // 巡逻点列表（在初始位置周围随机生成）
        private Vector2Int[] patrolPoints;
        private int patrolIndex = 0;

        // 冷却相关
        private bool hasAttackedThisTurn = false;
        public bool hasFinishedAction = false;
        private bool hasTriggeredUnitAction = false;

        public enum AIState
        {
            Idle,       // 空闲，原地待机
            Patrol,     // 巡逻，在固定区域移动
            Chase,      // 追击玩家
            Attack,     // 攻击
            Dead        // 死亡
        }

        void Awake()
        {
            enemyUnit = GetComponent<EnemyUnit>();
            movementSystem = MovementSystem.Instance;
            turnManager = TurnManager.Instance;

            if (enemyUnit == null)
            {
                Debug.LogError("EnemyAI: 找不到 EnemyUnit 组件");
                return;
            }

            // 初始状态为 Idle
            ChangeState(AIState.Idle);
        }

        void Start()
        {
            if (movementSystem == null) movementSystem = MovementSystem.Instance;
            if (turnManager == null) turnManager = TurnManager.Instance;
            // 生成巡逻点（以出生点为中心，半径 patrolRadius 内的随机可行走格子）
            GeneratePatrolPoints();
        }

        private void GeneratePatrolPoints()
        {
            Vector2Int startPos = enemyUnit.currentTile.gridPos;
            patrolPoints = new Vector2Int[4];

            // 定义四个方向：右、左、上、下
            Vector2Int[] directions = new Vector2Int[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            for (int i = 0; i < 4; i++)
            {
                int distance = Random.Range(1, (int)patrolRadius + 1);
                Vector2Int targetPos = startPos + directions[i] * distance;

                // 边界检查
                targetPos.x = Mathf.Clamp(targetPos.x, 0, GridManager.Instance.Width - 1);
                targetPos.y = Mathf.Clamp(targetPos.y, 0, GridManager.Instance.Height - 1);

                patrolPoints[i] = targetPos;
            }
        }

        /// <summary>
        /// 切换 AI 状态
        /// </summary>
        public void ChangeState(AIState newState)
        {
            if (currentAIState == newState) return;
            // 退出当前状态
            OnExitState(currentAIState);
            currentAIState = newState;
            // 进入新状态
            OnEnterState(currentAIState);
        }

        private void OnEnterState(AIState state)
        {
            switch (state)
            {
                case AIState.Idle:
                    stateTimer = idleDuration;
                    break;
                case AIState.Patrol:
                    // 选择第一个巡逻点
                    patrolIndex = 0;
                    patrolTargetPos = patrolPoints[0];
                    stateTimer = patrolWaitTime;
                    break;
                case AIState.Chase:
                    break;
                case AIState.Attack:
                    // 攻击状态不维持，执行一次攻击后立刻切回 Chase 或 Patrol
                    break;
                case AIState.Dead:
                    // 什么也不做，等待 Unit 真正死亡
                    break;
            }
        }

        private void OnExitState(AIState state)
        {
            // 留空
        }

        /// <summary>
        /// 每回合由 TurnManager 调用，执行 AI 行动
        /// </summary>
        public IEnumerator PerformTurnAction()
        {
            Debug.Log($"{enemyUnit.unitName} AI 开始行动，状态={currentAIState}");
            hasAttackedThisTurn = false;   // 每回合开始重置攻击标记
            hasFinishedAction = false;
            hasTriggeredUnitAction = false; // 重置标志位
            if (enemyUnit.currentHP <= 0)
            {
                ChangeState(AIState.Dead);
                yield break;
            }
            // 更新状态（检查转换条件）
            UpdateStateTransitions();

            // 根据当前状态执行行动
            switch (currentAIState)
            {
                case AIState.Idle:
                    yield return StartCoroutine(IdleAction());
                    break;
                case AIState.Patrol:
                    yield return StartCoroutine(PatrolAction());
                    break;
                case AIState.Chase:
                    yield return StartCoroutine(ChaseAction());
                    break;
                case AIState.Attack:
                    yield return StartCoroutine(AttackAction());
                    break;
                case AIState.Dead:
                    // 死亡，不行动
                    yield break;
            }
            if(!hasTriggeredUnitAction && currentAIState != AIState.Dead)
{
                Debug.Log($"{enemyUnit.unitName} AI 未触发单位动作，手动结束回合");
                yield return null;
                turnManager.UnitFinishedAction(enemyUnit);
            }
        }

        /// <summary>
        /// 检查状态转换条件（每回合调用一次）
        /// </summary>
        private void UpdateStateTransitions()
        {
            if (currentAIState == AIState.Dead) return;

            // 获取玩家单位（假设只有第一个玩家单位）
            FriendlyUnit player = GetNearestPlayer();
            if (player == null || player.currentHP <= 0)
            {
                // 没有存活的玩家，进入空闲
                if (currentAIState != AIState.Idle)
                    ChangeState(AIState.Idle);
                return;
            }

            int distanceToPlayer = GridManager.GetDistance(enemyUnit.currentTile, player.currentTile);

            // 死亡判定已在外部处理
            // 攻击判定：在攻击范围内，且攻击未冷却
            if (distanceToPlayer <= attackDistance && !hasAttackedThisTurn)
            {
                if (currentAIState != AIState.Attack)
                    ChangeState(AIState.Attack);
                return;
            }

            // 追击判定：在追击范围内
            if (distanceToPlayer <= chaseDistance)
            {
                if (currentAIState != AIState.Chase && currentAIState != AIState.Attack)
                    ChangeState(AIState.Chase);
                return;
            }

            // 否则进入巡逻或空闲
            if (currentAIState == AIState.Chase || currentAIState == AIState.Attack)
            {
                ChangeState(AIState.Patrol);
            }
            else if (currentAIState != AIState.Patrol && currentAIState != AIState.Idle)
            {
                ChangeState(AIState.Patrol);
            }
        }

        /// <summary>
        /// 空闲行动：什么都不做，等待计时结束
        /// </summary>
        private IEnumerator IdleAction()
        {
            // 等待定时器（模拟发呆）
            yield return new WaitForSeconds(stateTimer);
        }

        /// <summary>
        /// 巡逻行动：向当前巡逻点移动
        /// </summary>
        private IEnumerator PatrolAction()
        {
            if (enemyUnit.currentTile.gridPos == patrolTargetPos || !IsTileWalkable(patrolTargetPos))
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                patrolTargetPos = patrolPoints[patrolIndex];
            }

            Tile targetTile = GridManager.Instance.GetTile(patrolTargetPos);
            if (targetTile != null && targetTile.IsWalkable() && targetTile != enemyUnit.currentTile)
            {
                hasTriggeredUnitAction = true;
                Debug.Log($"{enemyUnit.unitName} 巡逻移动到 {patrolTargetPos}");
                enemyUnit.MoveTo(targetTile);
                yield break;
            }
            else
            {
                // 无法移动，直接结束
                hasFinishedAction = true;
                yield break;
            }
        }

        /// <summary>
        /// 追击行动：向玩家移动一步
        /// </summary>
        private IEnumerator ChaseAction()
        {
            FriendlyUnit player = GetNearestPlayer();
            if (player == null)
            {
                hasFinishedAction = true;
                yield break;
            }

            // 获取可移动格子
            List<Tile> moveableTiles = movementSystem.GetMoveableTiles(enemyUnit, enemyUnit.moveRange);
            if (moveableTiles.Count == 0)
            {
                Debug.Log($"{enemyUnit.unitName} 无可移动格子，放弃移动");
                hasFinishedAction = true;
                yield break;
            }

            // 找到离玩家最近的可行走格子
            Tile targetTile = FindTileClosestToPlayer(moveableTiles, player);
            if (targetTile == null || targetTile == enemyUnit.currentTile)
            {
                Debug.Log($"{enemyUnit.unitName} 已位于最佳位置或找不到目标格子");
                hasFinishedAction = true;
                yield break;
            }

            // 标记已经触发了单位动作，防止 AI 协程重复调用回合结束
            hasTriggeredUnitAction = true;
            Debug.Log($"{enemyUnit.unitName} 向 {targetTile.gridPos} 移动");
            enemyUnit.MoveTo(targetTile);
            yield break;
        }

        /// <summary>
        /// 攻击行动：对玩家发动攻击
        /// </summary>
        private IEnumerator AttackAction()
        {
            FriendlyUnit player = GetNearestPlayer();
            if (player == null || hasAttackedThisTurn)
            {
                // 如果没有目标或已攻击过，直接结束行动
                hasFinishedAction = true;
                yield break;
            }

            // 检查是否在攻击范围内
            int distance = GridManager.GetDistance(enemyUnit.currentTile, player.currentTile);
            if (distance > attackDistance)
            {
                // 如果不在攻击距离内，退回 Chase 状态寻找位置
                ChangeState(AIState.Chase);
                hasFinishedAction = true;
                yield break;
            }
            Debug.Log($"[EnemyAI] {enemyUnit.unitName} 准备攻击！");
            Debug.Log($"[EnemyAI] 目标: {player.unitName}");
            Debug.Log($"[EnemyAI] 敌人拥有的技能数量: {enemyUnit.skillData.Count}");

            // 打印出所有技能的详细信息
            for (int i = 0; i < enemyUnit.skillData.Count; i++)
            {
                var skill = enemyUnit.skillData[i];
                if (skill != null)
                {
                    Debug.Log($"[EnemyAI] 索引 {i} 的技能: {skill.skillName}, 动画: {(skill.skillAnimation != null ? "已配置" : "NULL!")}");
                }
                else
                {
                    Debug.LogError($"[EnemyAI] 索引 {i} 的技能是 NULL！");
                }
            }
            hasTriggeredUnitAction = true;
            // 打印即将使用的索引
            int attackIndex = 0;
            Debug.Log($"[EnemyAI] 即将使用索引 {attackIndex} 发起攻击");
            // 执行攻击
            yield return StartCoroutine(enemyUnit.AttackAndWait(player, attackIndex));
            hasAttackedThisTurn = true;
        }

        /// <summary>
        /// 获取最近的存活玩家
        /// </summary>
        private FriendlyUnit GetNearestPlayer()
        {
            FriendlyUnit nearest = null;
            int minDist = int.MaxValue;
            foreach (var player in turnManager.playerUnits)
            {
                if (player == null || player.currentHP <= 0) continue;
                int dist = GridManager.GetDistance(enemyUnit.currentTile, player.currentTile);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = player;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 从给定的格子列表中，找到离玩家最近的格子
        /// </summary>
        private Tile FindTileClosestToPlayer(List<Tile> tiles, FriendlyUnit player)
        {
            Tile bestTile = null;
            int minDistance = int.MaxValue;
            Vector2Int playerPos = player.currentTile.gridPos;

            foreach (var tile in tiles)
            {
                if (tile == null || !tile.IsWalkable()) continue;
                int dist = Mathf.Abs(tile.gridPos.x - playerPos.x) + Mathf.Abs(tile.gridPos.y - playerPos.y);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTile = tile;
                }
            }
            return bestTile;
        }

        private bool IsTileWalkable(Vector2Int pos)
        {
            Tile t = GridManager.Instance.GetTile(pos);
            return t != null && t.IsWalkable();
        }
        public void OnTurnStart()
        {
            hasAttackedThisTurn = false;
            hasFinishedAction = false;
            hasTriggeredUnitAction = false;
        }
    }
}