using Rewired;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using UnityEngine;

public class OctreeViewer : MonoBehaviour
{
    // Start is called before the first frame update
    public Octree octree;
    public Material lineMaterial;

    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private List<Vector3> positions = new List<Vector3>();

    private void Start()
    {
        octree = new Octree(gameObject.GetComponent<Renderer>().bounds);
        DrawBounds(octree);
    }
    private void Update()
    {
        //if ()
        //{
        //    Debug.Log("Mouse down");
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //    RaycastHit hit;
        //
        //    if (Physics.Raycast(ray, out hit))
        //    {
        //        positions.Add(hit.point);
        //        octree.Insert(hit.point);
        //    }
        //}
    }
    private void OnDrawGizmos()
    {
        foreach (Vector3 vector3 in positions)
        {
            Gizmos.DrawSphere(vector3, 0.1f);
        }
    }
    private void DrawBounds(Octree node)
    {
        GameObject nodeObject = new GameObject("NodeBounds");
        nodeObject.transform.SetParent(transform);

        Bounds bounds = node.Bounds;

        LineRenderer lineRenderer = nodeObject.AddComponent<LineRenderer>();
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;

        Vector3[] corners = new Vector3[8];
        corners[0] = bounds.min;
        corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
        corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
        corners[3] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
        corners[4] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
        corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        corners[6] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
        corners[7] = bounds.max;

        lineRenderer.positionCount = 16;

        lineRenderer.SetPosition(0, corners[0]);
        lineRenderer.SetPosition(1, corners[1]);
        lineRenderer.SetPosition(2, corners[1]);
        lineRenderer.SetPosition(3, corners[3]);
        lineRenderer.SetPosition(4, corners[3]);
        lineRenderer.SetPosition(5, corners[2]);
        lineRenderer.SetPosition(6, corners[2]);
        lineRenderer.SetPosition(7, corners[0]);

        lineRenderer.SetPosition(8, corners[4]);
        lineRenderer.SetPosition(9, corners[5]);
        lineRenderer.SetPosition(10, corners[5]);
        lineRenderer.SetPosition(11, corners[7]);
        lineRenderer.SetPosition(12, corners[7]);
        lineRenderer.SetPosition(13, corners[6]);
        lineRenderer.SetPosition(14, corners[6]);
        lineRenderer.SetPosition(15, corners[4]);

        lineRenderers.Add(lineRenderer);

        if (node.children != null)
        {
            foreach (Octree child in node.children)
            {
                DrawBounds(child);
            }
        }
    }
}
