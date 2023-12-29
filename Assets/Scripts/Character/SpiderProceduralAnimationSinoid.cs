using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;
using DG.Tweening.Plugins.Core.PathCore;

public class SpiderProceduralAnimationSinoid : MonoBehaviour
{
    public Transform PlayerTransform;
    public SpiderLeg[] Legs;
    public Transform[] legTargets;
    [Range(0, 1.5f)]
    public float maxStepDistance;
    public int smoothness = 2;
    [Range(0.01f, 0.3f)]
    public float stepHeight = 0.1f;
    [Range(0f, 0.2f)]
    public float stepSize = 0.05f;
    public float progress;
    public float distanceTraveled;
    public int indexToMove = -1;
    private int lastIndexToMove;
    public Path gait;
    public float sphereCastRadius = 0.125f;
    public bool bodyOrientation = true;

    public float raycastRange = 1.5f;

    private Vector3 lastBodyUp;
    private int nbLegs;

    private Vector3 velocity;
    private Vector3 lastVelocity;
    private Vector3 lastBodyPos;

    [SerializeField, Range(-1, 1)]
    private float velocityMultiplier = 15f;

    public float targetPointSize = 0.1f;

    public bool immediateStep;

    public Vector3[] desiredPositions = new Vector3[8];
    public int[] legsToMove = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };


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

    float lastProgress = 0f;

    void FixedUpdate()
    {
        velocity = PlayerTransform.position - lastBodyPos;
        velocity = (velocity + smoothness * lastVelocity) / (smoothness + 1f);

        if (velocity.magnitude < 0.00025f)
        {
            velocity = lastVelocity;
        }
        else
        {
            distanceTraveled += Mathf.Abs(velocity.magnitude);
            lastVelocity = velocity;
        }

        //Loop over distance in the interval of stepsize from 0-1
        float progress = distanceTraveled % stepSize / stepSize;

        // check if progress has been reset
        if (progress < lastProgress)
        {
            // Find random leg to move
            indexToMove = FindIndexToMove();
            var legToMove = Legs[indexToMove];

            // relative default positionts to current player location
            legToMove.desiredLegPosition = PlayerTransform.TransformPoint(legToMove.defaultLegPosition);

            // Shift targetpoint towards the velocity vector multiplied by the distance to the desired position
            Vector3 targetPoint = legToMove.defaultLegPosition;

            // Calculate a surface point that matches the target point closest
            Vector3[] positionAndNormalFwd = MatchToSurfaceFromAbove(targetPoint, raycastRange, (transform.parent.up - velocity * 100).normalized);


            StartCoroutine(TweenStep(Legs[indexToMove], indexToMove, positionAndNormalFwd[0]));
        }


        lastBodyPos = PlayerTransform.position;
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

        // update lastProgress
        lastProgress = progress;
    }

    int FindIndexToMove()
    {
        var randomLegIndex = Random.Range(0, nbLegs);
        return randomLegIndex;
    }

    IEnumerator TweenStep(SpiderLeg leg, int index, Vector3 targetPoint)
    {
        leg.isMoving = true;
        Tween jumpTween = leg.legTarget.DOLocalJump(leg.defaultLegPosition, stepHeight, 1, 0.25f);
        yield return jumpTween.WaitForCompletion();

        // This will happen after the tween has completed
        leg.lastLegPosition = leg.legTarget.position;
        leg.isMoving = false;
        Debug.Log($"finished moving leg {index}");
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
        if (Legs == null || Legs.Length < 1)
            return;

        for (int i = 0; i < Legs.Length; ++i)
        {
            var leg = Legs[i];
            if (leg.legTarget == null)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(leg.desiredLegPosition, targetPointSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leg.legTarget.position, targetPointSize);
        }
    }
}
