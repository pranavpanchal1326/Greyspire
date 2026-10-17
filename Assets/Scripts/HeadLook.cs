using UnityEngine;

// People notice things: a character's head turns toward what has their attention,
// as far as a neck comfortably goes, on top of whatever the body is doing.
// Townsfolk watch the hero go by; the hero looks at the nearest threat, or at
// whoever he is about to speak to.
public class HeadLook : MonoBehaviour
{
    const float Range = 6.5f;
    const float MaxTurn = 62f;

    public bool hero;           // look where the game says the hero's attention is, instead of at the hero

    Transform head;
    Vector3 forwardInHead;      // the direction the face points, in the head bone's own space
    float weight;

    void Start()
    {
        Rebind();
    }

    // Finds the head again: call when the body under this object has been rebuilt.
    public void Rebind()
    {
        head = LimbAnimator.FindDeep(transform, "Head");
        if (head != null) forwardInHead = Quaternion.Inverse(head.rotation) * transform.forward;
        weight = 0f;
    }

    void LateUpdate()
    {
        if (head == null || GameClocks.HardPause) return;
        Vector3 point;
        bool has;
        if (hero)
        {
            has = Game.I.Attention(out point);
        }
        else
        {
            point = Game.I.Player.transform.position + Vector3.up * 1.6f;
            has = true;
        }

        Vector3 to = point - head.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        float range = hero ? 12f : Range;
        bool sees = has && to.magnitude < range && flat.magnitude > 0.5f && Vector3.Angle(transform.forward, flat) < 105f;
        weight = Mathf.MoveTowards(weight, sees ? 1f : 0f, GameClocks.WorldDelta * (hero ? 4f : 2.2f));
        if (weight < 0.001f || to.sqrMagnitude < 0.01f) return;

        Vector3 facing = head.rotation * forwardInHead;
        Vector3 want = Vector3.RotateTowards(transform.forward, to.normalized, MaxTurn * Mathf.Deg2Rad, 0f);
        Quaternion turn = Quaternion.FromToRotation(facing, want);
        head.rotation = Quaternion.Slerp(Quaternion.identity, turn, Mathf.SmoothStep(0f, 1f, weight) * 0.85f) * head.rotation;
    }
}
