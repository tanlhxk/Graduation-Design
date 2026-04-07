using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("对象预制体")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    [Header("地图种子")]
    public int seed;

    public SimpleWFCGenerator simpleWFCGenerator;
    public FacingCamera facingCamera;
    private FriendlyUnit playerUnit;
    private EnemyUnit enemyUnit;
    private GameObject playerObj;
    private GameObject enemyObj;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        simpleWFCGenerator.GenerateAndBuildMap(seed);
        SpawnEnemyAt(new Vector2Int(7, 7));
        SpawnPlayerAt(new Vector2Int(1, 1)); // 再生成玩家
        TurnManager.Instance.OnGameInitialized();
        Debug.Log("GameManager 初始化完毕，触发 TurnManager");

        if (GridManager.Instance != null && CameraController.Instance != null)
        {
            float worldWidth = GridManager.Instance.Width * GridManager.Instance.CellSize;
            float worldHeight = GridManager.Instance.Height * GridManager.Instance.CellSize;
            // 假设地图左下角为 (0,0)，右上角为 (worldWidth, worldHeight)
            Bounds bounds = new Bounds(new Vector3(worldWidth * 0.5f, worldHeight * 0.5f, 0),
                                       new Vector3(worldWidth, worldHeight, 0));
            CameraController.Instance.SetWorldBounds(bounds);

            // 将摄像机初始位置也限制在边界内
            CameraController.Instance.ForcePosition(playerObj.transform.position);
        }
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            SpawnEnemyAt(new Vector2Int(7, 7));
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
        enemyObj = Instantiate(enemyPrefab, worldPos, Quaternion.identity,facingCamera.transform);
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
        facingCamera.RefreshFacing();
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
        playerObj = Instantiate(playerPrefab, worldPos, Quaternion.identity,facingCamera.transform);
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
            //playerUnit.AddSkill(SkillType.NormalAttack, "普攻", 1, 1, 0);
            //playerUnit.AddSkill(SkillType.BattleSkill, "战技", 2,2, 2);
            //playerUnit.AddSkill(SkillType.Ultimate, "终结技", 5, 5, 5);
            SkillDataSO normalAttack = Resources.Load<SkillDataSO>("Skills/NormalAttack");
            playerUnit.AddSkill(normalAttack);
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
        facingCamera.RefreshFacing();
        Debug.Log($"玩家已生成在网格位置 {gridPos}");
    }
}
