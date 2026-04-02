using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("平滑移动")]
    public float smoothTime = 0.2f;               // 自动移动的平滑时间
    private Vector3 autoMoveVelocity = Vector3.zero;

    [Header("摄像机设置")]
    public float zDepth = -10f;

    [Header("拖拽设置")]
    public bool enableDrag = true;
    public float dragSensitivity = 5f;            // 鼠标移动对摄像机速度的影响
    public bool invertDrag = true;                 // true: 地图跟随鼠标（推荐）

    [Header("物理参数")]
    public float dragLinearDamping = 8f;           // 拖拽时的线性阻尼（速度衰减）
    public float inertiaDamping = 5f;              // 松开后的惯性阻尼
    public float edgeSpringStiffness = 20f;        // 边界弹簧刚度
    public float edgeSpringDamping = 8f;           // 边界弹簧阻尼
    public float maxSpeed = 30f;                   // 最大速度限制

    [Header("边界")]
    public bool clampToBounds = true;
    private Bounds worldBounds;

    private Camera mainCamera;
    private Vector3 currentVelocity;                // 当前摄像机速度

    // 拖拽状态
    private bool isDragging = false;
    private Vector2 lastMousePos;                   // 上一帧鼠标位置（屏幕坐标）

    // 自动移动目标
    private Vector3? targetPosition;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
            mainCamera.orthographic = true;
    }

    private void Update()
    {
        // 处理输入和物理
        HandleInput();
        ApplyPhysics();

        // 自动移动（如果没有拖拽且没有物理速度时）
        if (!isDragging && currentVelocity.magnitude < 0.01f && targetPosition.HasValue)
        {
            Vector3 desired = new Vector3(targetPosition.Value.x, targetPosition.Value.y, zDepth);
            desired = ApplyBoundaryForce(desired, ref autoMoveVelocity, smoothTime); // 用弹簧平滑到达
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref autoMoveVelocity, smoothTime);

            if (Vector3.Distance(transform.position, desired) < 0.01f)
            {
                transform.position = desired;
                targetPosition = null;
                autoMoveVelocity = Vector3.zero;
            }
        }
    }

    private void HandleInput()
    {
        if (!enableDrag) return;

        // 鼠标在UI上时不处理
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (isDragging) isDragging = false;
            return;
        }

        // 右键按下：开始拖拽
        if (Input.GetMouseButtonDown(1))
        {
            isDragging = true;
            lastMousePos = Input.mousePosition;
            targetPosition = null;                  // 取消自动移动
            autoMoveVelocity = Vector3.zero;
        }
        // 右键按住：根据鼠标移动施加速度
        else if (isDragging && Input.GetMouseButton(1))
        {
            Vector2 currentMousePos = Input.mousePosition;
            Vector2 delta = currentMousePos - lastMousePos;

            // 将屏幕像素位移转换为世界速度
            float worldUnitsPerPixel = (mainCamera.orthographicSize * 2f) / Screen.height;
            Vector3 targetSpeed = new Vector3(delta.x, delta.y, 0) * worldUnitsPerPixel * dragSensitivity / Time.deltaTime;
            if (invertDrag) targetSpeed = -targetSpeed;

            // 目标速度叠加到当前速度（瞬间响应，也可用平滑，但直接加手感更快）
            currentVelocity += targetSpeed * Time.deltaTime * 10f; // 乘10快速响应，可调
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxSpeed);

            lastMousePos = currentMousePos;
        }
        // 右键松开：停止拖拽（惯性由物理自动处理）
        else if (isDragging && Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }
    }

    private void ApplyPhysics()
    {
        if (!isDragging)
        {
            // 未拖拽时应用惯性阻尼
            float damping = inertiaDamping;
            currentVelocity *= (1f - Mathf.Clamp01(damping * Time.deltaTime));
        }
        else
        {
            // 拖拽时也应用一点阻尼，防止无限加速
            currentVelocity *= (1f - Mathf.Clamp01(dragLinearDamping * Time.deltaTime));
        }

        // 边界弹簧力
        if (clampToBounds && worldBounds.size != Vector3.zero)
        {
            Vector3 position = transform.position;
            float vertExtent = mainCamera.orthographicSize;
            float horzExtent = vertExtent * Screen.width / Screen.height;

            float minX = worldBounds.min.x + horzExtent;
            float maxX = worldBounds.max.x - horzExtent;
            float minY = worldBounds.min.y + vertExtent;
            float maxY = worldBounds.max.y - vertExtent;

            Vector3 force = Vector3.zero;
            if (position.x < minX) force.x = (minX - position.x) * edgeSpringStiffness;
            else if (position.x > maxX) force.x = (maxX - position.x) * edgeSpringStiffness;

            if (position.y < minY) force.y = (minY - position.y) * edgeSpringStiffness;
            else if (position.y > maxY) force.y = (maxY - position.y) * edgeSpringStiffness;

            // 弹簧力加阻尼
            currentVelocity += force * Time.deltaTime;
            currentVelocity -= currentVelocity * edgeSpringDamping * Time.deltaTime;
        }

        // 应用速度移动摄像机
        transform.position += currentVelocity * Time.deltaTime;
        transform.position = new Vector3(transform.position.x, transform.position.y, zDepth);

        // 如果速度极小，直接置零防止微动
        if (currentVelocity.magnitude < 0.01f)
            currentVelocity = Vector3.zero;
    }

    // 辅助方法：对目标位置应用弹簧力（用于自动移动）
    private Vector3 ApplyBoundaryForce(Vector3 desiredPosition, ref Vector3 velocity, float smoothTime)
    {
        if (!clampToBounds || worldBounds.size == Vector3.zero)
            return desiredPosition;

        float vertExtent = mainCamera.orthographicSize;
        float horzExtent = vertExtent * Screen.width / Screen.height;
        float minX = worldBounds.min.x + horzExtent;
        float maxX = worldBounds.max.x - horzExtent;
        float minY = worldBounds.min.y + vertExtent;
        float maxY = worldBounds.max.y - vertExtent;

        Vector3 clamped = desiredPosition;
        if (clamped.x < minX) clamped.x = minX;
        else if (clamped.x > maxX) clamped.x = maxX;
        if (clamped.y < minY) clamped.y = minY;
        else if (clamped.y > maxY) clamped.y = maxY;

        return clamped;
    }

    /// <summary>
    /// 设置世界边界
    /// </summary>
    public void SetWorldBounds(Bounds bounds)
    {
        worldBounds = bounds;
    }

    /// <summary>
    /// 平滑移动到目标位置（选中单位时调用）
    /// </summary>
    public void SmoothMoveTo(Vector3 targetWorldPosition)
    {
        if (isDragging) return;          // 拖拽时忽略
        currentVelocity = Vector3.zero;   // 停止物理
        targetPosition = targetWorldPosition;
    }

    /// <summary>
    /// 强制瞬间定位
    /// </summary>
    public void ForcePosition(Vector3 targetWorldPosition)
    {
        isDragging = false;
        currentVelocity = Vector3.zero;
        autoMoveVelocity = Vector3.zero;
        targetPosition = null;
        transform.position = new Vector3(targetWorldPosition.x, targetWorldPosition.y, zDepth);
    }

    /// <summary>
    /// 获取摄像机视野矩形
    /// </summary>
    public Rect GetCameraBounds()
    {
        float screenAspect = (float)Screen.width / Screen.height;
        float camHeight = mainCamera.orthographicSize;
        float camWidth = camHeight * screenAspect;

        Vector3 bottomLeft = transform.position - new Vector3(camWidth, camHeight, 0);
        Vector3 topRight = transform.position + new Vector3(camWidth, camHeight, 0);

        return new Rect(bottomLeft.x, bottomLeft.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
    }
}