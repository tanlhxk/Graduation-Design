using UnityEngine;
using UnityEngine.UI;
using TMPro;

// SkillButtonUI.cs
public class SkillButtonUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;      // 拖拽UI中的Image组件
    public TMP_Text skillNameText;   // 拖拽UI中的Text组件
    public Button buttonComponent; // 拖拽Button组件

    private SkillDataSO currentData;
    private FriendlyUnit ownerUnit;

    // 1. 初始化按钮的方法
    public void SetupButton(SkillDataSO skillData, FriendlyUnit owner)
    {
        currentData = skillData;
        ownerUnit = owner;
        iconImage.sprite = skillData.icon;
        skillNameText.text = skillData.skillName;
        buttonComponent.onClick.RemoveAllListeners();
        buttonComponent.onClick.AddListener(() => {
            PlayerInput.Instance?.OnSkillSelected(currentData);
        });
    }

    // 2. 按钮点击时触发
    void OnButtonClicked()
    {
        // 3. 修改：调用 PlayerInput 的新方法
        PlayerInput.Instance?.OnSkillSelected(currentData);
    }
    public void SetSelected(bool isSelected)
    {
        ColorBlock colors = buttonComponent.colors;
        if (isSelected)
        {
            colors.normalColor = Color.yellow;
            colors.highlightedColor = Color.yellow;
        }
        else
        {
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
        }
        buttonComponent.colors = colors;
    }
    public SkillDataSO GetSkillData()
    {
        return currentData;
    }

    public void SetCooldown(bool isCooling)
    {
        Color color = isCooling ? Color.gray : Color.white;
        if (iconImage != null) iconImage.color = color;
        buttonComponent.interactable = !isCooling;
    }
}