using UnityEngine;

public class BoundFix : MonoBehaviour
{
    public float boundMultiplier = 10f;
    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            return;
        }
        Mesh mesh = meshFilter.mesh;
        Bounds bounds = mesh.bounds;
        bounds.extents *= boundMultiplier;
        mesh.bounds = bounds;
    }
}
