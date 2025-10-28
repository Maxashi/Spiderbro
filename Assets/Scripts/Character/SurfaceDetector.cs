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
    public float groundCheckDistance = 0.7f;
    public float mainSampleRadius = 0.5f;
    public int numberOfPoints = 8;

    [Header("Main Sample Pattern")]
    public SamplePattern mainSamplePattern = SamplePattern.Grid;

    [Header("Circle pattern")]
    [SerializeField] public SamplingPass[] circleSamplingPasses;

    [Header("Grid pattern")]
    public UnityEngine.Vector3 gridSampleOffset;
    public float curvature;

    public LayerMask groundLayer = -1;

    [Header("Debug Variables")]
    public bool debugGizmos;
    [Range(0.01f, 0.05f)]
    public float samplePointHeadSize = 0.05f;


    public Mesh debugMeshPlane;
    public float debugMeshPlaneSize = 1f;

    public bool isGrounded;
    private int m_numberOfPoints = 8;
    private float m_sampleDepth;
    private float m_sampleRadius = 0.5f;
    private float m_groundCheckDistance = 0.7f;
    private float timeSinceLastCheck = 0f;
    public float surfaceCheckInterval = 0.1f;
    private Vector3 raycastTargetPoint;

    private SamplePoint[] gridSamplePoints;
    private SamplePoint[] circleSamplePoints;

    [Serializable]
    public struct SamplingPass
    {
        [Range(0, 32)]
        public int circleSampleCount;
        public float circleSampleRadius;
        public float circularDirectionAngle;
        public Vector3 circleSampleCenterOffset;
    }

    public struct SamplePoint
    {
        public Vector3 position;
        public Vector3 direction;
    }

    public enum SamplePattern
    {
        Grid,
        Sphere
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

    #region Initialize
    public void Initialize()
    {
        if (controller == null)
        {
            if (!TryGetComponent(out controller))
            {
                UnityEngine.Debug.LogError("ImprovedWallWalker requires a CharacterController component!");
                return;
            }
        }

        //set the target point for the raycasts to be directly below the player at a distance of sampleDepth
        raycastTargetPoint = Vector3.up * -sampleDepth;
        m_sampleRadius = mainSampleRadius;
        m_numberOfPoints = numberOfPoints;

        InitializeSamplepoints();
    }

    private void InitializeSamplepoints()
    {
        switch (mainSamplePattern)
        {
            case SamplePattern.Grid:
                InitializeGroundSampleGrid();
                break;
            case SamplePattern.Sphere:
                InitializeGroundSampleSphere();
                break;
            default:
                InitializeGroundSampleSphere();
                break;
        }

        InitializeCircleSamplePoints();
    }

    // Create a circular pattern of sample points around the character
    private void InitializeCircleSamplePoints()
    {
        if (circleSamplingPasses == null || circleSamplingPasses.Length == 0)
        {
            return;
        }

        // first get the total amount of sample points needed
        var totalAmount = 0;
        foreach (var item in circleSamplingPasses)
            totalAmount += item.circleSampleCount;

        // then create the array to hold them
        circleSamplePoints = new SamplePoint[totalAmount];

        // Track the current index across all passes
        int currentIndex = 0;

        // each pass defines a different set of circle sample points
        foreach (var pass in circleSamplingPasses)
        {
            float angleStep = 360f / pass.circleSampleCount;

            for (int i = 0; i < pass.circleSampleCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                circleSamplePoints[currentIndex] = new SamplePoint
                {
                    position = direction * pass.circleSampleRadius + pass.circleSampleCenterOffset,
                    direction = Quaternion.Euler(pass.circularDirectionAngle, angle, 0) * Vector3.forward
                };
                currentIndex++; // Increment for each point added
            }
        }
    }

    private void InitializeGroundSampleGrid()
    {
        isGrounded = false;
        timeSinceLastCheck = 0f;
        float baseY = Mathf.Max(controller.height, 0.3f);

        // Set the grid size and spacing
        // Calculate number of points along one axis based on total number of points
        float square = Mathf.Sqrt(numberOfPoints);
        int pointsPerAxis = Mathf.CeilToInt(square);
        gridSamplePoints = new SamplePoint[pointsPerAxis * pointsPerAxis];
        float spacing = (pointsPerAxis > 1) ? (mainSampleRadius * 2) / (pointsPerAxis - 1) : 0;

        for (int x = 0; x < pointsPerAxis; x++)
        {
            for (int z = 0; z < pointsPerAxis; z++)
            {
                // Calculate normalized position (-0.5 to 0.5)
                float normalizedX = (x / (float)(pointsPerAxis - 1)) - 0.5f;
                float normalizedZ = (z / (float)(pointsPerAxis - 1)) - 0.5f;

                // Calculate distance from center (0 at center, 1 at max corner)
                float distanceFromCenter = Mathf.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ);

                // Apply quadratic function to y value
                // Points at the center will have baseY, points at edges will be lower
                float quadraticFactor = distanceFromCenter * distanceFromCenter;
                float y = baseY * (1f + quadraticFactor * curvature);

                // Calculate position for each point in the grid
                Vector3 position = new Vector3(
                    (x * spacing) - (pointsPerAxis - 1) * spacing * 0.5f,
                    y,
                    (z * spacing) - (pointsPerAxis - 1) * spacing * 0.5f
                );
                gridSamplePoints[x + z * pointsPerAxis] = new SamplePoint
                {
                    position = MainSampleCenterOffset - position,

                    direction = (MainSampleCenterOffset - position).normalized
                };
            }
        }
    }

    private void InitializeGroundSampleSphere()
    {
        isGrounded = false;
        timeSinceLastCheck = 0f;

        var sphereRadius = mainSampleRadius;
        gridSamplePoints = new SamplePoint[numberOfPoints];
        float offset = 2f / numberOfPoints;
        float increment = Mathf.PI * (3f - Mathf.Sqrt(5f)); // Golden angle in radians

        for (int i = 0; i < numberOfPoints; i++)
        {
            float y = -Mathf.Abs((i * offset) - 1 + (offset / 2));
            float r = Mathf.Sqrt(1 - y * y);

            float phi = i * increment;

            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;

            Vector3 pointOnSphere = MainSampleCenterOffset + new Vector3(x, y, z) * sphereRadius;

            gridSamplePoints[i] = new SamplePoint
            {
                position = pointOnSphere,

                // the direction should point away from the center of the sphere 
                direction = (pointOnSphere - MainSampleCenterOffset).normalized
            };
        }

    }

    #endregion
    public void Update()
    {
        CheckUpdatedVariables();
        CheckGrounded();
        Debug();
        timeSinceLastCheck += Time.deltaTime;
    }

    /// <summary>
    /// This function generates sample vectors on a sphere around the character, centered at the transform's position.
    /// </summary>
    private void CheckGrounded()
    {
        int hitCount = 0;
        var averageNormal = Vector3.zero;

        foreach (SamplePoint samplePoint in gridSamplePoints)
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
            CurrentNormal = (averageNormal / hitCount).normalized;
        }
    }

    private bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = 1f)
    {
        // Normalize direction to ensure consistent behavior
        direction = direction.normalized;

        if (Physics.Raycast(origin, direction, out hit, maxDistance, groundLayer))
        {
            if (debugGizmos)
            {
                // Hit occurred - draw green ray to hit point and show normal
                UnityEngine.Debug.DrawLine(origin, hit.point, Color.green, 0.1f, true);

                // Draw normal at hitPoint 
                UnityEngine.Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.cyan, 0.01f, true);
            }
            return true;
        }

        // No hit - draw red ray showing full length
        if (debugGizmos)
        {
            UnityEngine.Debug.DrawRay(origin, direction * maxDistance, Color.red, 0.1f, true);
        }

        hit = default;
        return false;
    }

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

        if (m_sampleDepth != sampleDepth)
        {
            m_sampleDepth = sampleDepth;
            // Update the raycast target point when sampleDepth changes
            raycastTargetPoint = Vector3.up * -sampleDepth;
        }

        // Update sample radius if changed
        if (m_sampleRadius != mainSampleRadius || m_numberOfPoints != numberOfPoints)
        {
            m_sampleRadius = mainSampleRadius;
            m_numberOfPoints = numberOfPoints;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        // Draw the normal of the current surface

        Gizmos.DrawLine(transform.position, transform.position + CurrentNormal * sampleDepth);

        DrawPlaneGizmo();

        //Draw the samplePoint setup
        if (!Application.isPlaying && debugGizmos)
        {
            if (gridSamplePoints != null)
            {
                Gizmos.color = Color.red;
                foreach (SamplePoint point in gridSamplePoints)
                    DrawRayGizmos(point, Color.red);
            }


            if (circleSamplePoints != null)
            {
                foreach (SamplePoint point in circleSamplePoints)
                    DrawRayGizmos(point, Color.blue);

            }
        }
    }

    private void DrawRayGizmos(SamplePoint point, Color color)
    {
        // Draw the ground check sphere
        Gizmos.color = color;
        var pos = transform.position + point.position;

        Gizmos.DrawSphere(transform.position + point.position, samplePointHeadSize);
        Gizmos.DrawRay(pos, point.direction * groundCheckDistance);
    }


    // Draw the ground check plane gizmo
    private void DrawPlaneGizmo()
    {
        // Draw the ground check plane
        Gizmos.color = Color.yellow;
        var rot = Quaternion.LookRotation(transform.forward, CurrentNormal);
        var center = transform.position + MainSampleCenterOffset;
        center.y -= controller.height;

        Gizmos.DrawMesh(debugMeshPlane, center, rot, Vector3.one * debugMeshPlaneSize);
    }

    void Debug()
    {
        // Visualize current up direction
        UnityEngine.Debug.DrawLine(transform.position, transform.position + CurrentNormal * 2f, Color.blue);

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
