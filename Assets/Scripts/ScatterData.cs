using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class ScatterData
{
    public QuadData parent;
    public Scatter scatter;
    public ScatterRenderer renderer;

    ComputeShader shader;

    public ComputeBuffer noise;
    public ComputeBuffer distribution;
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

    PositionData[] rawColliderData;
    RawColliderData colliderData;

    public ScatterData(QuadData parent, Scatter scatter, ScatterRenderer renderer)
    {
        this.parent = parent;
        this.scatter = scatter;
        this.renderer = renderer;
        _MaxCount = parent.triangleCount * scatter.distribution._PopulationMultiplier;
    }
    public void Start()
    {
        InitializeShader();
        if (!scatter.inherits)
        {
            InitializeDistribute();
        }
        else
        {
            InitializeInheritance();
        }
        InitializeEvaluate();
        if (!scatter.inherits)
        {
            GeneratePositions();
        }
        ComputeDispatchArgs();
        ready = true;
    }
    public void InitializeShader()
    {
        renderer.OnEvaluatePositions += EvaluatePositions;
        //shader = UnityEngine.Object.Instantiate(Mod.ParallaxInstance.quadShader);       //Load the shader for this quad
        shader = ShaderPool.Retrieve();
        distributeKernel = shader.FindKernel("Distribute");
        countKernel = shader.FindKernel("DetermineCount");
        evaluateKernel = shader.FindKernel("Evaluate");
    }
    int populationFactor = 0;
    public void InitializeDistribute()
    {
        //Initialize Generation - Skipped if this scatter inherits from another
        populationFactor = (int)Mathf.Pow(2, parent.quad.QuadSphere.MaxSubdivisionLevel - parent.quad.SubdivisionLevel);
        _MaxCount *= Mathf.CeilToInt((float)populationFactor * ParallaxSettings.densityMultiplier);

        distribution = new ComputeBuffer(parent.vertexCount, 4, ComputeBufferType.Structured);
        noise = new ComputeBuffer(parent.vertexCount, 4, ComputeBufferType.Structured);
        positions = new ComputeBuffer(_MaxCount, 32, ComputeBufferType.Append);

        distribution.SetData(scatter.sharesNoiseWith.noise[parent.quad].distribution);
        noise.SetData(scatter.sharesNoiseWith.noise[parent.quad].noise);    // If the scatter inherits noise from another scatter, this is the parent scatter noise and not noise generated for this scatter

        shader.SetBuffer(distributeKernel, "Vertices", parent.vertices);
        shader.SetBuffer(distributeKernel, "Triangles", parent.triangles);
        shader.SetBuffer(distributeKernel, "Normals", parent.normals);
        shader.SetBuffer(distributeKernel, "Distribution", distribution);
        shader.SetBuffer(distributeKernel, "Noise", noise);
        shader.SetBuffer(distributeKernel, "Positions", positions);

        // Quads subdivide into 4 quads, so in order for a lesser subdivided quad to have the same number of trees (and maintain a constant scatter density), increase its pop mult by 4
        
        shader.SetInt("_MaxCount", _MaxCount);
        shader.SetFloat("_Seed", scatter.distribution._Seed);
        shader.SetInt("_PopulationMultiplier", (int)Mathf.Min(scatter.distribution._PopulationMultiplier, 200));
        shader.SetFloat("_SpawnChance", scatter.distribution._SpawnChance * parent.densityFactor);
        shader.SetVector("_MinScale", scatter.distribution._MinScale);
        shader.SetVector("_MaxScale", scatter.distribution._MaxScale);
        shader.SetFloat("_SizeJitterAmount", scatter.distribution._SizeJitterAmount);
        shader.SetFloat("_Coverage", scatter.distribution._Coverage);
        shader.SetFloat("_MinAltitude", scatter.distribution._MinAltitude);
        shader.SetFloat("_MaxAltitude", scatter.distribution._MaxAltitude);
        shader.SetFloat("_MaxNormalDeviance", scatter.distribution._MaxNormalDeviance);
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);
        shader.SetFloat("_PlanetRadius", (float)parent.quad.QuadSphere.PlanetData.Radius);
        shader.SetVector("_PlanetOrigin", (Vector3)parent.quad.QuadSphere.FramePosition);
        shader.SetInt("_AbsoluteNoise", scatter.distribution._RidgedNoise == true ? 1 : 0);
        shader.SetFloat("_BiomeCutoff", scatter.distribution._BiomeOverride);
        shader.SetFloat("_PlanetEdgeVertexCount", (Mod.ParallaxInstance.activePlanetVertexCount + 1) * 2);

        shader.SetInt("_NumTris", parent.triangleCount);

        positions.SetCounterValue(0);
    }
    public void InitializeInheritance()
    {
        positions = parent.data.Find(x => x.scatter.DisplayName == scatter.inheritsFrom).positions;
    }
    public void InitializeEvaluate()
    {
        //Initialize Evaluate

        lod0 = renderer.lod0;           //All append to these buffers
        lod1 = renderer.lod1;
        lod2 = renderer.lod2;

        shader.SetBuffer(evaluateKernel, "PositionsIn", positions);
        shader.SetBuffer(evaluateKernel, "Triangles", parent.triangles);
        shader.SetBuffer(evaluateKernel, "Vertices", parent.vertices);
        shader.SetBuffer(evaluateKernel, "LOD0", lod0);
        shader.SetBuffer(evaluateKernel, "LOD1", lod1);
        shader.SetBuffer(evaluateKernel, "LOD2", lod2);

        shader.SetFloat("_Lod01Split", scatter.distribution.lod0.distance / scatter.distribution._Range);
        shader.SetFloat("_Lod12Split", scatter.distribution.lod1.distance / scatter.distribution._Range);
        shader.SetFloat("_MaxRange", scatter.distribution._Range);
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);
        shader.SetFloat("_AlignToTerrainNormal", scatter.distribution._AlignToTerrainNormal);

        shader.SetFloat("_CullRadius", scatter.cullRadius);
        shader.SetFloat("_CullLimit", scatter.cullLimit);
    }
    private void GeneratePositions()    //Generates positions in local space
    {
        shader.Dispatch(distributeKernel, Mathf.CeilToInt((float)parent.triangleCount / 32f), 1, 1);
        
    }
    private void ComputeDispatchArgs()  //Determine dispatch args and store them on the GPU
    {
        dispatchArgs = new ComputeBuffer(1, 12, ComputeBufferType.IndirectArguments);
        objectLimits = new ComputeBuffer(1, 12, ComputeBufferType.IndirectArguments);     //IndirectArgs must be size 3 at least
        uint[] indirectArgs = { 1, 1, 1 };
        dispatchArgs.SetData(indirectArgs);
        objectLimits.SetData(indirectArgs);
        ComputeBuffer.CopyCount(positions, dispatchArgs, 0);    //This count is used for dispatchIndirect
        ComputeBuffer.CopyCount(positions, objectLimits, 0);    //This count is unmodified and used in EvaluatePositions to early return

        shader.SetBuffer(countKernel, "DispatchArgs", dispatchArgs);
        shader.SetBuffer(evaluateKernel, "ObjectLimits", objectLimits);     //We need to early return out from evaluation if the thread exceeds the number of objects - prevents funny floaters
        shader.Dispatch(countKernel, 1, 1, 1);

        if (ParallaxSettings.enableColliders && scatter.collisionLevel >= ParallaxSettings.collisionSizeThreshold && parent.quad.SubdivisionLevel == parent.quad.QuadSphere.MaxSubdivisionLevel)
        {
            // Process colliders
            AsyncGPUReadback.Request(objectLimits, GetCount);
        }
    }
    int[] count = new int[] { 0, 0, 0 };
    public void GetCount(AsyncGPUReadbackRequest req)
    {
        count = req.GetData<int>().ToArray(); //Creates garbage, unfortunate
        
        rawColliderData = new PositionData[count[0]];
        positions.GetData(rawColliderData);
        colliderData = new RawColliderData(parent.quad, rawColliderData);
        scatter.AddColliderData(colliderData);
    }
    public void EvaluatePositions()     //Evaluate LODs and frustum cull
    {
        // Don't evaluate quads uninitialized, invisible or out of range
        if (!ready) { return; }
        if (!parent.isVisible) { return; }
        if (parent.sqrQuadCameraDistance > scatter.sqrRange + (parent.quadDiagLength * 1.5f * parent.quadDiagLength)) { return; }
        shader.SetMatrix("_ObjectToWorldMatrix", parent.quadToWorldMatrix);
        shader.SetFloats("_CameraFrustumPlanes", Utils.planeNormals);
        shader.SetVector("_WorldSpaceCameraPosition", parent.manager.mainCamera.transform.position);
        shader.SetVector("_PlanetNormal", parent.planetNormal);

        if (ParallaxSettings.enableDynamicLOD)
        {
            shader.SetFloat("_Lod01Split", (scatter.distribution.lod0.distance / scatter.distribution._Range) * FramerateMonitor.factor);
            shader.SetFloat("_Lod12Split", (scatter.distribution.lod1.distance / scatter.distribution._Range) * FramerateMonitor.factor);
        }
       
        shader.DispatchIndirect(evaluateKernel, dispatchArgs);
    }
    public void Pause()
    {
        renderer.OnEvaluatePositions -= EvaluatePositions;
    }
    public void Resume()
    {
        renderer.OnEvaluatePositions += EvaluatePositions;
    }
    public void GUITool_Reinitialize()
    {
        positions?.Release();
        distribution?.Release();
        noise?.Release();
        dispatchArgs?.Release();
        objectLimits?.Release();

        scatter.RemoveColliderData(colliderData);

        // If population multiplier changes, it must be updated here
        _MaxCount = parent.triangleCount * scatter.distribution._PopulationMultiplier;

        if (!scatter.inherits)
        {
            InitializeDistribute();
        }
        else
        {
            InitializeInheritance();
        }
        InitializeEvaluate();
        if (!scatter.inherits)
        {
            GeneratePositions();
        }
        ComputeDispatchArgs();
    }
    public void Cleanup()
    {
        renderer.OnEvaluatePositions -= EvaluatePositions;

        positions?.Release();
        distribution?.Release();
        noise?.Release();
        dispatchArgs?.Release();
        objectLimits?.Release();

        scatter.RemoveColliderData(colliderData);

        ShaderPool.Return(shader);
    }
}
