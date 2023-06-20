using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderPool
{
    public static Queue<ComputeShader> computeShaders = new Queue<ComputeShader>();
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
        return computeShaders.Dequeue();
    }
}
