using Assets.Scripts.Terrain;
using ModApi.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

//Adapted from Advanced Subdivision by Linx

public struct Triangle
{
    public Vector3 center;
    public Vector3 v1, v2, v3;
    public Vector3 n1, n2, n3;
    public int index1, index2, index3;
    public Triangle(Vector3 center, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n1, Vector3 n2, Vector3 n3, int index1, int index2, int index3)
    {
        this.center = center;
        this.v1 = v1; this.v2 = v2; this.v3 = v3;
        this.n1 = n1; this.n2 = n2; this.n3 = n3;
        this.index1 = index1;
        this.index2 = index2;
        this.index3 = index3;
    }
}
public class TriangleOps    //Works with meshes on per-triangle basis, making it easy to modify the mesh based on given conditions
{
    private static Triangle[] quadTris;

    public static Dictionary<Vector3, int> newVertexIndices = new Dictionary<Vector3, int>();
    public static HashSet<Vector3> newHashVerts = new HashSet<Vector3>();
    public static List<int> newTris = new List<int>();
    public static List<Vector3> newVerts = new List<Vector3>();
    public static List<Vector3> newNormals = new List<Vector3>();
    public static List<Color> newColors = new List<Color>();
    public static void AppendTriangle(Triangle tri)
    {
        int index1;
        int index2;
        int index3;

        if (newHashVerts.Add(tri.v1)) { index1 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v1, index1); newVerts.Add(tri.v1); newNormals.Add(tri.n1); } else { index1 = newVertexIndices[tri.v1]; }
        if (newHashVerts.Add(tri.v2)) { index2 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v2, index2); newVerts.Add(tri.v2); newNormals.Add(tri.n2); } else { index2 = newVertexIndices[tri.v2]; }
        if (newHashVerts.Add(tri.v3)) { index3 = newHashVerts.Count - 1; newVertexIndices.Add(tri.v3, index3); newVerts.Add(tri.v3); newNormals.Add(tri.n3); } else { index3 = newVertexIndices[tri.v3]; }

        newTris.Add(index1);
        newTris.Add(index2);
        newTris.Add(index3);
    }
    public static void Clear()
    {
        newVertexIndices.Clear();
        newHashVerts.Clear();
        newTris.Clear();
        newVerts.Clear();
        newNormals.Clear();
        newColors.Clear();
    }

    public static void CreateTris(int[] quadIndices, Vector3[] quadVerts, Vector3[] quadNormals)
    {
        quadTris = new Triangle[quadIndices.Length / 3];
        for (int i = 0; i < quadIndices.Length; i += 3)                             //Create array of each triangle in the quad
        {
            int index1 = quadIndices[i + 0];
            int index2 = quadIndices[i + 1];
            int index3 = quadIndices[i + 2];

            Vector3 v1 = quadVerts[index1];
            Vector3 v2 = quadVerts[index2];
            Vector3 v3 = quadVerts[index3];

            Vector3 n1 = quadNormals[index1];
            Vector3 n2 = quadNormals[index2];
            Vector3 n3 = quadNormals[index3];

            Vector3 center = (v1 + v2 + v3) / 3;

            Triangle tri = new Triangle(center, v1, v2, v3, n1, n2, n3, quadIndices[i], quadIndices[i + 1], quadIndices[i + 2]);
            quadTris[i / 3] = tri;
        }
    }
    public static void RemoveSkirts()
    {   
        Triangle tri;

        for (int i = 0; i < quadTris.Length; i++)
        {
            tri = quadTris[i];

            if (i <= 52 || i % 52 == 0 || i % 52 == 1 || i % 52 == 51 || i % 52 == 50 || i >= quadTris.Length - 53)
            {
                continue;
            }

            AppendTriangle(tri);                                        //Construct new quad mesh with skirts removed
        }
    }
}