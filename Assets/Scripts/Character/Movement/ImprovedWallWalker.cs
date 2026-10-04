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
    public bool debugMovement;

    // Read by SurfaceDetector.
    [HideInInspector] public Vector3 velocity;

    public bool IsStuck { get; private set; }

    const float Skin = 0.02f;

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

        if (debugMovement)
        {
            Debug.DrawRay(transform.position, normal, Color.green);
            Debug.DrawRay(transform.position, velocity, Color.yellow);
        }
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
            if (Physics.SphereCast(pos, radius, d, out RaycastHit wall, dist + Skin, surfaceMask, QueryTriggerInteraction.Ignore)
                && wall.distance > 0f)
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
        if (!Physics.SphereCast(pos + normal * radius, radius, -normal, out RaycastHit hit, reach, surfaceMask, QueryTriggerInteraction.Ignore))
            return false;

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
        if (Physics.SphereCast(origin, radius * 0.5f, back, out RaycastHit hit, reach, surfaceMask, QueryTriggerInteraction.Ignore)
            && hit.distance > 0f)
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
            if (Physics.SphereCast(pos, radius, d, out RaycastHit hit, dist + Skin, surfaceMask, QueryTriggerInteraction.Ignore)
                && hit.distance > 0f && Time.time >= airUntil)
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
