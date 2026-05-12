using UnityEngine;

namespace Game.Camera
{
    public class FacingCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            Vector3 camForward = UnityEngine.Camera.main.transform.forward;
            //camForward.y = 0;
            transform.forward = camForward.normalized;
        }
    }
}