using UnityEngine;
using UnityEngine.UI;
using TMPro;

// SkillButtonUI.cs
public class SkillButtonUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;           // 技能图标
    private string skillNameText;    // 技能名称
    public Button buttonComponent;    // 按钮组件
    public Image borderImage;         // 边框图片（用于高亮显示）

    [Header("Border Sprites")]
    public Sprite normalBorder;       // 未选中时的边框图片
    public Sprite selectedBorder;     // 选中时的边框图片

    private SkillDataSO currentData;
    private FriendlyUnit ownerUnit;

    // 初始化按钮
    public void SetupButton(SkillDataSO skillData, FriendlyUnit owner)
    {
        currentData = skillData;
        ownerUnit = owner;
        iconImage.sprite = skillData.icon;
        skillNameText = skillData.skillName;
        buttonComponent.onClick.RemoveAllListeners();
        buttonComponent.onClick.AddListener(() => {
            PlayerInput.Instance?.OnSkillSelected(currentData);
        });

        // 确保初始状态为未选中边框
        SetSelected(false);
    }

    // 设置选中状态（切换边框图片）
    public void SetSelected(bool isSelected)
    {
        if (borderImage != null && normalBorder != null && selectedBorder != null)
        {
            borderImage.sprite = isSelected ? selectedBorder : normalBorder;
        }
    }

    // 获取关联的技能数据
    public SkillDataSO GetSkillData()
    {
        return currentData;
    }

    // 设置冷却状态（按钮变灰，图标变灰，不可交互）
    public void SetCooldown(bool isCooling)
    {
        if (iconImage != null)
            iconImage.color = isCooling ? Color.gray : Color.white;
        buttonComponent.interactable = !isCooling;

        // 冷却时不清除边框选中状态，但通常冷却时不应该被选中，所以可额外处理：
        if (isCooling)
        {
            SetSelected(false);
        }
    }
}