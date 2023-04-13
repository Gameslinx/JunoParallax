using Assets.Scripts;
using Assets.Scripts.Terrain;
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

    private Vector3[] vertexData;
    private int[] triangleData;
    private Vector3[] normalData;

    public ComputeBuffer vertices;
    public ComputeBuffer normals;
    public ComputeBuffer triangles;
    
    public int vertexCount;
    public int triangleCount;       //This is actually tricount / 3

    public ScatterData data;        //Change to array

    public Matrix4x4 quadToWorldMatrix;
    public Vector3 planetNormal;

    public QuadData(QuadScript quad)
    {
        this.quad = quad;

        RegisterEvents();
        Initialize();
    }
    public void RegisterEvents()
    {
        Mod.ParallaxInstance.scatterManagers[quad.QuadSphere.PlanetData.Id].OnQuadUpdate += OnQuadDataUpdate;
    }
    public void Initialize()        //Initialize buffers, then scatters
    {
        Debug.Log("[QuadData] Initializing");

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
        GetQuadToWorldMatrix();
        
        //CHANGE THIS TO ADD SCATTERS PROPERLY
        Scatter scatter = Mod.ParallaxInstance.dummyScatter;
        Debug.Log("[QuadData] Initialized, creating scatter data...");
        data = new ScatterData(this, scatter, Mod.ParallaxInstance.scatterRenderers[scatter]);
        Debug.Log("[QuadData] Scatter data created");
    }
    void OnQuadDataUpdate()         //Occurs every time before EvaluatePositions is called on ScatterData
    {
        GetQuadToWorldMatrix();
    }
    private void GetQuadToWorldMatrix()
    {
        Debug.Log("QTWM");
        Matrix4x4 parentMatrix = Matrix4x4.TRS(quad.QuadSphere.Transform.position, quad.QuadSphere.Transform.rotation, quad.QuadSphere.Transform.localScale);
        Vector3 transformedPosition = parentMatrix.MultiplyPoint(quad.PlanetPosition.ToVector3());
        quadToWorldMatrix = Matrix4x4.TRS(transformedPosition, quad.QuadSphere.Transform.rotation, quad.QuadSphere.Transform.localScale);
    }
    public void Cleanup()           //Clean up scatter data, then the quad data
    {
        data.Cleanup();

        vertices?.Release();
        normals?.Release();
        triangles?.Release();

        Mod.ParallaxInstance.scatterManagers[quad.QuadSphere.PlanetData.Id].OnQuadUpdate -= OnQuadDataUpdate;
    }
}
