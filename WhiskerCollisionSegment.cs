using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class WhiskerCollisionSegment : MonoBehaviour
{
    private Rigidbody rb;
    private CapsuleCollider col;

    private Whisker parentWhisker;
    private Vector3 p0World, p1World;

    private float s0, s1; // arc-length fraction range along whisker (0=base, 1=tip)

    private float radius;

    [SerializeField] private float proximityMultiplier = 2.0f;

    void Awake()
    {
        int whiskerLayer = LayerMask.NameToLayer("Whisker");
        if (whiskerLayer != -1) gameObject.layer = whiskerLayer;

        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        rb.isKinematic = true;
        rb.useGravity = false;

        col.isTrigger = true;
        col.direction = 1; // capsule height along local Y
    }

    public void Init(Whisker parent)
    {
        parentWhisker = parent;
    }

    public void SetFromEndpoints(Vector3 p0, Vector3 p1, float colliderRadius, float s0_, float s1_)
    {
        p0World = p0;
        p1World = p1;
        radius = colliderRadius;

        s0 = s0_;
        s1 = s1_;

        Vector3 mid = 0.5f * (p0World + p1World);
        Vector3 dir = (p1World - p0World);
        float dist = dir.magnitude;
        if (dist < 1e-6f) dist = 1e-6f;

        transform.position = mid;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, dir / dist);

        col.radius = radius;
        col.height = dist + 2f * radius; // slight extension past endpoints
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterContact(other);
    }

    private void RegisterContact(Collider other)
    {
        if (parentWhisker == null) return;

        // closest point on the obstacle to the segment midpoint
        Vector3 mid = 0.5f * (p0World + p1World);
        Vector3 cw = other.ClosestPoint(mid);

        Vector3 ab = p1World - p0World;
        float ab2 = Vector3.Dot(ab, ab);
        if (ab2 < 1e-10f) return;

        float t = Vector3.Dot(cw - p0World, ab) / ab2;
        t = Mathf.Clamp01(t);

        Vector3 proj = p0World + t * ab;
        float distToSegment = Vector3.Distance(cw, proj);

        if (distToSegment > radius * proximityMultiplier)
            return;

        // u=0 at base, u=1 at tip
        float u = Mathf.Lerp(s0, s1, t);
        float intensity = Mathf.Clamp01(1f - u);

        parentWhisker.RegisterIntensity(intensity, other.name);

    }
}
