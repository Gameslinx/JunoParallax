using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Octree
{
    public Bounds Bounds { get; private set; }
    private List<Vector3> positions;
    public Octree[] children;

    public Octree(Bounds bounds)
    {
        Bounds = bounds;
        positions = new List<Vector3>();
        children = null;
    }

    public bool Contains(Vector3 point)
    {
        return Bounds.Contains(point);
    }

    public bool Intersects(Bounds otherBounds)
    {
        return Bounds.Intersects(otherBounds);
    }

    public void Insert(Vector3 position)
    {
        if (children != null)
        {
            int index = GetChildIndex(position);
            if (index != -1)
            {
                children[index].Insert(position);
                return;
            }
        }

        positions.Add(position);

        if (positions.Count > 5)
        {
            Subdivide();
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                Vector3 pos = positions[i];
                int index = GetChildIndex(pos);
                if (index != -1)
                {
                    children[index].Insert(pos);
                    positions.RemoveAt(i);
                }
            }
        }
    }

    private int GetChildIndex(Vector3 point)
    {
        int index = 0;
        Vector3 center = Bounds.center;

        if (point.x > center.x) index |= 1;
        if (point.y > center.y) index |= 2;
        if (point.z > center.z) index |= 4;

        return index;
    }

    private void Subdivide()
    {
        Vector3 center = Bounds.center;
        Vector3 halfSize = Bounds.size * 0.5f;

        children = new Octree[8];
        children[0] = new Octree(new Bounds(center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), halfSize));
        children[1] = new Octree(new Bounds(center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), halfSize));
        children[2] = new Octree(new Bounds(center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), halfSize));
        children[3] = new Octree(new Bounds(center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), halfSize));
        children[4] = new Octree(new Bounds(center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), halfSize));
        children[5] = new Octree(new Bounds(center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), halfSize));
        children[6] = new Octree(new Bounds(center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), halfSize));
        children[7] = new Octree(new Bounds(center + new Vector3(halfSize.x, halfSize.y, halfSize.z), halfSize));
    }

    public List<Vector3> Query(Bounds queryBounds)
    {
        List<Vector3> result = new List<Vector3>();

        if (!Intersects(queryBounds))
            return result;

        foreach (Vector3 pos in positions)
        {
            if (queryBounds.Contains(pos))
                result.Add(pos);
        }

        if (children != null)
        {
            foreach (Octree child in children)
                result.AddRange(child.Query(queryBounds));
        }

        return result;
    }
}
