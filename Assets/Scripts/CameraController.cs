using System.Collections.Generic;
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
    public Vector3 cameraOffset = new Vector3(-10f, 12f, -10f);

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
            desiredPosition.y = cameraOffset.y;

            // 边界 Clamp（直接限制目标位置）
            if (clampToBounds && worldBounds.size != Vector3.zero)
            {
                GetCameraMoveBounds(out float minX, out float maxX, out float minZ, out float maxZ);
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
                desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);
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
            Vector3 targetSpeed = (mainCamera.transform.right * delta.x + mainCamera.transform.forward * delta.y)
                      * worldUnitsPerPixel * dragSensitivity / Time.deltaTime;
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
            float minZ = worldBounds.min.z + vertExtent;
            float maxZ = worldBounds.max.z - vertExtent;

            Vector3 force = Vector3.zero;
            if (position.x < minX) force.x = (minX - position.x) * edgeSpringStiffness;
            else if (position.x > maxX) force.x = (maxX - position.x) * edgeSpringStiffness;

            if (position.z < minZ) force.z = (minZ - position.z) * edgeSpringStiffness;
            else if (position.z > maxZ) force.z = (maxZ - position.z) * edgeSpringStiffness;

            // 弹簧力加阻尼
            currentVelocity += force * Time.deltaTime;
            currentVelocity -= currentVelocity * edgeSpringDamping * Time.deltaTime;
        }

        // 应用速度移动摄像机
        transform.position += new Vector3(currentVelocity.x, 0, currentVelocity.z) * Time.deltaTime;
        transform.position = new Vector3(transform.position.x, cameraOffset.y, transform.position.z);

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
        transform.position = new Vector3(transform.position.x, cameraOffset.y, transform.position.z);
    }
    // 获取地面可视半宽半高
    private void GetGroundViewExtents(out float halfWidth, out float halfHeight)
    {
        Camera cam = mainCamera;
        Transform camTrans = transform;
        Plane groundPlane = new Plane(Vector3.up, 0f); // 地面 Y=0

        // 正交相机的视锥体尺寸
        float orthoSize = cam.orthographicSize;
        float aspect = cam.aspect;
        float halfHeightWorld = orthoSize;
        float halfWidthWorld = orthoSize * aspect;

        // 相机的局部坐标系：前、右、上
        Vector3 forward = camTrans.forward;
        Vector3 right = camTrans.right;
        Vector3 up = camTrans.up;

        // 相机在世界空间中的位置
        Vector3 camPos = camTrans.position;

        // 计算视锥体近平面中心点沿 forward 方向偏移近平面距离
        // 对于正交相机，视锥体的四个角点方向与相机局部坐标轴平行
        Vector3 nearCenter = camPos + forward * cam.nearClipPlane;
        Vector3[] cornersLocal = new Vector3[]
        {
        new Vector3(-halfWidthWorld, -halfHeightWorld, 0), // 左下
        new Vector3( halfWidthWorld, -halfHeightWorld, 0), // 右下
        new Vector3(-halfWidthWorld,  halfHeightWorld, 0), // 左上
        new Vector3( halfWidthWorld,  halfHeightWorld, 0)  // 右上
        };

        List<Vector3> groundPoints = new List<Vector3>(4);
        foreach (var localOffset in cornersLocal)
        {
            // 将局部偏移转换到世界坐标：相机局部坐标系下的偏移 (right, up) 方向
            Vector3 worldOffset = right * localOffset.x + up * localOffset.y;
            Vector3 pointOnNearPlane = nearCenter + worldOffset;

            // 从相机位置发射经过该点的射线（正交相机中，所有投影线平行于 forward）
            Ray ray = new Ray(camPos, (pointOnNearPlane - camPos).normalized);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                groundPoints.Add(hitPoint);
            }
            else
            {
                // 理论上不会发生（相机在地面之上且向下看）
                groundPoints.Add(camPos + forward * 1000f);
            }
        }

        // 计算这些点的 X 范围与 Z 范围
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in groundPoints)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        halfWidth = (maxX - minX) * 0.5f;
        halfHeight = (maxZ - minZ) * 0.5f;
    }

    // 获取摄像机可移动的边界范围
    private void GetCameraMoveBounds(out float minX, out float maxX, out float minZ, out float maxZ)
    {
        if (!clampToBounds || worldBounds.size == Vector3.zero)
        {
            minX = float.NegativeInfinity; maxX = float.PositiveInfinity;
            minZ = float.NegativeInfinity; maxZ = float.PositiveInfinity;
            return;
        }
        GetGroundViewExtents(out float halfW, out float halfH);
        minX = worldBounds.min.x + halfW;
        maxX = worldBounds.max.x - halfW;
        minZ = worldBounds.min.z + halfH;
        maxZ = worldBounds.max.z - halfH;
        if (minX > maxX) { float mid = (minX + maxX) * 0.5f; minX = maxX = mid; }
        if (minZ > maxZ) { float mid = (minZ + maxZ) * 0.5f; minZ = maxZ = mid; }
    }
}