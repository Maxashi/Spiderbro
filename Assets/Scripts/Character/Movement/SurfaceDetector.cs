using System;
using UnityEngine;
/// <summary>
/// Surface detection system for characters, using multiple sample points to determine ground contact and surface normals.
/// </summary>
public class SurfaceDetector : MonoBehaviour
{
    CharacterController controller;
    public Vector3 CurrentNormal { get; private set; } = Vector3.up;

    [Header("Ground Sampling Settings")]
    public Vector3 MainSampleCenterOffset;
    public float sampleDepth;
    public float downwardCheckDistance = 0.7f;
    public float downwardSampleRadius = 0.5f;
    public float forwardCheckDistance = 0.7f;
    public float forwardSampleRadius = 0.5f;



    public LayerMask groundLayer = -1;

    [Header("Debug Variables")]
    public bool debugGizmos;
    [Range(0.01f, 0.05f)]
    public float samplePointHeadSize = 0.05f;

    public Mesh debugMeshPlane;
    public float debugMeshPlaneSize = 1f;

    public bool isGrounded;
    private float m_sampleDepth;
    private float m_sampleRadius = 0.5f;
    private float m_groundCheckDistance = 0.7f;
    private float timeSinceLastCheck = 0f;
    public float surfaceCheckInterval = 0.1f;

    public struct SamplePoint
    {
        public Vector3 position;
        public Vector3 direction;
    }


    void Start()
    {
        Initialize();
    }

    void OnValidate()
    {
        CheckUpdatedVariables();
        Initialize();
    }

    public void Initialize()
    {
        if (controller == null)
        {
            if (!TryGetComponent(out controller))
            {
                Debug.LogError("ImprovedWallWalker requires a CharacterController component!");
                return;
            }
        }

        //set the target point for the raycasts to be directly below the player at a distance of sampleDepth
        m_sampleRadius = downwardSampleRadius;
    }


    public void Update()
    {
        CheckUpdatedVariables();
        CheckGrounded();
        DebugMovement();
        timeSinceLastCheck += Time.deltaTime;
    }

    private void CheckGrounded()
    {
        int hitCount = 0;
        var averageNormal = Vector3.zero;
        isGrounded = false; // Reset grounded state

        // Primary downward check for ground
        if (Physics.SphereCast(transform.position, downwardSampleRadius, -transform.up, out RaycastHit downHit, downwardCheckDistance, groundLayer))
        {
            hitCount++;
            isGrounded = true;
            averageNormal += downHit.normal;

            if (debugGizmos)
            {
                Debug.DrawLine(transform.position, downHit.point, Color.yellow, 0.125f, false);
            }
        }

        // Multi-directional checks for wall detection
        Vector3[] directions = { transform.forward, transform.right, -transform.right, -transform.forward };

        foreach (Vector3 direction in directions)
        {
            if (Physics.SphereCast(transform.position, forwardSampleRadius, direction, out RaycastHit hit, forwardCheckDistance, groundLayer))
            {
                // Accept any surface for spider-like movement
                isGrounded = true;
                hitCount++;

                // Weight hits based on how aligned they are with current movement
                float directionWeight = Vector3.Dot(direction, transform.forward) + 1f; // 0-2 range
                averageNormal += hit.normal * directionWeight;

                if (debugGizmos)
                {
                    Color debugColor = direction == transform.forward ? Color.green : Color.cyan;
                    Debug.DrawLine(transform.position, hit.point, debugColor, 0.125f, false);
                }
            }
        }

        // Additional check along current velocity for better surface following
        Vector3 velocityDir = GetComponent<ImprovedWallWalker>()?.velocity.normalized ?? Vector3.zero;
        if (velocityDir != Vector3.zero && velocityDir.magnitude > 0.1f)
        {
            if (Physics.SphereCast(transform.position, forwardSampleRadius * 0.7f, velocityDir, out RaycastHit velHit, forwardCheckDistance * 1.5f, groundLayer))
            {
                isGrounded = true;
                hitCount++;
                averageNormal += velHit.normal * 1.5f; // Higher weight for velocity direction

                if (debugGizmos)
                {
                    Debug.DrawLine(transform.position, velHit.point, Color.magenta, 0.125f, false);
                }
            }
        }

        if (isGrounded && hitCount > 0)
        {
            CurrentNormal = averageNormal.normalized;
        }
        else
        {
            // When not grounded, gradually return to world up
            CurrentNormal = Vector3.Slerp(CurrentNormal, Vector3.up, Time.deltaTime * 2f);
        }
    }

    private void CheckUpdatedVariables()
    {
        // Update ground check radius and distance if changed
        if (m_groundCheckDistance != downwardCheckDistance)
        {
            m_groundCheckDistance = downwardCheckDistance;
        }

        if (controller == null)
        {
            controller = GetComponentInChildren<CharacterController>();
        }

        if (m_sampleDepth != sampleDepth)
        {
            m_sampleDepth = sampleDepth;
        }

        // Update sample radius if changed
        if (m_sampleRadius != downwardSampleRadius)
        {
            m_sampleRadius = downwardSampleRadius;
        }
    }

    private void OnDrawGizmos()
    {
        //Draw the samplePoint setup
        if (Application.isPlaying && debugGizmos)
        {
            DrawPlaneGizmo();

        }
    }

    // Draw the ground check plane gizmo
    private void DrawPlaneGizmo()
    {
        // Draw the ground check plane
        var col = Color.yellow;
        col.a = 0.3f;
        Gizmos.color = col;

        var rot = Quaternion.LookRotation(transform.forward, CurrentNormal);
        var center = transform.position + MainSampleCenterOffset;
        center.y -= controller.height;

        Gizmos.DrawMesh(debugMeshPlane, center, rot, Vector3.one * debugMeshPlaneSize);
    }

    /// <summary>
    /// Check if movement in a direction would cause collision
    /// </summary>
    public bool CheckMovementCollision(Vector3 direction, float distance)
    {
        Vector3 castOrigin = transform.position + Vector3.up * (controller.radius + 0.1f);
        return Physics.CapsuleCast(
            castOrigin,
            castOrigin + Vector3.up * (controller.height - controller.radius * 2f),
            controller.radius * 0.9f, // Slightly smaller to avoid edge cases
            direction,
            distance,
            groundLayer
        );
    }

    /// <summary>
    /// Get the safe movement distance in a direction before hitting an obstacle
    /// </summary>
    public float GetSafeMovementDistance(Vector3 direction, float maxDistance)
    {
        if (!isGrounded)
        {
            // In air, use normal collision detection
            Vector3 castOrigin = transform.position + Vector3.up * (controller.radius + 0.1f);
            if (Physics.CapsuleCast(
                castOrigin,
                castOrigin + Vector3.up * (controller.height - controller.radius * 2f),
                controller.radius * 0.9f,
                direction,
                out RaycastHit hit,
                maxDistance,
                groundLayer))
            {
                return Mathf.Max(0f, hit.distance - controller.radius * 0.1f);
            }
            return maxDistance;
        }

        // When grounded, allow movement along surfaces with more lenient collision
        // Use smaller capsule cast to avoid getting stuck on small terrain variations
        Vector3 smallCastOrigin = transform.position + CurrentNormal * (controller.radius * 0.5f);
        if (Physics.CapsuleCast(
            smallCastOrigin,
            smallCastOrigin + CurrentNormal * (controller.height * 0.5f),
            controller.radius * 0.7f, // Smaller radius for surface following
            direction,
            out RaycastHit surfaceHit,
            maxDistance * 1.2f, // Allow slightly more distance for surface movement
            groundLayer))
        {
            // If we hit something, check if it's a surface we can walk on
            float surfaceAngle = Vector3.Angle(CurrentNormal, surfaceHit.normal);
            if (surfaceAngle < 45f) // Similar surface orientation, allow closer approach
            {
                return Mathf.Max(maxDistance * 0.8f, surfaceHit.distance - controller.radius * 0.05f);
            }
            return Mathf.Max(0f, surfaceHit.distance - controller.radius * 0.2f);
        }
        return maxDistance;
    }

    void DebugMovement()
    {
        // Visualize current up direction
        Debug.DrawLine(transform.position, transform.position + CurrentNormal * 2f, Color.blue);
    }
}
