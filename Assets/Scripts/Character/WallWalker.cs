using UnityEngine;

/// <summary>
/// Handles wall-walking mechanics with smooth surface transitions and gravity when not grounded.
/// Uses a spherecast for ground detection and smoothly rotates the character to align with surfaces.
/// </summary>
public class WallWalker : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public float gravity = 20f;
    
    [Header("Ground Detection")]
    public float groundCheckRadius = 0.5f;
    public float groundCheckDistance = 0.7f;
    public LayerMask groundLayer = -1; // All layers by default
    public float minSurfaceAngle = 0.5f; // Minimum angle (in radians) to consider a surface walkable

    [Header("Rotation Settings")]
    public float rotationSpeed = 10f;
    public float surfaceCheckInterval = 0.1f; // How often to check for new surfaces

    // Private variables
    private Vector3 velocity;
    private Vector3 currentNormal = Vector3.up;
    private bool isGrounded;
    private float lastSurfaceCheck;
    private CharacterController controller;
    private Transform cameraTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("WallWalker requires a CharacterController component!");
            enabled = false;
            return;
        }

        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
        {
            Debug.LogError("No main camera found!");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        CheckGrounded();
        HandleMovement();
        VisualizeDebug();
    }

    void CheckGrounded()
    {
        // Check if we're on any surface (ground or wall)
        RaycastHit hit;
        isGrounded = Physics.SphereCast(
            transform.position + Vector3.up * groundCheckRadius,
            groundCheckRadius,
            -currentNormal,
            out hit,
            groundCheckDistance,
            groundLayer
        );

        // Update surface normal if grounded
        if (isGrounded)
        {
            // Only update normal if we haven't checked recently
            if (Time.time - lastSurfaceCheck > surfaceCheckInterval)
            {
                // Check if the surface angle is walkable
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                if (angle < 89f) // Allow walking on almost vertical surfaces
                {
                    currentNormal = hit.normal;
                }
                lastSurfaceCheck = Time.time;
            }
        }
        else
        {
            // When in air, gradually rotate back to use world up as normal
            currentNormal = Vector3.Lerp(currentNormal, Vector3.up, Time.deltaTime * rotationSpeed);
        }
    }

    void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Calculate movement direction relative to camera and current surface
        Vector3 right = Vector3.Cross(cameraTransform.forward, currentNormal).normalized;
        Vector3 forward = Vector3.Cross(currentNormal, right).normalized;
        
        Vector3 moveDirection = (forward * vertical + right * horizontal).normalized;
        
        // Handle jumping
        if (isGrounded)
        {
            velocity = moveDirection * moveSpeed;
            
            if (Input.GetButtonDown("Jump"))
            {
                velocity += currentNormal * jumpForce;
                isGrounded = false;
            }
        }
        else
        {
            // Apply gravity relative to world up when in air
            velocity += Physics.gravity * Time.deltaTime;
            
            // Maintain some horizontal movement control in air
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 airMove = moveDirection * moveSpeed * 0.5f;
            velocity = Vector3.Lerp(horizontalVelocity, airMove, Time.deltaTime * 2f) + Vector3.Project(velocity, Vector3.up);
        }

        // Move the character
        controller.Move(velocity * Time.deltaTime);

        // Rotate the character to align with the surface
        if (moveDirection != Vector3.zero || !isGrounded)
        {
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, currentNormal) * transform.rotation;
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    void VisualizeDebug()
    {
        // Visualize ground check
        Debug.DrawLine(transform.position, transform.position - currentNormal * groundCheckDistance, isGrounded ? Color.green : Color.red);
        
        // Visualize current up direction
        Debug.DrawLine(transform.position, transform.position + currentNormal * 2f, Color.blue);
        
        // Visualize velocity
        Debug.DrawLine(transform.position, transform.position + velocity.normalized * 2f, Color.yellow);
    }
}
