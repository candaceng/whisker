using UnityEngine;
using System.Collections.Generic;

public class Whisker : MonoBehaviour {
    private LineRenderer lineRenderer;
    private MeshCollider meshCollider;
    private List<Vector3[]> framePoints = new List<Vector3[]>();
    private int currentFrame;
    private float blendFactor;

    private bool hasContact = false;
    private Vector3 lastContactPoint = Vector3.zero;
    private Vector3 lastContactNormal = Vector3.zero;
    private string lastContactObjectName = "";
    private int currentFrameIndex;
    private Vector3[] currentShape;
    private Mesh bakedMesh;

    [Header("Collision segments along whisker")]
    [Tooltip("How many capsule segments approximate this whisker for collisions")]
    public int collisionSegments = 5;  
      
    public float thetaWDeg { get; private set; } = 0f;
    public float SContact { get; private set; }  

    [Tooltip("Radius of each collision capsule in world units (meters)")]
    public float colliderRadius = 1.0f;    // ~1.5 mm, tune to scale

    private WhiskerCollisionSegment[] collisionSegs;

    [Tooltip("Eye camera that renders this whisker (assign right/left eye)")]
    public Camera eyeCamera;

    [Range(0.5f, 4f)] public float minPixelsBase = 4.0f;
    [Range(0.5f, 4f)] public float minPixelsTip = 4.0f;
    [Range(0.1f, 2f)] public float pixelThicknessGain = 2.0f;
    public float maxWorldWidth = 0.1f;

    public int targetPixelHeight = 64;  // height of the final saved PNG (outH)
    public bool useMeshCollider = true;

    float PixelsToWorldWidth(float pixels, float distance) {
        float vfovRad = (eyeCamera ? eyeCamera.fieldOfView : 140f) * Mathf.Deg2Rad;
        float worldPerPixel = 2f * distance * Mathf.Tan(vfovRad * 0.5f) / Mathf.Max(1, targetPixelHeight);
        return pixels * worldPerPixel;
    }

    public void Initialize(List<Vector3[]> allFrames, Material material) {
        framePoints = allFrames;
        
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;

        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.numCapVertices    = 6;
        lineRenderer.textureMode = LineTextureMode.Stretch;

        if (material == null) {
            material = new Material(Shader.Find("Unlit/Color"));
            material.color = Color.black; 
        }
        lineRenderer.material = material;
        
        UpdateVisuals(0, 0, 0);
        CreateCollisionSegments();
    }

    public void UpdateFrame(int frameIndex, float blend, int whiskerIndex) {
        frameIndex = Mathf.Clamp(frameIndex, 0, framePoints.Count - 2);
        blend = Mathf.Clamp01(blend);
        UpdateVisuals(frameIndex, blend, whiskerIndex);

        Debug.Log($"frameIndex: {frameIndex}, theta: {frameIndex - 10f}");
        thetaWDeg = frameIndex - 10f;
    }

    void UpdateVisuals(int frameIndex, float blend, int whiskerIndex) {
        frameIndex = Mathf.Clamp(frameIndex, 0, framePoints.Count - 2);
        int nextIndex = Mathf.Min(frameIndex + 1, framePoints.Count - 1);

        Vector3[] currentFramePositions = framePoints[frameIndex];
        Vector3[] nextFramePositions = framePoints[nextIndex];

        Vector3[] interpolatedPositions = new Vector3[100];
        for (int i = 0; i < 100; i++) {
            interpolatedPositions[i] = Vector3.Lerp(currentFramePositions[i], nextFramePositions[i], blend);
        }

        lineRenderer.positionCount = interpolatedPositions.Length;
        lineRenderer.SetPositions(interpolatedPositions);

        // pick two representative points: near base & tip (local space)
        Vector3 p0Local = interpolatedPositions[0];
        Vector3 pNLocal = interpolatedPositions[interpolatedPositions.Length - 1];

        // convert to world for distance to camera
        Vector3 p0World = transform.TransformPoint(p0Local);
        Vector3 pNWorld = transform.TransformPoint(pNLocal);

        float d0 = (eyeCamera ? Vector3.Distance(eyeCamera.transform.position, p0World) : 0.2f);
        float dN = (eyeCamera ? Vector3.Distance(eyeCamera.transform.position, pNWorld) : 0.2f);

        // Compensate for local scale if useWorldSpace == false
        float scale = Mathf.Max(1e-6f, transform.lossyScale.x);

        // Compute world widths for the *saved* image height, then soften & cap
        float wBase = PixelsToWorldWidth(minPixelsBase * pixelThicknessGain, d0) / scale;
        float wTip  = PixelsToWorldWidth(minPixelsTip  * pixelThicknessGain, dN) / scale;

        wBase = Mathf.Min(wBase, maxWorldWidth);
        wTip  = Mathf.Min(wTip,  maxWorldWidth);

        // Apply as width curve
        var wc = new AnimationCurve();
        wc.AddKey(0f, wBase);
        wc.AddKey(1f, Mathf.Max(0.0005f, wTip));
        lineRenderer.widthCurve = wc;

        currentShape = interpolatedPositions;
        currentFrameIndex = frameIndex;

        // update collision capsules along this whisker 
        UpdateCollisionSegments(interpolatedPositions);

        if (useMeshCollider) BakeMeshCollider(); 

    }

    private float ComputeSFromContact(Vector3 contactHead, Vector3[] ptsHead)
    {
        if (ptsHead == null || ptsHead.Length < 2) return 0f;

        // Precompute segment lengths + total length
        float totalLen = 0f;
        float[] segLen = new float[ptsHead.Length - 1];
        for (int i = 0; i < ptsHead.Length - 1; i++)
        {
            float L = Vector3.Distance(ptsHead[i], ptsHead[i + 1]);
            segLen[i] = L;
            totalLen += L;
        }
        if (totalLen < 1e-8f) return 0f;

        // Find closest projection onto any segment
        int bestIdx = 0;
        float bestT = 0f;
        float bestDist2 = float.PositiveInfinity;

        for (int i = 0; i < ptsHead.Length - 1; i++)
        {
            Vector3 a = ptsHead[i];
            Vector3 b = ptsHead[i + 1];
            Vector3 ab = b - a;
            float ab2 = Vector3.Dot(ab, ab);
            if (ab2 < 1e-10f) continue;

            float t = Vector3.Dot(contactHead - a, ab) / ab2;
            t = Mathf.Clamp01(t);

            Vector3 proj = a + t * ab;
            float d2 = (contactHead - proj).sqrMagnitude;

            if (d2 < bestDist2)
            {
                bestDist2 = d2;
                bestIdx = i;
                bestT = t;
            }
        }

        // Arc-length up to projection point
        float arc = 0f;
        for (int i = 0; i < bestIdx; i++) arc += segLen[i];
        arc += bestT * segLen[bestIdx];

        return Mathf.Clamp01(arc / totalLen);
    }

    private Vector3[] GetCurrentShapeInHeadCoords()
    {
        Transform head = GameObject.Find("HeadOrigin").transform;

        Vector3[] ptsHead = new Vector3[currentShape.Length];
        for (int i = 0; i < currentShape.Length; i++)
        {
            // currentShape[i] is whisker-local -> world -> head-local
            Vector3 world = transform.TransformPoint(currentShape[i]);
            ptsHead[i] = head.InverseTransformPoint(world);
        }
        return ptsHead;
    }

    void CreateCollisionSegments()
    {
        collisionSegments = Mathf.Max(1, collisionSegments);
        collisionSegs = new WhiskerCollisionSegment[collisionSegments];

        for (int i = 0; i < collisionSegments; i++)
        {
            var child = new GameObject($"{gameObject.name}_Seg{i}");
            child.transform.SetParent(transform, worldPositionStays: false);

            var seg = child.AddComponent<WhiskerCollisionSegment>();
            seg.SetWhiskerId(gameObject.name);   // all segments report as this whisker

            collisionSegs[i] = seg;
        }
    }

    void OnCollisionEnter(Collision collision) {
        hasContact = true;

        // Get contact point in world coordinates
        Vector3 worldContact = collision.contacts[0].point;
        Transform ratHead = GameObject.Find("HeadOrigin").transform;
        Vector3 localContact = ratHead.InverseTransformPoint(worldContact);

        lastContactPoint = localContact; 
        lastContactNormal = collision.contacts[0].normal;  
        lastContactObjectName = collision.gameObject.name;
        // Debug.Log($"{gameObject.name} collided with {lastContactObjectName} at {lastContactPoint}");

        var ptsHead = GetCurrentShapeInHeadCoords();
        SContact = ComputeSFromContact(lastContactPoint, ptsHead);
    }

    public void ResetContactInfo() {
        hasContact = false;
        lastContactPoint = Vector3.zero;
        lastContactNormal = Vector3.zero;
        lastContactObjectName = "";

        SContact = 0f;  
    }

    void BakeMeshCollider() {
        if (bakedMesh != null) {
            Destroy(bakedMesh);  
        }

        bakedMesh = new Mesh();
        lineRenderer.BakeMesh(bakedMesh, false);

        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        meshCollider.sharedMesh = bakedMesh;
        meshCollider.convex = true;
        meshCollider.providesContacts = true;
    }

    // Sample along a polyline of points (local space)
    Vector3 SampleAlong(Vector3[] pts, float s)
    {
        s = Mathf.Clamp01(s);
        int n = pts.Length;
        float f = s * (n - 1);
        int i0 = Mathf.Min(n - 2, Mathf.FloorToInt(f));
        int i1 = i0 + 1;
        float u = f - i0;
        return Vector3.Lerp(pts[i0], pts[i1], u);
    }

    void UpdateCollisionSegments(Vector3[] interpolatedPositions)
    {
        if (collisionSegs == null || collisionSegs.Length == 0) return;
        if (interpolatedPositions == null || interpolatedPositions.Length < 2) return;

        for (int i = 0; i < collisionSegs.Length; i++)
        {
            // parameters along whisker from base→tip (avoid true endpoints a bit)
            float s0 = (i + 0.1f) / (collisionSegs.Length + 0.2f);
            float s1 = (i + 0.9f) / (collisionSegs.Length + 0.2f);

            Vector3 p0Local = SampleAlong(interpolatedPositions, s0);
            Vector3 p1Local = SampleAlong(interpolatedPositions, s1);

            // convert to world space
            Vector3 p0World = transform.TransformPoint(p0Local);
            Vector3 p1World = transform.TransformPoint(p1Local);

            collisionSegs[i].SetFromEndpoints(p0World, p1World, colliderRadius);
        }
    }

    void OnDestroy() {
        if (bakedMesh != null) {
            Destroy(bakedMesh);  // 🧹 clean up once whisker is removed
        }
    }
    
    public bool HasContact() {
        return hasContact;
    }

    public Vector3 GetLastContactPoint() {
        return lastContactPoint;
    }

    // === For generating objects for behavioural analysis ===
    public Vector3 GetTipWorld() {
        // Prefer LineRenderer (authoritative on-screen points)
        if (lineRenderer != null && lineRenderer.positionCount > 0) {
            Vector3 tip = lineRenderer.GetPosition(lineRenderer.positionCount - 1);
            return lineRenderer.useWorldSpace ? tip : transform.TransformPoint(tip);
        }
        // Fallback to your stored curve
        if (currentShape != null && currentShape.Length > 0) {
            return transform.TransformPoint(currentShape[currentShape.Length - 1]);
        }
        return transform.position;
    }

    public Vector3 GetBaseWorld() {
        if (lineRenderer != null && lineRenderer.positionCount > 0) {
            Vector3 p0 = lineRenderer.GetPosition(0);
            return lineRenderer.useWorldSpace ? p0 : transform.TransformPoint(p0);
        }
        if (currentShape != null && currentShape.Length > 0) {
            return transform.TransformPoint(currentShape[0]);
        }
        return transform.position;
    }

    public Vector3 GetDirectionWorld() {
        Vector3 a = GetBaseWorld();
        Vector3 b = GetTipWorld();
        Vector3 d = b - a;
        return d.sqrMagnitude > 1e-8f ? d.normalized : transform.forward;
    }

    void Start() {
        if (eyeCamera == null) {
            // Try to auto-detect by tag or name
            var rightCam = GameObject.Find("RightEyeCamera");
            var leftCam  = GameObject.Find("LeftEyeCamera");
            if (rightCam) eyeCamera = rightCam.GetComponent<Camera>();
            // or pick the correct side based on whisker name
        }
    }
}
