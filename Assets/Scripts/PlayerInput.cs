using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Security.Cryptography;

public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance;
    private Camera mainCamera;
    private GridManager gridManager;
    private TurnManager turnManager;
    private MovementSystem movementSystem;

    [Header("攻击连线")]
    private LineRenderer attackLine;
    public Color canAttackColor = Color.yellow; // 能攻击时的颜色
    public Color cannotAttackColor = Color.red;  // 不能攻击时的颜色
    public Material lineMat;

    [Header("选择状态")]
    private FriendlyUnit selectedUnit;
    private Tile selectedTile;

    [Header("技能按钮组")]
    public List<Button> skillButtons = new List<Button>();
    public List<Image> skillIcons = new List<Image>();     // 对应的图标
    private SkillDataSO currentSelectedSkillData = null; // null 代表移动/空手状态
    private FriendlyUnit currentSelectedUnit; // 当前被选中的我方单位
    private List<Tile> currentMoveRange;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        selectedUnit = null;
        mainCamera = Camera.main;
        gridManager = FindObjectOfType<GridManager>();
        turnManager = FindObjectOfType<TurnManager>();
        movementSystem = FindObjectOfType<MovementSystem>();

        CreateAttackLine();
    }

    void Update()
    {
        if (turnManager.currentPhase != TurnManager.TurnPhase.PlayerTurn) return;
        HandleMouseInput();

        if (selectedUnit != null && currentSelectedSkillData != null)
        {
            // 使用射线检测获取准确的鼠标位置
            Vector3? worldMousePos = GetMouseWorldPosition();

            if (worldMousePos.HasValue)
            {
                Vector2Int gridPos = gridManager.WorldToGrid(worldMousePos.Value);
                Tile hoverTile = gridManager.GetTile(gridPos);

                HideAttackLine(); // 清除

                if (hoverTile != null && hoverTile.occupyingUnit != null)
                {
                    if (hoverTile.occupyingUnit.unitType == UnitType.Enemy)
                    {
                        EnemyUnit targetEnemy = (EnemyUnit)hoverTile.occupyingUnit;

                        bool inRange = selectedUnit.CanUseSkill(targetEnemy, currentSelectedSkillData);

                        ShowAttackLine(selectedUnit.transform.position, targetEnemy.transform.position,
                                      inRange ? canAttackColor : cannotAttackColor);
                    }
                }
            }
            else
            {
                HideAttackLine();
            }
        }
        else
        {
            HideAttackLine();
        }
    }

    Vector3? GetMouseWorldPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 地图在 XY 平面，Z=0
        Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return null;
    }

    // 1. UI按钮被点击时调用
    public void OnSkillSelected(SkillDataSO skillData)
    {
        // 如果没有选中单位，尝试自动选中
        if (selectedUnit == null)
        {
            FriendlyUnit currentActive = turnManager.currentActiveUnit as FriendlyUnit;
            if (currentActive != null)
            {
                SelectUnit(currentActive);
            }
            else return; // 无法选中，不处理技能
        }

        // 切换技能状态：如果再次点击同一个技能，取消选中
        if (currentSelectedSkillData == skillData)
        {
            currentSelectedSkillData = null; // 回到移动状态
        }
        else
        {
            currentSelectedSkillData = skillData;
        }

        // 通知 UIManager 更新视觉（高亮）
        UIManager.Instance?.UpdateSkillSelectionVisual(currentSelectedSkillData);
    }

    /*// 2. 当切换我方单位时，调用此方法刷新UI
    public void RefreshUIForUnit(FriendlyUnit unit)
    {
        currentSelectedUnit = unit;
        UpdateSkillButtonVisuals();
    }

    // 3. 统一的UI刷新逻辑
    void UpdateSkillButtonVisuals()
    {
        // 如果没有选中单位，禁用所有按钮
        if (currentSelectedUnit == null)
        {
            foreach (var btn in skillButtons) btn.interactable = false;
            return;
        }

        // 启用按钮
        foreach (var btn in skillButtons) btn.interactable = true;

        // 遍历所有技能槽，更新图标、文字和高亮状态
        for (int i = 0; i < skillButtons.Count; i++)
        {
            // 获取该单位对应的技能数据
            SkillDataSO data = currentSelectedUnit.GetSkillData(i);

            // 更新图标
            if (skillIcons[i] != null && data != null)
                skillIcons[i].sprite = data.icon;


            // 处理选中高亮 (被选中的按钮变亮/变色)
            // 这里简单用 Outline 组件或者直接改颜色
            ColorBlock colors = skillButtons[i].colors;
            if (i == currentSelectedSkillIndex)
            {
                colors.normalColor = Color.yellow; // 选中时变黄
                colors.highlightedColor = Color.yellow;
            }
            else
            {
                colors.normalColor = Color.white;  // 未选中时白色
                colors.highlightedColor = Color.white;
            }
            skillButtons[i].colors = colors;
        }
    }*/

    // 4. 攻击执行逻辑 (修改之前的 AttackEnemy)
    public void ExecuteAttack(EnemyUnit targetUnit)
    {
        if (selectedUnit == null || targetUnit == null || currentSelectedSkillData == null) return;
        selectedUnit.ExecuteSkill(targetUnit, currentSelectedSkillData);

        // 攻击结束后，通常回合结束或状态重置
        // turnManager.UnitFinishedAction(selectedUnit);

        // 清理状态
        ClearSelection();
    }

    /// <summary>
    /// 显示攻击连线
    /// </summary>
    /// <param name="startPos">起始位置（通常是玩家单位）</param>
    /// <param name="endPos">结束位置（通常是鼠标悬停的敌人或格子）</param>
    /// <param name="lineColor">线的颜色</param>
    void ShowAttackLine(Vector3 startPos, Vector3 endPos, Color lineColor)
    {
        Debug.Log($"连线尝试绘制: Start={startPos}, End={endPos}");
        if (attackLine == null)
        {
            Debug.LogError("AttackLine 未初始化！请检查 CreateAttackLine 是否被调用。");
            return;
        }
        Vector3 fixedStart = new Vector3(startPos.x, startPos.y, 0);
        Vector3 fixedEnd = new Vector3(endPos.x, endPos.y, 0);

        attackLine.SetPosition(0, fixedStart);
        attackLine.SetPosition(1, fixedEnd);

        // 使用 Gradient 设置颜色 (适配新旧版本)
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(lineColor, 0.0f), new GradientColorKey(lineColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0.0f), new GradientAlphaKey(1f, 1.0f) }
        );
        attackLine.colorGradient = gradient;

        attackLine.enabled = true;
    }
    /// <summary>
    /// 隐藏攻击连线
    /// </summary>
    void HideAttackLine()
    {
        if (attackLine != null)
        {
            attackLine.enabled = false;
        }
    }

    /// <summary>
    /// 安全地创建攻击连线对象
    /// </summary>
    void CreateAttackLine()
    {
        // 1. 检查是否已存在，防止重复创建
        if (attackLine != null) return;

        // 2. 创建 GameObject
        GameObject lineObj = new GameObject("AttackLine");
        lineObj.transform.SetParent(this.transform);

        // 关键：为了确保 2D 渲染正确，手动添加 SpriteRenderer 并移除默认的 MeshRenderer
        // LineRenderer 在 2D 中有时会因为没有 SpriteRenderer 而不显示
        var spriteRenderer = lineObj.AddComponent<SpriteRenderer>();
        // 注意：这里添加 SpriteRenderer 主要是为了占位 Sorting Layer，LineRenderer 会自己处理绘制

        attackLine = lineObj.AddComponent<LineRenderer>();
        attackLine.useWorldSpace = true;

        attackLine.material = lineMat;

        // 4. 设置颜色模式 (新版 LineRenderer API)
        // 注意：新版 Unity 使用 colorGradient 而不是 startColor/endColor
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0.0f), new GradientAlphaKey(1f, 1.0f) }
        );
        attackLine.colorGradient = gradient;

        // 5. 设置宽度
        attackLine.startWidth = 0.1f; // 2.5D 场景稍微粗一点好看
        attackLine.endWidth = 0.1f;

        // 6. 【关键修复】层级设置 (Sorting Layer)
        // 方案 1：尝试设置为 "UI" 或 "Foreground"
        // 注意：必须确保 "UI" 层级在你的摄像机设置中是存在的
        attackLine.sortingLayerName = "UI";

        // 方案 2：如果 SortingLayerName 不生效，直接强制 Order in Layer (推荐)
        // 确保这个数值比你场景里所有单位的 SortingOrder 都大
        attackLine.sortingOrder = 100;

        // 7. 初始化设置
        attackLine.positionCount = 2;
        attackLine.useWorldSpace = true; // 必须是世界坐标，否则移动单位时线不动
        attackLine.enabled = false;
    }

    void HandleMouseInput()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0))
        {
            // 获取主摄像机
            Camera cam = mainCamera ?? Camera.main;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);

            float enter = 0;

            // 计算射线与地面的交点
            if (groundPlane.Raycast(ray, out enter))
            {
                // 计算出世界坐标
                Vector3 hitPoint = ray.GetPoint(enter);

                // 对齐坐标（防止浮点误差导致格子错位）
                // 根据你的 GridManager 中的 cellSize 进行取整对齐
                float cellSize = gridManager.CellSize;
                hitPoint.x = Mathf.Floor(hitPoint.x / cellSize) * cellSize + cellSize / 2;
                hitPoint.y = Mathf.Floor(hitPoint.y / cellSize) * cellSize + cellSize / 2;
                // hitPoint.z 根据平面保持不变

                // 转换为网格坐标
                Vector2Int gridPos = gridManager.WorldToGrid(hitPoint);
                Tile clickedTile = gridManager.GetTile(gridPos);

                if (clickedTile == null)
                {
                    ClearSelection();
                    return;
                }

                Unit targetUnit = clickedTile.occupyingUnit;

                if (targetUnit != null)
                {
                    // 点击了单位
                    if (targetUnit == turnManager.currentActiveUnit)
                    {
                        // 点击自己：选中自己
                        // 逻辑转移到 SelectUnit 方法里
                        if (selectedUnit != (FriendlyUnit)targetUnit)
                        {
                            SelectUnit((FriendlyUnit)targetUnit);
                        }
                    }
                    else if (selectedUnit != null && targetUnit.unitType != UnitType.Player)
                    {
                        EnemyUnit enemy = (EnemyUnit)targetUnit;
                        if (selectedUnit.CanUseSkill(enemy, currentSelectedSkillData))
                        {
                            // 通过状态机攻击，而不是直接执行技能
                            selectedUnit.Attack(enemy, currentSelectedSkillData);
                        }
                        else
                        {
                            Debug.Log("目标超出范围或无法攻击");
                        }
                    }
                }
                else
                {
                    // 点击空地
                    if (selectedUnit != null)
                    {
                        if (currentMoveRange != null && currentMoveRange.Contains(clickedTile))
                        {
                            MoveSelectedUnitTo(clickedTile);
                        }
                        else
                        {
                            ClearSelection();
                        }
                    }
                    else
                    {
                        // 点击空地取消技能选择
                        currentSelectedSkillData = null;
                        UIManager.Instance?.UpdateSkillSelectionVisual(null);
                    }
                }
            }
        }
    }

    // 辅助方法：统一清理选中状态
    void ClearSelection()
    {
        if (selectedUnit != null)
        {
            movementSystem.ClearHighlights(GridManager.tileDict);
            selectedUnit = null;
            currentMoveRange = null;

            // 通知 UI 清除技能高亮
            UIManager.Instance?.ClearSkillSelection();
        }
    }

    void SelectUnit(FriendlyUnit unit)
    {
        if (unit.unitType != UnitType.Player || unit != turnManager.currentActiveUnit) return;

        selectedUnit = unit;

        // 通知 UI 管理器刷新技能栏
        UIManager.Instance?.RefreshSkillBar(unit);

        currentMoveRange = movementSystem.GetMoveableTiles(unit, unit.moveRange);
        movementSystem.HighlightMoveRange(currentMoveRange);

        if (CameraController.Instance != null)
            CameraController.Instance.SmoothMoveTo(unit.transform.position);
    }

    void MoveSelectedUnitTo(Tile targetTile)
    {
        if (selectedUnit == null || currentMoveRange == null) return;

        // 调用 Unit 自带的 MoveTo 方法，这会触发 ChangeState(UnitState.Moving)
        // 进而由 UnitMovingState 处理移动协程并在结束后通知 TurnManager
        selectedUnit.MoveTo(targetTile);

        // 移动开始后，可以暂时清除选中和高亮，或者等移动结束再清除（取决于你的UI需求）
        // 注意：如果在 MoveTo 内部已经处理了状态锁定，这里不需要额外操作
        movementSystem.ClearHighlights(GridManager.tileDict);
        selectedUnit = null;
        currentMoveRange = null;
    }
}