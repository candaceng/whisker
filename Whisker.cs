// Whisker.cs
using UnityEngine;
using System.Collections.Generic;

public class Whisker : MonoBehaviour {
    private LineRenderer lineRenderer;
    private MeshCollider meshCollider;
    private List<Vector3[]> framePoints = new List<Vector3[]>();
    private int currentFrame;
    private float blendFactor;
    private int smoothness;

    public void Initialize(List<Vector3[]> allFrames, int smoothness, Material material) {
        this.smoothness = smoothness;
        framePoints = allFrames;
        
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.useWorldSpace = true;

        if (material == null) {
            material = new Material(Shader.Find("Unlit/Color"));
            material.color = Color.black; 
        }
        lineRenderer.material = material;
        
        UpdateVisuals(0, 0, 0);
    }

    public void UpdateFrame(int frameIndex, float blend, int whiskerIndex) {
        frameIndex = Mathf.Clamp(frameIndex, 0, framePoints.Count - 2);
        blend = Mathf.Clamp01(blend);
        UpdateVisuals(frameIndex, blend, whiskerIndex);
    }

    void UpdateVisuals(int frameIndex, float blend, int whiskerIndex) {
        WhiskerManager manager = FindObjectOfType<WhiskerManager>();
        bool isRight = gameObject.name.StartsWith("R");
        whiskerIndex = isRight ? whiskerIndex : whiskerIndex - 30; // Left whiskers 

        Vector3[] currentFramePositions = manager.GetWhiskerPositions(frameIndex, whiskerIndex, isRight);
        Vector3[] nextFramePositions = manager.GetWhiskerPositions(Mathf.Min(frameIndex + 1, 29), whiskerIndex, isRight);
        
        Vector3[] interpolatedPositions = new Vector3[100];
        for (int i = 0; i < 100; i++) {
            interpolatedPositions[i] = Vector3.Lerp(currentFramePositions[i], nextFramePositions[i], blend);
        }

        List<Vector3> smoothed = SmoothPoints(new List<Vector3>(framePoints[11]), smoothness); // Use 0° shape as base
        lineRenderer.positionCount = interpolatedPositions.Length;
        lineRenderer.SetPositions(interpolatedPositions);

        BakeMeshCollider();
    }

    void OnCollisionEnter(Collision collision) {
        Debug.Log($"Collision detected with {collision.gameObject.name}");
    }

    // Bezier curve smoothing
    List<Vector3> SmoothPoints(List<Vector3> points, int subdivisions) {
        List<Vector3> smoothed = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)];

            for (int j = 0; j < subdivisions; j++)
            {
                float t = j / (float)subdivisions;
                smoothed.Add(CalculateBezierPoint(t, p0, p1, p2, p3));
            }
        }
        
        return smoothed;
    }

    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
        return 0.5f * (
            (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t) +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) +
            (-p0 + p2) * t +
            2f * p1
        );
    }

    void BakeMeshCollider() {
        // Create mesh from LineRenderer
        Mesh mesh = new Mesh();
        lineRenderer.BakeMesh(mesh, true);

        // Add/update MeshCollider
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        
        meshCollider.sharedMesh = mesh;
    }

}