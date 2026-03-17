using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("平滑移动")]
    public float smoothTime = 0.2f;
    private Vector3 velocity = Vector3.zero;
    public float zDepth = -10f;

    [Header("拖拽设置")]
    public bool enableDrag = true;
    public float dragSensitivity = 1f;
    public bool invertDrag = true; // true: 地图跟随鼠标（推荐）, false: 同向

    [Header("惯性滑动")]
    [Tooltip("每秒速度衰减比例 (0~10之间)，5表示高速时很快停止，低速时平滑衰减")]
    public float inertiaDamping = 5f;
    public float minInertiaSpeed = 0.1f;

    [Header("边界限制")]
    public bool clampToBounds = true;
    private Bounds worldBounds;

    private Camera mainCamera;
    private Vector3? targetPosition;

    // 拖拽相关
    private bool isDragging = false;
    private Vector2 dragOriginScreen;
    private Vector3 cameraOriginPos;
    private Vector2 dragVelocity;                // 当前拖拽速度（用于惯性）

    // 惯性相关
    private bool isInertiaMoving = false;

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
        // 处理右键拖拽（包括惯性）
        if (enableDrag)
            HandleDrag();

        // 惯性滑动处理（独立于拖拽状态）
        if (isInertiaMoving)
        {
            ApplyInertia();
        }

        // 自动平滑移动（仅在未拖拽且无惯性时执行）
        if (!isDragging && !isInertiaMoving && targetPosition.HasValue)
        {
            Vector3 desiredPosition = new Vector3(targetPosition.Value.x, targetPosition.Value.y, zDepth);
            if (clampToBounds)
                desiredPosition = ClampPositionToBounds(desiredPosition);

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

            if (Vector3.Distance(transform.position, desiredPosition) < 0.01f)
            {
                transform.position = desiredPosition;
                targetPosition = null;
                velocity = Vector3.zero;
            }
        }
    }

    private void HandleDrag()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (isDragging)
                isDragging = false;
            return;
        }

        // 右键按下：开始拖拽
        if (Input.GetMouseButtonDown(1))
        {
            isDragging = true;
            isInertiaMoving = false;            // 停止任何惯性
            dragVelocity = Vector2.zero;
            dragOriginScreen = Input.mousePosition;
            cameraOriginPos = transform.position;
            targetPosition = null;
            velocity = Vector3.zero;
        }
        // 右键按住：移动摄像机
        else if (isDragging && Input.GetMouseButton(1))
        {
            Vector2 currentScreen = Input.mousePosition;
            Vector2 screenDelta = currentScreen - dragOriginScreen;

            // 屏幕像素 → 世界单位
            float worldUnitsPerPixel = (mainCamera.orthographicSize * 2f) / Screen.height;
            Vector3 worldDelta = new Vector3(screenDelta.x, screenDelta.y, 0) * worldUnitsPerPixel * dragSensitivity;

            // 计算新位置（根据 invertDrag 决定方向）
            Vector3 newPos = cameraOriginPos + (invertDrag ? -worldDelta : worldDelta);
            newPos.z = zDepth;

            if (clampToBounds)
                newPos = ClampPositionToBounds(newPos);

            // 更新速度（用于惯性）
            dragVelocity = (newPos - transform.position) / Time.deltaTime;

            transform.position = newPos;
        }
        // 右键松开：结束拖拽，启动惯性
        else if (isDragging && Input.GetMouseButtonUp(1))
        {
            isDragging = false;
            if (dragVelocity.magnitude > minInertiaSpeed)
            {
                isInertiaMoving = true;
            }
            else
            {
                dragVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 应用惯性滑动（每帧调用）
    /// </summary>
    private void ApplyInertia()
    {
        // 阻尼衰减：速度 *= (1 - damping * deltaTime)
        float dampingFactor = 1f - Mathf.Clamp01(inertiaDamping * Time.deltaTime);
        dragVelocity *= dampingFactor;

        // 如果速度太小，停止惯性
        if (dragVelocity.magnitude < minInertiaSpeed)
        {
            dragVelocity = Vector2.zero;
            isInertiaMoving = false;
            return;
        }

        // 根据速度移动
        Vector3 moveDelta = new Vector3(dragVelocity.x, dragVelocity.y, 0) * Time.deltaTime;
        Vector3 newPos = transform.position + moveDelta;
        newPos.z = zDepth;

        if (clampToBounds)
            newPos = ClampPositionToBounds(newPos);

        // 防止卡边界微动
        if (Vector3.Distance(newPos, transform.position) < 0.001f)
        {
            dragVelocity = Vector2.zero;
            isInertiaMoving = false;
        }
        else
        {
            transform.position = newPos;
        }
    }

    /// <summary>
    /// 获取鼠标在世界坐标的位置（平面 Z=0）
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -transform.position.z;
        return Camera.main.ScreenToWorldPoint(mouseScreen);
    }

    /// <summary>
    /// 将位置限制在地图边界内
    /// </summary>
    private Vector3 ClampPositionToBounds(Vector3 position)
    {
        if (!clampToBounds || worldBounds.size == Vector3.zero)
            return position;

        float vertExtent = mainCamera.orthographicSize;
        float horzExtent = vertExtent * Screen.width / Screen.height;

        float minX = worldBounds.min.x + horzExtent;
        float maxX = worldBounds.max.x - horzExtent;
        float minY = worldBounds.min.y + vertExtent;
        float maxY = worldBounds.max.y - vertExtent;

        if (minX > maxX)
        {
            position.x = (worldBounds.min.x + worldBounds.max.x) * 0.5f;
        }
        else
        {
            position.x = Mathf.Clamp(position.x, minX, maxX);
        }

        if (minY > maxY)
        {
            position.y = (worldBounds.min.y + worldBounds.max.y) * 0.5f;
        }
        else
        {
            position.y = Mathf.Clamp(position.y, minY, maxY);
        }

        return position;
    }

    /// <summary>
    /// 设置世界边界（由GridManager调用）
    /// </summary>
    public void SetWorldBounds(Bounds bounds)
    {
        worldBounds = bounds;
        if (clampToBounds)
        {
            transform.position = ClampPositionToBounds(transform.position);
        }
    }

    /// <summary>
    /// 平滑移动到指定位置（用于玩家选中单位）
    /// </summary>
    public void SmoothMoveTo(Vector3 targetWorldPosition)
    {
        if (isDragging)
            return;

        // 停止惯性
        isInertiaMoving = false;
        dragVelocity = Vector2.zero;

        targetPosition = targetWorldPosition;
    }

    /// <summary>
    /// 强制瞬间定位（用于敌人行动、游戏开始时）
    /// </summary>
    public void ForcePosition(Vector3 targetWorldPosition)
    {
        isDragging = false;
        isInertiaMoving = false;
        dragVelocity = Vector2.zero;
        transform.position = new Vector3(targetWorldPosition.x, targetWorldPosition.y, zDepth);
        targetPosition = null;
        velocity = Vector3.zero;
    }

    /// <summary>
    /// 获取当前摄像机覆盖的范围
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