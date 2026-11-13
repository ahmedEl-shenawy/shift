using UnityEngine;

public class CameraRig : MonoBehaviour
{
    public Transform target;
    public Vector3 focusPoint;
    public float yaw = 30f, pitch = 52f, distance = 13f;
    public bool orbit;
    public float minY = -1.5f;

    Vector3 focus, focusVel;
    float shake;
    float curDistance;

    public void Snap()
    {
        focus = Desired();
        curDistance = distance;
        Apply(0);
    }

    public void Shake(float amount) => shake = Mathf.Max(shake, amount);

    Vector3 Desired()
    {
        Vector3 f = target != null ? target.position + Vector3.up * 0.6f : focusPoint;
        f.y = Mathf.Max(f.y, minY);
        return f;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (orbit) yaw += 7f * dt;
        focus = Vector3.SmoothDamp(focus, Desired(), ref focusVel, target != null ? 0.18f : 0.6f, Mathf.Infinity, dt);
        curDistance = Mathf.Lerp(curDistance, distance, 1 - Mathf.Exp(-3f * dt));
        Apply(dt);
    }

    void Apply(float dt)
    {
        var rot = Quaternion.Euler(pitch, yaw, 0);
        Vector3 pos = focus - rot * Vector3.forward * curDistance;
        if (shake > 0)
        {
            pos += Random.insideUnitSphere * shake * 0.25f;
            shake = Mathf.MoveTowards(shake, 0, dt * 2.5f);
        }
        transform.SetPositionAndRotation(pos, rot);
    }
}
