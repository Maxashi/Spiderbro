using UnityEngine;
/// <summary>
/// Surface detection system for characters, using multiple sample points to determine ground contact and surface normals.
/// </summary>
public class SurfaceDetector : MonoBehaviour
{
    Transform playerTransform;
    CharacterController controller;
    public Vector3 CurrentNormal { get; private set; } = Vector3.up;
    public int numberOfPoints = 8;
    public float sampleDepth;
    public float groundCheckDistance = 0.7f;
    public bool debugGizmos;
    [Header("Sample Pattern")]
    public SamplePattern samplePattern = SamplePattern.Grid;
    public float sampleRadius = 0.5f;
    public LayerMask groundLayer = -1;

    public bool isGrounded;

    private int m_numberOfPoints = 8;
    private float m_sampleRadius = 0.5f;
    private float m_groundCheckDistance = 0.7f;
    private float m_sampleDepth;
    public float curvature;
    private float timeSinceLastCheck = 0f;
    public float surfaceCheckInterval = 0.1f;
    private Vector3 raycastTargetPoint;
    private SamplePoint[] samplePoints;

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
        Initialize();
        CheckUpdatedVariables();
    }


    public void Initialize()
    {
        if (!TryGetComponent(out controller))
        {
            UnityEngine.Debug.LogError("ImprovedWallWalker requires a CharacterController component!");
            return;
        }

        //set the target point for the raycasts to be directly below the player at a distance of sampleDepth
        raycastTargetPoint = Vector3.up * -sampleDepth;
        playerTransform = controller.transform;
        m_sampleRadius = sampleRadius;
        m_numberOfPoints = numberOfPoints;

        InitializeSamplepoints();
    }

    private void InitializeSamplepoints()
    {
        switch (samplePattern)
        {
            case SamplePattern.Grid:
                InitializeGroundSampleGrid();
                break;
            case SamplePattern.Sphere:
                InitializeGroundSampleSphere();
                break;
            default:
                InitializeGroundSampleGrid();
                break;
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
        samplePoints = new SamplePoint[pointsPerAxis * pointsPerAxis];
        float spacing = (pointsPerAxis > 1) ? (sampleRadius * 2) / (pointsPerAxis - 1) : 0;

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
                samplePoints[x + z * pointsPerAxis] = new SamplePoint
                {
                    position = position,

                    direction = (raycastTargetPoint - position).normalized
                };
            }
        }
    }
    private void InitializeGroundSampleSphere()
    {
        isGrounded = false;
        timeSinceLastCheck = 0f;

        var sphereRadius = sampleRadius;
        samplePoints = new SamplePoint[numberOfPoints];
        float offset = 2f / numberOfPoints;
        float increment = Mathf.PI * (3f - Mathf.Sqrt(5f)); // Golden angle in radians

        for (int i = 0; i < numberOfPoints; i++)
        {
            float y = ((i * offset) - 1) + (offset / 2);
            float r = Mathf.Sqrt(1 - y * y);

            float phi = i * increment;

            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;

            Vector3 pointOnSphere = new Vector3(x, y, z) * sphereRadius;

            samplePoints[i] = new SamplePoint
            {
                position = pointOnSphere,
                direction = (raycastTargetPoint - pointOnSphere).normalized
            };
        }

    }

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

        foreach (SamplePoint samplePoint in samplePoints)
        {
            // Get the sample point in world space
            var position = playerTransform.position + playerTransform.TransformDirection(samplePoint.position);
            // Direction is from character center toward the sample point
            var direction = (position - playerTransform.position).normalized;

            bool isHit = Raycast(playerTransform.position, samplePoint.direction, out RaycastHit hit, m_groundCheckDistance);
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
        if (m_sampleRadius != sampleRadius || m_numberOfPoints != numberOfPoints)
        {
            m_sampleRadius = sampleRadius;
            m_numberOfPoints = numberOfPoints;
        }
    }

    private void OnDrawGizmos()
    {

        Gizmos.color = Color.green;
        // Draw the normal of the current surface
        Gizmos.DrawLine(transform.position, transform.position + CurrentNormal * sampleDepth);

        // Draw sample points
        if (samplePoints != null && debugGizmos)
        {
            Gizmos.color = Color.red;
            foreach (SamplePoint point in samplePoints)
            {

                var pos = transform.position + transform.TransformDirection(point.position);


                Gizmos.DrawWireCube(pos, Vector3.one * 0.05f);
                Gizmos.DrawRay(transform.position + point.position, point.direction * groundCheckDistance);

                //Gizmos.DrawLine(transform.position + point.position, transform.position + transform.TransformDirection(point.direction) * groundCheckDistance);
            }
        }


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
