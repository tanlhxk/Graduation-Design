using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening.Core.Easing;

public class SkillButton
{
    public SkillData skillData;
    public Image skillImage;

    // 构造函数
    public SkillButton(SkillData sd, Image si)
    {
        skillData = sd;
        skillImage = si;
    }
}

[CreateAssetMenu(fileName = "SkillImageSet", menuName = "Skill/ImageSet")]
public class SkillImageSet : ScriptableObject
{
    [System.Serializable]
    public struct SkillEntry
    {
        public SkillData skillData;
        public Image skillImage;
    }

    public SkillEntry[] skillEntries;

    public Image GetImage(SkillData skillData)
    {
        foreach (var data in skillEntries)
        {
            if (data.skillData == skillData) return data.skillImage;
        }
        return null;
    }
}

public class UIManager : MonoBehaviour
{
    [Header("回合信息")]
    public TMP_Text turnText;
    public TMP_Text phaseText;

    [Header("单位信息")]
    public GameObject unitInfoPanel;
    public TMP_Text unitNameText;
    public TMP_Text unitHPText;
    public TMP_Text unitAttackText;

    [Header("按钮")]
    [SerializeField] private Button endTurnButton;
    //[SerializeField] private SkillButton[] skillButton;
    [SerializeField] private SkillImageSet skillImageSet;

    public Button skillButtonPrefab;

    private TurnManager turnManager;
    private GameManager gameManager;
    void Start()
    {
        turnManager = FindObjectOfType<TurnManager>();
        gameManager = FindObjectOfType<GameManager>();

        endTurnButton.onClick.AddListener(OnEndTurnClicked);
        //InstantiateSkillPrefab(null, new Vector3(0, 0, 0));
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
    public void InstantiateSkillPrefab(Image image,Vector3 pos)
    {
        Button skillButton = Instantiate(skillButtonPrefab, pos, Quaternion.identity);
        if(skillButton.GetComponent<Image>()!=null && image != null)
        {
            skillButton.GetComponent<Image>().sprite = image.sprite;
        }
    }

    public void ShowUnitInfo(FriendlyUnit unit)
    {
        unitInfoPanel.SetActive(true);
        unitNameText.text = unit.unitName;
        unitHPText.text = $"HP: {unit.currentHP}/{unit.maxHP}";
        unitAttackText.text = $"攻击力: {unit.baseAttack}";
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