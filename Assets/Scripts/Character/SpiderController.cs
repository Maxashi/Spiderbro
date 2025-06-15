using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


/// <summary>
/// Controls spider-like character movement, allowing for wall climbing and surface adaptation.
/// Uses raycasting to sample surface normals and adjusts character orientation accordingly.
/// </summary>
public class SpiderController : MonoBehaviour
{
    // Movement parameters
    public float _speed = 3f;                    // Base movement speed
    public float smoothness = 5f;                // Movement and rotation smoothing factor
    
    // Surface normal sampling parameters
    public int raysNb = 8;                       // Number of rays to cast for surface detection
    public float raysEccentricity = 0.2f;        // How far rays spread from the center
    public float outerRaysOffset = 2f;           // Offset for outer ring of rays
    public float innerRaysOffset = 25f;          // Offset for inner ring of rays
    public float normalInterpolationSpeed = 8f;   // How quickly character aligns to surface normal
    public float rotationSpeed = 10f;            // Character rotation speed

    private Vector3 velocity;
    private Vector3 lastVelocity;
    private Vector3 lastPosition;
    private Vector3 forward;
    private Vector3 upward;
    private Quaternion lastRot;
    private Vector3[] pn;
    private Vector3 targetUpward;


    /// <summary>
    /// Generates vertices for an icosphere of a given subdivision depth.
    /// Used for creating uniformly distributed sampling directions.
    /// </summary>
    /// <param name="depth">Subdivision depth of the icosphere</param>
    /// <returns>Array of vertex positions on the icosphere</returns>
    Vector3[] GetIcoSphereCoords(int depth)
    {
        Vector3[] res = new Vector3[(int)Mathf.Pow(4, depth) * 12];
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        res[0] = (new Vector3(t, 1, 0));
        res[1] = (new Vector3(-t, -1, 0));
        res[2] = (new Vector3(-1, 0, t));
        res[3] = (new Vector3(0, -t, 1));
        res[4] = (new Vector3(-t, 1, 0));
        res[5] = (new Vector3(1, 0, t));
        res[6] = (new Vector3(-1, 0, -t));
        res[7] = (new Vector3(0, t, -1));
        res[8] = (new Vector3(t, -1, 0));
        res[9] = (new Vector3(1, 0, -t));
        res[10] = (new Vector3(0, t, 1));
        res[11] = (new Vector3(0, -t, -1));

        return res;
    }

    /// <summary>
    /// Samples surface points and normals using an icosphere-based pattern.
    /// This provides more uniform sampling compared to radial patterns.
    /// </summary>
    /// <param name="point">Center point for sampling</param>
    /// <param name="up">Current up vector</param>
    /// <param name="halfRange">Half the total sampling range</param>
    /// <returns>Array containing average point[0] and normal[1]</returns>
    Vector3[] GetClosestPointIco(Vector3 point, Vector3 up, float halfRange)
    {
        Vector3[] res = new Vector3[2] { point, up };

        Vector3[] dirs = GetIcoSphereCoords(0);
        raysNb = dirs.Length;

        float amount = 1f;

        foreach (Vector3 dir in dirs)
        {
            RaycastHit hit;
            Ray ray = new Ray(point + up * 0.15f, dir);
            //Debug.DrawRay(ray.origin, ray.direction);
            if (Physics.SphereCast(ray, 0.01f, out hit, 2f * halfRange))
            {
                res[0] += hit.point;
                res[1] += hit.normal;
                amount += 1;
            }
        }
        res[0] /= amount;
        res[1] /= amount;
        return res;
    }

    /// <summary>
    /// Samples surface points and normals using a radial pattern.
    /// Performs two rings of samples at different offsets for better surface detection.
    /// </summary>
    /// <param name="point">Center point for sampling</param>
    /// <param name="forward">Current forward direction</param>
    /// <param name="up">Current up vector</param>
    /// <param name="halfRange">Half the total sampling range</param>
    /// <param name="eccentricity">How far rays spread from the center</param>
    /// <param name="offset1">Offset for inner ring of samples</param>
    /// <param name="offset2">Offset for outer ring of samples</param>
    /// <param name="rayAmount">Number of rays to cast</param>
    /// <returns>Array containing average point[0] and weighted normal[1]</returns>
    static Vector3[] GetClosestPoint(Vector3 point, Vector3 forward, Vector3 up, float halfRange, float eccentricity, float offset1, float offset2, int rayAmount)
    {
        Vector3[] res = new Vector3[2] { point, up };
        Vector3 right = Vector3.Cross(up, forward);
        float normalAmount = 0f;
        float positionAmount = 0f;
        Vector3 weightedNormal = Vector3.zero;

        // Calculate ray directions in a radial pattern
        Vector3[] dirs = new Vector3[rayAmount];
        float angularStep = 2f * Mathf.PI / (float)rayAmount;
        float currentAngle = angularStep / 2f;
        for (int i = 0; i < rayAmount; ++i)
        {
            // Create ray direction based on angle and eccentricity
            dirs[i] = -up + (right * Mathf.Cos(currentAngle) + forward * Mathf.Sin(currentAngle)) * eccentricity;
            dirs[i].Normalize();
            currentAngle += angularStep;
        }

        foreach (Vector3 dir in dirs)
        {
            RaycastHit hit;
            Vector3 largener = Vector3.ProjectOnPlane(dir, up);
            
            // Cast first ring of rays (inner ring)
            Ray ray = new Ray(point - (dir + largener) * halfRange + largener.normalized * offset1 / 100f, dir);
            Debug.DrawRay(ray.origin, ray.direction);
            if (Physics.SphereCast(ray, 0.01f, out hit, 2f * halfRange))
            {
                // Weight normal based on distance - closer hits have more influence
                float weight = 1.0f - (hit.distance / (2f * halfRange));
                weightedNormal += hit.normal * weight;
                res[0] += hit.point;
                normalAmount += weight;
                positionAmount += 1;
            }

            // Cast second ring of rays (outer ring)
            ray = new Ray(point - (dir + largener) * halfRange + largener.normalized * offset2 / 100f, dir);
            Debug.DrawRay(ray.origin, ray.direction, Color.green);
            if (Physics.SphereCast(ray, 0.01f, out hit, 2f * halfRange))
            {
                float weight = 1.0f - (hit.distance / (2f * halfRange));
                weightedNormal += hit.normal * weight;
                res[0] += hit.point;
                normalAmount += weight;
                positionAmount += 1;
            }
        }

        if (normalAmount > 0f)
        {
            res[1] = weightedNormal.normalized;
        }
        
        if (positionAmount > 0f)
        {
            res[0] /= positionAmount;
        }
        
        return res;
    }

    // Start is called before the first frame update
    void Start()
    {
        velocity = new Vector3();
        forward = transform.forward;
        upward = transform.up;
        lastRot = transform.rotation;
    }

    /// <summary>
    /// Performs physics-based movement and surface adaptation updates.
    /// Handles input processing, movement direction calculation, and character orientation.
    /// </summary>
    void FixedUpdate()
    {
        // Update velocity with smoothing
        velocity = (smoothness * velocity + (transform.position - lastPosition)) / (1f + smoothness);
        if (velocity.magnitude < 0.00025f)
            velocity = lastVelocity;
        lastPosition = transform.position;
        lastVelocity = velocity;

        // Handle sprint movement modifier
        float multiplier = 1f;
        if (Input.GetKey(KeyCode.LeftShift))
            multiplier = 2f;

        // Get movement input
        float valueY = Input.GetAxis("Vertical");
        float valueX = Input.GetAxis("Horizontal");

        // Calculate camera-relative movement direction
        var camera = Camera.main;
        var cameraForward = camera.transform.forward;
        var cameraRight = camera.transform.right;

        // Project camera vectors onto the plane perpendicular to current up vector
        Vector3 projectedForward = Vector3.ProjectOnPlane(cameraForward, upward).normalized;
        Vector3 projectedRight = Vector3.ProjectOnPlane(cameraRight, upward).normalized;

        var desiredMoveDirection = projectedForward * valueY + projectedRight * valueX;
        
        // Apply movement with proper speed control
        float currentSpeed = _speed * multiplier * Time.deltaTime;
        if (desiredMoveDirection.magnitude > 1f)
            desiredMoveDirection.Normalize();
            
        transform.position += desiredMoveDirection * currentSpeed;

        // Update surface adaptation and orientation
        if (valueX != 0 || valueY != 0 || Time.time < 1f) // Also update in the first second to ensure proper initialization
        {
            // Sample surface normals to determine orientation
            pn = GetClosestPoint(transform.position, transform.forward, upward, 0.5f, 0.1f, 30, -30, 8);
            
            // Update target upward direction based on surface normal
            if (pn[1] != Vector3.zero)
            {
                targetUpward = pn[1];
            }
            
            // Smoothly interpolate current upward direction towards target
            upward = Vector3.Slerp(upward, targetUpward, Time.deltaTime * normalInterpolationSpeed);
            upward.Normalize();

            // Update position based on surface sampling
            Vector3[] pos = GetClosestPoint(transform.position, transform.forward, upward, 0.5f, raysEccentricity, innerRaysOffset, outerRaysOffset, raysNb);
            transform.position = Vector3.Lerp(transform.position, pos[0], Time.deltaTime * smoothness);

            // Calculate new forward direction based on movement
            Vector3 moveDirection = desiredMoveDirection.normalized;
            if (moveDirection.magnitude < 0.01f)
                moveDirection = transform.forward;

            // Ensure movement direction is perpendicular to current up vector
            moveDirection = Vector3.ProjectOnPlane(moveDirection, upward).normalized;
            
            // Apply rotation to align with movement direction and surface normal
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, upward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        lastRot = transform.rotation;
    }
}
