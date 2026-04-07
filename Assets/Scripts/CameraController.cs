using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("平滑移动")]
    public float smoothTime = 0.2f;               // 自动移动的平滑时间
    private Vector3 autoMoveVelocity = Vector3.zero;

    [Header("摄像机设置")]
    public Vector3 cameraOffset = new Vector3(0f, -10f, -10f);   // 根据实际俯仰角调整

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

        //if (mainCamera != null)
            //mainCamera.orthographic = true;
    }

    private void Update()
    {
        // 处理输入和物理
        HandleInput();
        ApplyPhysics();

        // 自动移动（如果没有拖拽且没有物理速度时）
        if (!isDragging && currentVelocity.magnitude < 0.01f && targetPosition.HasValue)
        {
            Vector3 desiredPosition = targetPosition.Value + cameraOffset;
            desiredPosition.z = cameraOffset.z;

            // 边界 Clamp（直接限制目标位置）
            if (clampToBounds && worldBounds.size != Vector3.zero)
            {
                GetCameraMoveBounds(out float minX, out float maxX, out float minY, out float maxY);
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref autoMoveVelocity, smoothTime);

            // 到达判断
            if (Vector3.Distance(transform.position, desiredPosition) < 0.01f)
            {
                transform.position = desiredPosition;
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
            float vertExtent, horzExtent;
            if (mainCamera.orthographic)
            {
                vertExtent = mainCamera.orthographicSize;
                horzExtent = vertExtent * Screen.width / Screen.height;
            }
            else
            {
                // 透视模式：地面 Z=0，摄像机到地面的距离 = |transform.position.z|
                float distanceToGround = Mathf.Abs(transform.position.z);
                float pitchRad = transform.eulerAngles.x * Mathf.Deg2Rad;
                float halfFovRad = mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;

                // 摄像机在 Z=0 平面上的可视半高（未考虑俯仰角）
                float rawHalfHeight = Mathf.Tan(halfFovRad) * distanceToGround;
                // 因为俯仰角导致地面被拉长，实际半高需要除以 cos(pitch)
                float actualHalfHeight = rawHalfHeight / Mathf.Cos(pitchRad);
                // 半宽只需根据屏幕宽高比计算，X 轴不受俯仰角影响（若摄像机无 roll）
                float actualHalfWidth = actualHalfHeight * Screen.width / Screen.height;

                vertExtent = actualHalfHeight;
                horzExtent = actualHalfWidth;
            }

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
        transform.position = new Vector3(transform.position.x, transform.position.y, cameraOffset.z);

        // 如果速度极小，直接置零防止微动
        if (currentVelocity.magnitude < 0.01f)
            currentVelocity = Vector3.zero;
    }

    // 辅助方法：对目标位置应用弹簧力（用于自动移动）
    private Vector3 ApplyBoundaryForce(Vector3 desiredPosition, ref Vector3 velocity, float smoothTime)
    {
        if (!clampToBounds || worldBounds.size == Vector3.zero)
            return desiredPosition;

        float vertExtent, horzExtent;
        if (mainCamera.orthographic)
        {
            vertExtent = mainCamera.orthographicSize;
            horzExtent = vertExtent * Screen.width / Screen.height;
        }
        else
        {
            // 透视模式：地面 Z=0，摄像机到地面的距离 = |transform.position.z|
            float distanceToGround = Mathf.Abs(transform.position.z);
            float pitchRad = transform.eulerAngles.x * Mathf.Deg2Rad;
            float halfFovRad = mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;

            // 摄像机在 Z=0 平面上的可视半高（未考虑俯仰角）
            float rawHalfHeight = Mathf.Tan(halfFovRad) * distanceToGround;
            // 因为俯仰角导致地面被拉长，实际半高需要除以 cos(pitch)
            float actualHalfHeight = rawHalfHeight / Mathf.Cos(pitchRad);
            // 半宽只需根据屏幕宽高比计算，X 轴不受俯仰角影响（若摄像机无 roll）
            float actualHalfWidth = actualHalfHeight * Screen.width / Screen.height;

            vertExtent = actualHalfHeight;
            horzExtent = actualHalfWidth;
        }
        Vector3 cameraForwardOnGround = transform.forward;
        cameraForwardOnGround.y = 0; // 忽略Y分量，只看水平投影
        Vector3 groundCenter = desiredPosition;
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
        if (isDragging) return;
        currentVelocity = Vector3.zero;
        targetPosition = targetWorldPosition;   // 存储目标点的地面坐标 (x, y, 0)
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
        transform.position = targetWorldPosition + cameraOffset;
        transform.position = new Vector3(transform.position.x, transform.position.y, cameraOffset.z);
    }
    // 获取地面可视半宽半高
    private void GetGroundViewExtents(out float halfWidth, out float halfHeight)
    {
        float distance = Mathf.Abs(transform.position.z);
        Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, distance));
        Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, distance));
        bottomLeft.z = 0; topRight.z = 0;
        halfWidth = (topRight.x - bottomLeft.x) * 0.5f;
        halfHeight = (topRight.y - bottomLeft.y) * 0.5f;
    }

    // 获取摄像机可移动的边界范围
    private void GetCameraMoveBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        if (!clampToBounds || worldBounds.size == Vector3.zero)
        {
            minX = float.NegativeInfinity; maxX = float.PositiveInfinity;
            minY = float.NegativeInfinity; maxY = float.PositiveInfinity;
            return;
        }
        GetGroundViewExtents(out float halfW, out float halfH);
        minX = worldBounds.min.x + halfW;
        maxX = worldBounds.max.x - halfW;
        minY = worldBounds.min.y + halfH;
        maxY = worldBounds.max.y - halfH;
        if (minX > maxX) { float mid = (minX + maxX) * 0.5f; minX = maxX = mid; }
        if (minY > maxY) { float mid = (minY + maxY) * 0.5f; minY = maxY = mid; }
    }
}