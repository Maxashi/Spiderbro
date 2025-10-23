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
    private Vector3 velocity;

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
            // Apply movement along the surface
            velocity = moveDirection * moveSpeed;

            // Handle jumping
            if (Input.GetButtonDown("Jump"))
            {
                velocity += surfaceDetector.CurrentNormal * jumpForce;
                surfaceDetector.isGrounded = false;
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
        if (moveDirection != Vector3.zero || !surfaceDetector.isGrounded)
        {
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, surfaceDetector.CurrentNormal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
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
