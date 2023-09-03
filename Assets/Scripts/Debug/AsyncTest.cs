using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AsyncTest : MonoBehaviour
{
    public LinkedList<Matrix4x4> colliderData = new LinkedList<Matrix4x4>();
    private ConcurrentQueue<Matrix4x4> matricesWithin50Meters = new ConcurrentQueue<Matrix4x4>();

    bool inProgress = false;

    private void Start()
    {
        Random.InitState(0);
        for (int i = 0; i < 1000000; i++)
        {
            Matrix4x4 matrix;
            Vector3 pos = new Vector3(Random.value * 1000, Random.value * 1000, Random.value * 1000);
            matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            colliderData.AddLast(matrix);
        }
        //ProcessColliderDataAsync();
        Debug.Log("Start() finished");
    }
    private void Update()
    {
        ProcessColliderDataAsync();
        Debug.Log("<color=#fffff>UPDATE</color>");
    }
    private async void ProcessColliderDataAsync()
    {
        if (inProgress) { return; }
        inProgress = true;
        matricesWithin50Meters.Clear();
        await Task.Run(() =>
        {
            foreach (Matrix4x4 matrix in colliderData)
            {
                Vector3 position = matrix.GetColumn(3);
                float distanceToOrigin = position.magnitude;

                if (distanceToOrigin <= 50f)
                {
                    matricesWithin50Meters.Enqueue(matrix);
                }
            }
        });
        Debug.Log("<color=#2DB700>In Range: </color>" + matricesWithin50Meters.Count);
        inProgress = false;
    }
}
