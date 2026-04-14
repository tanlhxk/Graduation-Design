using UnityEngine;
using System.Collections.Generic;

public class SortingOrderManager : MonoBehaviour
{
    public static SortingOrderManager Instance { get; private set; }

    // 存储所有待排序的实体
    private List<SortableEntity> entities = new List<SortableEntity>();

    // 可选的性能优化：每隔几帧更新一次
    [Header("性能优化")]
    public int updateFrameSkip = 1;      // 每1帧更新（设为2则每2帧更新一次）
    private int frameCounter = 0;

    // 实体数据结构
    public class SortableEntity
    {
        public SpriteRenderer renderer;   // 渲染器
        public Transform transform;       // 物体的变换组件（用于获取世界坐标）
        public int baseOrder;             // 基础排序值（偏移量）
        public int orderFactor;           // 排序系数（灵敏度）
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// 注册一个需要动态排序的物体
    /// </summary>
    public void Register(SpriteRenderer renderer, Transform trans, int baseOrder = 0, int orderFactor = 100)
    {
        if (renderer == null || trans == null) return;
        entities.Add(new SortableEntity
        {
            renderer = renderer,
            transform = trans,
            baseOrder = baseOrder,
            orderFactor = orderFactor
        });
    }

    /// <summary>
    /// 注销物体（物体销毁时必须调用）
    /// </summary>
    public void Unregister(SpriteRenderer renderer)
    {
        entities.RemoveAll(e => e.renderer == renderer);
    }

    private void LateUpdate()
    {
        // 帧间隔控制
        frameCounter++;
        if (frameCounter % updateFrameSkip != 0) return;

        // 遍历所有实体，根据世界Z坐标计算 sortingOrder
        foreach (var entity in entities)
        {
            if (entity.renderer == null) continue;

            // 核心公式：排序值 = 基础偏移 - (Z坐标 × 系数)
            // Z 越小（越靠近屏幕底部）→ 减去负数 → 排序值越大 → 渲染越靠前
            int order = entity.baseOrder - Mathf.RoundToInt((entity.transform.position.x + entity.transform.position.z) * entity.orderFactor);
            entity.renderer.sortingOrder = order;
        }
    }
}