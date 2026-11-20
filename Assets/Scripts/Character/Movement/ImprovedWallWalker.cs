using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Simple spider controller: walks on any surface and treats walls as more floor.
/// Kinematic, sphere-cast based. Does not use CharacterController.
/// </summary>
public class ImprovedWallWalker : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float jumpForce = 5f;
    public float gravity = 15f;
    [Tooltip("How fast the body turns to match the surface (higher = snappier).")]
    public float alignSpeed = 12f;
    [Tooltip("How quickly the body eases toward the camera while airborne.")]
    public float jumpAlignSpeed = 5f;

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
    [Tooltip("Maximum downward camera angle relative to the current surface.")]
    public float maxDownLookAngle = 15f;
    [Tooltip("Set as Follow target of the Cinemachine camera. Created automatically if empty.")]
    public Transform cameraTarget;
    public float cameraTargetHeight = 0.5f;
    [Tooltip("How fast the camera up vector follows the surface (lower = lazier).")]
    public float cameraUpSpeed = 4f;

    [Header("Jump Aim")]
    public float aimMouseSensitivity = 0.45f;
    public float aimFieldOfView = 30f;
    public float cameraFovSpeed = 8f;
    [Tooltip("Optional. The active Cinemachine camera is found automatically when empty.")]
    public CinemachineCamera gameplayCamera;

    [Header("Trajectory")]
    public float trajectoryDuration = 4f;
    public float trajectoryStep = 0.05f;
    public float trajectoryWidth = 0.035f;
    public Color trajectoryColor = new Color(0.25f, 0.85f, 1f, 0.9f);
    public Color landingColor = new Color(0.25f, 0.9f, 1f, 0.8f);
    public float landingMarkerSize = 0.85f;

    [Header("Debug")]
    public bool debugMovement = true;
    public bool debugOverlay = true;

    [HideInInspector] public Vector3 velocity;

    public bool IsStuck { get; private set; }
    public bool IsAiming { get; private set; }

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

    float cameraPitch;
    Vector3 cameraUp = Vector3.up;
    Vector3 cameraForward = Vector3.forward;
    Vector3 normal = Vector3.up;
    Vector3 airForward = Vector3.forward;
    float airUntil;
    float defaultFieldOfView;
    Vector3 aimLandingPoint;
    bool hasAimLandingPoint;
    CinemachineFollow cameraFollow;
    LineRenderer trajectoryRenderer;
    MeshRenderer landingMarker;
    Material trajectoryMaterial;
    Material landingMaterial;
    Texture2D landingTexture;
    readonly List<Vector3> trajectoryPoints = new List<Vector3>(128);

    void Start()
    {
        // Our own casts would otherwise fight the CharacterController's collider.
        if (TryGetComponent(out CharacterController cc)) cc.enabled = false;

        normal = transform.up;
        cameraForward = transform.forward;
        IsStuck = true;

        if (cameraTarget == null) cameraTarget = new GameObject("CameraTarget").transform;
        cameraTarget.SetParent(null);
        cameraUp = transform.up;
        UpdateCameraTarget(1f);
        InitializeAimVisuals();

        if (gameplayCamera == null) gameplayCamera = FindAnyObjectByType<CinemachineCamera>();
        if (gameplayCamera != null) defaultFieldOfView = gameplayCamera.Lens.FieldOfView;
        else if (Camera.main != null) defaultFieldOfView = Camera.main.fieldOfView;
        if (gameplayCamera != null) cameraFollow = gameplayCamera.GetComponent<CinemachineFollow>();

        CaptureCursor();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            CaptureCursor();

        Look();
        UpdateJumpInput();

        if (IsStuck)
        {
            if (!IsAiming) WalkSurface();
        }
        else
        {
            FlyAir();
        }

        Align();
        UpdateAimCamera();
        UpdateTrajectory();
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
        float sensitivity = IsAiming ? mouseSensitivity * aimMouseSensitivity : mouseSensitivity;
        float mx = Input.GetAxis("Mouse X") * sensitivity;
        float my = Input.GetAxis("Mouse Y") * sensitivity;

        if (IsAiming || !IsStuck)
        {
            cameraForward = Quaternion.AngleAxis(mx, cameraUp) * cameraForward;
        }
        else
        {
            transform.Rotate(transform.up, mx, Space.World);
            cameraForward = transform.forward;
        }
        cameraPitch = Mathf.Clamp(cameraPitch - my, -maxLookAngle, maxDownLookAngle);
    }

    void LateUpdate() => UpdateCameraTarget(1f - Mathf.Exp(-cameraUpSpeed * Time.deltaTime));

    // Target keeps a lagged up vector so the camera swings over the shoulder on walls and ceilings.
    void UpdateCameraTarget(float upBlend)
    {
        Vector3 goalUp = IsStuck ? normal : transform.up;
        cameraUp = Vector3.Slerp(cameraUp, goalUp, upBlend).normalized;

        Vector3 fwd = Vector3.ProjectOnPlane(cameraForward, cameraUp);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(transform.up, cameraUp);
        cameraForward = fwd.normalized;

        Vector3 targetPosition = transform.position + cameraUp * cameraTargetHeight;
        Quaternion targetRotation = Quaternion.LookRotation(fwd, cameraUp) * Quaternion.Euler(cameraPitch, 0f, 0f);
        if (IsAiming && hasAimLandingPoint)
            targetRotation = GetLandingAimRotation(targetPosition, targetRotation);

        cameraTarget.SetPositionAndRotation(targetPosition, targetRotation);
    }

    Quaternion GetLandingAimRotation(Vector3 targetPosition, Quaternion fallbackRotation)
    {
        if (cameraFollow == null) return GetRotationLookingAtLanding(
            Camera.main != null ? Camera.main.transform.position : targetPosition, fallbackRotation);

        Quaternion rotation = fallbackRotation;
        for (int i = 0; i < 4; i++)
        {
            Vector3 predictedCameraPosition = targetPosition + rotation * cameraFollow.FollowOffset;
            rotation = GetRotationLookingAtLanding(predictedCameraPosition, rotation);
        }
        return rotation;
    }

    Quaternion GetRotationLookingAtLanding(Vector3 viewpoint, Quaternion fallbackRotation)
    {
        Vector3 lookDirection = aimLandingPoint - viewpoint;
        return lookDirection.sqrMagnitude > 1e-4f
            ? Quaternion.LookRotation(lookDirection.normalized, cameraUp)
            : fallbackRotation;
    }

    void UpdateJumpInput()
    {
        if (!IsStuck)
        {
            IsAiming = false;
            return;
        }

        if (Input.GetButtonDown("Jump"))
        {
            IsAiming = true;
            velocity = Vector3.zero;
            cameraForward = Vector3.ProjectOnPlane(cameraForward, normal).normalized;
            if (cameraForward.sqrMagnitude < 1e-4f) cameraForward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
        }

        if (IsAiming && Input.GetButtonUp("Jump")) LaunchJump();
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

    void LaunchJump()
    {
        IsAiming = false;
        hasAimLandingPoint = false;
        airForward = GetJumpDirection();
        velocity = GetJumpVelocity();
        IsStuck = false;
        airUntil = Time.time + 0.2f;
        cameraForward = airForward;
        SetAimVisualsVisible(false);
    }

    Vector3 GetJumpDirection()
    {
        Vector3 direction = Vector3.ProjectOnPlane(cameraForward, normal);
        if (direction.sqrMagnitude < 1e-4f) direction = Vector3.ProjectOnPlane(transform.forward, normal);
        return direction.normalized;
    }

    Vector3 GetJumpVelocity()
    {
        float pitchRadians = cameraPitch * Mathf.Deg2Rad;
        Vector3 aimedDirection =
            GetJumpDirection() * Mathf.Cos(pitchRadians) -
            normal * Mathf.Sin(pitchRadians);
        Vector3 jumpVelocity = normal * jumpForce + aimedDirection * moveSpeed;

        float outwardSpeed = Vector3.Dot(jumpVelocity, normal);
        if (outwardSpeed < Skin)
            jumpVelocity += normal * (Skin - outwardSpeed);

        return jumpVelocity;
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
        Quaternion target;
        if (IsStuck)
        {
            target = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;
        }
        else
        {
            Vector3 targetUp = Vector3.up;
            Vector3 targetForward = airForward;
            if (Vector3.Cross(targetUp, targetForward).sqrMagnitude < 1e-5f)
                targetUp = transform.up;
            target = Quaternion.LookRotation(targetForward, targetUp);
        }
        float turnSpeed = IsStuck ? alignSpeed : jumpAlignSpeed;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }

    void UpdateAimCamera()
    {
        if (gameplayCamera == null && Camera.main == null) return;

        float goalFov = IsAiming ? aimFieldOfView : defaultFieldOfView;
        float blend = 1f - Mathf.Exp(-cameraFovSpeed * Time.deltaTime);
        if (gameplayCamera != null)
            gameplayCamera.Lens.FieldOfView = Mathf.Lerp(gameplayCamera.Lens.FieldOfView, goalFov, blend);
        else
            Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, goalFov, blend);
    }

    void InitializeAimVisuals()
    {
        var trajectoryObject = new GameObject("Jump Trajectory");
        trajectoryObject.transform.SetParent(transform, false);
        trajectoryRenderer = trajectoryObject.AddComponent<LineRenderer>();
        trajectoryRenderer.positionCount = 0;
        trajectoryRenderer.useWorldSpace = true;
        trajectoryRenderer.widthMultiplier = trajectoryWidth;
        trajectoryRenderer.textureMode = LineTextureMode.Stretch;
        trajectoryRenderer.numCornerVertices = 2;
        trajectoryRenderer.numCapVertices = 4;

        trajectoryMaterial = new Material(Shader.Find("Sprites/Default"));
        trajectoryMaterial.color = trajectoryColor;
        trajectoryRenderer.sharedMaterial = trajectoryMaterial;

        landingTexture = CreateLandingTexture();
        var markerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        markerObject.name = "Jump Landing Marker";
        markerObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        markerObject.transform.SetParent(transform, false);
        var markerCollider = markerObject.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);

        landingMarker = markerObject.GetComponent<MeshRenderer>();
        landingMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent"));
        landingMaterial.mainTexture = landingTexture;
        if (landingMaterial.HasProperty("_BaseColor")) landingMaterial.SetColor("_BaseColor", landingColor);
        else landingMaterial.color = landingColor;
        if (landingMaterial.HasProperty("_Surface"))
        {
            landingMaterial.SetFloat("_Surface", 1f);
            landingMaterial.SetFloat("_Blend", 0f);
            landingMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            landingMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            landingMaterial.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            landingMaterial.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            landingMaterial.SetInt("_ZWrite", 0);
            landingMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            landingMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        }
        landingMaterial.renderQueue = 3000;
        landingMarker.sharedMaterial = landingMaterial;
        SetAimVisualsVisible(false);
    }

    Texture2D CreateLandingTexture()
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float rings = Mathf.Pow(1f - Mathf.Repeat(distance * 3.5f, 1f), 8f);
                float edge = Mathf.Clamp01((1f - distance) * 7f);
                float alpha = (0.12f + rings * 0.65f) * edge;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    void UpdateTrajectory()
    {
        if (!IsAiming || trajectoryRenderer == null)
        {
            hasAimLandingPoint = false;
            SetAimVisualsVisible(false);
            return;
        }

        trajectoryPoints.Clear();
        Vector3 position = transform.position;
        Vector3 simulatedVelocity = GetJumpVelocity();
        trajectoryPoints.Add(position);
        RaycastHit landingHit = default;
        bool hasLanding = false;
        float elapsed = 0f;
        float stepTime = Mathf.Max(0.01f, trajectoryStep);

        while (elapsed < trajectoryDuration)
        {
            simulatedVelocity += Vector3.down * (gravity * stepTime);
            Vector3 step = simulatedVelocity * stepTime;
            float distance = step.magnitude;
            if (distance > 0.0001f &&
                Physics.SphereCast(position, radius, step / distance, out RaycastHit hit,
                    distance + Skin, surfaceMask, QueryTriggerInteraction.Ignore) &&
                elapsed >= 0.2f && hit.distance > Skin)
            {
                position = hit.point + hit.normal * Skin;
                trajectoryPoints.Add(position);
                landingHit = hit;
                hasLanding = true;
                break;
            }

            position += step;
            elapsed += stepTime;
            trajectoryPoints.Add(position);
        }

        trajectoryRenderer.positionCount = trajectoryPoints.Count;
        for (int i = 0; i < trajectoryPoints.Count; i++)
            trajectoryRenderer.SetPosition(i, trajectoryPoints[i]);
        trajectoryRenderer.enabled = true;
        landingMarker.enabled = hasLanding;
        hasAimLandingPoint = hasLanding;
        if (hasLanding)
        {
            aimLandingPoint = landingHit.point + landingHit.normal * 0.012f;
            landingMarker.transform.SetPositionAndRotation(
                aimLandingPoint,
                Quaternion.LookRotation(-landingHit.normal));
            float pulse = 0.9f + Mathf.Sin(Time.time * 5f) * 0.1f;
            landingMarker.transform.localScale = Vector3.one * (landingMarkerSize * pulse);
        }
    }

    void SetAimVisualsVisible(bool visible)
    {
        if (trajectoryRenderer != null) trajectoryRenderer.enabled = visible;
        if (landingMarker != null) landingMarker.enabled = visible;
    }

    void OnDisable()
    {
        IsAiming = false;
        hasAimLandingPoint = false;
        SetAimVisualsVisible(false);
        if (gameplayCamera != null && defaultFieldOfView > 0f) gameplayCamera.Lens.FieldOfView = defaultFieldOfView;
        else if (Camera.main != null && defaultFieldOfView > 0f) Camera.main.fieldOfView = defaultFieldOfView;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isActiveAndEnabled)
            CaptureCursor();
    }

    static void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDestroy()
    {
        if (trajectoryMaterial != null) Destroy(trajectoryMaterial);
        if (landingMaterial != null) Destroy(landingMaterial);
        if (landingTexture != null) Destroy(landingTexture);
    }
}
