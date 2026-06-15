using UnityEngine;

namespace Game.Camera
{
    public class FacingCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null) return;  // 安全退出
            Vector3 camForward = UnityEngine.Camera.main.transform.forward;
            //camForward.y = 0;
            transform.forward = camForward.normalized;
        }
    }
}