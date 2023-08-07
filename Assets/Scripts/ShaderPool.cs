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
public class ColliderPool
{
    public static Queue<GameObject> objects = new Queue<GameObject>();
    static float memoryOverBudget = 0;
    public static Material mat;
    public static GameObject originalObject;
    public static void Initialize(int count)
    {
        mat = new Material(Shader.Find("Standard"));
        for (int i = 0; i < count; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); //new GameObject();
            go.layer = 29;
            //go.AddComponent<MeshFilter>();
            //go.AddComponent<MeshCollider>();
            //go.AddComponent<AutoDisabler>();
            //go.AddComponent<MeshRenderer>();
            //go.GetComponent<MeshRenderer>().sharedMaterial = mat;



            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.AddComponent<MeshCollider>();

            go.SetActive(false);
            GameObject.DontDestroyOnLoad(go);
            objects.Enqueue(go);
            if (i == 0) { originalObject = go; }
        }
    }
    public static void Return(GameObject go)
    {
        objects.Enqueue(go);
    }
    public static GameObject Retrieve()
    {
        if (objects.Count - 1 == 0)
        {
            // Pool is about to run out!
            memoryOverBudget += 0.41f;
            Debug.Log("[Parallax] WARNING: Object pool is empty - this will lead to frame stuttering. Increase the allocated memory in ParallaxSettings.xml. Additional " + memoryOverBudget + "mb required!");
            return UnityEngine.Object.Instantiate(originalObject);
        }
        return objects.Dequeue();
    }
}
