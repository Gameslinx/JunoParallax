using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.PlanetStudio.Flyouts.CelestialBodyProperties;
using ModApi.Flight;
using ModApi.Planet;
using ModApi.Scenes;
using ModApi.Scenes.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public struct RendererStats
{
    // Object count AFTER culling - What is actually being rendered right now
    public int objectCount;
    public int vertexCount;
    public int triangleCount;
    public string scatterName;
}
public class ScatterRenderer : MonoBehaviour                   //There is an instance of this PER SCATTER, on each quad sphere
{
    public ScatterManager manager;
    public Scatter scatter;

    public ComputeShader shader;                               //Shader containing functions to sort objects by their shadow cascades to prevent rendering them in all four cascades

    public ComputeBuffer lod0;
    public ComputeBuffer lod1;
    public ComputeBuffer lod2;

    ComputeBuffer argslod0;
    ComputeBuffer argslod1;
    ComputeBuffer argslod2;

    int _MaxCount;

    public delegate void EvaluatePositions();
    public EvaluatePositions OnEvaluatePositions;

    public Mesh meshLod0;
    public Mesh meshLod1;
    public Mesh meshLod2;

    public Material materialLOD0;
    public Material materialLOD1;
    public Material materialLOD2;

    public Bounds rendererBounds;

    public ShadowCastingMode shadowsLOD0;
    public ShadowCastingMode shadowsLOD1;
    public ShadowCastingMode shadowsLOD2;

    int layerlod0 = 0;
    int layerlod1 = 0;
    int layerlod2 = 0;

    void Prerequisites()    //Load mesh, materials...
    {
        Mesh mesh = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.material._Mesh);
        Mesh mesh2 = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.distribution.lod0.material._Mesh);
        Mesh mesh3 = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.distribution.lod1.material._Mesh);

        shadowsLOD0 = scatter.material.castShadows ? (ParallaxSettings.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off) : ShadowCastingMode.Off;
        shadowsLOD1 = scatter.distribution.lod0.material.castShadows ? (ParallaxSettings.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off) : ShadowCastingMode.Off;
        shadowsLOD2 = scatter.distribution.lod1.material.castShadows ? (ParallaxSettings.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off) : ShadowCastingMode.Off;

        Material mat = SetupMaterial(scatter.material._Shader);
        materialLOD0 = Instantiate(mat);

        Material matlod1 = SetupMaterial(scatter.distribution.lod0.material._Shader);
        materialLOD1 = Instantiate(matlod1);

        Material matlod2 = SetupMaterial(scatter.distribution.lod1.material._Shader);
        materialLOD2 = Instantiate(matlod2);

        meshLod0 = Instantiate(mesh);
        meshLod1 = Instantiate(mesh2);
        meshLod2 = Instantiate(mesh3);

        // Calculate the maximum size of the mesh - this is for collisions

        GetMeshRadius();

        FirstTimeArgs();
    }
    void GetMeshRadius()
    {
        Vector3[] verts = meshLod1.vertices;
        // First get the furthest vertex away from the origin in local space
        float furthestDist = 0;
        Vector3 furthestVert = Vector3.zero;
        for (int i = 0; i < verts.Length; i++)
        {
            float distance = Vector3.SqrMagnitude(verts[i]);
            if (distance > furthestDist)
            {
                furthestDist = distance;
                furthestVert = verts[i];
            }
        }

        // Construct a TRS matrix with a translation of 0, rotation of 0, and scale = max scale. Now we can get world space distance to furthest vert - essentially radius of mesh
        Matrix4x4 objectToWorld = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scatter.distribution._MaxScale);
        furthestVert = objectToWorld.MultiplyPoint3x4(furthestVert);
        // Get sqr distance from origin
        scatter.sqrCollisionMeshRadius = Vector3.SqrMagnitude(furthestVert);
    }
    Material SetupMaterial(ScatterShader scatterShader)
    {
        Shader shader = Mod.ParallaxInstance.ResourceLoader.LoadAsset<Shader>($"Assets/Scripts/Shaders/ParallaxShaders/{scatterShader.resourceName}.shader");
        if (shader == null)
        {
            Debug.Log("[Exception] Could not load shader: " + scatterShader.name + ", is it included in the build?");
        }
        Material mat = new Material(shader);

        // Setup material
        mat = scatterShader.AssignMaterialVariables(mat);

        return mat;
    }
    

    public void Initialize()
    {
        Prerequisites();

        shader = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.renderShader);

        _MaxCount = scatter.maxObjectsToRender;     //Triangle count * pop mult

        rendererBounds = new Bounds(Vector3.zero, Vector3.one * 100000);

        lod0 = new ComputeBuffer(_MaxCount / 10, TransformData.Size(), ComputeBufferType.Append);
        lod1 = new ComputeBuffer(_MaxCount / 2, TransformData.Size(), ComputeBufferType.Append);
        lod2 = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);

        materialLOD0.SetBuffer("_Properties", lod0);
        materialLOD1.SetBuffer("_Properties", lod1);
        materialLOD2.SetBuffer("_Properties", lod2);

        lod0.SetCounterValue(0);
        lod1.SetCounterValue(0);
        lod2.SetCounterValue(0);
    }
    void FirstTimeArgs()
    {
        uint[] argumentsLod0 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod0[0] = (uint)meshLod0.GetIndexCount(0);
        argumentsLod0[1] = 0; //This is the count, but we will use copycount to fill this in to avoid reading back
        argumentsLod0[2] = (uint)meshLod0.GetIndexStart(0);
        argumentsLod0[3] = (uint)meshLod0.GetBaseVertex(0);

        uint[] argumentsLod1 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod1[0] = (uint)meshLod1.GetIndexCount(0);
        argumentsLod1[1] = 0; //This is the count, but we will use copycount to fill this in to avoid reading back
        argumentsLod1[2] = (uint)meshLod1.GetIndexStart(0);
        argumentsLod1[3] = (uint)meshLod1.GetBaseVertex(0);

        uint[] argumentsLod2 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod2[0] = (uint)meshLod2.GetIndexCount(0);
        argumentsLod2[1] = 0; //This is the count, but we will use copycount to fill this in to avoid reading back
        argumentsLod2[2] = (uint)meshLod2.GetIndexStart(0);
        argumentsLod2[3] = (uint)meshLod2.GetBaseVertex(0);

        argslod0 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod0.SetData(argumentsLod0);

        argslod1 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod1.SetData(argumentsLod1);

        argslod2 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod2.SetData(argumentsLod2);
    }
    void Update()       //Evaluate cascades, render
    {
        if (scatter.numActive == 0) { return; }
        if (!manager.mainCamera.isActiveAndEnabled) { return; }
        //Control evaluate points

        rendererBounds.center = Vector3.zero;

        lod0.SetCounterValue(0);
        lod1.SetCounterValue(0);
        lod2.SetCounterValue(0);

        EvaluatePoints();

        ComputeBuffer.CopyCount(lod0, argslod0, 4);
        ComputeBuffer.CopyCount(lod1, argslod1, 4);
        ComputeBuffer.CopyCount(lod2, argslod2, 4);

        materialLOD0.SetVector("_PlanetOrigin", (Vector3)manager.quadSphere.FramePosition);
        materialLOD1.SetVector("_PlanetOrigin", (Vector3)manager.quadSphere.FramePosition);
        materialLOD2.SetVector("_PlanetOrigin", (Vector3)manager.quadSphere.FramePosition);

        Graphics.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0, rendererBounds, argslod0, 0, null, shadowsLOD0, ParallaxSettings.receiveShadows, layerlod0, manager.mainCamera);
        Graphics.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1, rendererBounds, argslod1, 0, null, shadowsLOD1, ParallaxSettings.receiveShadows, layerlod1, manager.mainCamera);
        Graphics.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2, rendererBounds, argslod2, 0, null, shadowsLOD2, ParallaxSettings.receiveShadows, layerlod2, manager.mainCamera);
    }
    void EvaluatePoints()
    {
        if (OnEvaluatePositions != null)
        {
            OnEvaluatePositions();
        }
    }
    public RendererStats[] GetStats()
    {
        // One stat for each LOD
        RendererStats[] stats = new RendererStats[3];

        uint[] args0 = new uint[5];
        uint[] args1 = new uint[5];
        uint[] args2 = new uint[5];

        argslod0.GetData(args0);
        argslod1.GetData(args1);
        argslod2.GetData(args2);

        stats[0] = new RendererStats { objectCount = (int)args0[1], triangleCount = (int)args0[1] * (meshLod0.triangles.Length / 3), vertexCount = (int)args0[1] * meshLod0.vertexCount, scatterName = scatter.DisplayName };
        stats[1] = new RendererStats { objectCount = (int)args1[1], triangleCount = (int)args1[1] * (meshLod1.triangles.Length / 3), vertexCount = (int)args1[1] * meshLod1.vertexCount, scatterName = scatter.DisplayName };
        stats[2] = new RendererStats { objectCount = (int)args2[1], triangleCount = (int)args2[1] * (meshLod2.triangles.Length / 3), vertexCount = (int)args2[1] * meshLod2.vertexCount, scatterName = scatter.DisplayName };

        return stats;
    }
    public void Cleanup()
    {
        lod0?.Release();
        lod1?.Release();
        lod2?.Release();

        argslod0?.Release();
        argslod1?.Release();
        argslod2?.Release();
    }
}
