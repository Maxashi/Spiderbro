using System;
using UnityEngine;

/// <summary>
/// Improved wall-walking mechanics with multiple surface sampling points and mouse look.
/// Provides smoother transitions between surfaces and better camera control.
/// </summary>
public class ImprovedWallWalker : MonoBehaviour
{

    public float characterHeight;
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

    private float m_groundCheckRadius = 0.5f;
    private float m_groundCheckDistance = 0.7f;
    private float m_sampleRadius = 1f;

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
    private bool isGrounded;
    private float lastSurfaceCheck;
    private CharacterController controller;
    private float cameraPitch = 0f;
    private Vector3[] samplePoints;
    private Transform cameraHolder;

    void Start()
    {
        InitializeComponents();
        InitializeSamplePoints();
    }

    void InitializeComponents()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("ImprovedWallWalker requires a CharacterController component!");
            enabled = false;
            return;
        }

        playerCamera = Camera.main != null ? Camera.main.transform : null;
        if (playerCamera == null)
        {
            Debug.LogError("Player camera not assigned!");
            enabled = false;
            return;
        }

        characterHeight = controller.height;

        numberOfPoints = 8;

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
        if (circular)
        {
            InitializeSamplePointsCircular();
            CheckGroundCircular();
        }
        else
        {
            InitializeSamplePointsSpherical();
            CheckGroundSpherical();
        }
    }

    private void InitializeSamplePointsCircular()
    {
        samplePoints = new Vector3[numberOfPoints];
        float angleStep = 360f / numberOfPoints;

        for (int i = 0; i < numberOfPoints; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            samplePoints[i] = new Vector3(Mathf.Cos(angle) * m_sampleRadius, 0f, Mathf.Sin(angle) * m_sampleRadius);
        }
    }

    private void InitializeSamplePointsSpherical()
    {
        isGrounded = false;

        samplePoints = new Vector3[numberOfPoints];
        Vector3 down = transform.up * -1f; // Down direction relative to the character
        Vector3 center = transform.position;
        Vector3 axis = Vector3.Cross(down, Vector3.forward);
        if (axis.sqrMagnitude < 0.001f)
            axis = Vector3.Cross(down, Vector3.right);
        axis.Normalize();

        float angleStep = 360f / numberOfPoints;
        for (int i = 0; i < numberOfPoints; i++)
        {
            float angle = angleStep * i;
            Quaternion rot = Quaternion.AngleAxis(angle, down);
            Vector3 dir = rot * axis;
            Vector3 pointOnSphere = center + dir * m_sampleRadius;
            Vector3 normal = (pointOnSphere - center).normalized;
            samplePoints[i] = normal;
        }
    }

    /// <summary>
    /// This function generates sample vectors on a sphere around the character, centered at the transform's position.
    /// </summary>
    void CheckGroundSpherical()
    {
        int hitCount = 0;
        Vector3 averageNormal = Vector3.zero;

        foreach (Vector3 offset in samplePoints)
        {
            Vector3 samplePoint = transform.position + transform.TransformDirection(offset);
            bool isHit = Physics.Raycast(transform.position, samplePoint, out RaycastHit hit, m_groundCheckDistance);
            if (isHit)
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }
    }

    void CheckGrounded()
    {
        isGrounded = false;

        Vector3 averageNormal = Vector3.zero;
        int hitCount = 0;
        // Check center point
        if (SamplePoint(transform.position, out RaycastHit centerHit))
        {
            averageNormal += centerHit.normal;
            hitCount++;
        }

        // Check points in a circle around the character
        foreach (Vector3 offset in samplePoints)
        {
            Vector3 samplePoint = transform.position + transform.TransformDirection(offset);
            if (SamplePoint(samplePoint, out RaycastHit hit))
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }

        // Update surface normal if we found any valid surfaces
        if (hitCount > 0)
        {
            isGrounded = true;
            Vector3 targetNormal = (averageNormal / hitCount).normalized;

            // Smoothly interpolate to the new normal
            currentNormal = Vector3.Slerp(currentNormal, targetNormal, Time.deltaTime * rotationSpeed);
        }
        else
        {
            // When in air, gradually rotate back to world up
            currentNormal = Vector3.Slerp(currentNormal, Vector3.up, Time.deltaTime * rotationSpeed);
        }
    }

    /// <summary>
    /// This function generates sample points in a circular pattern around the character.
    void CheckGroundCircular()
    {
        isGrounded = false;

        Vector3 averageNormal = Vector3.zero;
        int hitCount = 0;

        foreach (Vector3 offset in samplePoints)
        {
            Vector3 samplePoint = transform.position + transform.TransformDirection(offset);
            bool isHit = Physics.Raycast(transform.position, samplePoint, out RaycastHit hit, m_groundCheckDistance);
            if (isHit)
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }
    }

    void Update()
    {
        HandleMouseLook();
        CheckGrounded();
        HandleMovement();
        CheckUpdatedVariables();
        VisualizeDebug();
    }

    private void CheckUpdatedVariables()
    {
        // Update ground check radius and distance if changed
        if (m_groundCheckRadius != groundCheckRadius || m_groundCheckDistance != groundCheckDistance)
        {
            m_groundCheckRadius = groundCheckRadius;
            m_groundCheckDistance = groundCheckDistance;
        }

        // Update sample radius if changed
        if (m_sampleRadius != sampleRadius)
        {
            m_sampleRadius = sampleRadius;
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

    bool SamplePoint(Vector3 point, out RaycastHit hit)
    {
        Vector3 origin = point + Vector3.up * m_groundCheckRadius;

        bool isHit = Physics.SphereCast(origin, m_groundCheckRadius,
            -currentNormal,
            out hit,
            m_groundCheckDistance
        );

        Debug.DrawLine(origin, hit.point, Color.cyan);
        Color sampleColor = isHit ? Color.green : Color.red;
        float radius = isHit ? hit.distance : m_groundCheckDistance;
        DebugGizmos.DrawIcosphere(hit.point, radius, sampleColor);

        return isHit;
    }

    void HandleMovement()
    {
        // Get input relative to camera view
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calculate movement direction relative to camera and current surface
        Vector3 cameraForward = Vector3.ProjectOnPlane(playerCamera.forward, currentNormal).normalized;
        Vector3 cameraRight = Vector3.Cross(currentNormal, cameraForward).normalized;

        Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;

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

    void VisualizeDebug()
    {
        // // Visualize surface sampling points
        // Color sampleColor = isGrounded ? Color.green : Color.red;
        // foreach (Vector3 offset in samplePoints)
        // {
        //     var samplePoint = transform.position + transform.TransformDirection(offset);
        //     Debug.DrawLine(samplePoint, samplePoint - currentNormal * m_groundCheckDistance, sampleColor);
        // }

        // Move the scene view camera locked to the character
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
        Debug.DrawLine(transform.position, transform.position + currentNormal * 2f, Color.blue);

        // Visualize movement direction
        Debug.DrawLine(transform.position, transform.position + velocity.normalized * 2f, Color.yellow);
    }

    void OnDisable()
    {
        // Restore cursor when disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
