using Assets.Scripts;
using Assets.Scripts.Terrain;
using ModApi.Flight.Sim;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Rewired.ComponentControls.Effects.RotateAroundAxis;

public class ScatterData
{
    public QuadData parent;
    public Scatter scatter;
    public ScatterRenderer renderer;

    ComputeShader shader;

    public ComputeBuffer noise;
    public ComputeBuffer positions;

    public ComputeBuffer lod0;      //Reference the renderer buffers
    public ComputeBuffer lod1;
    public ComputeBuffer lod2;

    ComputeBuffer dispatchArgs;
    ComputeBuffer objectLimits;

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
        renderer.OnEvaluatePositions += EvaluatePositions;
        shader = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.quadShader);       //Load the shader for this quad

        distributeKernel = shader.FindKernel("Distribute");
        countKernel = shader.FindKernel("DetermineCount");
        evaluateKernel = shader.FindKernel("Evaluate");

        noise = new ComputeBuffer(parent.vertexCount, sizeof(float), ComputeBufferType.Structured);
        positions = new ComputeBuffer(_MaxCount, PositionData.Size(), ComputeBufferType.Append);

        lod0 = renderer.lod0;           //All append to these buffers
        lod1 = renderer.lod1;
        lod2 = renderer.lod2;
        noise.SetData(scatter.noise[parent.quad].noise);

        shader.SetBuffer(distributeKernel, "Vertices", parent.vertices);
        shader.SetBuffer(distributeKernel, "Triangles", parent.triangles);
        shader.SetBuffer(distributeKernel, "Normals", parent.normals);
        shader.SetBuffer(distributeKernel, "Noise", noise);
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

        shader.SetFloat("_Lod01Split", 0.15f);
        shader.SetFloat("_Lod12Split", 0.6f);
        shader.SetFloat("_MaxRange", 3000);
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);

        GeneratePositions();
        ComputeDispatchArgs();
        ready = true;
    }
    private void GeneratePositions()    //Generates positions in local space
    {
        shader.Dispatch(distributeKernel, Mathf.CeilToInt((float)parent.triangleCount / 32f), 1, 1);
        
    }
    private void ComputeDispatchArgs()  //Determine dispatch args and store them on the GPU
    {
        dispatchArgs = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);
        objectLimits = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);     //IndirectArgs must be size 3 at least
        uint[] indirectArgs = { 1, 1, 1 };
        dispatchArgs.SetData(indirectArgs);
        objectLimits.SetData(indirectArgs);
        ComputeBuffer.CopyCount(positions, dispatchArgs, 0);    //This count is used for dispatchIndirect
        ComputeBuffer.CopyCount(positions, objectLimits, 0);    //This count is unmodified and used in EvaluatePositions to early return
        shader.SetBuffer(countKernel, "DispatchArgs", dispatchArgs);
        shader.SetBuffer(evaluateKernel, "ObjectLimits", objectLimits);     //We need to early return out from evaluation if the thread exceeds the number of objects - prevents funny floaters
        shader.Dispatch(countKernel, 1, 1, 1);
    }
    public void EvaluatePositions()     //Evaluate LODs and frustum cull
    {
        if (!ready) { return; }
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);
        shader.SetFloats("_CameraFrustumPlanes", Utils.planeNormals);
        shader.SetVector("_WorldSpaceCameraPosition", Camera.main.transform.position);
        shader.SetVector("_PlanetNormal", parent.planetNormal);
        shader.DispatchIndirect(evaluateKernel, dispatchArgs);
    }
    public void Cleanup()
    {
        renderer.OnEvaluatePositions -= EvaluatePositions;
        positions?.Release();
        noise?.Release();
        dispatchArgs?.Release();
        objectLimits?.Release();
    }
}
