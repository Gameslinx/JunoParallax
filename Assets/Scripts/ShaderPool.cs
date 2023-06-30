using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderPool
{
    public static Queue<ComputeShader> computeShaders = new Queue<ComputeShader>();
    static float memoryOverBudget = 0;
    public static void Initialize(int count)
    {
        for (int i = 0; i < count; i++)
        {
            ComputeShader cs = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.quadShader);
            computeShaders.Enqueue(cs);
        }
    }
    public static void Return(ComputeShader cs)
    {
        computeShaders.Enqueue(cs);
    }
    public static ComputeShader Retrieve()
    {
        // NOTE: Shaders retrived from the pool CAN have their previous buffers/values set. REASSIGN EVERYTHING before dispatching to prevent weird shit
        // Not safe from running out of compute shaders yet
        if (computeShaders.Count - 1 == 0)
        {
            // Pool is about to run out!
            memoryOverBudget += 0.41f;
            Debug.Log("[Parallax] WARNING: Compute shader pool is empty - this will lead to frame stuttering. Increase the allocated memory in ParallaxSettings.xml. Additional " + memoryOverBudget + "mb required!");
            return UnityEngine.Object.Instantiate(Mod.ParallaxInstance.quadShader);
        }
        return computeShaders.Dequeue();
    }
}
