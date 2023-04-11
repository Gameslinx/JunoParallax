using Assets.Scripts;
using Assets.Scripts.Terrain;
using ModApi.Flight.Sim;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Rewired.ComponentControls.Effects.RotateAroundAxis;

public class ScatterData
{
    public QuadData parent;
    public Scatter scatter;
    public ScatterRenderer renderer;

    ComputeShader shader;

    public ComputeBuffer positions;

    public ComputeBuffer lod0;      //Reference the renderer buffers
    public ComputeBuffer lod1;
    public ComputeBuffer lod2;

    ComputeBuffer dispatchArgs;

    public int _MaxCount;

    int distributeKernel;
    int countKernel;
    int evaluateKernel;

    bool ready = false;

    public ScatterData(QuadData parent, Scatter scatter, ScatterRenderer renderer)
    {
        this.parent = parent;
        this.scatter = scatter;
        this.renderer = renderer;
        _MaxCount = parent.triangleCount * scatter.distributionData._PopulationMultiplier;
        Initialize();
    }
    public void Initialize()
    {
        //The renderer must be initialized before this
        //Initialize Generate
        Debug.Log("[ScatterData] Initializing...");
        renderer.OnEvaluatePositions += EvaluatePositions;
        shader = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.quadShader);       //Load the shader for this quad

        distributeKernel = shader.FindKernel("Distribute");
        countKernel = shader.FindKernel("DetermineCount");
        evaluateKernel = shader.FindKernel("Evaluate");

        positions = new ComputeBuffer(_MaxCount, PositionData.Size(), ComputeBufferType.Append);

        lod0 = renderer.lod0;           //All append to these buffers
        lod1 = renderer.lod1;
        lod2 = renderer.lod2;

        shader.SetBuffer(distributeKernel, "Vertices", parent.vertices);
        shader.SetBuffer(distributeKernel, "Triangles", parent.triangles);
        shader.SetBuffer(distributeKernel, "Normals", parent.normals);
        shader.SetBuffer(distributeKernel, "Positions", positions);

        shader.SetInt("_MaxCount", _MaxCount);
        shader.SetFloat("_Seed", 1);
        shader.SetInt("_PopulationMultiplier", scatter.distributionData._PopulationMultiplier);

        positions.SetCounterValue(0);

        //Initialize Evaluate

        shader.SetBuffer(evaluateKernel, "PositionsIn", positions);
        shader.SetBuffer(evaluateKernel, "LOD0", lod0);
        shader.SetBuffer(evaluateKernel, "LOD1", lod1);
        shader.SetBuffer(evaluateKernel, "LOD2", lod2);

        shader.SetFloat("_Lod01Split", 0.3f);
        shader.SetFloat("_Lod12Split", 0.6f);
        shader.SetFloat("_MaxRange", 100);

        Debug.Log("[ScatterData] Initialized, generating positions");

        GeneratePositions();
        ComputeDispatchArgs();
        ready = true;
    }
    private void GeneratePositions()    //Generates positions in local space
    {
        Debug.Log("Generating positions...");
        shader.Dispatch(distributeKernel, Mathf.CeilToInt((float)parent.triangleCount / 32f), 1, 1);
    }
    private void ComputeDispatchArgs()  //Determine dispatch args and store them on the GPU
    {
        dispatchArgs = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);
        uint[] indirectArgs = { 1, 1, 1 };
        dispatchArgs.SetData(indirectArgs);
        ComputeBuffer.CopyCount(positions, dispatchArgs, 0);
        shader.SetBuffer(countKernel, "DispatchArgs", dispatchArgs);
        shader.Dispatch(countKernel, 1, 1, 1);
    }
    public void EvaluatePositions()     //Evaluate LODs and frustum cull
    {
        if (!ready) { return; }
        shader.SetMatrix("_ObjectToWorld", parent.quadToWorldMatrix);
        shader.SetFloats("_CameraFrustumPlanes", Utils.planeNormals);
        shader.SetVector("_WorldSpaceCameraPosition", Camera.main.transform.position);

        shader.DispatchIndirect(evaluateKernel, dispatchArgs);
    }
    public void Cleanup()
    {
        renderer.OnEvaluatePositions -= EvaluatePositions;
        positions?.Release();
        dispatchArgs?.Release();
    }
}
