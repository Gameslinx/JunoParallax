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

    ComputeBuffer lod0cascade0; //Cascade 1
    ComputeBuffer lod0cascade1; //Cascade 2
    ComputeBuffer lod0cascade2; //Cascade 3
    ComputeBuffer lod0cascade3; //Cascade 4

    ComputeBuffer lod1cascade0; //Cascade 1
    ComputeBuffer lod1cascade1; //Cascade 2
    ComputeBuffer lod1cascade2; //Cascade 3
    ComputeBuffer lod1cascade3; //Cascade 4

    ComputeBuffer lod2cascade0; //Cascade 1
    ComputeBuffer lod2cascade1; //Cascade 2
    ComputeBuffer lod2cascade2; //Cascade 3
    ComputeBuffer lod2cascade3; //Cascade 4

    ComputeBuffer argslod0cascade0;
    ComputeBuffer argslod0cascade1;
    ComputeBuffer argslod0cascade2;
    ComputeBuffer argslod0cascade3;
    ComputeBuffer argslod0;

    ComputeBuffer argslod1cascade0;
    ComputeBuffer argslod1cascade1;
    ComputeBuffer argslod1cascade2;
    ComputeBuffer argslod1cascade3;
    ComputeBuffer argslod1;

    ComputeBuffer argslod2cascade0;
    ComputeBuffer argslod2cascade1;
    ComputeBuffer argslod2cascade2;
    ComputeBuffer argslod2cascade3;
    ComputeBuffer argslod2;

    public ComputeBuffer lod0out;
    public ComputeBuffer lod1out;
    public ComputeBuffer lod2out;

    ComputeBuffer dispatchArgsLOD0;
    ComputeBuffer dispatchArgsLOD1;
    ComputeBuffer dispatchArgsLOD2;

    ComputeBuffer maxCountLOD0;
    ComputeBuffer maxCountLOD1;
    ComputeBuffer maxCountLOD2;

    int countKernelLOD0;
    public int lod0kernel;

    int countKernelLOD1;
    public int lod1kernel;

    int countKernelLOD2;
    public int lod2kernel;

    int _MaxCount;

    public delegate void EvaluatePositions();
    public EvaluatePositions OnEvaluatePositions;

    public Mesh meshLod0;
    public Mesh meshLod1;
    public Mesh meshLod2;

    Material materialLOD0Cascade0;
    Material materialLOD0Cascade1;
    Material materialLOD0Cascade2;
    Material materialLOD0Cascade3;
    public Material materialLOD0;
    
    Material materialLOD1Cascade0;
    Material materialLOD1Cascade1;
    Material materialLOD1Cascade2;
    Material materialLOD1Cascade3;
    public Material materialLOD1;

    Material materialLOD2Cascade0;
    Material materialLOD2Cascade1;
    Material materialLOD2Cascade2;
    Material materialLOD2Cascade3;
    public Material materialLOD2;

    public Bounds rendererBounds;

    CommandBuffer dm;

    CommandBuffer renderer1;
    CommandBuffer renderer2;
    CommandBuffer renderer3;
    CommandBuffer renderer4;

    Camera renderCamera;

    public ShadowCastingMode shadowsLOD0;
    public ShadowCastingMode shadowsLOD1;
    public ShadowCastingMode shadowsLOD2;
    void OnCameraModeChanged(CameraMode newMode, CameraMode oldMode)
    {
        Debug.Log("Camera Mode Changed from " + oldMode + " to " + newMode);
        renderCamera = newMode.CameraController.CameraTransform.gameObject.GetComponent<Camera>();
        Debug.Log("a");
        if (renderCamera != null)
        {
            Debug.Log("Camera: " + renderCamera.name);
        }
        if (!(oldMode == null))
        {
            RemoveCommandBuffers(oldMode.CameraController.CameraTransform.gameObject.GetComponent<Camera>());
        }
        
        AddCommandBuffers(newMode.CameraController.CameraTransform.gameObject.GetComponent<Camera>());
    }
    void RemoveCommandBuffers(Camera camera)
    {
        if (camera != null)
        {
            camera.RemoveCommandBuffer(CameraEvent.AfterEverything, dm);
        }
    }
    void AddCommandBuffers(Camera camera)
    {
        camera.AddCommandBuffer(CameraEvent.AfterEverything, dm);
    }
    void Prerequisites()    //Load mesh, materials...
    {
        Debug.Log("Prereqs: " + scatter.DisplayName);

        Mesh mesh = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.material._Mesh);
        Mesh mesh2 = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.distribution.lod0.material._Mesh);
        Mesh mesh3 = Mod.Instance.ResourceLoader.LoadAsset<Mesh>(scatter.distribution.lod1.material._Mesh);

        shadowsLOD0 = scatter.material.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        shadowsLOD1 = scatter.distribution.lod0.material.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        shadowsLOD2 = scatter.distribution.lod1.material.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        Debug.Log("Material 1");

        Material mat = SetupMaterial(scatter.material._Shader);

        materialLOD0Cascade0 = Instantiate(mat);
        materialLOD0Cascade1 = Instantiate(mat);
        materialLOD0Cascade2 = Instantiate(mat);
        materialLOD0Cascade3 = Instantiate(mat);
        materialLOD0 = Instantiate(mat);

        Debug.Log("Material 2");

        Material matlod1 = SetupMaterial(scatter.distribution.lod0.material._Shader);

        materialLOD1Cascade0 = Instantiate(matlod1);
        materialLOD1Cascade1 = Instantiate(matlod1);
        materialLOD1Cascade2 = Instantiate(matlod1);
        materialLOD1Cascade3 = Instantiate(matlod1);
        materialLOD1 = Instantiate(matlod1);

        Debug.Log("Material 3");

        Material matlod2 = SetupMaterial(scatter.distribution.lod1.material._Shader);

        materialLOD2Cascade0 = Instantiate(matlod2);
        materialLOD2Cascade1 = Instantiate(matlod2);
        materialLOD2Cascade2 = Instantiate(matlod2);
        materialLOD2Cascade3 = Instantiate(matlod2);
        materialLOD2 = Instantiate(matlod2);

        Debug.Log("Meshes");

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
    public void RegisterEvents()
    {
        if (CameraManagerScript.Instance == null) { Debug.Log("Camera manager instance is null, not registering events"); return; }
        CameraManagerScript.Instance.CameraModeChanged += OnCameraModeChanged;
    }
    public void UnregisterEvents()
    {
        if (CameraManagerScript.Instance == null) { Debug.Log("Camera manager instance is null, not unregistering events"); return; }
        CameraManagerScript.Instance.CameraModeChanged -= OnCameraModeChanged;
    }

    public void Initialize()
    {

        RegisterEvents();
        Prerequisites();
        Debug.Log("[ScatterRenderer] Initializing...");

        shader = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.renderShader);

        countKernelLOD0 = shader.FindKernel("DetermineCountLOD0");
        countKernelLOD1 = shader.FindKernel("DetermineCountLOD1");
        countKernelLOD2 = shader.FindKernel("DetermineCountLOD2");

        lod0kernel = shader.FindKernel("EvaluateCascadesLOD0");
        lod1kernel = shader.FindKernel("EvaluateCascadesLOD1");
        lod2kernel = shader.FindKernel("EvaluateCascadesLOD2");

        _MaxCount = scatter.maxObjectsToRender;     //Triangle count * pop mult

        rendererBounds = new Bounds(Vector3.zero, Vector3.one * 100000);

        lod0cascade0 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod0cascade1 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod0cascade2 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod0cascade3 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod0 = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);

        lod1cascade0 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod1cascade1 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod1cascade2 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod1cascade3 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod1 = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);

        lod2cascade0 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod2cascade1 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod2cascade2 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod2cascade3 = new ComputeBuffer(1, TransformData.Size(), ComputeBufferType.Append);
        lod2 = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);

        lod0out = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);
        lod1out = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);
        lod2out = new ComputeBuffer(_MaxCount, TransformData.Size(), ComputeBufferType.Append);

        dispatchArgsLOD0 = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);
        dispatchArgsLOD1 = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);
        dispatchArgsLOD2 = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);

        maxCountLOD0 = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        maxCountLOD1 = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        maxCountLOD2 = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);

        shader.SetBuffer(lod0kernel, "Lod0Cascade0", lod0cascade0);
        shader.SetBuffer(lod0kernel, "Lod0Cascade1", lod0cascade1);
        shader.SetBuffer(lod0kernel, "Lod0Cascade2", lod0cascade2);
        shader.SetBuffer(lod0kernel, "Lod0Cascade3", lod0cascade3);
        shader.SetBuffer(lod0kernel, "Lod0", lod0);
        shader.SetBuffer(lod0kernel, "MaxCountLOD0", maxCountLOD0);
        shader.SetBuffer(lod0kernel, "LOD0OUT", lod0out);

        shader.SetBuffer(lod1kernel, "Lod1Cascade0", lod1cascade1);
        shader.SetBuffer(lod1kernel, "Lod1Cascade1", lod1cascade1);
        shader.SetBuffer(lod1kernel, "Lod1Cascade2", lod1cascade2);
        shader.SetBuffer(lod1kernel, "Lod1Cascade3", lod1cascade3);
        shader.SetBuffer(lod1kernel, "Lod1", lod1);
        shader.SetBuffer(lod1kernel, "MaxCountLOD1", maxCountLOD1);
        shader.SetBuffer(lod1kernel, "LOD1OUT", lod1out);

        shader.SetBuffer(lod2kernel, "Lod2Cascade0", lod2cascade0);
        shader.SetBuffer(lod2kernel, "Lod2Cascade1", lod2cascade1);
        shader.SetBuffer(lod2kernel, "Lod2Cascade2", lod2cascade2);
        shader.SetBuffer(lod2kernel, "Lod2Cascade3", lod2cascade3);
        shader.SetBuffer(lod2kernel, "Lod2", lod2);
        shader.SetBuffer(lod2kernel, "MaxCountLOD2", maxCountLOD2);
        shader.SetBuffer(lod2kernel, "LOD2OUT", lod2out);

        materialLOD0Cascade0.SetBuffer("_Properties", lod0cascade0);
        materialLOD0Cascade1.SetBuffer("_Properties", lod0cascade1);
        materialLOD0Cascade2.SetBuffer("_Properties", lod0cascade2);
        materialLOD0Cascade3.SetBuffer("_Properties", lod0cascade3);
        materialLOD0.SetBuffer("_Properties", lod0out);

        materialLOD1Cascade0.SetBuffer("_Properties", lod1cascade0);
        materialLOD1Cascade1.SetBuffer("_Properties", lod1cascade1);
        materialLOD1Cascade2.SetBuffer("_Properties", lod1cascade2);
        materialLOD1Cascade3.SetBuffer("_Properties", lod1cascade3);
        materialLOD1.SetBuffer("_Properties", lod1out);
        
        materialLOD2Cascade0.SetBuffer("_Properties", lod2cascade0);
        materialLOD2Cascade1.SetBuffer("_Properties", lod2cascade1);
        materialLOD2Cascade2.SetBuffer("_Properties", lod2cascade2);
        materialLOD2Cascade3.SetBuffer("_Properties", lod2cascade3);
        materialLOD2.SetBuffer("_Properties", lod2out);

        lod0cascade0.SetCounterValue(0);
        lod0cascade1.SetCounterValue(0);
        lod0cascade2.SetCounterValue(0);
        lod0cascade3.SetCounterValue(0);
        lod0.SetCounterValue(0);
        lod0out.SetCounterValue(0);

        lod1cascade0.SetCounterValue(0);
        lod1cascade1.SetCounterValue(0);
        lod1cascade2.SetCounterValue(0);
        lod1cascade3.SetCounterValue(0);
        lod1.SetCounterValue(0);
        lod1out.SetCounterValue(0);

        lod2cascade0.SetCounterValue(0);
        lod2cascade1.SetCounterValue(0);
        lod2cascade2.SetCounterValue(0);
        lod2cascade3.SetCounterValue(0);
        lod2.SetCounterValue(0);
        lod2out.SetCounterValue(0);

        dm = new CommandBuffer();
        //dm.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0, 0, argslod0);
        //dm.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1, 0, argslod1);
        //dm.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2, 0, argslod2);
        
        renderer1 = new CommandBuffer();
        //renderer1.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0Cascade0, 1, argslod0cascade0);
        //renderer1.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1Cascade0, 1, argslod1cascade0);
        //renderer1.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2Cascade0, 1, argslod2cascade0);
        
        renderer2 = new CommandBuffer();
        //renderer2.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0Cascade1, 1, argslod0cascade1);
        //renderer2.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1Cascade1, 1, argslod1cascade1);
        //renderer2.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2Cascade1, 1, argslod2cascade1);
        
        renderer3 = new CommandBuffer();
        //renderer3.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0Cascade2, 1, argslod0cascade2);
        //renderer3.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1Cascade2, 1, argslod1cascade2);
        //renderer3.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2Cascade2, 1, argslod2cascade2);
        
        renderer4 = new CommandBuffer();
        //renderer4.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0Cascade3, 1, argslod0cascade3);
        //renderer4.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1Cascade3, 1, argslod1cascade3);
        //renderer4.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2Cascade3, 1, argslod2cascade3);

        //if (!Game.InPlanetStudioScene)
        //{
        //    Light light = Game.Instance.FlightScene.ViewManager.GameView.SunLight;
        //    light.AddCommandBuffer(LightEvent.BeforeShadowMapPass, renderer1, ShadowMapPass.DirectionalCascade0);
        //    light.AddCommandBuffer(LightEvent.BeforeShadowMapPass, renderer2, ShadowMapPass.DirectionalCascade1);
        //    light.AddCommandBuffer(LightEvent.BeforeShadowMapPass, renderer3, ShadowMapPass.DirectionalCascade2);
        //    light.AddCommandBuffer(LightEvent.BeforeShadowMapPass, renderer4, ShadowMapPass.DirectionalCascade3);
        //}

        debugBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);

        GetMemoryUsage();
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

        argslod0cascade0 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod0cascade0.SetData(argumentsLod0);
        argslod0cascade1 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod0cascade1.SetData(argumentsLod0);
        argslod0cascade2 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod0cascade2.SetData(argumentsLod0);
        argslod0cascade3 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod0cascade3.SetData(argumentsLod0);
        argslod0 = new ComputeBuffer(1, argumentsLod0.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod0.SetData(argumentsLod0);

        argslod1cascade0 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod1cascade0.SetData(argumentsLod1);
        argslod1cascade1 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod1cascade1.SetData(argumentsLod1);
        argslod1cascade2 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod1cascade2.SetData(argumentsLod1);
        argslod1cascade3 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod1cascade3.SetData(argumentsLod1);
        argslod1 = new ComputeBuffer(1, argumentsLod1.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod1.SetData(argumentsLod1);

        argslod2cascade0 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod2cascade0.SetData(argumentsLod2);
        argslod2cascade1 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod2cascade1.SetData(argumentsLod2);
        argslod2cascade2 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod2cascade2.SetData(argumentsLod2);
        argslod2cascade3 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod2cascade3.SetData(argumentsLod2);
        argslod2 = new ComputeBuffer(1, argumentsLod2.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argslod2.SetData(argumentsLod2);
    }
    ComputeBuffer debugBuffer;
    void Update()       //Evaluate cascades, render
    {
        //Control evaluate points

        rendererBounds.center = Vector3.zero;

        lod0.SetCounterValue(0);
        lod1.SetCounterValue(0);
        lod2.SetCounterValue(0);

        lod0cascade0.SetCounterValue(0);
        lod0cascade1.SetCounterValue(0);
        lod0cascade2.SetCounterValue(0);
        lod0cascade3.SetCounterValue(0);

        lod1cascade0.SetCounterValue(0);
        lod1cascade1.SetCounterValue(0);
        lod1cascade2.SetCounterValue(0);
        lod1cascade3.SetCounterValue(0);

        lod2cascade0.SetCounterValue(0);
        lod2cascade1.SetCounterValue(0);
        lod2cascade2.SetCounterValue(0);
        lod2cascade3.SetCounterValue(0);

        EvaluatePoints();
        
        PrepareLOD0();
        PrepareLOD1();
        PrepareLOD2();

        lod0out.SetCounterValue(0);
        lod1out.SetCounterValue(0);
        lod2out.SetCounterValue(0);

        shader.DispatchIndirect(lod0kernel, dispatchArgsLOD0);
        shader.DispatchIndirect(lod1kernel, dispatchArgsLOD1);
        shader.DispatchIndirect(lod2kernel, dispatchArgsLOD2);

        //ComputeBuffer.CopyCount(lod0cascade0, argslod0cascade0, 4);
        //ComputeBuffer.CopyCount(lod0cascade1, argslod0cascade1, 4);
        //ComputeBuffer.CopyCount(lod0cascade2, argslod0cascade2, 4);
        //ComputeBuffer.CopyCount(lod0cascade3, argslod0cascade3, 4);
        ComputeBuffer.CopyCount(lod0out, argslod0, 4);

        //ComputeBuffer.CopyCount(lod1cascade0, argslod1cascade0, 4);
        //ComputeBuffer.CopyCount(lod1cascade1, argslod1cascade1, 4);
        //ComputeBuffer.CopyCount(lod1cascade2, argslod1cascade2, 4);
        //ComputeBuffer.CopyCount(lod1cascade3, argslod1cascade3, 4);
        ComputeBuffer.CopyCount(lod1out, argslod1, 4);
        
        //ComputeBuffer.CopyCount(lod2cascade0, argslod2cascade0, 4);
        //ComputeBuffer.CopyCount(lod2cascade1, argslod2cascade1, 4);
        //ComputeBuffer.CopyCount(lod2cascade2, argslod2cascade2, 4);
        //ComputeBuffer.CopyCount(lod2cascade3, argslod2cascade3, 4);
        ComputeBuffer.CopyCount(lod2out, argslod2, 4);

        materialLOD0.SetVector("_PlanetOrigin", (Vector3)manager.quadSphere.FramePosition);
        materialLOD1.SetVector("_PlanetOrigin", (Vector3)manager.quadSphere.FramePosition);
        materialLOD2.SetVector("_PlanetOrigin", (Vector3)manager.quadSphere.FramePosition);

        //For debugging command buffers:

        Graphics.DrawMeshInstancedIndirect(meshLod0, 0, materialLOD0, rendererBounds, argslod0, 0, null, shadowsLOD0, true, 0, Camera.main);
        Graphics.DrawMeshInstancedIndirect(meshLod1, 0, materialLOD1, rendererBounds, argslod1, 0, null, shadowsLOD1, true, 0, Camera.main);
        Graphics.DrawMeshInstancedIndirect(meshLod2, 0, materialLOD2, rendererBounds, argslod2, 0, null, shadowsLOD2, true, 0, Camera.main);
    }
    private void PrepareLOD0()
    {
        uint[] indirectArgsLOD0 = { 1, 1, 1 };
        dispatchArgsLOD0.SetData(indirectArgsLOD0);
        ComputeBuffer.CopyCount(lod0, dispatchArgsLOD0, 0);
        ComputeBuffer.CopyCount(lod0, maxCountLOD0, 0);
        shader.SetBuffer(countKernelLOD0, "DispatchArgsLOD0", dispatchArgsLOD0);
        shader.Dispatch(countKernelLOD0, 1, 1, 1);
    }
    private void PrepareLOD1()
    {
        uint[] indirectArgsLOD1 = { 1, 1, 1 };
        dispatchArgsLOD1.SetData(indirectArgsLOD1);
        ComputeBuffer.CopyCount(lod1, dispatchArgsLOD1, 0);
        ComputeBuffer.CopyCount(lod1, maxCountLOD1, 0);
        shader.SetBuffer(countKernelLOD1, "DispatchArgsLOD1", dispatchArgsLOD1);
        shader.Dispatch(countKernelLOD1, 1, 1, 1);
    }
    private void PrepareLOD2()
    {
        uint[] indirectArgsLOD2 = { 1, 1, 1 };
        dispatchArgsLOD2.SetData(indirectArgsLOD2);
        ComputeBuffer.CopyCount(lod2, dispatchArgsLOD2, 0);
        ComputeBuffer.CopyCount(lod2, maxCountLOD2, 0);
        shader.SetBuffer(countKernelLOD2, "DispatchArgsLOD2", dispatchArgsLOD2);
        shader.Dispatch(countKernelLOD2, 1, 1, 1);
    }
    void EvaluatePoints()
    {
        if (OnEvaluatePositions != null)
        {
            OnEvaluatePositions();
        }
    }
    public void GetMemoryUsage()
    {
        int usage = lod0cascade0.count * lod0cascade0.stride * 18;
        double usageInMB = usage * 0.000001f;
        Debug.Log("Renderer memory usage is " + usageInMB + " MB");
    }
    public void Cleanup()
    {
        UnregisterEvents();

        lod0cascade0?.Release();
        lod0cascade1?.Release();
        lod0cascade2?.Release();
        lod0cascade3?.Release();

        lod1cascade0?.Release();
        lod1cascade1?.Release();
        lod1cascade2?.Release();
        lod1cascade3?.Release();

        lod2cascade0?.Release();
        lod2cascade1?.Release();
        lod2cascade2?.Release();
        lod2cascade3?.Release();

        lod0?.Release();
        lod1?.Release();
        lod2?.Release();

        argslod0cascade0?.Release();
        argslod0cascade1?.Release();
        argslod0cascade2?.Release();
        argslod0cascade3?.Release();
        argslod0?.Release();

        argslod1cascade0?.Release();
        argslod1cascade1?.Release();
        argslod1cascade2?.Release();
        argslod1cascade3?.Release();
        argslod1?.Release();

        argslod2cascade0?.Release();
        argslod2cascade1?.Release();
        argslod2cascade2?.Release();
        argslod2cascade3?.Release();
        argslod2?.Release();

        lod0out?.Release();
        lod1out?.Release();
        lod2out?.Release();

        dispatchArgsLOD0?.Release();
        dispatchArgsLOD1?.Release();
        dispatchArgsLOD2?.Release();

        maxCountLOD0?.Release();
        maxCountLOD1?.Release();
        maxCountLOD2?.Release();

        debugBuffer?.Release();

        scatter.material._Shader.UnloadTextures();
    }
}
