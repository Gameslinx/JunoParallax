using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoDisabler : MonoBehaviour
{
    // If this is set too low, the object will be deleted before the async task completes
    public int framesLeft = 10;
    void OnEnable()
    {
        framesLeft = 10;
    }

    private void FixedUpdate()
    {
        framesLeft--;
        if (framesLeft == 0)
        {
            gameObject.SetActive(false);
        }
        ColliderPool.Return(this.gameObject);
    }
}
