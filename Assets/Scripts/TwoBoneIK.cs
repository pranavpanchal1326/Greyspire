using UnityEngine;

// Analytic two-bone IK (thigh-shin-foot, upper arm-forearm-hand). Used at edit time to author the
// side steps (A01) and every frame to place feet and the off hand (A02).
//
// The joint bends toward bendHint (a knee forward, an elbow down and out), and the upper bone is
// turned as a whole frame, its "front" carried onto that hint, so its twist is fully decided by the
// hint rather than left to whatever a from-to rotation happens to produce. That keeps successive
// frames continuous, which is what lets a solved pose be stored as humanoid muscle curves.
public static class TwoBoneIK
{
    // Moves end toward target (as far as the limb reaches), keeping end's world rotation.
    // Returns the distance left over.
    public static float Solve(Transform upper, Transform middle, Transform end, Vector3 target, Vector3 bendHint, float weight = 1f)
    {
        if (upper == null || middle == null || end == null || weight <= 0f) return 0f;
        Vector3 a = upper.position, b = middle.position, c = end.position;
        target = Vector3.Lerp(c, target, Mathf.Clamp01(weight));
        float upperLength = (b - a).magnitude, lowerLength = (c - b).magnitude;
        Vector3 toTarget = target - a;
        if (toTarget.sqrMagnitude < 1e-8f || upperLength < 1e-5f || lowerLength < 1e-5f) return (c - target).magnitude;
        float reach = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 1e-4f, upperLength + lowerLength - 1e-4f);
        Vector3 aim = toTarget.normalized;
        Vector3 bend = Perp(bendHint, aim);
        if (bend.sqrMagnitude < 1e-8f) bend = Perp(b - a, aim);
        if (bend.sqrMagnitude < 1e-8f) return (c - target).magnitude;
        bend.Normalize();
        float cos = Mathf.Clamp((upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * upperLength * reach), -1f, 1f);
        Vector3 joint = a + upperLength * (cos * aim + Mathf.Sqrt(1f - cos * cos) * bend);

        // The upper bone's current frame: along the bone, and its front (where the joint points).
        Vector3 along0 = b - a, front0 = Perp(Perp(b - a, c - a), along0);
        if (front0.sqrMagnitude < 1e-8f) front0 = Perp(bendHint, along0);
        Vector3 along1 = joint - a, front1 = Perp(bend, along1);
        Quaternion endRotation = end.rotation;
        if (front0.sqrMagnitude > 1e-8f && front1.sqrMagnitude > 1e-8f)
            upper.rotation = Quaternion.LookRotation(along1, front1) * Quaternion.Inverse(Quaternion.LookRotation(along0, front0)) * upper.rotation;
        else
            upper.rotation = Quaternion.FromToRotation(along0, along1) * upper.rotation;
        middle.rotation = Quaternion.FromToRotation(end.position - middle.position, target - middle.position) * middle.rotation;
        end.rotation = endRotation;
        return (end.position - target).magnitude;
    }

    static Vector3 Perp(Vector3 v, Vector3 axis)
    {
        Vector3 n = axis.normalized;
        return v - n * Vector3.Dot(v, n);
    }
}
