using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Unity.VisualScripting;

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

    public GridManager gridManager;
    public MovementSystem movementSystem;
    private bool isGameReady = false;
    public TurnPhase currentPhase = TurnPhase.None;
    public int currentTurnNumber = 1;

    public void OnGameInitialized()
    {
        Debug.Log("TurnManager 接收到初始化信号，开始收集单位...");

        // 此时 GameManager 已经生成了玩家和敌人
        allUnits = new List<Unit>(FindObjectsOfType<Unit>());
        playerUnits = new List<FriendlyUnit>(FindObjectsOfType<FriendlyUnit>());
        enemyUnits = new List<EnemyUnit>(FindObjectsOfType<EnemyUnit>());

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

        // 计算并高亮可移动范围
        List<Tile> moveableTiles = movementSystem.GetMoveableTiles(unit, unit.moveRange);
        //movementSystem.HighlightMoveRange(moveableTiles);

        // 通知UI更新
        // UIManager.Instance.ShowUnitActions(unit);
    }

    // 单位完成行动
    public void UnitFinishedAction(Unit unit)
    {
        if (unit != currentActiveUnit) return;

        // 清除高亮
        movementSystem.ClearHighlights(GridManager.tileDict);

        // 移动到下一个单位
        if (currentPhase == TurnPhase.PlayerTurn)
        {
            currentUnitIndex++;

            if (currentUnitIndex < playerUnits.Count)
            {
                // 还有玩家单位未行动
                ActivateUnit(playerUnits[currentUnitIndex]);
            }
            else
            {
                // 所有玩家单位行动完毕，进入敌人回合
                Invoke(nameof(StartEnemyTurn), 0.5f);
            }
        }
        else if (currentPhase == TurnPhase.EnemyTurn)
        {
            currentUnitIndex++;

            if (currentUnitIndex < enemyUnits.Count)
            {
                // 还有敌人单位未行动
                ActivateUnit(enemyUnits[currentUnitIndex]);
                // 敌人AI自动行动
                StartCoroutine(ExecuteEnemyTurn(enemyUnits[currentUnitIndex]));
            }
            else
            {
                // 所有敌人行动完毕，开始新的一回合
                currentTurnNumber++;
                Invoke(nameof(StartPlayerTurn), 0.5f);
            }
        }
    }

    // 开始敌人回合
    void StartEnemyTurn()
    {
        currentPhase = TurnPhase.EnemyTurn;
        Debug.Log($"===== 第 {currentTurnNumber} 回合 - 敌人回合 =====");
        Debug.Log($"===== 剩余 {enemyUnits.Count} 个敌人 =====");

        enemyUnits.RemoveAll(u => u == null || u.currentHP <= 0);

        // 重置所有敌人单位状态
        foreach (var unit in enemyUnits)
        {
            unit.NewTurn();
        }

        // 激活第一个敌人单位
        currentUnitIndex = 0;
        if (enemyUnits.Count > 0)
        {
            ActivateUnit(enemyUnits[currentUnitIndex]);
            StartCoroutine(ExecuteEnemyTurn(enemyUnits[currentUnitIndex]));
        }
        else
        {
            // 没有敌人，直接进入下一回合
            currentTurnNumber++;
            StartPlayerTurn();
        }
    }

    // 执行敌人AI行动
    IEnumerator ExecuteEnemyTurn(EnemyUnit enemy)
    {
        // 如果敌人在此之前已经死亡，直接结束
        if (enemy == null || enemy.currentHP <= 0)
        {
            UnitFinishedAction(enemy);
            yield break;
        }

        yield return new WaitForSeconds(0.5f); // 等待一下，让玩家看清

        // 简单的AI逻辑
        // 1. 检查是否可以攻击玩家
        FriendlyUnit targetPlayer = FindNearestPlayer(enemy);

        if (targetPlayer != null && enemy.CanAttack((FriendlyUnit)targetPlayer, 1))
        {
            // 可以攻击，直接攻击
            Debug.Log($"{enemy.unitName} 攻击 {targetPlayer.unitName}");
            enemy.Attack(targetPlayer);
            yield return new WaitForSeconds(0.5f);
            UnitFinishedAction(enemy);
        }
        else
        {
            // 不能攻击，尝试向玩家移动
            if (targetPlayer != null)
            {
                // 计算可移动范围
                List<Tile> moveableTiles = movementSystem.GetMoveableTiles(enemy, enemy.moveRange);

                // 寻找离玩家最近的可行走格子
                Tile bestTile = FindTileClosestToPlayer(moveableTiles, targetPlayer);

                if (bestTile != null && bestTile != enemy.currentTile)
                {
                    // 寻路移动到该格子
                    List<Tile> path = movementSystem.FindPath(enemy, enemy.currentTile, bestTile);

                    // 限制路径长度不超过移动范围
                    if (path.Count > enemy.moveRange + 1)
                    {
                        path = path.Take(enemy.moveRange + 1).ToList();
                    }

                    if (path.Count > 1)
                    {
                        yield return StartCoroutine(movementSystem.MoveUnitAlongPath(enemy, path));
                    }
                    else
                    {
                        UnitFinishedAction(enemy);
                    }
                }
                else
                {
                    UnitFinishedAction(enemy);
                }
            }
            else
            {
                UnitFinishedAction(enemy);
            }
        }
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
            // 这里根据你的逻辑决定，可以在 UnitFinishedAction 中处理
        }
    }
}