using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

/// <summary>
/// Improved wall-walking mechanics with mouse look.
/// Provides smoother transitions between surfaces and better camera control.
/// </summary>
public partial class ImprovedWallWalker : MonoBehaviour
{
    public CharacterController controller;
    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float jumpForce = 4f;
    public float gravity = 8f;

    [Header("Surface Detection")]
    [SerializeField] public SurfaceDetector surfaceDetector;

    [Header("Camera Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;
    public Transform playerCamera;

    [Header("Rotation Settings")]
    public float rotationSpeed = 10f;

    // Private variables
    public Vector3 velocity; // Made public for SurfaceDetector access

    private float cameraPitch = 0f;
    private Transform cameraHolder;
    private Vector3 moveDirection;
    public bool debugMovement;

    void Start()
    {
        InitializeComponents();
    }

    #region Initialize
    void InitializeComponents()
    {
        playerCamera = Camera.main != null ? Camera.main.transform : null;
        if (playerCamera == null)
        {
            UnityEngine.Debug.LogError("Player camera not assigned!");
            return;
        }

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
 
    #endregion

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        DebugMovement();
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

        if (surfaceDetector.isGrounded)
        {
            // Project movement onto surface to follow contours
            Vector3 surfaceMovement = Vector3.ProjectOnPlane(moveDirection, surfaceDetector.CurrentNormal).normalized;
            
            // Calculate movement relative to surface
            velocity = surfaceMovement * moveSpeed;

            // Add stronger surface adherence for wall walking
            float surfaceAngle = Vector3.Angle(Vector3.up, surfaceDetector.CurrentNormal);
            float adherenceForce = Mathf.Clamp01(surfaceAngle / 90f) * gravity * 0.3f; // Stronger on steeper surfaces
            velocity += -surfaceDetector.CurrentNormal * adherenceForce;

            // Handle jumping
            if (Input.GetButtonDown("Jump"))
            {
                velocity = surfaceDetector.CurrentNormal * jumpForce;
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

        // Move the character using CharacterController to prevent clipping
        Vector3 movement = velocity * Time.deltaTime;

        // For grounded movement, use less restrictive collision checking
        if (movement.magnitude > 0.001f && !surfaceDetector.isGrounded)
        {
            float safeDistance = surfaceDetector.GetSafeMovementDistance(movement.normalized, movement.magnitude);
            movement = movement.normalized * safeDistance;
        }
        else if (movement.magnitude > 0.001f && surfaceDetector.isGrounded)
        {
            // For surface movement, allow more freedom and let CharacterController handle collisions
            float safeDistance = surfaceDetector.GetSafeMovementDistance(movement.normalized, movement.magnitude);
            if (safeDistance < movement.magnitude * 0.5f) // Only restrict if collision is very close
            {
                movement = movement.normalized * safeDistance;
            }
        }

        // Use CharacterController.Move for proper collision handling
        controller.Move(movement);

        // Align character with surface - faster rotation for better wall walking
        if (surfaceDetector.isGrounded)
        {
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, surfaceDetector.CurrentNormal) * transform.rotation;
            float rotSpeed = moveDirection != Vector3.zero ? rotationSpeed * 2f : rotationSpeed; // Faster rotation when moving
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotSpeed);
        }
    }

    void OnDrawGizmos()
    {
        if (debugMovement)
        {
            // Draw the character's movement direction
            Gizmos.color = Color.yellow;

            // Draw the movement direction line
            Gizmos.DrawLine(transform.position, transform.position + transform.TransformDirection(moveDirection) * 2f);
        }
    }
    void DebugMovement()
    {

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
