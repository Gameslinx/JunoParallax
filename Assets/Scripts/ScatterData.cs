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

    public ComputeBuffer noise;
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

        noise = new ComputeBuffer(parent.vertexCount, sizeof(float), ComputeBufferType.Structured);
        positions = new ComputeBuffer(_MaxCount, PositionData.Size(), ComputeBufferType.Append);

        lod0 = renderer.lod0;           //All append to these buffers
        lod1 = renderer.lod1;
        lod2 = renderer.lod2;

        shader.SetBuffer(distributeKernel, "Vertices", parent.vertices);
        shader.SetBuffer(distributeKernel, "Triangles", parent.triangles);
        shader.SetBuffer(distributeKernel, "Normals", parent.normals);
        //shader.SetBuffer(distributeKernel, "Noise", noise);
        shader.SetBuffer(distributeKernel, "Positions", positions);

        shader.SetInt("_MaxCount", _MaxCount);
        shader.SetFloat("_Seed", 1);
        shader.SetInt("_PopulationMultiplier", scatter.distributionData._PopulationMultiplier);

        positions.SetCounterValue(0);

        //Initialize Evaluate

        Debug.Log("Initialize Evaluate");

        shader.SetBuffer(evaluateKernel, "PositionsIn", positions);
        shader.SetBuffer(evaluateKernel, "LOD0", lod0);
        shader.SetBuffer(evaluateKernel, "LOD1", lod1);
        shader.SetBuffer(evaluateKernel, "LOD2", lod2);

        shader.SetFloat("_Lod01Split", 0.15f);
        shader.SetFloat("_Lod12Split", 0.6f);
        shader.SetFloat("_MaxRange", 3000);
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);
        
        Debug.Log("[ScatterData] Initialized, generating positions");

        //GenerateNoise();
        GeneratePositions();
        ComputeDispatchArgs();
        ready = true;
    }
    private void GeneratePositions()    //Generates positions in local space
    {
        Debug.Log("Generating positions...");
        
        shader.Dispatch(distributeKernel, Mathf.CeilToInt((float)parent.triangleCount / 32f), 1, 1);
        //PositionData[] data = new PositionData[1352];
        //positions.GetData(data);
        //for (int i = 0; i < data.Length; i++)
        //{
        //    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //    go.transform.position = parent.quadToWorldMatrix.MultiplyPoint(data[i].pos);
        //    go.transform.localScale = Vector3.one * 10f;
        //    Debug.Log("Pos: " + go.transform.position.ToString("F3"));
        //}
    }
    private void ComputeDispatchArgs()  //Determine dispatch args and store them on the GPU
    {
        dispatchArgs = new ComputeBuffer(1, sizeof(uint) * 3, ComputeBufferType.IndirectArguments);
        uint[] indirectArgs = { 1, 1, 1 };
        dispatchArgs.SetData(indirectArgs);
        ComputeBuffer.CopyCount(positions, dispatchArgs, 0);
        dispatchArgs.GetData(indirectArgs);
        shader.SetBuffer(countKernel, "DispatchArgs", dispatchArgs);
        shader.Dispatch(countKernel, 1, 1, 1);
    }
    public void EvaluatePositions()     //Evaluate LODs and frustum cull
    {
        if (!ready) { return; }
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);
        shader.SetFloats("_CameraFrustumPlanes", Utils.planeNormals);
        shader.SetVector("_WorldSpaceCameraPosition", Game.Instance.FlightScene.ViewManager.GameView.GameCamera.NearCamera.transform.position);
        shader.SetVector("_PlanetNormal", parent.planetNormal);

        shader.DispatchIndirect(evaluateKernel, dispatchArgs);
    }
    void GenerateNoise()
    {
        float[] distributionNoise = new float[parent.vertexCount];
        Vector3d normal;
        for (int i = 0; i < parent.vertexCount; i++)
        {
            normal = Vector3d.Normalize(parent.vertexData[i] + parent.quad.RenderingData.LocalPosition - parent.quad.QuadSphere.PlanetPosition);
            normal = normal * parent.quad.QuadSphere.PlanetData.Radius;    //Not actually the normal any more - Instead it's a position at a fixed radius from planet center
            distributionNoise[i] = OpenSimplex2.Noise3_Fallback(1, normal.x / 400d, normal.y / 400d, normal.z / 400d) * 0.5f + 0.5f;
        }
        noise.SetData(distributionNoise);
        Debug.Log("Noise gen complete");
    }
    public void Cleanup()
    {
        renderer.OnEvaluatePositions -= EvaluatePositions;
        positions?.Release();
        noise?.Release();
        dispatchArgs?.Release();
    }
}
