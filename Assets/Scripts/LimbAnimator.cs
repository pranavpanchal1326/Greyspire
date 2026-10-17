using UnityEngine;

// Procedural animation for the Blender characters. The models are exported as
// separate parts (Body, Head, ArmL, ArmR, LegL, LegR) with their origins at
// the joints, so walking and attacking is just rotating those parts.
public class LimbAnimator : MonoBehaviour
{
    [HideInInspector] public float move;        // 0..1 how fast we're walking
    [HideInInspector] public float attack = -1; // 0..1 during a sword swing, -1 otherwise
    [HideInInspector] public int attackStyle;   // 0 overhead chop, 1 backhand sweep, 2 spin
    [HideInInspector] public float raise;       // 0..1 both arms overhead (heavy wind-up)
    [HideInInspector] public bool airborne;

    class Limb
    {
        public Transform t;
        public Quaternion rest;
        public Quaternion frame; // parent's rest orientation in model space
        public Vector3 restPos;
    }

    Transform root;
    Quaternion rootRest;
    Limb body, head, armL, armR, legL, legR;
    float phase;

    public void Bind(Transform model)
    {
        root = model;
        rootRest = model.localRotation;
        body = Find("Body");
        head = Find("Head");
        armL = Find("ArmL");
        armR = Find("ArmR");
        legL = Find("LegL");
        legR = Find("LegR");
    }

    Limb Find(string partName)
    {
        Transform t = FindDeep(root, partName);
        if (t == null) return null;
        return new Limb
        {
            t = t,
            rest = t.localRotation,
            restPos = t.localPosition,
            frame = Quaternion.Inverse(root.rotation) * t.parent.rotation,
        };
    }

    public static Transform FindDeep(Transform t, string partName)
    {
        if (t.name == partName) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform r = FindDeep(t.GetChild(i), partName);
            if (r != null) return r;
        }
        return null;
    }

    // Applies a rotation expressed in model space on top of the part's rest pose.
    static void Set(Limb l, Quaternion swing)
    {
        if (l == null) return;
        l.t.localRotation = Quaternion.Inverse(l.frame) * swing * l.frame * l.rest;
    }

    void Update()
    {
        if (root == null || GameClocks.HardPause) return;
        phase += GameClocks.WorldDelta * (3f + move * 10f);
        float s = Mathf.Sin(phase);

        float leg = s * 42f * move;
        if (airborne)
        {
            Set(legL, Quaternion.Euler(-30f, 0, 0));
            Set(legR, Quaternion.Euler(25f, 0, 0));
        }
        else
        {
            Set(legL, Quaternion.Euler(leg, 0, 0));
            Set(legR, Quaternion.Euler(-leg, 0, 0));
        }

        Quaternion left = Quaternion.Euler(-leg * 0.8f, 0, 0);
        Quaternion right = Quaternion.Euler(leg * 0.8f, 0, 0);
        float twist = 0f, spin = 0f;
        if (airborne) left = right = Quaternion.Euler(-50f, 0, 0);
        if (raise > 0f) left = right = Quaternion.Euler(-165f * raise, 0, 0);

        if (attack >= 0f)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(attack), 3f);
            if (attackStyle == 0)
            {
                right = Quaternion.Euler(-Mathf.Lerp(165f, 15f, e), 0, 0);
                twist = Mathf.Lerp(-30f, 30f, e);
            }
            else if (attackStyle == 1)
            {
                right = Quaternion.Euler(0, Mathf.Lerp(80f, -80f, e), 0) * Quaternion.Euler(-85f, 0, 0);
                twist = Mathf.Lerp(35f, -35f, e);
            }
            else
            {
                right = Quaternion.Euler(-90f, 0, 0);
                left = Quaternion.Euler(-60f, 0, 0);
                spin = e * 360f;
            }
        }

        Set(armL, left);
        Set(armR, right);
        Set(body, Quaternion.Euler(move * 10f, twist, 0));
        Set(head, Quaternion.Euler(Mathf.Sin(phase * 0.5f) * 3f - move * 5f, 0, 0));
        root.localRotation = Quaternion.Euler(0, spin, 0) * rootRest;

        if (body != null)
        {
            float bob = Mathf.Abs(s) * 0.05f * move + Mathf.Sin(phase * 0.6f) * 0.012f;
            body.t.localPosition = body.restPos + Quaternion.Inverse(body.frame) * Vector3.up * bob;
        }
    }
}
