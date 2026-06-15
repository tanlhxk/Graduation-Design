using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Unity.VisualScripting;
using Game.Map;
using Game.Combat;
using Game.Combat.Units;
using Unit = Game.Combat.Units.Unit;
using FriendlyUnit = Game.Combat.Units.FriendlyUnit;
using EnemyUnit = Game.Combat.Units.EnemyUnit;
using Game.RogueLike;
using Game.Combat.AI;
using Game.Core;

namespace Game.Combat
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance;
        public enum TurnPhase
        {
            PlayerTurn,   // 玩家回合
            EnemyTurn,    // 敌人回合
            TurnEnd,      // 回合结束/切换中
            None
        }


        [Header("单位列表")]
        public List<Unit> allUnits;
        public List<FriendlyUnit> playerUnits;
        public List<EnemyUnit> enemyUnits;

        [Header("当前行动单位")]
        public Unit currentActiveUnit;
        private int currentUnitIndex = 0;

        private bool isGameReady = false;
        private bool isBattleOver = false;
        public TurnPhase currentPhase = TurnPhase.None;
        public int currentTurnNumber = 1;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        public void OnGameInitialized()
        {
            Debug.Log("TurnManager 接收到初始化信号，开始收集单位...");

            // 直接使用 Unit.AllUnits，无需再次查找
            allUnits = Unit.AllUnits;
            playerUnits = new List<FriendlyUnit>();
            enemyUnits = new List<EnemyUnit>();
            foreach (Unit unit in Unit.AllUnits)
            {
                if (unit is FriendlyUnit friendly)
                    playerUnits.Add(friendly);
                else if (unit is EnemyUnit enemy)
                    enemyUnits.Add(enemy);
            }

            // 调试输出：看看找到了多少单位
            Debug.Log($"找到 {playerUnits.Count} 个玩家单位, {enemyUnits.Count} 个敌人单位");

            isGameReady = true;

            // 尝试开始游戏
            AttemptStartGame();
        }
        void AttemptStartGame()
        {
            if (isGameReady && currentPhase == TurnPhase.None)
            {
                Debug.Log("准备开始第一回合...");
                currentTurnNumber = 1;
                // 再次防御性检查
                if (playerUnits == null || playerUnits.Count == 0)
                {
                    Debug.LogError("错误：没有找到玩家单位！");
                    return;
                }

                currentPhase = TurnPhase.PlayerTurn;
                StartPlayerTurn();
            }
            else
            {
                Debug.Log($"无法开始游戏。准备状态: {isGameReady}, 当前阶段: {currentPhase}");
            }
        }
        // 开始玩家回合
        void StartPlayerTurn()
        {
            CheckVictory();
            if (isBattleOver) return;
            currentPhase = TurnPhase.PlayerTurn;
            Debug.Log($"===== 第 {currentTurnNumber} 回合 - 玩家回合 =====");

            // 重置所有玩家单位状态
            foreach (var unit in playerUnits)
            {
                unit.NewTurn();
            }

            // 激活第一个玩家单位
            currentUnitIndex = 0;
            ActivateUnit(playerUnits[currentUnitIndex]);
        }

        // 激活一个单位
        void ActivateUnit(Unit unit)
        {
            currentActiveUnit = unit;
            Debug.Log($"当前行动单位: {unit.unitName}");

            if (unit is FriendlyUnit)
                PlayerInput.Instance?.ClearSelection();

            if (unit is EnemyUnit enemy)
            {
                EnemyAI ai = enemy.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    StartCoroutine(ai.PerformTurnAction());
                }
                else
                {
                    Debug.LogError($"{enemy.unitName} 缺少 EnemyAI 组件！");
                    // 如果没有 AI，直接结束该单位回合，避免卡死
                    UnitFinishedAction(unit);
                }
            }
        }

        // 单位完成行动
        public void UnitFinishedAction(Unit unit)
        {
            if (isBattleOver) return;  // 战斗已结束，忽略后续
            if (unit != currentActiveUnit) return;

            MovementSystem.Instance.ClearHighlights();

            if (currentPhase == TurnPhase.PlayerTurn)
            {
                currentUnitIndex++;
                if (currentUnitIndex < playerUnits.Count)
                {
                    ActivateUnit(playerUnits[currentUnitIndex]);
                }
                else
                {
                    currentActiveUnit = null;
                    // 所有玩家行动完成，检查胜利
                    CheckVictory();
                    if (!isBattleOver && enemyUnits.Count > 0)
                        Invoke(nameof(StartEnemyTurn), 0.5f);
                }
            }
            else if (currentPhase == TurnPhase.EnemyTurn)
            {
                currentUnitIndex++;
                if (currentUnitIndex < enemyUnits.Count)
                {
                    ActivateUnit(enemyUnits[currentUnitIndex]);
                    //StartCoroutine(ExecuteEnemyTurns());
                }
                else
                {
                    CheckVictory();
                    if (!isBattleOver)
                    {
                        currentTurnNumber++;
                        Invoke(nameof(StartPlayerTurn), 0.5f);
                    }
                }
            }
            CheckVictory();
        }

        // 开始敌人回合
        void StartEnemyTurn()
        {
            CheckVictory();
            if (isBattleOver) return;
            enemyUnits.RemoveAll(u => u == null || u.currentHP <= 0);
            if (enemyUnits.Count == 0)
            {
                CheckVictory();
                return;
            }
            currentPhase = TurnPhase.EnemyTurn;
            Debug.Log($"===== 第 {currentTurnNumber} 回合 - 敌人回合 =====");
            Debug.Log($"===== 剩余 {enemyUnits.Count} 个敌人 =====");

            enemyUnits.RemoveAll(u => u == null || u.currentHP <= 0);

            // 重置所有敌人单位状态
            foreach (var unit in enemyUnits)
            {
                unit.NewTurn();
            }
            // 重置索引
            currentUnitIndex = 0;

            // 激活第一个敌人单位
            if (enemyUnits.Count > 0)
            {
                ActivateUnit(enemyUnits[currentUnitIndex]);
            }
        }

        // 执行敌人AI行动
        IEnumerator ExecuteEnemyTurns()
        {
            while (currentUnitIndex < enemyUnits.Count)
            {
                EnemyUnit enemy = enemyUnits[currentUnitIndex];
                if (enemy == null || enemy.currentHP <= 0)
                {
                    currentUnitIndex++;
                    continue;
                }

                EnemyAI ai = enemy.GetComponent<EnemyAI>();
                if (ai == null)
                {
                    currentUnitIndex++;
                    continue;
                }

                ActivateUnit(enemy);

                //  等待 AI 协程完全结束
                yield return StartCoroutine(ai.PerformTurnAction());

                // 防止死循环的保险机制：如果 AI 没有正确结束行动，强制推进
                if (!ai.hasFinishedAction)
                {
                    Debug.LogWarning($"强制结束 {enemy.unitName} 的行动，AI 可能陷入死锁");
                    TurnManager.Instance.UnitFinishedAction(enemy);
                }

                // 确保索引增加
                currentUnitIndex++;
            }
            // 切换回合
            currentTurnNumber++;
            StartPlayerTurn();
        }
        float EvaluateSkill(EnemyUnit caster, FriendlyUnit target, SkillDataSO skill)
        {
            float score = 0f;
            // 基础伤害期望（假设伤害倍率直接乘攻击力）
            float damage = caster.baseAttack * skill.damageMultiplier;
            score += damage;
            // 如果目标血量很低，可以增加权重（斩杀倾向）
            if (target.currentHP <= damage) score += 50f;
            // 如果技能有特殊效果（如眩晕），可额外加分
            // 如果技能在冷却中，score = -1 跳过
            return score;
        }
        // 找到最近的玩家
        FriendlyUnit FindNearestPlayer(EnemyUnit enemy)
        {
            FriendlyUnit nearest = null;
            int minDistance = int.MaxValue;

            foreach (var player in playerUnits)
            {
                if (player.currentHP <= 0) continue;

                int distance = Mathf.Abs(enemy.currentTile.gridPos.x - player.currentTile.gridPos.x) +
                              Mathf.Abs(enemy.currentTile.gridPos.y - player.currentTile.gridPos.y);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = player;
                }
            }

            return nearest;
        }

        // 找到离玩家最近的可行走格子
        Tile FindTileClosestToPlayer(List<Tile> tiles, Unit player)
        {
            Tile bestTile = null;
            int minDistance = int.MaxValue;

            foreach (var tile in tiles)
            {
                int distance = Mathf.Abs(tile.gridPos.x - player.currentTile.gridPos.x) +
                              Mathf.Abs(tile.gridPos.y - player.currentTile.gridPos.y);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestTile = tile;
                }
            }

            return bestTile;
        }
        public void RemoveUnit(Unit unit)
        {
            if (unit.unitType == UnitType.Enemy)
            {
                if (enemyUnits.Contains(unit))
                    enemyUnits.Remove((EnemyUnit)unit);
            }
            else if (unit.unitType == UnitType.Player)
            {
                if (playerUnits.Contains(unit))
                    playerUnits.Remove((FriendlyUnit)unit);
            }

            if (allUnits.Contains(unit))
                allUnits.Remove(unit);

            // 如果当前行动的单位就是被移除的，需要处理（见下文）
            if (currentActiveUnit == unit)
            {
                // 可以立即结束该单位行动，并转到下一个
                currentActiveUnit = null;
                // 如果当前是敌人回合，可能需要继续下一个敌人
            }
            CheckPlayerDefeat();
        }
        public void OnEnemyDied(EnemyUnit enemy)
        {
            enemyUnits.Remove(enemy);
            //死亡特效
            CheckVictory();
        }
        private void CheckPlayerDefeat()
        {
            if (isBattleOver) return;

            // 清理无效引用
            playerUnits.RemoveAll(p => p == null || p.currentHP <= 0);

            if (playerUnits.Count == 0)
            {
                isBattleOver = true;
                currentPhase = TurnPhase.None;
                if (currentActiveUnit != null)
                {
                    MovementSystem.Instance.ClearHighlights();
                    currentActiveUnit = null;
                }
                StopAllCoroutines();
                Debug.Log("玩家队伍全灭，游戏结束！");
                RouteManager.Instance?.EndGame(false);   // 失败结束
            }
        }
        public void ResetBattle()
        {
            isBattleOver = false;
            currentPhase = TurnPhase.None;
            currentActiveUnit = null;
            currentUnitIndex = 0;
            currentTurnNumber = 1;
            StopAllCoroutines();    // 停止残留的敌人 AI 协程
            MovementSystem.Instance?.ClearHighlights(); // 清除高亮
        }
        private void CheckVictory()
        {
            if (isBattleOver) return;
            enemyUnits.RemoveAll(u => u == null || u.currentHP <= 0);
            if (enemyUnits.Count == 0)
            {
                isBattleOver = true;
                Debug.Log("战斗胜利！");
                currentPhase = TurnPhase.None;
                if (currentActiveUnit != null)
                {
                    MovementSystem.Instance.ClearHighlights();
                    currentActiveUnit = null;
                }
                StopAllCoroutines();  // 停止所有敌人 AI 协程
                if (RouteManager.Instance != null)
                    RouteManager.Instance.OnRoomCleared();
                else
                    Debug.LogWarning("战斗胜利，但场景中未找到 RouteManager，跳过房间清理流程。");
            }
        }
    }
}