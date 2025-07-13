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
        isGrounded = false;
        if (circular)
        {
            samplePoints = SamplePattern.Circle(characterHeight * m_sampleRadius, numberOfPoints);
        }
        else
        {
            samplePoints = SamplePattern.Hemisphere(transform.up, characterHeight, numberOfPoints);
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
    void CheckGrounded()
    {
        if (m_circular != circular)
        {
            m_circular = circular;
            InitializeSamplePoints();
        }

        if (m_circular)
        {
            CheckGroundCircular();
        }
        else
        {
            CheckGroundSpherical();
        }
    }

    /// <summary>
    /// This function generates sample vectors on a sphere around the character, centered at the transform's position.
    /// </summary>
    void CheckGroundSpherical()
    {
        int hitCount = 0;
        var averageNormal = Vector3.zero;

        foreach (Vector3 point in samplePoints)
        {
            var samplePoint = GetLocationAtSamplePoint(point);

            bool isHit = Raycast(transform.position, -currentNormal, out RaycastHit hit, m_groundCheckDistance);
            if (isHit)
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }
    }

    /// <summary>
    /// This function generates sample points in a circular pattern around the character.
    void CheckGroundCircular()
    {
        isGrounded = false;

        var averageNormal = Vector3.zero;
        var hitCount = 0;

        foreach (Vector3 offset in samplePoints)
        {
            Vector3 samplePoint = transform.position + transform.TransformDirection(offset);
            samplePoint.y = transform.position.y + characterHeight / 2f;

            bool isHit = Raycast(samplePoint, -currentNormal, out RaycastHit hit, m_groundCheckDistance);
            if (isHit)
            {
                averageNormal += hit.normal;
                hitCount++;
            }
        }
    }

    private bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = Mathf.Infinity)
    {
        Color color = Color.red;

        if (Physics.Raycast(origin, direction, out hit, characterHeight))
        {
            color = Color.green;
            Vector3 normalPoint = hit.point + hit.normal * characterHeight;
            UnityEngine.Debug.DrawLine(hit.point, normalPoint, Color.aliceBlue, 0.1f, true);
            return true;
        }

        UnityEngine.Debug.DrawLine(origin, hit.point, color, 0.1f, true);

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
        if (samplePoints != null)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 point in samplePoints)
            {
                Gizmos.DrawWireCube(GetLocationAtSamplePoint(point), Vector3.one * 0.05f);
            }
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
