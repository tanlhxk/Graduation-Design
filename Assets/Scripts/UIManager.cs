using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("回合信息")]
    public Text turnText;
    public Text phaseText;

    [Header("单位信息")]
    public GameObject unitInfoPanel;
    public Text unitNameText;
    public Text unitHPText;
    public Text unitAttackText;

    [Header("按钮")]
    public Button endTurnButton;

    private TurnManager turnManager;

    void Start()
    {
        turnManager = FindObjectOfType<TurnManager>();

        endTurnButton.onClick.AddListener(OnEndTurnClicked);
    }

    void Update()
    {
        // 更新UI显示
        if (turnManager != null)
        {
            turnText.text = $"回合: {turnManager.currentTurnNumber}";
            phaseText.text = turnManager.currentPhase == TurnManager.TurnPhase.PlayerTurn ? "玩家回合" : "敌人回合";
        }
    }

    public void ShowUnitInfo(Unit unit)
    {
        unitInfoPanel.SetActive(true);
        unitNameText.text = unit.unitName;
        unitHPText.text = $"HP: {unit.currentHP}/{unit.maxHP}";
        unitAttackText.text = $"攻击力: {unit.attackPower}";
    }

    void OnEndTurnClicked()
    {
        if (turnManager.currentPhase == TurnManager.TurnPhase.PlayerTurn)
        {
            // 强制结束玩家回合
            // 需要通知TurnManager跳过剩余玩家单位
        }
    }
}