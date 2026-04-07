using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening.Core.Easing;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{

    public static UIManager Instance;

    [Header("Button Prefabs")]
    public SkillButtonUI skillButtonPrefab; // 预制体

    [Header("Container")]
    public Transform buttonContainer; // 按钮的父物体 (Vertical Layout Group)

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

    private List<SkillButtonUI> activeButtons = new List<SkillButtonUI>();
    void Start()
    {
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
        //InstantiateSkillPrefab(null, new Vector3(0, 0, 0));
    }
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // 更新UI显示
        if (TurnManager.Instance != null)
        {
            turnText.text = $"回合: {TurnManager.Instance.currentTurnNumber}";
            phaseText.text = TurnManager.Instance.currentPhase == TurnManager.TurnPhase.PlayerTurn ? "玩家回合" : "敌人回合";
        }
    }
    public void InstantiateSkillPrefab(Image image,Vector3 pos)
    {
        SkillButtonUI skillButton = Instantiate(skillButtonPrefab, pos, Quaternion.identity);
        if(skillButton.GetComponent<Image>()!=null && image != null)
        {
            skillButton.GetComponent<Image>().sprite = image.sprite;
        }
    }
    public void RefreshSkillBar(FriendlyUnit unit)
    {
        ClearSkillButtons();   // 清除旧按钮
        List<SkillDataSO> skills = unit.GetUnitSkills();
        for (int i = 0; i < skills.Count; i++)
        {
            SkillButtonUI btn = CreateSkillButton(skills[i], unit);
            // 可选：根据冷却状态设置按钮
            bool isOnCooldown = false; // 需要你自己实现冷却管理
            btn.SetCooldown(isOnCooldown);
            activeButtons.Add(btn);
        }
    }
    public void ClearSkillButtons()
    {
        foreach (var btn in activeButtons)
        {
            Destroy(btn.gameObject);
        }
        activeButtons.Clear();
    }

    SkillButtonUI CreateSkillButton(SkillDataSO skillData, FriendlyUnit ownerUnit)
    {
        SkillButtonUI newButton = Instantiate(skillButtonPrefab, buttonContainer);

        // SetupButton 现在需要传入技能数据和拥有者
        newButton.SetupButton(skillData, ownerUnit);

        // 如果按钮点击，会调用 PlayerInput.OnSkillSelected
        activeButtons.Add(newButton);
        return newButton;
    }

    public void UpdateSkillSelectionVisual(SkillDataSO selectedSkill)
    {
        // 遍历所有按钮，同步高亮状态
        foreach (var btn in activeButtons)
        {
            // 如果这个按钮的技能 == PlayerInput 传来的当前选中技能，就高亮
            btn.SetSelected(btn.GetSkillData() == selectedSkill);
        }
    }
    public void ClearSkillSelection()
    {
        foreach (var btn in activeButtons)
        {
            btn.SetSelected(false);
        }
        // 这里不需要 Clear，因为 PlayerInput 会紧接着调用 RefreshSkillBar 或者只是取消高亮
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
        if (TurnManager.Instance.currentPhase == TurnManager.TurnPhase.PlayerTurn)
        {
            // 强制结束玩家回合
            // 需要通知TurnManager跳过剩余玩家单位
        }
    }
}