using System;
using UnityEngine;

/// <summary>
/// Improved wall-walking mechanics with multiple surface sampling points and mouse look.
/// Provides smoother transitions between surfaces and better camera control.
/// </summary>
public class ImprovedWallWalker : MonoBehaviour
{
    public float characterHeight = 1f;
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public float gravity = 20f;

    [Header("Surface Detection")]
    public float groundCheckRadius = 0.5f;
    public float groundCheckDistance = 0.7f;
    public float sampleRadius = 1f;
    public LayerMask groundLayer = -1;

    public int numberOfPoints = 8;
    [SerializeField]
    private bool circular;
    private bool m_circular;

    private float m_groundCheckRadius = 0.5f;
    private float m_groundCheckDistance = 0.7f;
    private float m_sampleRadius = 1f;

    private int m_numberOfPoints = 8;

    [Header("Camera Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;
    public Transform playerCamera;

    [Header("Rotation Settings")]
    public float rotationSpeed = 10f;
    public float surfaceCheckInterval = 0.1f;

    // Private variables
    private Vector3 velocity;
    private Vector3 currentNormal = Vector3.up;

    private bool isOnSurface;
    [SerializeField]
    private bool isNearSurface;

    private float distanceToSurface;
    private CharacterController controller;
    private float cameraPitch = 0f;
    private Vector3[] samplePoints;
    private Transform cameraHolder;

    [SerializeField] private bool debugGroundCheck;
    [SerializeField] private bool debugMovement;
    private Vector3 moveDirection;

    void Start()
    {
        InitializeComponents();
        InitializeSamplePoints();
    }

    #region Initialize
    void InitializeComponents()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            UnityEngine.Debug.LogError("ImprovedWallWalker requires a CharacterController component!");
            return;
        }

        playerCamera = Camera.main != null ? Camera.main.transform : null;
        if (playerCamera == null)
        {
            UnityEngine.Debug.LogError("Player camera not assigned!");
            return;
        }

        characterHeight = controller.height;
        m_numberOfPoints = numberOfPoints;


        //TODO: Create a better Cinemachine camera
        // Create camera holder
        GameObject holder = new("CameraHolder");
        cameraHolder = holder.transform;
        cameraHolder.position = transform.position;
        cameraHolder.parent = transform;
        playerCamera.parent = cameraHolder;

        // Lock and hide cursor
        if (!Application.isEditor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Initializes sample points based on the selected sampling method (circular or spherical).
    /// </summary>
    void InitializeSamplePoints()
    {
        isNearSurface = false;

        // Create sample points in local space
        if (circular)
        {
            // Create points in a circle on the XZ plane
            samplePoints = new Vector3[numberOfPoints];
            float angleStep = 360f / numberOfPoints;

            for (int i = 0; i < numberOfPoints; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = Mathf.Sin(angle) * sampleRadius;
                float z = Mathf.Cos(angle) * sampleRadius;
                samplePoints[i] = new Vector3(x, 0, z);
            }
        }
        else
        {
            // Create points distributed on a hemisphere
            samplePoints = new Vector3[numberOfPoints];
            float goldenRatio = (1 + Mathf.Sqrt(5)) / 2;

            for (int i = 0; i < numberOfPoints; i++)
            {
                float t = (float)i / numberOfPoints;
                float inclination = Mathf.Acos(1 - 2 * t);
                float azimuth = 2 * Mathf.PI * goldenRatio * i;

                float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth) * sampleRadius;
                float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth) * sampleRadius;
                float z = Mathf.Cos(inclination) * sampleRadius;

                samplePoints[i] = new Vector3(x, y, z);
            }
        }
    }
    #endregion

    void Update()
    {
        CheckUpdatedVariables();
        HandleMouseLook();
        CheckGrounded();
        HandleMovement();
        Debug();
    }

    void OnValidate()
    {
        InitializeSamplePoints();
    }

    #region GroundCheck

    /// <summary>
    /// This function generates sample vectors on a sphere around the character, centered at the transform's position.
    /// </summary>
    void CheckGrounded()
    {
        int hitCount = 0;
        var averageNormal = Vector3.zero;

        foreach (Vector3 point in samplePoints)
        {
            // Get the sample point in world space
            var samplePoint = transform.position + transform.TransformDirection(point);
            // Direction is from character center toward the sample point
            var direction = (samplePoint - transform.position).normalized;

            bool isHit = Raycast(transform.position, direction, out RaycastHit hit, m_groundCheckDistance);
            if (isHit)
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }

        // Update ground state and normal direction
        isNearSurface = hitCount > 0;
        if (isNearSurface && hitCount > 0)
        {
            currentNormal = (averageNormal / hitCount).normalized;
        }
    }


    void CheckClosestDistanceToSurface()
    {
        float closestDistance = float.MaxValue;

        var samplePoint = transform.position;
        var direction = (samplePoint - transform.position).normalized;

        if (Raycast(transform.position, direction, out RaycastHit hit, m_groundCheckDistance))
        {
            float distance = Vector3.Distance(transform.position, hit.point);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }

        distanceToSurface = closestDistance;
    }

    private bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = 1f)
    {
        // Normalize direction to ensure consistent behavior
        direction = direction.normalized;

        if (Physics.Raycast(origin, direction, out hit, maxDistance, groundLayer))
        {
            if (debugGroundCheck)
            {
                // Hit occurred - draw green ray to hit point and show normal
                UnityEngine.Debug.DrawLine(origin, hit.point, Color.green, 0.1f, true);
                UnityEngine.Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.cyan, 0.1f, true);
            }
            return true;
        }

        // No hit - draw red ray showing full length
        if (debugGroundCheck)
        {
            UnityEngine.Debug.DrawRay(origin, direction * maxDistance, Color.red, 0.1f, true);
        }

        hit = default;
        return false;
    }
    #endregion

    private void CheckUpdatedVariables()
    {
        // Update ground check radius and distance if changed
        if (m_groundCheckRadius != groundCheckRadius || m_groundCheckDistance != groundCheckDistance)
        {
            m_groundCheckRadius = groundCheckRadius;
            m_groundCheckDistance = groundCheckDistance;
        }

        // Update sample radius if changed
        if (m_sampleRadius != sampleRadius || m_numberOfPoints != numberOfPoints)
        {
            m_sampleRadius = sampleRadius;
            m_numberOfPoints = numberOfPoints;
            InitializeSamplePoints();
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Vertical rotation (pitch)
        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0, 0);

        // Horizontal rotation (yaw)
        transform.Rotate(Vector3.up * mouseX);
    }



    void HandleMovement()
    {
        // Get input relative to camera view
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calculate movement direction relative to camera and current surface
        Vector3 playerForward = transform.forward;
        Vector3 playerRight = transform.right; // Use character's right direction for lateral movement

        moveDirection = (playerForward * vertical + playerRight * horizontal).normalized;

        if (isNearSurface)
        {
            // Apply movement along the surface
            velocity = moveDirection * moveSpeed;

            //when the spider is not tightly adhering to the surface, move them towards the surface
            if (!isOnSurface)
            {
                velocity -= currentNormal * Time.deltaTime;
            }

            // Handle jumping
            if (Input.GetButtonDown("Jump"))
            {
                velocity += currentNormal * jumpForce;
                isNearSurface = false;
            }
        }
        else
        {
            // Apply gravity when in air
            velocity += Physics.gravity * Time.deltaTime;

            // Allow some air control
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 airMove = moveDirection * moveSpeed * 0.5f;
            velocity = Vector3.Lerp(horizontalVelocity, airMove, Time.deltaTime * 2f) + Vector3.Project(velocity, Vector3.up);
        }

        // Move the character
        controller.Move(velocity * Time.deltaTime);

        // Align character with surface
        if (moveDirection != Vector3.zero || !isNearSurface)
        {
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, currentNormal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private Vector3 GetLocationAtSamplePoint(Vector3 point)
    {
        return transform.position + transform.TransformDirection(point);
    }

    private void OnDrawGizmos()
    {

        Gizmos.color = Color.green;
        // Draw the normal of the current surface
        Gizmos.DrawLine(transform.position, transform.position + currentNormal * characterHeight);


        // Draw sample points
        if (samplePoints != null && debugGroundCheck)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 point in samplePoints)
            {
                Gizmos.DrawWireCube(GetLocationAtSamplePoint(point), Vector3.one * 0.05f);
            }
        }

        if (debugMovement)
        {
            // Draw the character's movement direction
            Gizmos.color = Color.yellow;

            // Draw the movement direction line
            Gizmos.DrawLine(transform.position, transform.position + transform.TransformDirection(moveDirection) * 2f);
        }

    }
    void Debug()
    {
#if UNITY_EDITOR
        if (UnityEditor.SceneView.lastActiveSceneView != null)
        {
            Camera sceneCam = UnityEditor.SceneView.lastActiveSceneView.camera;
            if (sceneCam != null)
            {
                UnityEditor.SceneView.lastActiveSceneView.pivot = transform.position + currentNormal * characterHeight;
                UnityEditor.SceneView.lastActiveSceneView.Repaint();
            }
        }
#endif


        // Visualize current up direction
        UnityEngine.Debug.DrawLine(transform.position, transform.position + currentNormal * 2f, Color.blue);

        // Visualize movement direction
        UnityEngine.Debug.DrawLine(transform.position, transform.position + velocity.normalized * 2f, Color.yellow);
    }

    void OnDisable()
    {
        // Restore cursor when disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
