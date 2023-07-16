using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.Terrain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

public struct PositionData
{
    public Vector3 pos;
    public Vector3 scale;
    public float rot;
    public uint index;
    public static int Size()
    {
        return sizeof(float) * 8;
    }
};
public struct TransformData
{
    public Matrix4x4 mat;
    public static int Size()
    {
        return sizeof(float) * 16;
    }
};
public class QuadData       //Holds the data for the quad - Verts, normals, triangles. Holds scatter data, too, but quad data is global and used for all scatters
{
    public QuadScript quad;
    public bool isVisible = false;
    public float quadDiagLength = 0;
    public float sqrHalfQuadDiagLength = 0;
    public float sqrQuadCameraDistance = 0;

    public Vector3[] vertexData;
    private int[] triangleData;
    private Vector3[] normalData;

    public ComputeBuffer vertices;
    public ComputeBuffer normals;
    public ComputeBuffer triangles;
    
    public int vertexCount;
    public int triangleCount;       //This is actually tricount / 3

    public List<ScatterData> data = new List<ScatterData>();        //Change to array

    public Matrix4x4 quadToWorldMatrix;
    public Vector3 planetNormal;

    public float quadHighestPoint = -1000000;
    public float quadLowestPoint = 1000000;

    public bool cleaned = true;

    Guid planetID;
    bool eventsRegistered = false;

    public ScatterManager manager;

    public QuadData(QuadScript quad)
    {
        this.quad = quad;

        //RegisterEvents();
        //Initialize();
    }
    public void RegisterEvents()
    {
        planetID = quad.QuadSphere.PlanetData.Id;
        manager = Mod.ParallaxInstance.scatterManagers[planetID];
        manager.OnQuadUpdate += OnQuadDataUpdate;
        eventsRegistered = true;
    }
    public void Initialize()        //Initialize buffers, then scatters
    {
        Profiler.BeginSample("Initialize QuadData");
        quadDiagLength = GetQuadDiagLength();
        sqrHalfQuadDiagLength = (quadDiagLength / 2.0f) * (quadDiagLength / 2.0f);
        bounds.size = Vector3.one * quadDiagLength;

        vertexData = quad.RenderingData.TerrainMesh.vertices;
        triangleData = quad.RenderingData.TerrainMesh.triangles;
        normalData = quad.RenderingData.TerrainMesh.normals;

        vertexCount = vertexData.Length;
        triangleCount = triangleData.Length / 3;

        vertices = new ComputeBuffer(vertexCount, 12, ComputeBufferType.Structured);
        triangles = new ComputeBuffer(triangleCount, 12, ComputeBufferType.Structured);
        normals = new ComputeBuffer(vertexCount, 12, ComputeBufferType.Structured);
        
        vertices.SetData(vertexData);
        triangles.SetData(triangleData);
        normals.SetData(normalData);

        planetNormal = (Vector3)quad.SphereNormal;

        // Request a planet matrix to construct quad to world matrix for determining world space positions in distribution shader (for min/max altitude constraints)
        OnQuadDataUpdate(manager.RequestPlanetMatrixNow());

        Profiler.BeginSample("Quad Altitude Range");
        GetQuadAltitudeRange();
        Profiler.EndSample();

        for (int i = 0; i < Mod.Instance.activeScatters.Length; i++)
        {
            Scatter scatter = Mod.Instance.activeScatters[i];
            // Does the scatter lie in the given altitude range?
            if (ScatterEligible(scatter))
            {
                ScatterData sd = new ScatterData(this, scatter, Mod.ParallaxInstance.scatterRenderers[scatter]);
                sd.Start();
                data.Add(sd);
            }
        }

        cleaned = false;

        Profiler.EndSample();
    }
    public float GetQuadDiagLength()
    {
        // Planet starts out as a cube sphere, radius r, where the circumference is divided up into 4 (4 sides of cube ignoring top and bottom)
        // So quad horizontal width is a function of maxLevel and planet radius
        //float circumference = Mathf.PI * (float)quad.QuadSphere.PlanetData.Radius * 2.0f;
        //float initialQuadWidth = circumference / 4.0f;
        //float finalQuadWidth = initialQuadWidth / Mathf.Pow(2.0f, (float)quad.QuadSphere.MaxSubdivisionLevel);
        // Cheeky pythagoras
        //finalQuadWidth = Mathf.Sqrt(finalQuadWidth * finalQuadWidth + finalQuadWidth * finalQuadWidth);

        float fqw = ((Mathf.PI * (float)quad.QuadSphere.PlanetData.Radius * 2.0f) / 4f) / (Mathf.Pow(2.0f, quad.QuadSphere.MaxSubdivisionLevel));

        return Mathf.Sqrt(fqw * fqw + fqw * fqw);
    }
    void OnQuadDataUpdate(Matrix4x4d m)         //Occurs every time before EvaluatePositions is called on ScatterData
    {
        GetQuadToWorldMatrix(m);
        UpdateVisibility();
        GetCameraDistance();
    }
    private void GetQuadToWorldMatrix(Matrix4x4d m)
    {
        if (quad.RenderingData == null) { Debug.Log("Quad rendering data was null"); return; }
        Matrix4x4 mQuad = m.ToMatrix4x4();
        Vector3d qpos = quad.RenderingData.LocalPosition;
        mQuad.m03 = (float)((m.m00 * qpos.x) + (m.m01 * qpos.y) + (m.m02 * qpos.z) + m.m03);
        mQuad.m13 = (float)((m.m10 * qpos.x) + (m.m11 * qpos.y) + (m.m12 * qpos.z) + m.m13);
        mQuad.m23 = (float)((m.m20 * qpos.x) + (m.m21 * qpos.y) + (m.m22 * qpos.z) + m.m23);
        quadToWorldMatrix = mQuad;
    }
    Vector3 worldSpacePosition = Vector3.zero;
    Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);     //The bound extents need to be the size of the quad
    private void UpdateVisibility()
    {
        worldSpacePosition.x = quadToWorldMatrix.m03;
        worldSpacePosition.y = quadToWorldMatrix.m13;
        worldSpacePosition.z = quadToWorldMatrix.m23;
        bounds.center = worldSpacePosition;
        isVisible = GeometryUtility.TestPlanesAABB(Utils.planes, bounds);
    }
    private void GetCameraDistance()
    {
        sqrQuadCameraDistance = (worldSpacePosition - manager.mainCamera.transform.position).sqrMagnitude;
    }
    private void GetQuadAltitudeRange()
    {
        Vector3 worldSpaceVertex = Vector3.zero;
        float altitude = 0;
        Vector3 worldSpacePlanetPos = (Vector3)quad.QuadSphere.FramePosition;
        float planetRadius = (float)quad.QuadSphere.PlanetData.Radius;
        // Iterating over all vertices is expensive, so approximate the altitude guess by iterating every 17 verts
        for (int i = 0; i < vertexCount; i += 17)
        {
            worldSpaceVertex = quadToWorldMatrix.MultiplyPoint(vertexData[i]);
            altitude = Vector3.Distance(worldSpaceVertex, worldSpacePlanetPos) - planetRadius;
            if (altitude > quadHighestPoint) { quadHighestPoint = altitude; }
            if (altitude < quadLowestPoint) {  quadLowestPoint = altitude; }
        }
    }
    // Min altitude or max altitude must be between quad min/max altitude, otherwise don't bother generating anything (quad is outside of scatter altitude bounds)
    private bool ScatterEligible(Scatter scatter)
    {
        if (quad.SubdivisionLevel < scatter.minimumSubdivision)
        {
            // Scatter can never appear on this subdivision level
            return false;
        }
        if (scatter.distribution._MaxAltitude < quadLowestPoint || scatter.distribution._MinAltitude > quadHighestPoint)
        {
            return false;
        }
        //return true;
        // Pick 3 points on a quad to sample the noise. Not hugely accurate but good enough
        float noiseSample = scatter.noise[quad].noise[0] + scatter.noise[quad].noise[28] + scatter.noise[quad].noise[700];
        // Absolutely no noise on this quad
        if (noiseSample == 0)
        {
            return false;
        }
        return true;
    }
    // Set this quad as inactive because it has been subdivided
    public void Pause()
    {
        for (int i = 0; i < data.Count; i++)
        {
            data[i].Pause();
        }
    }
    // Set this parent as active because its children have just collapsed (what a sentence)
    public void Resume()
    {
        for (int i = 0; i < data.Count; i++)
        {
            data[i].Resume();
        }
    }
    public void Cleanup()           // Clean up scatter data, then the quad data
    {
        if (cleaned) { return; }
        for (int i = 0; i < data.Count; i++)
        {
            data[i].Cleanup();
        }
        data.Clear();
        vertices?.Release();
        normals?.Release();
        triangles?.Release();

        if (eventsRegistered)
        {
            manager.OnQuadUpdate -= OnQuadDataUpdate;
            eventsRegistered = false;
        }
        cleaned = true;
    }
}
