using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.PlanetStudio.Flyouts.CelestialBodyProperties;
using ModApi.Flight;
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

    void Prerequisites()    //Load mesh, materials...
    {
        Mesh mesh = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.material._Mesh);
        Mesh mesh2 = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.distribution.lod0.material._Mesh);
        Mesh mesh3 = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.distribution.lod1.material._Mesh);

        shadowsLOD0 = scatter.material.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        shadowsLOD1 = scatter.distribution.lod0.material.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        shadowsLOD2 = scatter.distribution.lod1.material.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        Material mat = SetupMaterial(scatter.material._Shader);
        materialLOD0 = Instantiate(mat);

        Material matlod1 = SetupMaterial(scatter.distribution.lod0.material._Shader);
        materialLOD1 = Instantiate(matlod1);

        Material matlod2 = SetupMaterial(scatter.distribution.lod1.material._Shader);
        materialLOD2 = Instantiate(matlod2);

        meshLod0 = Instantiate(mesh);
        meshLod1 = Instantiate(mesh2);
        meshLod2 = Instantiate(mesh3);

        FirstTimeArgs();
    }
    Material SetupMaterial(ScatterShader scatterShader)
    {
        Debug.Log("Scatter shader name: " + scatterShader.name);
        Shader shader = Mod.ParallaxInstance.ResourceLoader.LoadAsset<Shader>($"Assets/Scripts/Shaders/ParallaxShaders/{scatterShader.resourceName}.shader");
        Material mat = new Material(shader);

        // Setup material
        mat = scatterShader.AssignMaterialVariables(mat);

        return mat;
    }
    

    public void Initialize()
    {
        Prerequisites();
        Debug.Log("[ScatterRenderer] Initializing...");

        shader = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.renderShader);

        _MaxCount = scatter.maxObjectsToRender;     //Triangle count * pop mult

        rendererBounds = new Bounds(Vector3.zero, Vector3.one * 100000);

        lod0 = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);
        lod1 = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);
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
        argumentsLod0[1] = 1; //This is the count, but we will use copycount to fill this in to avoid reading back
        argumentsLod0[2] = (uint)meshLod0.GetIndexStart(0);
        argumentsLod0[3] = (uint)meshLod0.GetBaseVertex(0);

        uint[] argumentsLod1 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod1[0] = (uint)meshLod1.GetIndexCount(0);
        argumentsLod1[1] = 1; //This is the count, but we will use copycount to fill this in to avoid reading back
        argumentsLod1[2] = (uint)meshLod1.GetIndexStart(0);
        argumentsLod1[3] = (uint)meshLod1.GetBaseVertex(0);

        uint[] argumentsLod2 = new uint[5] { 0, 0, 0, 0, 0 };
        argumentsLod2[0] = (uint)meshLod2.GetIndexCount(0);
        argumentsLod2[1] = 1; //This is the count, but we will use copycount to fill this in to avoid reading back
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

        //For debugging command buffers:

        Graphics.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0, rendererBounds, argslod0, 0, null, shadowsLOD0, true, 0, manager.mainCamera);
        Graphics.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1, rendererBounds, argslod1, 0, null, shadowsLOD1, true, 0, manager.mainCamera);
        Graphics.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2, rendererBounds, argslod2, 0, null, shadowsLOD2, true, 0, manager.mainCamera);
    }
    void EvaluatePoints()
    {
        if (OnEvaluatePositions != null)
        {
            OnEvaluatePositions();
        }
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
