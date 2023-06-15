using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Render 1 object at the origin with the correct shader
public class ShaderViewer : MonoBehaviour
{
    // Start is called before the first frame update
    ComputeBuffer stuffToRender;
    ComputeBuffer argsBuffer;
    public Mesh mesh;
    public Material material;
    public Bounds bounds;
    void Start()
    {
        Matrix4x4[] mat = new Matrix4x4[100];
        Random.InitState(0);
        for (int i = 0; i < 100; i++)
        {
            Vector3 pos = new Vector3(Random.value, Random.value, Random.value) - Vector3.one * 0.5f;
            pos *= 10;
            mat[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
        }

        mat = new Matrix4x4[] { Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one)};

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)mat.Length;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);

        stuffToRender = new ComputeBuffer(mat.Length, TransformData.Size(), ComputeBufferType.Structured);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        stuffToRender.SetData(mat);
        material.SetBuffer("_Properties", stuffToRender);
    }

    // Update is called once per frame
    void Update()
    {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }
    private void OnDisable()
    {
        stuffToRender.Dispose();
        argsBuffer.Dispose();
    }
}
