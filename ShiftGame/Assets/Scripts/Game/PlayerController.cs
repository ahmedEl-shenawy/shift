using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5.2f;
    public float groundAccel = 45f, airAccel = 24f;
    public float gravity = -28f;
    public float jumpHeight = 1.45f;

    public bool controllable = true;
    public bool alive = true;
    public Transform cameraTransform;
    public Transform visual;       // squash/stretch + facing
    public Renderer[] eyeRenderers;
    public Material eyeMat;
    public Light glow;

    CharacterController cc;
    Vector3 vel;
    float coyote, jumpBuffer;
    bool wasGrounded;
    float squash, squashVel;
    float lastAirVy;

    public System.Action Jumped, Landed;

    public Vector3 Velocity => vel;
    public bool Grounded => cc != null && cc.isGrounded;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.height = 0.9f;
        cc.radius = 0.28f;
        cc.center = new Vector3(0, 0.45f, 0);
        cc.stepOffset = 0.2f;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.03f;
        cc.minMoveDistance = 0f;
    }

    public void Teleport(Vector3 p)
    {
        cc.enabled = false;
        transform.position = p;
        cc.enabled = true;
        vel = Vector3.zero;
    }

    public Bounds WorldBounds => new Bounds(transform.position + new Vector3(0, 0.47f, 0), new Vector3(0.5f, 0.86f, 0.5f));

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        if (alive)
        {
            Vector2 mv = controllable ? GameInput.Move : Vector2.zero;
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (cameraTransform != null)
            {
                fwd = cameraTransform.forward; fwd.y = 0; fwd.Normalize();
                right = cameraTransform.right; right.y = 0; right.Normalize();
            }
            Vector3 wish = right * mv.x + fwd * mv.y;
            if (GameInput.SimActive) wish = controllable ? GameInput.SimWorldMove : Vector3.zero;
            if (wish.sqrMagnitude > 1) wish.Normalize();

            bool grounded = cc.isGrounded;
            Vector3 hv = new Vector3(vel.x, 0, vel.z);
            hv = Vector3.MoveTowards(hv, wish * speed, (grounded ? groundAccel : airAccel) * dt);

            if (grounded)
            {
                coyote = 0.11f;
                if (vel.y < 0) vel.y = -3f;
            }
            else coyote -= dt;

            if (controllable && GameInput.JumpPressed) jumpBuffer = 0.14f; else jumpBuffer -= dt;

            if (jumpBuffer > 0 && coyote > 0)
            {
                vel.y = Mathf.Sqrt(2f * -gravity * jumpHeight);
                jumpBuffer = 0; coyote = 0;
                squash = -0.28f;
                Jumped?.Invoke();
            }

            float g = gravity;
            if (vel.y > 0 && !GameInput.JumpHeld) g *= 2.2f;  // variable jump height
            else if (vel.y < 0) g *= 1.25f;                   // snappier fall
            vel.y = Mathf.Max(vel.y + g * dt, -30f);
            vel.x = hv.x; vel.z = hv.z;

            if (!grounded) lastAirVy = vel.y;
            var flags = cc.Move(vel * dt);
            if ((flags & CollisionFlags.Above) != 0 && vel.y > 0) vel.y = 0;

            if (cc.isGrounded && !wasGrounded && lastAirVy < -3f)
            {
                squash = Mathf.Clamp(-lastAirVy * 0.03f, 0.1f, 0.35f);
                Landed?.Invoke();
            }
            wasGrounded = cc.isGrounded;

            if (hv.sqrMagnitude > 0.2f)
            {
                var look = Quaternion.LookRotation(new Vector3(hv.x, 0, hv.z));
                visual.rotation = Quaternion.Slerp(visual.rotation, look, 1 - Mathf.Exp(-14f * dt));
            }
        }

        // spring squash
        squashVel += (-squash * 260f - squashVel * 16f) * dt;
        squash += squashVel * dt;
        float sy = 1f - squash;
        float sxz = 1f + squash * 0.5f;
        visual.localScale = new Vector3(sxz, sy, sxz);

        // phase coloured eyes + glow
        var pc = Palette.Current;
        if (eyeMat != null) Mats.SetColor(eyeMat, pc, 3f);
        if (glow != null)
        {
            glow.color = pc;
            glow.intensity = Mathf.Lerp(0.6f, 2.2f, PhaseState.Smooth);
        }
    }

    public void Bump(float s) => squash = s;
}
