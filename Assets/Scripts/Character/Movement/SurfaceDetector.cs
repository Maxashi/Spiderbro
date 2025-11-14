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
        Vector3 surfaceNormal = transform.up; // fallback

        // downward spherecast to check for ground directly 
        if (Physics.SphereCast(transform.position, downwardSampleRadius, -transform.up, out RaycastHit downHit, downwardCheckDistance, groundLayer))
        {
            hitCount++;
            isGrounded = true;
            averageNormal += downHit.normal;

            if (debugGizmos)
            {
                // Draw hit point
                Debug.DrawLine(transform.position, downHit.point, Color.yellow, 0.125f, false);
            }

            // Optionally snap position slightly to surface:
            Vector3 desiredPos = downHit.point + surfaceNormal * (controller.height * 0.5f - controller.radius);
            transform.position = Vector3.Lerp(transform.position, desiredPos, 0.2f);
        }


        //we also need a forward spherecast to allow for walking up slopes
        if (Physics.SphereCast(transform.position, forwardSampleRadius, transform.forward, out RaycastHit forwardHit, forwardCheckDistance, groundLayer))
        {
            isGrounded = true;
            hitCount++;
            averageNormal += forwardHit.normal * 2f;
            
            if (debugGizmos)
            {
                // Draw hit point
                Debug.DrawLine(transform.position, forwardHit.point, Color.softGreen, 0.125f, false);
            }
        }

        if (isGrounded && hitCount > 0)
        {
            CurrentNormal = (averageNormal).normalized;
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

    void DebugMovement()
    {
        // Visualize current up direction
        Debug.DrawLine(transform.position, transform.position + CurrentNormal * 2f, Color.blue);

        // Focus Scene view camera on the character's position
#if UNITY_EDITOR
        if (UnityEditor.SceneView.lastActiveSceneView != null)
        {
            Camera sceneCam = UnityEditor.SceneView.lastActiveSceneView.camera;
            if (sceneCam != null)
            {
                UnityEditor.SceneView.lastActiveSceneView.pivot = transform.position + CurrentNormal * sampleDepth;
                UnityEditor.SceneView.lastActiveSceneView.Repaint();
            }
        }
#endif
    }
}
