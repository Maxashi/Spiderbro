using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class SpiderController : MonoBehaviour
{
    public float _speed = 3f;
    public float smoothness = 5f;
    public int raysNb = 8;
    public float raysEccentricity = 0.2f;
    public float outerRaysOffset = 2f;
    public float innerRaysOffset = 25f;
    public float normalInterpolationSpeed = 8f;
    public float rotationSpeed = 10f;

    private Vector3 velocity;
    private Vector3 lastVelocity;
    private Vector3 lastPosition;
    private Vector3 forward;
    private Vector3 upward;
    private Quaternion lastRot;
    private Vector3[] pn;
    private Vector3 targetUpward;


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

    static Vector3[] GetClosestPoint(Vector3 point, Vector3 forward, Vector3 up, float halfRange, float eccentricity, float offset1, float offset2, int rayAmount)
    {
        Vector3[] res = new Vector3[2] { point, up };
        Vector3 right = Vector3.Cross(up, forward);
        float normalAmount = 0f;
        float positionAmount = 0f;
        Vector3 weightedNormal = Vector3.zero;

        Vector3[] dirs = new Vector3[rayAmount];
        float angularStep = 2f * Mathf.PI / (float)rayAmount;
        float currentAngle = angularStep / 2f;
        for (int i = 0; i < rayAmount; ++i)
        {
            dirs[i] = -up + (right * Mathf.Cos(currentAngle) + forward * Mathf.Sin(currentAngle)) * eccentricity;
            dirs[i].Normalize();
            currentAngle += angularStep;
        }

        foreach (Vector3 dir in dirs)
        {
            RaycastHit hit;
            Vector3 largener = Vector3.ProjectOnPlane(dir, up);
            Ray ray = new Ray(point - (dir + largener) * halfRange + largener.normalized * offset1 / 100f, dir);
            Debug.DrawRay(ray.origin, ray.direction);
            if (Physics.SphereCast(ray, 0.01f, out hit, 2f * halfRange))
            {
                float weight = 1.0f - (hit.distance / (2f * halfRange)); // Weight based on distance
                weightedNormal += hit.normal * weight;
                res[0] += hit.point;
                normalAmount += weight;
                positionAmount += 1;
            }
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

    // Update is called once per frame
    void FixedUpdate()
    {

        velocity = (smoothness * velocity + (transform.position - lastPosition)) / (1f + smoothness);
        if (velocity.magnitude < 0.00025f)
            velocity = lastVelocity;
        lastPosition = transform.position;
        lastVelocity = velocity;

        float multiplier = 1f;
        if (Input.GetKey(KeyCode.LeftShift))
            multiplier = 2f;

        float valueY = Input.GetAxis("Vertical");
        float valueX = Input.GetAxis("Horizontal");

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

        if (valueX != 0 || valueY != 0 || Time.time < 1f) // Also update in the first second to ensure proper initialization
        {
            // Sample surface normals
            pn = GetClosestPoint(transform.position, transform.forward, upward, 0.5f, 0.1f, 30, -30, 8);
            
            // Update target upward direction
            if (pn[1] != Vector3.zero)
            {
                targetUpward = pn[1];
            }
            
            // Smoothly interpolate upward direction
            upward = Vector3.Slerp(upward, targetUpward, Time.deltaTime * normalInterpolationSpeed);
            upward.Normalize();

            // Get new position from surface sampling
            Vector3[] pos = GetClosestPoint(transform.position, transform.forward, upward, 0.5f, raysEccentricity, innerRaysOffset, outerRaysOffset, raysNb);
            transform.position = Vector3.Lerp(transform.position, pos[0], Time.deltaTime * smoothness);

            // Calculate rotation
            Vector3 moveDirection = desiredMoveDirection.normalized;
            if (moveDirection.magnitude < 0.01f)
                moveDirection = transform.forward;

            // Project movement direction onto the plane perpendicular to upward
            moveDirection = Vector3.ProjectOnPlane(moveDirection, upward).normalized;
            
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, upward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        lastRot = transform.rotation;
    }
}
