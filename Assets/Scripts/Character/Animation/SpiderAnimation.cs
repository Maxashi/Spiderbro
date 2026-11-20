using UnityEditor.Animations;
using UnityEngine;

public class SpiderAnimation : MonoBehaviour
{
    public Transform PlayerTransform;

    public Animator playerAnimator;
    public float smoothness;
    public float speed;
    [Range(0f, 1f)]
    public float airborneSpeedMultiplier = 0.08f;
    [SerializeField] private float velocity;
    private Vector3 lastBodyPos;
    private float lastVelocity;
    private ImprovedWallWalker movementController;

    void Start()
    {
        movementController = GetComponentInParent<ImprovedWallWalker>();
    }


    void FixedUpdate()
    {
        velocity = (transform.position - lastBodyPos).magnitude;
        velocity = (velocity + smoothness * lastVelocity) / (smoothness + 1f);

        if (velocity < 0.000025f)
            velocity = 0;
        else
            lastVelocity = velocity;
        float animationVelocity = velocity * speed;
        if (movementController != null && !movementController.IsStuck)
            animationVelocity *= airborneSpeedMultiplier;

        playerAnimator.speed = animationVelocity;
        playerAnimator.SetFloat("velocity", animationVelocity);


        lastBodyPos = transform.position;
    }

}
