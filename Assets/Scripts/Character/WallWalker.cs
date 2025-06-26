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
    public int surfaceSamplePoints = 8;

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

        surfaceSamplePoints = 8;

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
    /// This function generates sample vectors on a sphere around the character.
    /// It initializes the samplePoints array with evenly distributed points
    /// based on the specified surfaceSamplePoints count around the downward normal.
    /// The points are calculated in a circular pattern around the icosahedron's base.
    /// </summary>
    void InitializeSampleVectors()
    {
        samplePoints = new Vector3[surfaceSamplePoints];
        // Generate sample points on a sphere using the Fibonacci sphere algorithm
        float phi = Mathf.PI * (Mathf.Sqrt(5f) - 1f); // golden angle in radians

        for (int i = 0; i < surfaceSamplePoints; i++)
        {
            float y = 1f - (i / (float)(surfaceSamplePoints - 1)) * 2f; // y goes from 1 to -1
            float radius = Mathf.Sqrt(1f - y * y); // radius at y

            float theta = phi * i; // golden angle increment

            float x = Mathf.Cos(theta) * radius;
            float z = Mathf.Sin(theta) * radius;

            samplePoints[i] = new Vector3(x, y, z) * m_sampleRadius;
        }
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
    void InitializeSamplePoints()
    {
        samplePoints = new Vector3[surfaceSamplePoints];
        var angleStep = 360f / surfaceSamplePoints;

        for (int i = 0; i < surfaceSamplePoints; i++)
        {
            var angle = i * angleStep * Mathf.Deg2Rad;
            samplePoints[i] = new Vector3(Mathf.Cos(angle) * m_sampleRadius, 0f, Mathf.Sin(angle) * m_sampleRadius);
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
        m_groundCheckRadius = characterHeight * groundCheckRadius; // Adjusted for gravity influence
        m_groundCheckDistance = characterHeight * groundCheckDistance;
        m_sampleRadius =
    }

    void HandleMouseLook()
    {
        var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Vertical rotation (pitch)
        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -maxLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0, 0);

        // Horizontal rotation (yaw)
        transform.Rotate(Vector3.up * mouseX);
    }

    void CheckGrounded()
    {
        isGrounded = false;
        var averageNormal = Vector3.zero;
        var hitCount = 0;

        // Check center point
        if (SamplePoint(transform.position, out RaycastHit centerHit))
        {
            averageNormal += centerHit.normal;
            hitCount++;
        }

        // Check points in a circle around the character
        foreach (Vector3 offset in samplePoints)
        {
            var samplePoint = transform.position + transform.TransformDirection(offset);
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
            var targetNormal = (Vector3.zero / hitCount).normalized;

            // Smoothly interpolate to the new normal
            currentNormal = Vector3.Slerp(currentNormal, targetNormal, Time.deltaTime * rotationSpeed);
        }
        else
        {
            // When in air, gradually rotate back to world up
            currentNormal = Vector3.Slerp(currentNormal, Vector3.up, Time.deltaTime * rotationSpeed);
        }
    }

    bool SamplePoint(Vector3 point, out RaycastHit hit)
    {
        var origin = point + Vector3.up * m_groundCheckRadius;

        var isHit = Physics.SphereCast(origin, m_groundCheckRadius,
            -currentNormal,
            out hit,
            m_groundCheckDistance
        );

        Debug.DrawLine(origin, hit.point, Color.cyan);
        var sampleColor = isHit ? Color.green : Color.red;
        var radius = isHit ? hit.distance : m_groundCheckDistance;
        DebugGizmos.DrawIcosphere(hit.point, radius, sampleColor);

        return isHit;
    }

    void HandleMovement()
    {
        // Get input relative to camera view
        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");

        // Calculate movement direction relative to camera and current surface
        var cameraForward = Vector3.ProjectOnPlane(playerCamera.forward, currentNormal).normalized;
        var cameraRight = Vector3.Cross(currentNormal, cameraForward).normalized;

        var moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;

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
            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            var airMove = moveDirection * moveSpeed * 0.5f;
            velocity = Vector3.Lerp(horizontalVelocity, airMove, Time.deltaTime * 2f)
                      + Vector3.Project(velocity, Vector3.up);
        }

        // Move the character
        controller.Move(velocity * Time.deltaTime);

        // Align character with surface
        if (moveDirection != Vector3.zero || !isGrounded)
        {
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, currentNormal)
                                      * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                                Time.deltaTime * rotationSpeed);
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
