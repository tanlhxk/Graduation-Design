using Game.Combat.Units;
using UnityEngine;
using UnityEngine.UI;
public class HealthBar : MonoBehaviour
{
    [Header("References")]
    public Slider slider;
    public Unit targetUnit;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0, 1.5f, 0);
    public Image Image;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (targetUnit == null)
            targetUnit = GetComponentInParent<Unit>();
        if (slider != null && targetUnit != null)
        {
            slider.maxValue = targetUnit.maxHP;
            slider.value = targetUnit.currentHP;
        }
    }

    void Update()
    {
        if (targetUnit == null)
        {
            Destroy(gameObject);
            return;
        }
        if (slider != null)
        {
            slider.value = targetUnit.currentHP;
            if(targetUnit is FriendlyUnit)
            {
                Image.color = Color.blue;
            }
            else
            {
                Image.color = Color.red;
            }
        }

        // 跟随目标
        transform.position = targetUnit.transform.position + offset;

        // 让血条始终面向相机（适合2.5D）
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                             mainCamera.transform.rotation * Vector3.up);
        }
    }
}