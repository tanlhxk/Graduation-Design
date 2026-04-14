using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacingCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Vector3 camForward = Camera.main.transform.forward;
        camForward.y = 0;
        transform.forward = camForward.normalized;
    }
}
