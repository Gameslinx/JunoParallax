using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FramerateMonitor : MonoBehaviour
{
    // Start is called before the first frame update
    public static float fps = 150;
    public float velocity = 0;

    public static float factor = 1.0f;
    public static float targetFactor = 1.0f;
    public float factorVelocity = 1.0f;

    // Tolerance FPS - Don't do anything if the fps barely changed
    public float tolerance = 4.0f;
    public float lastFpsChange = 0;
    // Update is called once per frame
    void Update()
    {
        if (!ParallaxSettings.enableDynamicLOD) { return; }
        fps = Mathf.SmoothDamp(fps, 1f / Time.deltaTime, ref velocity, 0.1f);

        // If within, for example, 4fps of the target, do nothing
        if (Mathf.Abs(fps - ParallaxSettings.targetFPS) < tolerance)
        {
            return;
        }

        if (fps < ParallaxSettings.targetFPS && factor > ParallaxSettings.minLODFactor)
        {
            targetFactor -= 0.005f;
        }
        if (fps > ParallaxSettings.targetFPS && factor < ParallaxSettings.maxLODFactor)
        {
            targetFactor += 0.005f;
        }
    }
    private void LateUpdate()
    {
        if (!ParallaxSettings.enableDynamicLOD) { return; }
        factor = Mathf.SmoothDamp(factor, targetFactor, ref factorVelocity, 0.2f);
        factor = Mathf.SmoothDamp(factor, targetFactor, ref factorVelocity, 0.2f);
    }
}
