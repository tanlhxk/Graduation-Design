using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    private Camera mainCamera;
    private GridManager gridManager;
    private TurnManager turnManager;
    private MovementSystem movementSystem;

    [Header("选择状态")]
    private Unit selectedUnit;
    private Tile selectedTile;
    private List<Tile> currentMoveRange;

    void Start()
    {
        selectedUnit=null;
        currentMoveRange = null;
        mainCamera = Camera.main;
        gridManager = FindObjectOfType<GridManager>();
        turnManager = FindObjectOfType<TurnManager>();
        movementSystem = FindObjectOfType<MovementSystem>();
    }

    void Update()
    {
        // 只在玩家回合处理输入
        if (turnManager.currentPhase != TurnManager.TurnPhase.PlayerTurn)
            return;

        HandleMouseInput();
    }

    void HandleMouseInput()
    {
        // 鼠标左键点击
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = gridManager.WorldToGrid(mousePos);
            Tile clickedTile = gridManager.GetTile(gridPos);

            if (clickedTile == null)
            {
                if (selectedUnit != null)
                {
                    selectedUnit = null;
                    currentMoveRange = null;
                    movementSystem.ClearHighlights(GridManager.tileDict);
                }
                return;
            }

            // 情况1：点击了当前激活的玩家单位
            if (clickedTile.occupyingUnit != null &&
                clickedTile.occupyingUnit == turnManager.currentActiveUnit)
            {
                SelectUnit(clickedTile.occupyingUnit);


            }
            // 情况2：已经有选中的单位，点击了可移动范围
            else if (selectedUnit != null && currentMoveRange != null)
            {
                if (currentMoveRange.Contains(clickedTile))
                {
                    MoveSelectedUnitTo(clickedTile);
                }
                else
                {
                    selectedUnit = null;
                    currentMoveRange = null;
                    movementSystem.ClearHighlights(GridManager.tileDict);
                }
            }
            /*
            // 情况3：点击了敌人（在攻击范围内）
            else if (selectedUnit != null &&
                     clickedTile.occupyingUnit != null &&
                     clickedTile.occupyingUnit.unitType == UnitType.Enemy &&
                     selectedUnit.CanAttack(clickedTile.occupyingUnit))
            {
                AttackEnemy(clickedTile.occupyingUnit);
            }
            */
        }
    }

    void SelectUnit(Unit unit)
    {
        // 只能选择当前回合可以行动的玩家单位
        if (unit.unitType != UnitType.Player || unit != turnManager.currentActiveUnit)
            return;

        selectedUnit = unit;

        // 计算并显示可移动范围
        currentMoveRange = movementSystem.GetMoveableTiles(unit, unit.moveRange);
        movementSystem.HighlightMoveRange(currentMoveRange);

        Debug.Log($"选中单位: {unit.unitName}");
    }

    void MoveSelectedUnitTo(Tile targetTile)
    {
        if (selectedUnit == null || currentMoveRange == null) return;

        // 寻路
        List<Tile> path = movementSystem.FindPath(selectedUnit, selectedUnit.currentTile, targetTile);

        // 限制移动范围不超过移动力
        if (path.Count > selectedUnit.moveRange + 1)
        {
            path = path.Take(selectedUnit.moveRange + 1).ToList();
        }

        if (path.Count > 1)
        {
            StartCoroutine(movementSystem.MoveUnitAlongPath(selectedUnit, path));

            // 移动后清除选择和高亮
            movementSystem.ClearHighlights(GridManager.tileDict);
            selectedUnit = null;
            currentMoveRange = null;
        }
    }

    void AttackEnemy(Unit enemy)
    {
        if (selectedUnit == null) return;

        selectedUnit.Attack(enemy);

        // 攻击后清除选择
        movementSystem.ClearHighlights(GridManager.tileDict);
        selectedUnit.currentState = UnitState.Idle;
        turnManager.UnitFinishedAction(selectedUnit);
        selectedUnit = null;
        currentMoveRange = null;
    }
}