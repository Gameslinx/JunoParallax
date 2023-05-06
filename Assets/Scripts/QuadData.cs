using Assets.Scripts;
using Assets.Scripts.Terrain;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public Vector3[] vertexData;
    private int[] triangleData;
    private Vector3[] normalData;

    public ComputeBuffer vertices;
    public ComputeBuffer normals;
    public ComputeBuffer triangles;
    
    public int vertexCount;
    public int triangleCount;       //This is actually tricount / 3

    public List<ScatterData> data = new List<ScatterData>();        //Change to array
    public Dictionary<Scatter, ScatterNoise> scatterNoises = new Dictionary<Scatter, ScatterNoise>();

    public Matrix4x4 quadToWorldMatrix;
    public Vector3 planetNormal;

    public ScatterManager manager;

    int colorCount = 0;

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
        planetID = quad.QuadSphere.PlanetData.Id;

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

        colorCount = quad.RenderingData.TerrainMesh.colors.Length;
        
        //CHANGE THIS TO ADD SCATTERS PROPERLY

        for (int i = 0; i < Mod.Instance.activeScatters.Length; i++)
        {
            Scatter scatter = Mod.Instance.activeScatters[i];
            data.Add(new ScatterData(this, scatter, Mod.ParallaxInstance.scatterRenderers[scatter]));
        }

        
        GetQuadMemoryUsage();
        
    }
    void OnQuadDataUpdate(Matrix4x4d m)         //Occurs every time before EvaluatePositions is called on ScatterData
    {
        GetQuadToWorldMatrix(m);
    }
    private void GetQuadToWorldMatrix(Matrix4x4d m)
    {
        if (quad.RenderingData == null) { return; }
        //Matrix4x4d drawQuadsMatrix = new Matrix4x4d();
        //drawQuadsMatrix.SetTRS(quad.QuadSphere.FramePosition, new Quaterniond(quad.QuadSphere.transform.parent.localRotation), Vector3.one);
        //Matrix4x4d m = drawQuadsMatrix;
        Matrix4x4 mQuad = m.ToMatrix4x4();
        var qpos = quad.RenderingData.LocalPosition;
        mQuad.m03 = (float)((m.m00 * qpos.x) + (m.m01 * qpos.y) + (m.m02 * qpos.z) + m.m03);
        mQuad.m13 = (float)((m.m10 * qpos.x) + (m.m11 * qpos.y) + (m.m12 * qpos.z) + m.m13);
        mQuad.m23 = (float)((m.m20 * qpos.x) + (m.m21 * qpos.y) + (m.m22 * qpos.z) + m.m23);
        quadToWorldMatrix = mQuad;
    }
    public void GetQuadMemoryUsage()
    {
        //int positions = data.positions.count * data.positions.stride;
        //double positionsInMB = positions * (0.000001);
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
