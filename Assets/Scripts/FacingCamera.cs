using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FacingCamera : MonoBehaviour
{
    Transform[] childs;

    // Update is called once per frame
    void Update()
    {
        if(childs != null)
        {
            foreach(Transform t in childs)
            {
                t.rotation = Camera.main.transform.rotation;
            }
        }
    }
    public void RefreshFacing()
    {
        childs = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            childs[i] = transform.GetChild(i);
        }
    }
}
