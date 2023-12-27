using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class SpiderProceduralAnimationSinoid : MonoBehaviour
{
    [SerializeField]
    public Transform PlayerTransform;
    public SpiderLeg[] Legs;
    public Transform[] legTargets;
    [Range(0, 1.5f)]
    public float maxStepDistance;
    public int smoothness = 2;
    [Range(0, 03)]
    public float stepHeight = 0.1f;
    [Range(0f, 0.2f)]
    public float stepSize = 0.05f;
    public float progress;
    public float distanceTraveled;
    public int indexToMove = -1;
    private int lastIndexToMove;
    public AnimationCurve gait;
    public float sphereCastRadius = 0.125f;
    public bool bodyOrientation = true;

    public float raycastRange = 1.5f;

    private Vector3 lastBodyUp;
    private int nbLegs;

    private Vector3 velocity;
    private Vector3 lastVelocity;
    private Vector3 lastBodyPos;

    private float velocityMultiplier = 15f;

    public float targetPointSize = 0.1f;

    public bool immediateStep;

    public Vector3[] desiredPositions = new Vector3[8];
    public List<SpiderLeg> legsToMove = new();


    void Start()
    {
        lastBodyUp = transform.up;

        nbLegs = legTargets.Length;
        desiredPositions = new Vector3[nbLegs];
        Legs = new SpiderLeg[nbLegs];
        for (int i = 0; i < nbLegs; ++i)
        {
            Legs[i].legTarget = legTargets[i];
            Legs[i].defaultLegPosition = legTargets[i].localPosition;
            Legs[i].lastLegPosition = legTargets[i].position;
            Legs[i].isMoving = false;
        }
        lastBodyPos = transform.position;
    }



    void FixedUpdate()
    {
        velocity = transform.position - lastBodyPos;
        velocity = (velocity + smoothness * lastVelocity) / (smoothness + 1f);

        distanceTraveled += Mathf.Abs(velocity.magnitude);

        float progress = (distanceTraveled % stepSize) / stepSize;

        if (velocity.magnitude < 0.000025f)
            velocity = lastVelocity;
        else
            lastVelocity = velocity;

        float maxDistance = stepSize;

        // loop over all legs to find the ones to move based on their distance to the desired position
        for (int i = 0; i < nbLegs; ++i)
        {
            Legs[i].desiredLegPosition = transform.TransformPoint(Legs[i].defaultLegPosition);

            float distance = Vector3.ProjectOnPlane(desiredPositions[i] + velocity * velocityMultiplier - Legs[i].lastLegPosition, transform.up).magnitude;

            // Put the leg in to list of legs to move when its not full
            // and the distance is larger than stepsize
            if (distance >= stepSize)
            {
                if (legsToMove.Count <= 3 && legsToMove.Contains(Legs[i]) == false)
                {
                    legsToMove.Add(Legs[i]);
                }
            }
            else
            {
                if (legsToMove.Contains(Legs[i]) == true)
                {
                    legsToMove.Remove(Legs[i]);
                }
            }
        }

        // Calculate 
        indexToMove = Mathf.FloorToInt(progress * nbLegs);

        //Check if index is set to new value this frame and if it is still moving
        if (indexToMove != -1 && indexToMove != lastIndexToMove)
        {
            Vector3 targetPoint = Legs[indexToMove].desiredLegPosition + Mathf.Clamp(velocity.magnitude * velocityMultiplier, 0.0f, stepSize) * (Legs[indexToMove].desiredLegPosition - Legs[indexToMove].legTarget.position) + velocity * velocityMultiplier;

            Vector3[] positionAndNormalFwd = MatchToSurfaceFromAbove(targetPoint + velocity * velocityMultiplier, raycastRange, (transform.parent.up - velocity * 100).normalized);
            Vector3[] positionAndNormalBwd = MatchToSurfaceFromAbove(targetPoint + velocity * velocityMultiplier, raycastRange * (1f + velocity.magnitude), (transform.parent.up + velocity * 75).normalized);

            Legs[indexToMove].isMoving = true;

            if (positionAndNormalFwd[1] == Vector3.zero)
            {
                StartCoroutine(PerformStep(Legs[indexToMove], indexToMove, positionAndNormalBwd[0]));
            }
            else
            {
                StartCoroutine(PerformStep(Legs[indexToMove], indexToMove, positionAndNormalFwd[0]));
            }
        }


        lastBodyPos = transform.position;
        lastIndexToMove = indexToMove;
        if (nbLegs > 3 && bodyOrientation)
        {
            Vector3 v1 = Legs[0].legTarget.position - Legs[1].legTarget.position;
            Vector3 v2 = Legs[2].legTarget.position - Legs[4].legTarget.position;
            Vector3 normal = Vector3.Cross(v1, v2).normalized;
            Vector3 up = Vector3.Lerp(lastBodyUp, normal, 1f / (float)(smoothness + 1));
            transform.up = up;
            transform.rotation = Quaternion.LookRotation(transform.parent.forward, up);
            lastBodyUp = transform.up;
        }
    }

    IEnumerator PerformStep(SpiderLeg leg, int index, Vector3 targetPoint)
    {
        Debug.Log($"start moving leg {indexToMove}");
        leg.isMoving = true;

        Vector3 startPos = leg.lastLegPosition;

        for (int i = 1; i <= smoothness; ++i)
        {
            legTargets[index].position = Vector3.Lerp(startPos, targetPoint, i / (float)(smoothness + 1f));
            leg.legTarget.position += transform.up * Mathf.Sin(i / (float)(smoothness + 1f) * Mathf.PI) * stepHeight;

            if (i == smoothness)
            {
                leg.legTarget.position = targetPoint;
                leg.lastLegPosition = leg.legTarget.position;
                leg.isMoving = false;
                if (legsToMove.Contains(leg) == true)
                {
                    legsToMove.Remove(leg);
                }
                Debug.Log($"finished moving leg {indexToMove}");
            }

            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForFixedUpdate();
    }

    Vector3[] MatchToSurfaceFromAbove(Vector3 point, float halfRange, Vector3 up)
    {
        Vector3[] res = new Vector3[2];
        res[1] = Vector3.zero;
        Ray ray = new(point + halfRange * up / 2f, -up);

        if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, 2f * halfRange))
        {
            res[0] = hit.point;
            res[1] = hit.normal;
        }
        else
        {
            res[0] = point;
        }
        return res;
    }

    private void OnDrawGizmos()
    {

        for (int i = 0; i < Legs.Length; ++i)
        {
            var leg = Legs[i];
            if (leg.legTarget == null)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leg.legTarget.position, targetPointSize);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(leg.desiredLegPosition, targetPointSize);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.TransformPoint(leg.defaultLegPosition), stepSize);
        }
    }
}
