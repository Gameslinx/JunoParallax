using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.Terrain;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Utils : MonoBehaviour
{
    public static float[] planeNormals;
    public static Plane[] planes = new Plane[6];
    private void Update()
    {
        ConstructFrustumPlanes(CameraManagerScript.Instance.CurrentCameraController.CameraTransform.gameObject.GetComponent<Camera>(), out planeNormals);  //Compute frustum planes for frustum culling
    }
    public static void ConstructFrustumPlanes(Camera camera, out float[] planeNormals)
    {
        const int floatPerNormal = 4;

        // https://docs.unity3d.com/ScriptReference/GeometryUtility.CalculateFrustumPlanes.html
        // Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        planes = GeometryUtility.CalculateFrustumPlanes(camera);

        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
        {
            planes[5].distance = 25000;
        }

        planeNormals = new float[planes.Length * floatPerNormal];
        for (int i = 0; i < planes.Length; ++i)
        {
            planeNormals[i * floatPerNormal + 0] = planes[i].normal.x;
            planeNormals[i * floatPerNormal + 1] = planes[i].normal.y;
            planeNormals[i * floatPerNormal + 2] = planes[i].normal.z;
            planeNormals[i * floatPerNormal + 3] = planes[i].distance;
        }
    }
    public static Matrix4x4 GetTranslationMatrix(Vector3 pos)
    {
        return Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
    }

    public static Matrix4x4 GetRotationMatrix(Vector3 anglesDeg)
    {
        Vector3 anglesRad = new Vector3(0, Mathf.Deg2Rad * anglesDeg.y, 0);

        float cosX = Mathf.Cos(anglesRad.x);
        float sinX = Mathf.Sin(anglesRad.x);
        float cosY = Mathf.Cos(anglesRad.y);
        float sinY = Mathf.Sin(anglesRad.y);
        float cosZ = Mathf.Cos(anglesRad.z);
        float sinZ = Mathf.Sin(anglesRad.z);

        Matrix4x4 rotationMatrix = new Matrix4x4();

        rotationMatrix[0, 0] = cosY * cosZ;
        rotationMatrix[0, 1] = -cosX * sinZ + sinX * sinY * cosZ;
        rotationMatrix[0, 2] = sinX * sinZ + cosX * sinY * cosZ;

        rotationMatrix[1, 0] = cosY * sinZ;
        rotationMatrix[1, 1] = cosX * cosZ + sinX * sinY * sinZ;
        rotationMatrix[1, 2] = -sinX * cosZ + cosX * sinY * sinZ;

        rotationMatrix[2, 0] = -sinY;
        rotationMatrix[2, 1] = sinX * cosY;
        rotationMatrix[2, 2] = cosX * cosY;

        rotationMatrix[3, 3] = 1f;

        return rotationMatrix;
    }

    public static Matrix4x4 TransformToPlanetNormal(Vector3 b)
    {
        Quaternion rotationQuaternion = Quaternion.FromToRotation(Vector3.up, b);
        return Matrix4x4.Rotate(rotationQuaternion);
    }

    public static void GetTRSMatrix(Vector3 position, Vector3 rotationAngles, Vector3 scale, Vector3 terrainNormal, ref Matrix4x4 mat)
    {
        mat = GetTranslationMatrix(position) * TransformToPlanetNormal(terrainNormal) * GetRotationMatrix(rotationAngles) * Matrix4x4.Scale(scale);
    }
}
