using UnityEngine;

/// <summary>
/// Simple spider controller: walks on any surface and treats walls as more floor.
/// Kinematic, sphere-cast based. Does not use CharacterController or SurfaceDetector.
/// </summary>
public class ImprovedWallWalker : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float jumpForce = 5f;
    public float gravity = 15f;
    [Tooltip("How fast the body turns to match the surface (higher = snappier).")]
    public float alignSpeed = 12f;

    [Header("Body")]
    [Tooltip("Radius of the collision sphere.")]
    public float radius = 0.3f;
    [Tooltip("Distance from body center to surface while stuck.")]
    public float hoverDistance = 0.4f;
    [Tooltip("How far to look for a surface to stick to.")]
    public float stickRange = 0.3f;
    public LayerMask surfaceMask = ~0;

    [Header("Camera")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;
    public Transform playerCamera;

    [Header("Debug")]
    public bool debugMovement = true;
    public bool debugOverlay = true;

    // Read by SurfaceDetector.
    [HideInInspector] public Vector3 velocity;

    public bool IsStuck { get; private set; }

    const float Skin = 0.02f;

    struct DebugCast
    {
        public bool valid, hit;
        public Vector3 origin, dir, point, normal;
        public float dist, radius;
        public Color color;
    }

    // 0 wall ahead, 1 stick, 2 edge wrap, 3 air
    readonly DebugCast[] casts = new DebugCast[4];

    Transform cameraHolder;
    float cameraPitch;
    Vector3 normal = Vector3.up;
    float airUntil;

    void Start()
    {
        // Our own casts would otherwise fight the CharacterController's collider.
        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;
        if (TryGetComponent(out SurfaceDetector sd)) sd.enabled = false;

        normal = transform.up;
        IsStuck = true;

        if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;
        if (playerCamera == null)
        {
            Debug.LogError("Player camera not assigned!");
            return;
        }

        cameraHolder = new GameObject("CameraHolder").transform;
        cameraHolder.SetParent(transform, false);
        playerCamera.SetParent(cameraHolder, true);

        if (!Application.isEditor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        Look();

        if (IsStuck) WalkSurface();
        else FlyAir();

        Align();

    }

    void Record(int i, Vector3 origin, Vector3 dir, float dist, float r, bool hit, RaycastHit h, Color c)
    {
        casts[i] = new DebugCast
        {
            valid = true, hit = hit, origin = origin, dir = dir, dist = hit ? h.distance : dist,
            radius = r, point = h.point, normal = h.normal, color = c
        };
    }

    void OnDrawGizmos()
    {
        if (!debugMovement) return;
        Vector3 p = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(p, transform.up * 1.5f);          // body up
        Gizmos.color = Color.green;
        Gizmos.DrawRay(p, normal * 1.5f);                // target surface normal
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(p, transform.forward * 1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(p, velocity);
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(p, radius);
        Gizmos.DrawWireSphere(p - normal * (hoverDistance - radius), 0.03f);

        if (!Application.isPlaying) return;
        foreach (var c in casts)
        {
            if (!c.valid) continue;
            Vector3 end = c.origin + c.dir * c.dist;
            Gizmos.color = c.color;
            Gizmos.DrawLine(c.origin, end);
            Gizmos.DrawWireSphere(c.origin, c.radius);
            Gizmos.DrawWireSphere(end, c.radius);
            if (!c.hit) continue;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(c.point, 0.05f);
            Gizmos.DrawRay(c.point, c.normal * 0.5f);
        }
    }

    void OnGUI()
    {
        if (!debugOverlay) return;
        GUI.Label(new Rect(10, 10, 420, 100),
            $"State: {(IsStuck ? "STUCK" : "AIR")}\nNormal: {normal:F2}\nBody up: {transform.up:F2}\nSpeed: {velocity.magnitude:F2}");
    }

    void Look()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(transform.up, mx, Space.World);

        if (cameraHolder == null) return;
        cameraPitch = Mathf.Clamp(cameraPitch - my, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    void WalkSurface()
    {
        Vector3 input = transform.forward * Input.GetAxis("Vertical") + transform.right * Input.GetAxis("Horizontal");
        Vector3 dir = Vector3.ProjectOnPlane(input, normal);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 pos = transform.position;
        Vector3 step = dir * (moveSpeed * Time.deltaTime);
        float dist = step.magnitude;
        velocity = dir * moveSpeed;

        if (Input.GetButtonDown("Jump"))
        {
            velocity = normal * jumpForce + dir * moveSpeed;
            IsStuck = false;
            airUntil = Time.time + 0.2f;
            return;
        }

        if (dist > 0.0001f)
        {
            Vector3 d = step / dist;
            // A blocking surface ahead (wall or ceiling) becomes the new floor.
            bool wallHit = Physics.SphereCast(pos, radius, d, out RaycastHit wall, dist + Skin, surfaceMask, QueryTriggerInteraction.Ignore);
            Record(0, pos, d, dist + Skin, radius, wallHit, wall, Color.magenta);
            if (wallHit && wall.distance > 0f)
            {
                pos += d * Mathf.Max(0f, wall.distance - Skin);
                normal = wall.normal;
            }
            else
            {
                pos += step;
            }
        }

        if (!Stick(ref pos)) WrapEdge(ref pos, dist);

        transform.position = pos;
    }

    // Re-snap to the surface under the body.
    bool Stick(ref Vector3 pos)
    {
        float reach = hoverDistance + stickRange;
        Vector3 origin = pos + normal * radius;
        bool found = Physics.SphereCast(origin, radius, -normal, out RaycastHit hit, reach, surfaceMask, QueryTriggerInteraction.Ignore);
        Record(1, origin, -normal, reach, radius, found, hit, Color.green);
        if (!found) return false;

        // Spherecast normals can be edge-smoothed; blend to avoid jitter.
        normal = Vector3.Slerp(normal, hit.normal, 0.5f).normalized;
        pos = hit.point + hit.normal * hoverDistance;
        return true;
    }

    // Walked off a convex edge: look back under the edge for the side face.
    void WrapEdge(ref Vector3 pos, float stepDist)
    {
        Vector3 back = -Vector3.ProjectOnPlane(velocity, normal).normalized;
        if (back == Vector3.zero) { LeaveSurface(); return; }

        Vector3 origin = pos - normal * (hoverDistance + radius);
        float reach = stepDist + radius * 2f + hoverDistance;
        bool found = Physics.SphereCast(origin, radius * 0.5f, back, out RaycastHit hit, reach, surfaceMask, QueryTriggerInteraction.Ignore);
        Record(2, origin, back, reach, radius * 0.5f, found, hit, Color.cyan);
        if (found && hit.distance > 0f)
        {
            normal = hit.normal;
            pos = hit.point + hit.normal * hoverDistance;
        }
        else
        {
            LeaveSurface();
        }
    }

    void LeaveSurface()
    {
        IsStuck = false;
        airUntil = 0f;
    }

    void FlyAir()
    {
        velocity += Vector3.down * (gravity * Time.deltaTime);

        Vector3 pos = transform.position;
        Vector3 step = velocity * Time.deltaTime;
        float dist = step.magnitude;

        if (dist > 0.0001f)
        {
            Vector3 d = step / dist;
            bool found = Physics.SphereCast(pos, radius, d, out RaycastHit hit, dist + Skin, surfaceMask, QueryTriggerInteraction.Ignore);
            Record(3, pos, d, dist + Skin, radius, found, hit, Color.white);
            if (found && hit.distance > 0f && Time.time >= airUntil)
            {
                // Land on whatever we hit, walls included.
                normal = hit.normal;
                pos = hit.point + hit.normal * hoverDistance;
                velocity = Vector3.zero;
                IsStuck = true;
            }
            else
            {
                pos += step;
            }
        }

        transform.position = pos;
    }

    void Align()
    {
        Vector3 targetUp = IsStuck ? normal : Vector3.up;
        Quaternion target = Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-alignSpeed * Time.deltaTime));
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
