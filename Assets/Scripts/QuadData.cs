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
    public Matrix4x4 ToMatrix4x4()
    {
        return Matrix4x4.TRS(pos, Quaternion.Euler(0, rot, 0), scale);
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
public class RawColliderData
{
    public QuadScript quad;
    public PositionData[] data;
    public RawColliderData(QuadScript quad, PositionData[] data)
    {
        this.quad = quad;
        this.data = data;
    }
}
public class ColliderData
{
    public QuadScript quad;
    public List<Matrix4x4> data;
    public ColliderData(QuadScript quad, List<Matrix4x4> data)
    {
        this.quad = quad;
        this.data = data;
    }
}
public class QuadData       //Holds the data for the quad - Verts, normals, triangles. Holds scatter data, too, but quad data is global and used for all scatters
{
    public QuadScript quad;
    public bool isVisible = false;
    public float quadDiagLength = 0;
    public float sqrHalfQuadDiagLength = 0;
    public float sqrQuadCameraDistance = 0;

    public Vector3[] vertexData;
    public int[] triangleData;
    public Vector3[] normalData;

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

    // Vertices are more dense at latitudes +-35.26, longitudes -135, -45, 45 and 135 by roughly a factor of 2.
    public float densityFactor = 1;

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
        manager.quadSphere = quad.QuadSphere;
        eventsRegistered = true;
    }
    public void Initialize()        //Initialize buffers, then scatters
    {
        Profiler.BeginSample("Initialize QuadData");
        quadDiagLength = GetQuadDiagLength();
        sqrHalfQuadDiagLength = (quadDiagLength / 2.0f) * (quadDiagLength / 2.0f);
        bounds.size = Vector3.one * quadDiagLength * 1.11f;

        vertexData = quad.RenderingData.TerrainMesh.vertices;
        triangleData = quad.RenderingData.TerrainMesh.triangles;
        normalData = quad.RenderingData.TerrainMesh.normals;

        vertexCount = vertexData.Length;
        triangleCount = triangleData.Length / 3;

        vertices = new ComputeBuffer(vertexCount, 12, ComputeBufferType.Structured);
        triangles = new ComputeBuffer(triangleCount * 3, 4, ComputeBufferType.Structured);
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

        GetDensityFactor(quad.SphereNormal);

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
    float lat = 0;
    float lon = 0;
    public void GetDensityFactor(Vector3d sphereNormal)
    {
        Utils.GetLatLon(sphereNormal, out lat, out lon);
        Debug.Log("Quad lat " + (lat * Mathf.Rad2Deg) + ", lon " + (lon * Mathf.Rad2Deg));

        // We want a value of 1 when at each corner - defines reduction factor
        // Sin function, peaks at -35.266, 35.266, but also -105 and +105. Leading to reduced density at the poles - this is fine!
        lat = Mathf.Abs(Mathf.Sin(lat * 2.552f));
        // Sin function, peaks at -135, -45, 45, 135
        lon = Mathf.Abs(Mathf.Sin(lon * 2));

        // Multiply spawn chance by density factor. At corners, density is 0.4x normal. At edges, density is 0.8x normal.
        densityFactor = Mathf.Pow((Mathf.Lerp(1, 0.03f, (lat + lon) / 2f)), 0.3333f);

        Debug.Log(" - Density: " + densityFactor);
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
    float distributionSample = 0;
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
        // If all 4 corners of the quad do not have the necessary distribution, it's very very unlikely to be in a biome where this scatter can spawn
        distributionSample = Mathf.Max(scatter.sharesNoiseWith.noise[quad].distribution[28], scatter.sharesNoiseWith.noise[quad].distribution[52], scatter.sharesNoiseWith.noise[quad].distribution[628], scatter.sharesNoiseWith.noise[quad].distribution[603]);
        if (distributionSample < 0.5f)
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
