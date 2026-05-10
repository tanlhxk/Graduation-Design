using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("对象预制体")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    [Header("地图种子")]
    public int seed;

    [Header("路线系统")]
    private NodeType currentCombatType;   // 记录当前战斗类型（普通/精英/BOSS）

    public SimpleWFCGenerator simpleWFCGenerator;
    public FacingCamera facingCamera;
    private FriendlyUnit playerUnit;
    private EnemyUnit enemyUnit;
    private GameObject playerObj;
    private GameObject enemyObj;
    private bool combatStarted = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (RouteManager.Instance != null && RouteManager.Instance.CurrentNode != null)
        {
            if (!combatStarted)
            {
                combatStarted = true;
                Debug.Log("GameManager 检测到肉鸽路线，开始当前节点战斗...");
                if (RouteManager.Instance.CurrentNode != null)
                    RouteManager.Instance.StartCurrentNode();
            }
        }
        else
        {
            // 没有路线数据（例如直接在编辑器中运行 GameScene），使用测试模式
            Debug.Log("未检测到肉鸽路线，使用测试模式生成地图");
            simpleWFCGenerator.GenerateAndBuildMap(seed);
            SpawnEnemyAt(new Vector2Int(7, 7));
            SpawnPlayerAt(new Vector2Int(1, 1));
            TurnManager.Instance.OnGameInitialized();
            SetupCameraBounds();
        }
        /*simpleWFCGenerator.GenerateAndBuildMap(seed);
        SpawnEnemyAt(new Vector2Int(7, 7));
        SpawnPlayerAt(new Vector2Int(1, 1)); // 再生成玩家
        TurnManager.Instance.OnGameInitialized();
        Debug.Log("GameManager 初始化完毕，触发 TurnManager");

        if (GridManager.Instance != null && CameraController.Instance != null)
        {
            float worldWidth = GridManager.Instance.Width * GridManager.Instance.CellSize;
            float worldHeight = GridManager.Instance.Height * GridManager.Instance.CellSize; // 实际是 Z 轴长度
            Bounds bounds = new Bounds(new Vector3(worldWidth * 0.5f, 0, worldHeight * 0.5f),
                                       new Vector3(worldWidth, 0, worldHeight));
            CameraController.Instance.SetWorldBounds(bounds);
            CameraController.Instance.SetWorldBounds(bounds);

            // 将摄像机初始位置也限制在边界内
            CameraController.Instance.ForcePosition(playerObj.transform.position);
        }*/
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            SpawnEnemyAt(GetValidEnemyPosition(), 20, "测试敌人");
        }
    }
    void SpawnEnemyAt(Vector2Int gridPos)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("预制体未指定！");
            return;
        }

        // 计算世界坐标
        Vector3 worldPos = GridManager.Instance.GridToWorld(gridPos);

        // 实例化
        enemyObj = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
        enemyObj.tag = "Enemy";  // 设置标签（可选）
        enemyObj.name = "Enemy"; // 重命名

        // 获取Unit组件并初始化
        enemyUnit = enemyObj.GetComponent<EnemyUnit>();
        if (enemyUnit != null)
        {
            enemyUnit.unitName = "敌人";
            enemyUnit.maxHP = 20;
            enemyUnit.currentHP = 20;
            enemyUnit.baseAttack = 5;
            enemyUnit.moveRange = 3;
            enemyUnit.attackRange = 1;
            enemyUnit.unitType = UnitType.Enemy;

            // 通知GridManager该单位占据了格子
            GridManager.Instance.SetUnitOnTile(enemyUnit, gridPos);
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.enemyUnits.Add(enemyUnit);
            TurnManager.Instance.allUnits.Add(enemyUnit);
        }
        //facingCamera.RefreshFacing();
        Debug.Log($"敌方已生成在网格位置 {gridPos}");
    }
    void SpawnPlayerAt(Vector2Int gridPos)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("玩家预制体未指定！");
            return;
        }

        // 计算世界坐标
        Vector3 worldPos = GridManager.Instance.GridToWorld(gridPos);

        // 实例化玩家
        playerObj = Instantiate(playerPrefab, worldPos, Quaternion.identity);
        playerObj.tag = "Player";  // 设置标签（可选）
        playerObj.name = "Player"; // 重命名

        // 获取Unit组件并初始化
        playerUnit = playerObj.GetComponent<FriendlyUnit>();
        if (playerUnit != null)
        {
            playerUnit.unitName = "勇者";
            playerUnit.maxHP = 20;
            playerUnit.currentHP = 20;
            playerUnit.baseAttack = 5;
            playerUnit.moveRange = 10;
            playerUnit.attackRange = 1;
            playerUnit.unitType = UnitType.Player;
            SkillDataSO normalAttack = Resources.Load<SkillDataSO>("Skills/NormalAttack");
            SkillDataSO battleAttack = Resources.Load<SkillDataSO>("Skills/BattleAttack");
            playerUnit.AddSkill(normalAttack);
            playerUnit.AddSkill(battleAttack);
            // 通知GridManager该单位占据了格子
            GridManager.Instance.SetUnitOnTile(playerUnit, gridPos);
        }

        // 将玩家添加到TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.playerUnits.Add(playerUnit);
            TurnManager.Instance.allUnits.Add(playerUnit);
        }
        if (CameraController.Instance != null)
        {
            CameraController.Instance.ForcePosition(playerObj.transform.position);
        }
        //facingCamera.RefreshFacing();
        Debug.Log($"玩家已生成在网格位置 {gridPos}");
    }
    /// <summary>
    /// 开始一场战斗（由 RouteManager 调用）
    /// </summary>
    /// <param name="nodeType">节点类型（普通/精英/BOSS）</param>
    public void StartCombat(NodeType nodeType)
    {
        TurnManager.Instance.ResetBattle();
        ClearAllUnits();
        currentCombatType = nodeType;

        // 生成地图
        int layerHint = RouteManager.Instance.CurrentNode.gridPos.y;
        seed = Random.Range(RouteManager.Instance.Seed, layerHint + 1);
        simpleWFCGenerator.GenerateAndBuildMap(seed);

        // 生成玩家（固定位置）
        SpawnPlayerAt(new Vector2Int(1, 1));

        // 根据节点类型生成敌人
        GenerateEnemiesByNodeType(nodeType);

        // 初始化回合系统
        TurnManager.Instance.OnGameInitialized();

        // 设置摄像机边界并跳转到玩家位置
        SetupCameraBounds();

        // 开始玩家回合（TurnManager 内部会激活第一个单位）
        // TurnManager 的 OnGameInitialized 已经自动开始回合，无需额外调用
    }
    private void ClearAllUnits()
    {
        // 销毁所有现有单位物体
        var allUnits = FindObjectsOfType<Unit>();
        foreach (var unit in allUnits)
        {
            Destroy(unit.gameObject);
        }
        // 清空静态列表和 TurnManager 中的列表
        Unit.AllUnits.Clear();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.playerUnits.Clear();
            TurnManager.Instance.enemyUnits.Clear();
            TurnManager.Instance.allUnits.Clear();
            TurnManager.Instance.currentActiveUnit = null;
        }
    }
    /// <summary>
    /// 根据节点类型生成不同的敌人阵容
    /// </summary>
    private void GenerateEnemiesByNodeType(NodeType nodeType)
    {
        // 先清除可能遗留的敌人（但理论上此时还没有）
        switch (nodeType)
        {
            case NodeType.Combat:      // 普通战斗
                SpawnEnemyAt(GetValidEnemyPosition(), 20, "普通敌人");
                break;
            case NodeType.EliteCombat: // 精英战斗
                SpawnEnemyAt(GetValidEnemyPosition(), 35, "精英敌人");
                SpawnEnemyAt(GetValidEnemyPosition(), 30, "精英随从");
                break;
            case NodeType.Boss:        // BOSS战
                SpawnEnemyAt(GetValidEnemyPosition(), 80, "BOSS");
                break;
        }
    }

    /// <summary>
    /// 找一个可行的敌人出生点（不与玩家重叠、可行走）
    /// </summary>
    private Vector2Int GetValidEnemyPosition()
    {
        Vector2Int playerPos = new Vector2Int(1, 1);
        Vector2Int enemyPos;
        do
        {
            // 随机在网格范围内找一个可行走且不是玩家的格子
            int x = Random.Range(3, GridManager.Instance.Width - 2);
            int z = Random.Range(3, GridManager.Instance.Height - 2);
            enemyPos = new Vector2Int(x, z);
        }
        while (enemyPos == playerPos || !GridManager.Instance.GetTile(enemyPos).IsWalkable());
        return enemyPos;
    }

    /// <summary>
    /// 增强版生成敌人，可指定血量和名称
    /// </summary>
    private void SpawnEnemyAt(Vector2Int gridPos, int hp, string name)
    {
        if (enemyPrefab == null) return;

        Vector3 worldPos = GridManager.Instance.GridToWorld(gridPos);
        GameObject enemyObj = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
        enemyObj.tag = "Enemy";
        enemyObj.name = name;

        EnemyUnit enemyUnit = enemyObj.GetComponent<EnemyUnit>();
        if (enemyUnit != null)
        {
            enemyUnit.unitName = name;
            enemyUnit.maxHP = hp;
            enemyUnit.currentHP = hp;
            enemyUnit.baseAttack = (int)(5 + hp / 20f); // 根据血量调整攻击
            enemyUnit.moveRange = 3;
            enemyUnit.attackRange = 1;
            enemyUnit.unitType = UnitType.Enemy;

            GridManager.Instance.SetUnitOnTile(enemyUnit, gridPos);
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.enemyUnits.Add(enemyUnit);
            TurnManager.Instance.allUnits.Add(enemyUnit);
        }
    }

    /// <summary>
    /// 设置摄像机的边界和初始位置
    /// </summary>
    private void SetupCameraBounds()
    {
        if (GridManager.Instance != null && CameraController.Instance != null)
        {
            float worldWidth = GridManager.Instance.Width * GridManager.Instance.CellSize;
            float worldHeight = GridManager.Instance.Height * GridManager.Instance.CellSize;
            Bounds bounds = new Bounds(new Vector3(worldWidth * 0.5f, 0, worldHeight * 0.5f),
                                       new Vector3(worldWidth, 0, worldHeight));
            CameraController.Instance.SetWorldBounds(bounds);
            // 强制摄像机移动到玩家位置
            if (playerObj != null)
                CameraController.Instance.ForcePosition(playerObj.transform.position);
        }
    }
}