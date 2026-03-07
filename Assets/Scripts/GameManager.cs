using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("对象预制体")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    private GridManager gridManager;
    private TurnManager turnManager;
    private DungeonGenerator dungeonGenerator;
    private Unit playerUnit;
    private Unit enemyUnit;
    private GameObject playerObj;
    private GameObject enemyObj;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        gridManager = FindObjectOfType<GridManager>();
        turnManager = FindObjectOfType<TurnManager>();
        dungeonGenerator = FindObjectOfType<DungeonGenerator>();
        dungeonGenerator.GenerateDungeon(GridManager.tileDict); // 先生成地牢
        SpawnEnemyAt(dungeonGenerator.EnemyStartPos);
        SpawnPlayerAt(dungeonGenerator.playerStartPos); // 再生成玩家
    }
    private void Update()
    {
        if (playerObj != null)
        {
            //mainCamera.GetComponent<Transform>().position=playerObj.GetComponent<Transform>().position;
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
        Vector3 worldPos = gridManager.GridToWorld(gridPos);

        // 实例化
        enemyObj = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
        enemyObj.tag = "Enemy";  // 设置标签（可选）
        enemyObj.name = "Enemy"; // 重命名

        // 获取Unit组件并初始化
        enemyUnit = enemyObj.GetComponent<Unit>();
        if (enemyUnit != null)
        {
            enemyUnit.unitName = "敌人";
            enemyUnit.maxHP = 20;
            enemyUnit.currentHP = 20;
            enemyUnit.attackPower = 5;
            enemyUnit.moveRange = 3;
            enemyUnit.attackRange = 1;
            enemyUnit.unitType = UnitType.Enemy;

            // 通知GridManager该单位占据了格子
            gridManager.SetUnitOnTile(enemyUnit, gridPos);
        }

        if (turnManager != null)
        {
            turnManager.enemyUnits.Add(enemyUnit);
            turnManager.allUnits.Add(enemyUnit);
        }

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
        Vector3 worldPos = gridManager.GridToWorld(gridPos);

        // 实例化玩家
        playerObj = Instantiate(playerPrefab, worldPos, Quaternion.identity);
        playerObj.tag = "Player";  // 设置标签（可选）
        playerObj.name = "Player"; // 重命名

        // 获取Unit组件并初始化
        playerUnit = playerObj.GetComponent<Unit>();
        if (playerUnit != null)
        {
            playerUnit.unitName = "勇者";
            playerUnit.maxHP = 20;
            playerUnit.currentHP = 20;
            playerUnit.attackPower = 5;
            playerUnit.moveRange = 3;
            playerUnit.attackRange = 1;
            playerUnit.unitType = UnitType.Player;

            // 通知GridManager该单位占据了格子
            gridManager.SetUnitOnTile(playerUnit, gridPos);
        }

        // 将玩家添加到TurnManager
        if (turnManager != null)
        {
            turnManager.playerUnits.Add(playerUnit);
            turnManager.allUnits.Add(playerUnit);
        }

        Debug.Log($"玩家已生成在网格位置 {gridPos}");
    }
}
