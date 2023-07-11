using Assets.Scripts;
using Assets.Scripts.Flight.GameView.Cameras;
using Assets.Scripts.Terrain;
using System.Collections;
using System.Collections.Generic;
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
}
