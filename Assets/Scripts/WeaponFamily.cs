using UnityEngine;

// One blow of a light combo. Every number the swing uses lives here, so the hit test,
// the drawn arc and the cancel window can never disagree about what a weapon does.
public readonly struct AttackStep
{
    public readonly float time;         // seconds at attack speed 1, before clip fitting
    public readonly float contact;      // fraction of the swing at which the blade connects
    public readonly float damage;       // multiplier on the hero's damage
    public readonly float reach;        // metres from the hero to the enemy's edge, before stats.reach
    public readonly float minDot;       // cosine of half the hit arc; -1 is a full circle
    public readonly float lunge;        // forward drive at the start of the swing
    public readonly float push;
    public readonly float stagger;      // seconds an ordinary enemy is held open by a hit
    public readonly bool heavy;         // breaks a raised guard
    public readonly bool thrust;        // hits a straight lane instead of an arc
    public AttackStep(float time, float contact, float damage, float reach, float minDot, float lunge, float push,
        float stagger = 0f, bool heavy = false, bool thrust = false)
    {
        this.time = time; this.contact = contact; this.damage = damage; this.reach = reach; this.minDot = minDot;
        this.lunge = lunge; this.push = push; this.stagger = stagger; this.heavy = heavy; this.thrust = thrust;
    }
    public bool Spin => minDot <= -1f;
    // The arc drawn in the air matches the arc that hits (never wider than the mesh can show).
    public float ArcDegrees => Spin ? 360f : Mathf.Clamp(2f * Mathf.Acos(Mathf.Clamp(minDot, -1f, 1f)) * Mathf.Rad2Deg, 60f, 300f);
}

public enum GuardStyle
{
    Shield,     // holds a guard, parries late, bashes out of it
    Haft,       // holds a guard with the axe's haft; a tighter parry and no bash
    Deflect,    // no held guard: a short deflection window that drops by itself
}

// A weapon family changes spacing and choices, not just damage (master plan section 6, C01).
public class WeaponFamily
{
    public string id, name, summary;
    public AttackStep[] steps;
    public GuardStyle guard;
    public float dodgeCancel;           // fraction of a light swing after which a dodge may cancel it
    public float parryWindow;           // seconds after raising the guard that count as a parry
    public float deflectHold;           // Deflect only: seconds before the guard drops by itself
    public float flankBonus;            // damage multiplier on hits from outside the enemy's front
    public float flankDot;              // enemy-forward dot at or below which a hit counts as a flank
    // Feedback: what the swing sounds and feels like.
    public float swingPitch, hitstop, finisherHitstop, shake, finisherShake;
    public Color edge;
    // A second sound under the shared swing and hit, so each family is recognisable by ear:
    // cue name, volume, pitch (an empty cue adds nothing).
    public string swingLayer = "", impactLayer = "";
    public float swingLayerVolume, swingLayerPitch = 1f, impactLayerVolume, impactLayerPitch = 1f;
    // The streak the weapon leaves through the air.
    public float trailWidth = 0.5f, trailTime = 0.11f;

    public bool CanBash => guard == GuardStyle.Shield;

    public static readonly WeaponFamily SwordShield = new WeaponFamily
    {
        id = "sword-shield", name = "Sword and shield",
        summary = "Reliable reach in front, a held guard, a late parry and a shield bash.",
        // These are the pre-C01 combo tables, unchanged: every earlier assertion was made against them.
        steps = new[]
        {
            new AttackStep(0.34f, 0.48f, 1f, 2.5f, 0.1f, 7f, 8f),
            new AttackStep(0.38f, 0.48f, 1.15f, 2.5f, 0.1f, 7f, 8f),
            new AttackStep(0.7f, 0.55f, 1.9f, 3.2f, -1f, 3f, 13f),
        },
        guard = GuardStyle.Shield, dodgeCancel = 0.55f, parryWindow = 0.25f,
        flankBonus = 1f, flankDot = -2f,
        swingPitch = 1f, hitstop = 0.045f, finisherHitstop = 0.09f, shake = 0.1f, finisherShake = 0.22f,
        edge = new Color(0.85f, 0.95f, 1f),
    };

    public static readonly WeaponFamily GreatAxe = new WeaponFamily
    {
        id = "great-axe", name = "Great axe",
        summary = "Wide, slow arcs that hold a crowd off; the third blow breaks guards. Late dodge cancel, tight parry, no bash.",
        steps = new[]
        {
            new AttackStep(0.42f, 0.56f, 1.15f, 2.6f, -0.34f, 4f, 11f, 0.25f),
            // The downward chop: narrower, lands early in a long, committed recovery, and staggers.
            new AttackStep(0.46f, 0.35f, 1.45f, 2.6f, 0.2f, 4f, 11f, 0.5f),
            new AttackStep(0.88f, 0.62f, 2.35f, 3.0f, -1f, 2f, 17f, 0.6f, heavy: true),
        },
        guard = GuardStyle.Haft, dodgeCancel = 0.72f, parryWindow = 0.16f,
        flankBonus = 1f, flankDot = -2f,
        swingPitch = 0.78f, hitstop = 0.07f, finisherHitstop = 0.13f, shake = 0.18f, finisherShake = 0.34f,
        edge = new Color(1f, 0.86f, 0.7f), trailWidth = 0.8f, trailTime = 0.16f,
        // A heavy, low rush of air, and a thud of weight under every hit.
        swingLayer = "dash", swingLayerVolume = 0.55f, swingLayerPitch = 0.62f,
        impactLayer = "slam", impactLayerVolume = 0.5f, impactLayerPitch = 1.15f,
    };

    public static readonly WeaponFamily LightBlade = new WeaponFamily
    {
        id = "light-blade", name = "Light blade",
        summary = "Short, narrow, quick cuts and a lunging thrust; strikes from the flank find weak points. Early dodge cancel, no held guard.",
        steps = new[]
        {
            new AttackStep(0.28f, 0.55f, 0.85f, 2.1f, 0.57f, 9f, 5f),
            new AttackStep(0.3f, 0.55f, 0.9f, 2.1f, 0.57f, 9f, 5f),
            new AttackStep(0.52f, 0.5f, 1.45f, 3.1f, 0.57f, 14f, 9f, 0.3f, thrust: true),
        },
        guard = GuardStyle.Deflect, dodgeCancel = 0.32f, parryWindow = 0.2f, deflectHold = 0.35f,
        flankBonus = 1.6f, flankDot = 0.3f,
        swingPitch = 1.25f, hitstop = 0.03f, finisherHitstop = 0.06f, shake = 0.06f, finisherShake = 0.14f,
        edge = new Color(0.78f, 1f, 0.92f), trailWidth = 0.28f, trailTime = 0.07f,
        // A short, bright whip through the air.
        swingLayer = "dash", swingLayerVolume = 0.35f, swingLayerPitch = 1.7f,
    };

    // Sol fights bare-handed with a System focus; that family is outside C01's three and keeps
    // the original tables so her behaviour does not change in this package.
    public static readonly WeaponFamily BareHands = new WeaponFamily
    {
        id = "bare-hands", name = "Bare hands and focus",
        summary = "The original balanced combo; the finisher sends a shockwave.",
        steps = SwordShield.steps,
        guard = GuardStyle.Shield, dodgeCancel = 0.55f, parryWindow = 0.25f,
        flankBonus = 1f, flankDot = -2f,
        swingPitch = 1f, hitstop = 0.045f, finisherHitstop = 0.09f, shake = 0.1f, finisherShake = 0.22f,
        edge = new Color(0.85f, 0.95f, 1f),
    };

    public static readonly WeaponFamily[] Chapter = { SwordShield, GreatAxe, LightBlade };

    // True when a hit from `from` lands outside the target's guarded front.
    public bool Flank(Transform target, Vector3 from)
    {
        if (flankBonus <= 1f || target == null) return false;
        Vector3 to = from - target.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return false;
        Vector3 facing = target.forward;
        facing.y = 0f;
        return Vector3.Dot(to.normalized, facing.normalized) <= flankDot;
    }
}
