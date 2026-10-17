using UnityEngine;

// Package A02, and the torso half of A01's directional movement. Runs after the animation has been
// evaluated (CharAnim evaluates in Update) and adjusts the finished pose:
//
//  - Feet: a foot the clip has planted is put with its ball on the ground under it, a lifted one
//    keeps its lift above that ground, the pelvis drops as far as the lower foot needs, and both
//    legs are solved with two-bone IK. On steps and slopes the planted foot meets the surface
//    instead of hovering over it or sinking into it, and the sole tilts to it (capped).
//  - The off hand: a two-handed weapon (the great axe) gets the left hand on its haft.
//  - Torso yaw: when the hips are turned toward the direction of travel (moving sideways at speed
//    while locked on), the spine and neck turn back by the same amount, so the chest and head still
//    face the target. The twist is spread over four joints.
//
// Measurements for the A02 acceptance are exposed: the sole's height error against the ground,
// and the off hand's distance from its grip.
[DefaultExecutionOrder(200)]
public class LimbPlacement : MonoBehaviour
{
    public const float MaxStep = 0.45f, FootRate = 2.6f, MaxTilt = 30f;
    static readonly float[] TwistShare = { 0.3f, 0.3f, 0.2f, 0.2f };

    Animator animator;
    Transform hips, lThigh, lShin, lFoot, lToe, rThigh, rShin, rFoot, rToe, lArm, lFore, lHand, rArmUpper, rFore, rHand;
    Transform[] spine;
    float pelvis, clearance;
    bool clearanceKnown;

    public bool FeetOn = true;
    // Degrees the chest turns back against the hips (set each frame by the controller).
    public float TorsoYaw;
    // The off-hand grip: a weapon, and how far from its hand-hold toward the butt the left hand closes.
    public Transform GripWeapon;
    // Along the haft from the right hand's hold (metres): the butt cap, the head's socket, and a
    // hand's width either side of the right hand that the left cannot share.
    public const float HaftButt = 0.28f, HaftReach = 0.52f, HandWidth = 0.11f;
    public bool GripOn;
    // Off, the grip is measured but not applied (the acceptance test's before).
    public bool GripAllowed = true;
    // How far the animated off hand is from its grip, before any correction (-1 when not gripping).
    public float GripGap { get; private set; } = -1f;
    Vector3 haftLocal; Transform haftOf;

    // For the A02 checks: worst sole error (m) against the ground under each foot this frame, and the
    // off hand's distance from its grip.
    public float SoleErrorLeft { get; private set; }
    public float SoleErrorRight { get; private set; }
    public float GripError { get; private set; } = -1f;
    public float Pelvis => pelvis;
    // Signed: how far each ball of the foot sits above (+) or in (-) the ground under it, and the
    // heights the feet were asked to move by.
    public float SoleAboveLeft { get; private set; }
    public float SoleAboveRight { get; private set; }
    public float RaiseLeft => raiseL;
    public float PlantedLeft => plantedL;
    public float PlantedRight => plantedR;
    public float RaiseRight => raiseR;

    public static LimbPlacement Bind(GameObject host, Animator animator)
    {
        if (animator == null || !animator.isHuman) return null;
        var p = host.GetComponent<LimbPlacement>() ?? host.AddComponent<LimbPlacement>();
        p.animator = animator;
        p.hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        p.lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); p.lShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        p.lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot); p.lToe = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? p.lFoot;
        p.rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg); p.rShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        p.rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot); p.rToe = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? p.rFoot;
        p.lArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); p.lFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        p.lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        p.rArmUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        p.rFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm); p.rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        p.spine = new[]
        {
            animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.Chest),
            animator.GetBoneTransform(HumanBodyBones.UpperChest), animator.GetBoneTransform(HumanBodyBones.Neck),
        };
        return p;
    }

    void LateUpdate()
    {
        if (animator == null || hips == null) return;
        float dt = Mathf.Min(GameClocks.WorldDelta, 0.05f);
        Twist();
        Feet(dt);
        Guard(dt);
        Grip();
    }

    // The chest turns back by TorsoYaw, spread over the spine; then the upper chest is corrected
    // until the shoulder line sits exactly where it would have with the hips square (a spine leaning
    // into a run turns less about the vertical than its joints are turned). The clip's own torso
    // movement is kept.
    void Twist()
    {
        ChestYawError = 0f;
        if (Mathf.Abs(TorsoYaw) < 0.05f || lArm == null || rArmUpper == null) return;
        Vector3 up = Vector3.up;
        float want = ShoulderYaw() + TorsoYaw;
        for (int i = 0; i < spine.Length; i++)
            if (spine[i] != null) spine[i].rotation = Quaternion.AngleAxis(TorsoYaw * TwistShare[i], up) * spine[i].rotation;
        Transform fix = spine[2] ?? spine[1];
        for (int pass = 0; pass < 3 && fix != null; pass++)
        {
            float error = Mathf.DeltaAngle(ShoulderYaw(), want);
            if (Mathf.Abs(error) < 0.2f) break;
            fix.rotation = Quaternion.AngleAxis(error, up) * fix.rotation;
        }
        ChestYawError = Mathf.DeltaAngle(ShoulderYaw(), want);
    }

    float ShoulderYaw()
    {
        Vector3 across = rArmUpper.position - lArm.position;
        return Mathf.Atan2(across.x, across.z) * Mathf.Rad2Deg;
    }

    // How far the shoulder line ended from where the twist meant it to be (degrees).
    public float ChestYawError { get; private set; }

    void Feet(float dt)
    {
        SoleErrorLeft = SoleErrorRight = 0f;
        if (lFoot == null || rFoot == null || lThigh == null || rThigh == null) return;
        Vector3 root = transform.position;
        WorldSupport.Surface under = default;
        bool grounded = WorldSupport.TryGround(root, out under, 0.3f, 0.35f);
        if (!grounded) { Relax(dt); hadToes = false; return; }
        // How high the root rides over level ground is learned there; on a slope the capsule sits
        // higher over the ground straight beneath it, so that is not the level the clips were made on.
        float above = root.y - under.point.y;
        if (under.normal.y > 0.995f) { clearance = clearanceKnown ? Mathf.Lerp(clearance, above, 0.2f) : above; clearanceKnown = true; }
        float level = root.y - (clearanceKnown ? clearance : above);

        bool hitL = WorldSupport.TryGround(new Vector3(lToe.position.x, root.y, lToe.position.z), out var surfL, MaxStep + 0.1f, MaxStep + 0.1f);
        bool hitR = WorldSupport.TryGround(new Vector3(rToe.position.x, root.y, rToe.position.z), out var surfR, MaxStep + 0.1f, MaxStep + 0.1f);
        float step = FootRate * Mathf.Max(dt, 1f / 120f);
        // Only the ground is smoothed, never the animation, so feet do not trail their own stride.
        groundL = Mathf.MoveTowards(groundL, Mathf.Clamp((hitL ? surfL.point.y : level) - level, -MaxStep, MaxStep), step);
        groundR = Mathf.MoveTowards(groundR, Mathf.Clamp((hitR ? surfR.point.y : level) - level, -MaxStep, MaxStep), step);
        if (!FeetOn) { SoleErrorLeft = Sole(lToe, surfL, hitL); SoleErrorRight = Sole(rToe, surfR, hitR); return; }

        // A foot the clip has planted goes onto the ground under it; a lifted one keeps its lift
        // above that ground. The pelvis drops as far as the lower foot needs.
        float speedL = dt > 0f && hadToes ? Flat(lToe.position - lastToeL).magnitude / dt : 0f;
        float speedR = dt > 0f && hadToes ? Flat(rToe.position - lastToeR).magnitude / dt : 0f;
        lastToeL = lToe.position; lastToeR = rToe.position; hadToes = true;
        float needL = Need(lToe, level, groundL, speedL, out plantedL), needR = Need(rToe, level, groundR, speedR, out plantedR);
        // The pelvis answers to the ground's shape only (a foot's ground lower than the level), never
        // to a clip's own relaxed foot, so level ground leaves the stance as it was made.
        pelvis = Mathf.MoveTowards(pelvis, Mathf.Min(0f, Mathf.Min(groundL, groundR)), step);
        if (Mathf.Abs(needL) < 0.002f && Mathf.Abs(needR) < 0.002f && Mathf.Abs(pelvis) < 0.002f)
        {
            SoleErrorLeft = Sole(lToe, surfL, hitL); SoleErrorRight = Sole(rToe, surfR, hitR);
            return;
        }
        hips.position += Vector3.up * pelvis;
        raiseL = needL - pelvis; raiseR = needR - pelvis;
        Leg(lThigh, lShin, lFoot, lToe, raiseL, hitL ? surfL.normal : Vector3.up, plantedL);
        Leg(rThigh, rShin, rFoot, rToe, raiseR, hitR ? surfR.normal : Vector3.up, plantedR);
        SoleErrorLeft = Sole(lToe, surfL, hitL); SoleErrorRight = Sole(rToe, surfR, hitR);
    }

    float groundL, groundR, raiseL, raiseR, plantedL, plantedR;
    // The animated toes last frame (before any placement), for how fast each moves through the world.
    Vector3 lastToeL, lastToeR;
    bool hadToes;
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // How far this ball of the foot must move: onto the ground when the clip has it down, or by the
    // ground's height when the clip has it lifted.
    float Need(Transform toe, float level, float ground, float worldSpeed, out float planted)
    {
        float lift = toe.position.y - level - SoleRest;
        // Standing means low and still: a relaxed foot can hover a few centimetres in its clip, but a
        // foot stepping in moves through the world while it is low, and keeps its lift.
        planted = (1f - Mathf.InverseLerp(0.06f, 0.14f, lift)) * (1f - Mathf.InverseLerp(0.4f, 1f, worldSpeed));
        float target = level + ground + SoleRest + Mathf.Max(0f, lift) * (1f - planted);
        return target - toe.position.y;
    }

    void Relax(float dt)
    {
        float step = FootRate * Mathf.Max(dt, 1f / 120f);
        pelvis = Mathf.MoveTowards(pelvis, 0f, step);
        groundL = Mathf.MoveTowards(groundL, 0f, step); groundR = Mathf.MoveTowards(groundR, 0f, step);
        if (Mathf.Abs(pelvis) > 0.002f) hips.position += Vector3.up * pelvis;
    }

    // How far a standing ball of the foot sits from the surface under it (0 when it is lifted).
    float Sole(Transform toe, WorldSupport.Surface surface, bool hit)
    {
        if (!hit) return 0f;
        float above = toe.position.y - surface.point.y;
        if (toe == lToe) SoleAboveLeft = above - SoleRest; else SoleAboveRight = above - SoleRest;
        // A foot more than 25 cm up is in its swing, not standing (a stair step is 18 cm).
        return above < 0.25f ? Mathf.Abs(above - SoleRest) : 0f;
    }

    // The ball of the foot's resting height over the ground in the library stance.
    public const float SoleRest = 0.008f;

    void Leg(Transform thigh, Transform shin, Transform foot, Transform toe, float raise, Vector3 normal, float planted)
    {
        if (Mathf.Abs(raise) < 0.001f) return;
        float toeGoal = toe.position.y + raise;
        Vector3 knee = shin.position - (thigh.position + foot.position) * 0.5f;
        TwoBoneIK.Solve(thigh, shin, foot, foot.position + Vector3.up * raise, knee);
        // A planted sole lies along the surface (within MaxTilt); a lifted one keeps its swing.
        if (planted <= 0f) return;
        Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.RotateTowards(Vector3.up, normal, MaxTilt * Mathf.Deg2Rad, 0f));
        foot.rotation = Quaternion.Slerp(Quaternion.identity, tilt, planted) * foot.rotation;
        // Tilting a turned-out foot to a slope also pitches its ball up or down: take the leg back
        // by what that moved, so the ball of the foot still lands where it was sent.
        float miss = toeGoal - toe.position.y;
        if (Mathf.Abs(miss) > 0.002f)
        {
            knee = shin.position - (thigh.position + foot.position) * 0.5f;
            TwoBoneIK.Solve(thigh, shin, foot, foot.position + Vector3.up * miss, knee);
        }
    }

    // The haft guard: there is no two-handed guard in the clip library, so it is posed here. The
    // right hand comes in front of the chest and turns the haft diagonally across the body, head high
    // on the left; Grip then closes the left hand on it. Eased in and out.
    public bool HaftGuard;
    public const float GuardEase = 9f;
    float guardWeight;
    public float GuardWeight => guardWeight;
    // The haft's direction the guard aims for, in the character's frame (right, up, forward).
    static readonly Vector3 GuardHaft = new Vector3(-0.55f, 0.78f, 0.28f), GuardHand = new Vector3(0.1f, -0.16f, 0.3f);
    public float GuardHaftError { get; private set; }

    void Guard(float dt)
    {
        guardWeight = Mathf.MoveTowards(guardWeight, HaftGuard && GripOn && GripWeapon != null ? 1f : 0f, GuardEase * Mathf.Max(dt, 1f / 120f));
        GuardHaftError = 0f;
        if (guardWeight <= 0f || rArmUpper == null || rFore == null || rHand == null || spine[2] == null || GripWeapon == null) return;
        Haft();
        float scale = transform.lossyScale.y;
        Vector3 right = transform.right, up = Vector3.up, fwd = transform.forward;
        Vector3 aim = (right * GuardHaft.x + up * GuardHaft.y + fwd * GuardHaft.z).normalized;
        Vector3 haft = GripWeapon.TransformDirection(haftLocal).normalized;
        rHand.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(haft, aim), guardWeight) * rHand.rotation;
        Vector3 at = spine[2].position + (right * GuardHand.x + up * GuardHand.y + fwd * GuardHand.z) * scale;
        TwoBoneIK.Solve(rArmUpper, rFore, rHand, Vector3.Lerp(rHand.position, at, guardWeight), -up + right * 0.6f);
        GuardHaftError = Vector3.Angle(GripWeapon.TransformDirection(haftLocal), aim);
    }

    // The haft is the prop's longest axis; it runs from the hand-hold toward the head.
    void Haft()
    {
        if (haftOf == GripWeapon) return;
        {
            haftOf = GripWeapon;
            var filter = GripWeapon.GetComponentInChildren<MeshFilter>();
            haftLocal = Vector3.up;
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds b = filter.sharedMesh.bounds;
                Vector3 size = b.size;
                int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                Vector3 v = Vector3.zero; v[axis] = 1f;
                if (Mathf.Abs(b.max[axis]) < Mathf.Abs(b.min[axis])) v = -v;
                haftLocal = filter.transform == GripWeapon ? v : GripWeapon.InverseTransformDirection(filter.transform.TransformDirection(v));
            }
        }
    }

    void Grip()
    {
        GripError = GripGap = -1f;
        if (!GripOn || GripWeapon == null || lArm == null || lFore == null || lHand == null) return;
        Haft();
        // The off hand rides the haft: it closes on the point of the grippable length (from the butt
        // cap to below the head, clear of the right hand) nearest the left shoulder, as a two-handed
        // axe grip slides between the guard and the swing.
        Vector3 head = GripWeapon.TransformDirection(haftLocal).normalized;
        float scale = transform.lossyScale.y;
        float along = Vector3.Dot(lArm.position - GripWeapon.position, head) / Mathf.Max(0.01f, scale);
        float below = Mathf.Clamp(along, -HaftButt, -HandWidth), above = Mathf.Clamp(along, HandWidth, HaftReach);
        float pick = Mathf.Abs(along - below) <= Mathf.Abs(along - above) ? below : above;
        Vector3 target = GripWeapon.position + head * pick * scale;
        GripGap = (lHand.position - target).magnitude;
        if (!GripAllowed) return;
        Vector3 elbow = lFore.position - (lArm.position + lHand.position) * 0.5f;
        TwoBoneIK.Solve(lArm, lFore, lHand, target, elbow.sqrMagnitude > 1e-6f ? elbow : -transform.up);
        GripError = (lHand.position - target).magnitude;
    }
}
