using Assets.Scripts.Terrain;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using UnityEngine;

public class FloatingOriginDebug : MonoBehaviour
{
    // Start is called before the first frame update
    public QuadScript quad;
    public Matrix4x4 quadToWorld;
    int framesAllowed = 0;
    void Start()
    {
        transform.localScale= Vector3.one * 50f;
    }

    // Update is called once per frame
    void Update()
    {
        //if (quad == null || quad.QuadRenderer == null || !quad.QuadRenderer.enabled)
        //{
        //    framesAllowed++;
        //    if (framesAllowed > 60)
        //    {
        //        gameObject.SetActive(false);
        //        Destroy(gameObject);
        //        return;
        //    }
        //    
        //}
        try
        {
            Matrix4x4 parentMatrix = Matrix4x4.TRS(quad.QuadSphere.Transform.position, quad.QuadSphere.Transform.rotation, quad.QuadSphere.Transform.localScale);
            Vector3 transformedPosition = parentMatrix.MultiplyPoint(quad.PlanetPosition.ToVector3());
            quadToWorld = Matrix4x4.TRS(transformedPosition, quad.QuadSphere.Transform.rotation, quad.QuadSphere.Transform.localScale);
            transform.position = transformedPosition;


            Vector3 scale;
            scale.x = new Vector4(quadToWorld.m00, quadToWorld.m10, quadToWorld.m20, quadToWorld.m30).magnitude;
            scale.y = new Vector4(quadToWorld.m01, quadToWorld.m11, quadToWorld.m21, quadToWorld.m31).magnitude;
            scale.z = new Vector4(quadToWorld.m02, quadToWorld.m12, quadToWorld.m22, quadToWorld.m32).magnitude;

            Vector3 position;
            position.x = quadToWorld.m03;
            position.y = quadToWorld.m13;
            position.z = quadToWorld.m23;

            Vector3 forward;
            forward.x = quadToWorld.m02;
            forward.y = quadToWorld.m12;
            forward.z = quadToWorld.m22;

            Vector3 upwards;
            upwards.x = quadToWorld.m01;
            upwards.y = quadToWorld.m11;
            upwards.z = quadToWorld.m21;

            Quaternion rotation = Quaternion.LookRotation(forward, upwards);

            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = scale * 50f;
        }
        catch
        {
            Destroy(gameObject);
        }
    }
}
