using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInput : MonoBehaviour
{
    private Camera mainCamera;
    private GridManager gridManager;
    private TurnManager turnManager;
    private MovementSystem movementSystem;

    [Header("攻击连线")]
    private LineRenderer attackLine;
    public Color canAttackColor = Color.yellow; // 能攻击时的颜色
    public Color cannotAttackColor = Color.red;  // 不能攻击时的颜色

    [Header("选择状态")]
    private FriendlyUnit selectedUnit;
    private Tile selectedTile;

    [Header("技能按钮组")]
    public List<Button> skillButtons = new List<Button>();
    public List<Image> skillIcons = new List<Image>();     // 对应的图标

    private int currentSelectedSkillIndex = -1; // 默认选中普攻
    private FriendlyUnit currentSelectedUnit; // 当前被选中的我方单位
    private List<Tile> currentMoveRange;

    private void Awake()
    {
        CreateAttackLine();
    }

    void Start()
    {
        selectedUnit=null;
        currentMoveRange = null;
        mainCamera = Camera.main;
        gridManager = FindObjectOfType<GridManager>();
        turnManager = FindObjectOfType<TurnManager>();
        movementSystem = FindObjectOfType<MovementSystem>();
        // 初始化按钮监听
        for (int i = 0; i < skillButtons.Count; i++)
        {
            int index = i; // 捕获循环变量
            skillButtons[i].onClick.AddListener(() => OnSkillButtonClicked(index));
        }

        // 默认进入游戏选中普攻
        UpdateSkillButtonVisuals();
    }

    void Update()
    {
        // 只在玩家回合处理输入
        if (turnManager.currentPhase != TurnManager.TurnPhase.PlayerTurn)
            return;


        HandleMouseInput();
        if (selectedUnit != null
    && currentSelectedSkillIndex != -1
    && turnManager.currentPhase == TurnManager.TurnPhase.PlayerTurn)
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = gridManager.WorldToGrid(mousePos);
            Tile hoverTile = gridManager.GetTile(gridPos);

            // 清除上一帧的辅助线 (非常重要，防止残留)
            HideAttackLine();

            // 情况1：鼠标悬停在地图上
            if (hoverTile != null)
            {
                // 情况1.1：悬停的格子上有单位
                if (hoverTile.occupyingUnit != null)
                {
                    // 判断是否为敌人
                    if (hoverTile.occupyingUnit.unitType == UnitType.Enemy)
                    {
                        EnemyUnit targetEnemy = (EnemyUnit)hoverTile.occupyingUnit;

                        // 调用选中单位的 CanAttack 方法判断是否在范围内
                        bool inRange = selectedUnit.CanAttack(targetEnemy, currentSelectedSkillIndex);

                        // 根据判断结果显示不同颜色的辅助线
                        ShowAttackLine(
                            selectedUnit.transform.position,
                            targetEnemy.transform.position,
                            inRange ? canAttackColor : cannotAttackColor
                        );

                        // 设置一个标志，表示已经显示了辅助线，防止后面清除
                    }
                }
                // 情况1.2：悬停在空地上 (通常不显示攻击辅助线)
                // 实现“地面技能”的预判（比如AOE），可以在这里添加逻辑
            }
            // 如果鼠标移出了地图，辅助线会在开头被清除
        }
        else
        {
            // 不满足显示条件时，隐藏辅助线
            HideAttackLine();
        }
    }

    // 1. UI按钮被点击时调用
    void OnSkillButtonClicked(int skillIndex)
    {
        // 只有当没有选中单位时，点击技能才无效或直接返回
        // (或者你可以设计成：点击技能时如果没有选中单位，自动选中当前行动单位)

        // 如果已经选中了单位，点击技能只是切换技能
        if (selectedUnit != null)
        {
            // 切换选中状态：如果再次点击已选中的技能，取消选中（变回手型）
            if (currentSelectedSkillIndex == skillIndex)
            {
                currentSelectedSkillIndex = -1; // -1 代表手型/移动状态
            }
            else
            {
                currentSelectedSkillIndex = skillIndex;
            }
            UpdateSkillButtonVisuals();

            // 关键点：这里不要取消 selectedUnit！
            // selectedUnit 应该保持选中，直到你点击地面移动，或者点击敌人攻击，或者点击空白处取消
        }
        else
        {
            // 特殊设计：如果没选中单位就点技能，自动选中当前行动单位（如果该单位是我方单位）
            FriendlyUnit currentActive = turnManager.currentActiveUnit as FriendlyUnit;
            if (currentActive != null)
            {
                SelectUnit(currentActive); // 这会自动计算移动范围
                currentSelectedSkillIndex = skillIndex;
                UpdateSkillButtonVisuals();
                // 注意：这里选中单位后，不要立即 ClearSelection
            }
            else
            {
                // 无法选中单位，取消技能选择
                currentSelectedSkillIndex = -1;
                UpdateSkillButtonVisuals();
            }
        }
    }

    // 2. 当切换我方单位时，调用此方法刷新UI
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

        // 遍历三个技能槽，更新图标、文字和高亮状态
        for (int i = 0; i < 3; i++)
        {
            // 获取该单位对应的技能数据
            SkillData data = currentSelectedUnit.GetSkillData(i);

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
    }

    // 4. 攻击执行逻辑 (修改之前的 AttackEnemy)
    public void ExecuteAttack(EnemyUnit targetUnit)
    {
        if (currentSelectedUnit == null || targetUnit == null) return;

        // 从当前选中的单位身上获取技能数据
        //SkillData skillToUse = currentSelectedUnit.GetSkillData(currentSelectedSkillIndex);

        // 执行攻击
        currentSelectedUnit.Attack(targetUnit, currentSelectedSkillIndex);

        // 攻击结束后，通常回合结束或状态重置
        // turnManager.UnitFinishedAction(currentSelectedUnit);
    }

    /// <summary>
    /// 显示攻击连线
    /// </summary>
    /// <param name="startPos">起始位置（通常是玩家单位）</param>
    /// <param name="endPos">结束位置（通常是鼠标悬停的敌人或格子）</param>
    /// <param name="lineColor">线的颜色</param>
    void ShowAttackLine(Vector3 startPos, Vector2 endPos, Color lineColor)
    {
        if (attackLine == null)
        {
            Debug.LogError("AttackLine 未初始化！");
            return;
        }

        // 2D 游戏关键：强制 Z 轴为 0 或摄像机的 Z，防止线跑出屏幕
        Vector3 fixedStart = new Vector3(startPos.x, startPos.y, 0);
        Vector3 fixedEnd = new Vector3(endPos.x, endPos.y, 0);

        attackLine.SetPosition(0, fixedStart);
        attackLine.SetPosition(1, fixedEnd);
        attackLine.startColor = lineColor;
        attackLine.endColor = lineColor;
        attackLine.enabled = true;
    }

    
    void ShowAttackLine(Vector3 start, Vector3 end, Color color)
    {
        if (attackLine != null)
        {
            attackLine.enabled = true;
            attackLine.startColor = color;
            attackLine.endColor = color;
            attackLine.SetPosition(0, start);
            attackLine.SetPosition(1, end);
        }
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
        // 1. 检查是否已经存在，防止重复创建
        if (attackLine != null) return;

        // 2. 创建一个新的 GameObject 来承载 LineRenderer
        // 注意：这里我们直接在代码里 new，不依赖外部拖拽
        GameObject lineObj = new GameObject("AttackLine");
        lineObj.transform.SetParent(this.transform); // 作为 PlayerInput 的子物体，方便管理

        // 3. 添加 LineRenderer 组件
        attackLine = lineObj.AddComponent<LineRenderer>();

        // 4. 【关键修复】根据你的渲染管线设置材质
        // 如果是内置渲染管线 (Built-in) 或 2D 项目，用这个：
        attackLine.material = new Material(Shader.Find("Sprites/Default"));

        // 如果是 URP 渲染管线，用这个（如果上面那行报错或显示粉色，就换成这行）：
        // attackLine.material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));

        // 5. 设置层级 (Sorting Layer) - 2D 游戏关键！
        // 确保连线显示在最上层
        attackLine.sortingLayerName = "UI"; // 或者你场景中最高的 Layer 名字，如 "Foreground"

        // 6. 设置颜色模式和宽度
        attackLine.startColor = Color.white;
        attackLine.endColor = Color.white;
        attackLine.startWidth = 0.05f; // 2D 场景线通常比较细
        attackLine.endWidth = 0.05f;

        // 7. 设置位置数量 (至少需要2个点才能画线)
        attackLine.positionCount = 2;

        // 8. 初始状态设为隐藏
        attackLine.enabled = false;
    }

    void HandleMouseInput()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            // Debug.Log("鼠标在UI上，忽略输入");
            return;
        }
        // 鼠标左键点击
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = gridManager.WorldToGrid(mousePos);
            Tile clickedTile = gridManager.GetTile(gridPos);

            // 1. 如果点击位置没有地图格子，直接取消当前选择
            if (clickedTile == null)
            {
                ClearSelection();
                return;
            }

            // 获取点击格子上的单位
            Unit targetUnit = clickedTile.occupyingUnit;

            // 情况 A: 点击了一个单位（无论是敌是友）
            if (targetUnit != null)
            {
                // 子情况 A1: 点击的是当前行动的己方单位 -> 选中/重新选中
                if (targetUnit == turnManager.currentActiveUnit)
                {
                    // 如果已经选中了该单位，再次点击应取消选中（或保持选中以便技能选择，这里我们保持选中）
                    // 如果你想实现“点击已选单位取消选中”，可以在这里加逻辑，为了操作连贯性，通常保持选中
                    if (selectedUnit != (FriendlyUnit)targetUnit)
                    {
                        SelectUnit((FriendlyUnit)targetUnit);
                        RefreshUIForUnit((FriendlyUnit)targetUnit);
                    }
                }
                // 子情况 A2: 已经选中了己方单位，且点击的是敌人 -> 尝试攻击
                else if (selectedUnit != null && targetUnit.unitType != UnitType.Player)
                {
                    EnemyUnit enemy = (EnemyUnit)targetUnit;

                    // 判断是否在攻击范围内（基于当前选中的技能）
                    if (selectedUnit.CanAttack(enemy, currentSelectedSkillIndex))
                    {
                        // 执行攻击
                        ExecuteAttack(enemy);
                        // 攻击后通常结束行动（根据你的回合制规则，也可以不结束，这里默认结束）
                        // ClearSelection(); // 攻击后是否保留选中状态取决于设计，通常攻击后回合结束
                    }
                    else
                    {
                        // 攻击距离不够，可以播放提示音，或者直接忽略
                        Debug.Log("目标超出攻击范围！");
                        // 注意：这里不取消选中，用户可能想移动后再攻击
                    }
                }
                // 子情况 A3: 点击敌人但未选中己方单位（或者点击友军），不做任何事
                // 可以添加“查看信息”逻辑，但不改变选中状态
            }
            // 情况 B: 点击了空地（或者友军占据的地面）
            else
            {
                // 如果有选中的单位，尝试移动
                if (selectedUnit != null)
                {
                    // 必须在计算出的可移动范围内才能移动
                    if (currentMoveRange != null && currentMoveRange.Contains(clickedTile))
                    {
                        MoveSelectedUnitTo(clickedTile);
                        // 移动后通常不清除选中，以便移动后可以继续攻击（除非移动后自动结束回合）
                        // 如果移动后自动结束回合，请调用 ClearSelection()
                    }
                    // 点击了不可移动区域，取消选中
                    else
                    {
                        ClearSelection();
                    }
                }
                // 如果没有选中单位，点击空地取消所有操作（或者保持现状）
                else
                {
                    // 点击空地通常不操作，除非你想实现“点击空地取消技能选择”
                    // 如果点击空地想取消技能选择：
                    if (currentSelectedSkillIndex != -1)
                    {
                        currentSelectedSkillIndex = -1;
                        UpdateSkillButtonVisuals();
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
            // 可选：取消技能选中
            // currentSelectedSkillIndex = -1;
            // UpdateSkillButtonVisuals();
            Debug.Log("取消选中单位");
        }
    }

    void SelectUnit(FriendlyUnit unit)
    {
        // 只能选择当前回合可以行动的玩家单位
        if (unit.unitType != UnitType.Player || unit != turnManager.currentActiveUnit)
            return;

        selectedUnit = unit;

        // 计算并显示可移动范围
        currentMoveRange = movementSystem.GetMoveableTiles(unit, unit.moveRange);
        movementSystem.HighlightMoveRange(currentMoveRange);
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SmoothMoveTo(unit.transform.position);
        }
        Debug.Log($"选中单位: {unit.unitName}");
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

    void AttackEnemy(EnemyUnit enemy)
    {
        // 1. 安全性检查 (防御性编程)
        if (selectedUnit == null || enemy == null)
        {
            Debug.LogError("攻击目标或选中单位为空！");
            HideAttackLine(); // 确保清理视觉残留
            return;
        }

        // 2. 逻辑验证：确认是否真的能攻击
        // 这里可以防止玩家在移动范围外强行点击攻击
        if (!selectedUnit.CanAttack(enemy, currentSelectedSkillIndex))
        {
            Debug.Log("目标超出攻击范围，无法攻击！");
            // 可以播放一个“叮”的无效音效
            HideAttackLine();
            return;
        }

        // 3. 执行攻击逻辑
        // 先播放技能动画，再计算伤害。
        selectedUnit.Attack(enemy);

        // 4. 状态清理与回合推进
        // 攻击结束后，清除选中状态和高亮
        movementSystem.ClearHighlights(GridManager.tileDict);
        HideAttackLine(); // 攻击结束，隐藏连线

        // 通知回合管理器：当前单位行动结束
        // 注意：在 Unit.Attack() 内部如果是异步（有动画），这里最好也做成异步回调
        // 现阶段为了简单，直接调用
        //turnManager.UnitFinishedAction(selectedUnit);

        // 5. 清理本地状态
        selectedUnit = null;
        currentMoveRange = null;

        Debug.Log($"{(selectedUnit?.unitName ?? "空名称")} 对 {(enemy?.unitName ?? "空名称")} 发起了攻击！");
    }
}