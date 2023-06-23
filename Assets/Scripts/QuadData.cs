using Assets.Scripts;
using Assets.Scripts.Terrain;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public struct PositionData
{
    public Vector3 pos;
    public Vector3 scale;
    public float rot;
    public static int Size()
    {
        return sizeof(float) * 7;
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

    Guid planetID;
    bool eventsRegistered = false;

    public QuadData(QuadScript quad)
    {
        this.quad = quad;

        //RegisterEvents();
        //Initialize();
    }
    public void RegisterEvents()
    {
        Mod.ParallaxInstance.scatterManagers[quad.QuadSphere.PlanetData.Id].OnQuadUpdate += OnQuadDataUpdate;
        eventsRegistered = true;
    }
    public void Initialize()        //Initialize buffers, then scatters
    {
        Profiler.BeginSample("Initialize QuadData");
        planetID = quad.QuadSphere.PlanetData.Id;
        quadDiagLength = GetQuadDiagLength();
        sqrHalfQuadDiagLength = (quadDiagLength / 2.0f) * (quadDiagLength / 2.0f);
        bounds.size = Vector3.one * quadDiagLength;

        vertexData = quad.RenderingData.TerrainMesh.vertices;
        triangleData = quad.RenderingData.TerrainMesh.triangles;
        normalData = quad.RenderingData.TerrainMesh.normals;

        vertexCount = vertexData.Length;
        triangleCount = triangleData.Length / 3;

        vertices = new ComputeBuffer(vertexCount, sizeof(float) * 3, ComputeBufferType.Structured);
        triangles = new ComputeBuffer(triangleCount * 3, sizeof(int), ComputeBufferType.Structured);
        normals = new ComputeBuffer(vertexCount, sizeof(float) * 3, ComputeBufferType.Structured);
        
        vertices.SetData(vertexData);
        triangles.SetData(triangleData);
        normals.SetData(normalData);

        planetNormal = quad.SphereNormal.ToVector3();

        // Request a planet matrix to construct quad to world matrix for determining world space positions in distribution shader (for min/max altitude constraints)
        OnQuadDataUpdate(Mod.ParallaxInstance.scatterManagers[quad.QuadSphere.PlanetData.Id].RequestPlanetMatrixNow());

        for (int i = 0; i < Mod.Instance.activeScatters.Length; i++)
        {
            Scatter scatter = Mod.Instance.activeScatters[i];
            data.Add(new ScatterData(this, scatter, Mod.ParallaxInstance.scatterRenderers[scatter]));
        }

        Profiler.EndSample();
    }
    public float GetQuadDiagLength()
    {
        // Planet starts out as a cube sphere, radius r, where the circumference is divided up into 4 (4 sides of cube ignoring top and bottom)
        // So quad horizontal width is a function of maxLevel and planet radius
        float circumference = Mathf.PI * (float)quad.QuadSphere.PlanetData.Radius * 2.0f;
        float initialQuadWidth = circumference / 4.0f;
        float finalQuadWidth = initialQuadWidth / Mathf.Pow(2.0f, (float)quad.QuadSphere.MaxSubdivisionLevel);
        // Cheeky pythagoras
        finalQuadWidth = Mathf.Sqrt(finalQuadWidth * finalQuadWidth + finalQuadWidth * finalQuadWidth);
        return finalQuadWidth;
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
        var qpos = quad.RenderingData.LocalPosition;
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
        sqrQuadCameraDistance = (worldSpacePosition - Camera.main.transform.position).sqrMagnitude;
    }
    public void Cleanup()           //Clean up scatter data, then the quad data
    {
        foreach (ScatterData scatterData in data)
        {
            scatterData.Cleanup();
        }
        vertices?.Release();
        normals?.Release();
        triangles?.Release();

        if (eventsRegistered)
        {
            Mod.ParallaxInstance.scatterManagers[planetID].OnQuadUpdate -= OnQuadDataUpdate;
            eventsRegistered = false;
        }
    }
}
