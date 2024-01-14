using UnityEditor.Animations;
using UnityEngine;

public class SpiderAnimation : MonoBehaviour
{
    public Transform PlayerTransform;

    public Animator playerAnimator;
    public float smoothness;
    public float speed;
    [SerializeField] private float velocity;
    private Vector3 lastBodyPos;
    private float lastVelocity;

    void Start()
    {

    }


    void FixedUpdate()
    {
        velocity = (transform.position - lastBodyPos).magnitude;
        velocity = (velocity + smoothness * lastVelocity) / (smoothness + 1f);

        if (velocity < 0.000025f)
            velocity = lastVelocity;
        else
            lastVelocity = velocity;


        playerAnimator.speed = velocity * speed;

        lastBodyPos = transform.position;
    }

}
