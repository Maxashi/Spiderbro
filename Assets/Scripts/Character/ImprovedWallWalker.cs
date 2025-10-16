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
    public float jumpForce = 2f;
    public float gravity = 10f;

    [Header("Surface Detection")]
    public float groundCheckRadius = 0.5f;
    public float groundCheckDistance = 0.7f;
    public float sampleRadius = 1f;
    public LayerMask groundLayer = -1;

    public int numberOfPoints = 8;
    [SerializeField]

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
    [SerializeField]
    private bool isGrounded;
    private float lastSurfaceCheck;
    private float timeSinceLastCheck = 0f;
    private CharacterController controller;
    private float cameraPitch = 0f;
    private SamplePoint[] samplePoints;
    private Transform cameraHolder;

    [SerializeField] private bool debugGroundCheck;
    [SerializeField] private bool debugMovement;
    private Vector3 moveDirection;

    public struct SamplePoint
    {
        public Vector3 position;
        public Vector3 direction;
    }
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

        sampleDepth = controller.height;
        m_numberOfPoints = numberOfPoints;

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

    public void InitializeSamplePoints()
    {
        if (grid)
        {
            InitializeSamplePointsGrid();
        }
        else
        {
            InitializeSamplePointsSpherical();
        }
    }
    /// <summary>
    /// Initializes sample points distributed over a hemisphere with specified radius and orientation.
    /// Uses the Fibonacci sphere algorithm for even distribution.
    /// </summary>
    void InitializeSamplePointsSpherical()
    {
        isGrounded = false;
        lastSurfaceCheck = 0f;

        samplePoints = new SamplePoint[numberOfPoints];

        // Calculate rotation to align hemisphere with the specified direction
        // Default hemisphere points down (-Y), rotate to match desired direction
        Quaternion rotation = Quaternion.Euler(Vector3.up);

        // Generate points using Fibonacci sphere algorithm (full sphere first)
        float offset = 2f / numberOfPoints;
        float increment = Mathf.PI * (3f - Mathf.Sqrt(5f)); // Golden angle in radians

            for (int i = 0; i < numberOfPoints; i++)
            {
            // Generate point on unit sphere
            float y = (i * offset) - 1 + (offset / 2);
            float r = Mathf.Sqrt(1 - y * y);
            float phi = i * increment;

            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;

            // Create point on unit sphere, but only use lower hemisphere (y <= 0)
            // This creates points in the -Y direction by default
            Vector3 pointOnUnitSphere = new Vector3(x, -Mathf.Abs(y) * -1f, z);

            // Apply rotation to orient hemisphere in the desired direction
            Vector3 rotatedPoint = rotation * pointOnUnitSphere;

            // Scale by sample radius
            Vector3 finalPosition = rotatedPoint * sampleRadius;

            samplePoints[i] = new SamplePoint
            {
                position = finalPosition,
                direction = rotatedPoint.normalized
            };
        }
    }

    private void InitializeSamplePointsGrid()
    {
        isGrounded = false;
        lastSurfaceCheck = 0f;
        float y = Mathf.Max(controller.height * 1.25f, 0.1f);

        // Set the grid size and spacing
        // Calculate number of points along one axis based on total number of points
        float square = Mathf.Sqrt(numberOfPoints);
        int pointsPerAxis = Mathf.CeilToInt(square);
        samplePoints = new SamplePoint[pointsPerAxis * pointsPerAxis];
        float spacing = (pointsPerAxis > 1) ? (sampleRadius * 2) / (pointsPerAxis - 1) : 0;

        for (int x = 0; x < pointsPerAxis; x++)
        {
            for (int z = 0; z < pointsPerAxis; z++)
            {
                // Calculate position for each point in the grid
                Vector3 position = new Vector3(
                    (x * spacing) - (pointsPerAxis - 1) * spacing * 0.5f,
                    y,
                    (z * spacing) - (pointsPerAxis - 1) * spacing * 0.5f
                );
                samplePoints[x + z * pointsPerAxis] = new SamplePoint
                {
                    position = position,
                    direction = (position - (Vector3.up * sampleDepth)).normalized
                };
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

        foreach (SamplePoint samplePoint in samplePoints)
        {
            // Get the sample point in world space
            var position = transform.position + transform.TransformDirection(samplePoint.position);
            // Direction is from character center toward the sample point
            var direction = (position - transform.position).normalized;

            bool isHit = Raycast(transform.position, samplePoint.direction, out RaycastHit hit, m_groundCheckDistance);
            if (isHit)
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }

        // Update ground state and normal direction
        isGrounded = hitCount > 0;
        if (isGrounded && hitCount > 0)
        {
            currentNormal = (averageNormal / hitCount).normalized;
        }
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
        if (m_groundCheckDistance != groundCheckDistance)
        {
            m_groundCheckDistance = groundCheckDistance;
        }

        if (controller == null)
        {
            controller = GetComponentInChildren<CharacterController>();
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

        if (isGrounded)
        {
            // Apply movement along the surface
            velocity = moveDirection * moveSpeed;

            // Handle jumping
            if (Input.GetButtonDown("Jump"))
            {
                velocity += currentNormal * jumpForce;
                isGrounded = false;
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
        if (moveDirection != Vector3.zero || !isGrounded)
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
        Gizmos.DrawLine(transform.position, transform.position + currentNormal * sampleDepth);


        // Draw sample points
        if (samplePoints != null && debugGroundCheck)
        {
            Gizmos.color = Color.red;
            foreach (SamplePoint point in samplePoints)
            {
                Gizmos.DrawWireCube(GetLocationAtSamplePoint(point.position), Vector3.one * 0.05f);
                Gizmos.DrawLine(transform.position + point.position, transform.position + transform.TransformDirection(point.direction) * groundCheckDistance);
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
                UnityEditor.SceneView.lastActiveSceneView.pivot = transform.position + currentNormal * sampleDepth;
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
