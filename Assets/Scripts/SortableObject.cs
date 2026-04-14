using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SortableObject : MonoBehaviour
{
    [Tooltip("基础排序偏移，值越大渲染越靠前")]
    public int baseOrder = 0;

    [Tooltip("排序系数，值越大Z坐标对排序影响越明显")]
    public int orderFactor = 100;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (SortingOrderManager.Instance != null)
        {
            SortingOrderManager.Instance.Register(spriteRenderer, transform, baseOrder, orderFactor);
        }
        else
        {
            Debug.LogError("场景中缺少 SortingOrderManager 实例！");
        }
    }

    private void OnDisable()
    {
        if (SortingOrderManager.Instance != null)
        {
            SortingOrderManager.Instance.Unregister(spriteRenderer);
        }
    }
}