using UnityEngine;

// What the hero has asked Mop to do (package N02). The order is kept in the journey.
public enum CompanionOrder
{
    Follow,     // at heel; goes for whatever is fighting nearest the hero (the original Mop)
    Assist,     // goes only for the hero's target, and his bump makes it flinch
    Hold,       // stays where he was told and strikes only what comes near that spot
    Solo,       // keeps well back and does not fight: the hero fights alone
}

// Mop, the tame blue slime. Follows the hero everywhere and, in the tower, bounces into
// enemies for a share of the hero's damage, as the hero orders. Mop cannot be hurt: a
// companion is there to be liked, not babysat.
public class Companion : MonoBehaviour
{
    static Companion instance;

    public const float FollowRange = 11f, AssistMemory = 4f, AssistCooldown = 1.6f, FollowCooldown = 1.1f;
    public const float HoldGuard = 3.5f, HoldLeash = 4f, SoloDistance = 4f, AssistFlinch = 0.3f, ReachSlack = 0.2f;
    const string OrderKey = "companion:order";

    Transform model;
    Vector3 baseScale;
    float attackCd, phase, lostTime;
    NavFollower nav;
    Vector3 holdAt;
    Transform holdScene;

    public static Companion Instance => instance;
    public Enemy Target { get; private set; }
    public int Strikes { get; private set; }

    public static CompanionOrder Order
    {
        get
        {
            int saved = JourneyStore.GetInt(OrderKey);
            return saved >= 0 && saved <= (int)CompanionOrder.Solo ? (CompanionOrder)saved : CompanionOrder.Follow;
        }
    }

    // The next order in turn: Follow, Assist, Hold, Solo. Holding fixes the spot Mop stands on now.
    public static CompanionOrder CycleOrder()
    {
        var next = (CompanionOrder)(((int)Order + 1) % 4);
        SetOrder(next);
        return next;
    }

    public static void SetOrder(CompanionOrder order)
    {
        JourneyStore.Transaction(() => JourneyStore.SetInt(OrderKey, (int)order));
        if (instance != null && order == CompanionOrder.Hold) instance.Anchor();
    }

    public static string Describe(CompanionOrder order)
    {
        switch (order)
        {
            case CompanionOrder.Assist: return "Assist: goes for your target and makes it flinch";
            case CompanionOrder.Hold: return "Hold: guards this spot";
            case CompanionOrder.Solo: return "Solo: stays back; you fight alone";
            default: return "Follow: at heel, fights beside you";
        }
    }

    void Anchor()
    {
        holdAt = transform.position;
        holdScene = Game.I != null ? Game.I.LevelRoot : null;
    }

    // Creates Mop if the hero has met him and he isn't in the world yet.
    public static void Ensure()
    {
        if (instance != null || !Save.Pet || Game.I == null) return;
        var go = new GameObject("Mop");
        instance = go.AddComponent<Companion>();
        // The same jelly as any slime, small and blue, with a heart that glows: he never reads as an enemy.
        instance.model = Gel.Build(go.transform, 0.55f, Gel.Blue, Gel.BlueDeep, 0.9f);
        instance.baseScale = instance.model.localScale;
        instance.nav = NavFollower.AddTo(go);
        instance.nav.label = "Mop";
        go.transform.position = Game.I.Player.transform.position - Game.I.Player.transform.forward * 1.5f;
        if (TryHeel(Game.I.Player, out Vector3 socket)) go.transform.position = socket;
        instance.Anchor();
    }

    static bool TryHeel(PlayerController player, out Vector3 socket, float back = 1.3f)
    {
        foreach (float side in new[] { -1f, 1f, 0f })
            if (WorldSupport.TryStand(player.transform.position - player.transform.forward * back + player.transform.right * side,
                0.35f, 1f, out socket)) return true;
        socket = player.transform.position;
        return false;
    }

    // A new journey has not met Mop yet.
    public static void Dismiss()
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = null;
    }

    // Who Mop should be going for under the current order, if anyone.
    Enemy ChooseTarget(PlayerController player, CompanionOrder order)
    {
        if (order == CompanionOrder.Solo) return null;
        if (order == CompanionOrder.Assist)
        {
            // The hero's locked target, otherwise whatever the hero has just been hitting.
            Enemy wanted = player.LockedTarget != null ? player.LockedTarget
                : Time.time - player.LastStruckAt <= AssistMemory ? player.LastStruck : null;
            return wanted != null && wanted.CombatEligible && CombatContact.CanContact(transform.position + Vector3.up * 0.5f, wanted, 1.1f) ? wanted : null;
        }
        Vector3 centre = order == CompanionOrder.Hold ? holdAt : player.transform.position;
        float best = order == CompanionOrder.Hold ? HoldGuard : FollowRange;
        Enemy target = null;
        foreach (Enemy e in Enemy.All)
        {
            if (!e.CombatEligible || !e.Engaged || !CombatContact.CanContact(transform.position + Vector3.up * 0.5f, e, 1.1f)) continue;
            float d = (e.transform.position - centre).magnitude;
            if (d < best) { best = d; target = e; }
        }
        return target;
    }

    void Update()
    {
        PlayerController player = Game.I.Player;
        if (!Game.I.Roaming || player.Dead)
        {
            nav.Hold();
            return;
        }

        float dt = GameClocks.WorldDelta;
        phase += dt;
        attackCd -= dt;
        CompanionOrder order = Order;
        // A hold point belongs to the floor it was given on.
        if (holdScene != Game.I.LevelRoot) Anchor();

        Enemy target = ChooseTarget(player, order);
        Target = target;
        Vector3 rest = order == CompanionOrder.Hold ? holdAt
            : player.transform.position - player.transform.forward * (order == CompanionOrder.Solo ? SoloDistance : 1.3f) - player.transform.right * 1.1f;
        Vector3 goal = target != null ? target.transform.position : rest;
        // Holding, he never strays far from his post.
        if (order == CompanionOrder.Hold && (goal - holdAt).magnitude > HoldLeash) goal = holdAt + (goal - holdAt).normalized * HoldLeash;
        Vector3 to = goal - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        float stop = target != null ? target.radius + 0.7f : 0.4f;

        // Use a complete route when navigation is ready, otherwise checked support/collision.
        bool pathing = nav.Attach();
        float speed = target != null ? 8f : Mathf.Lerp(3f, 10f, Mathf.Clamp01(dist / 6f));
        string what = order == CompanionOrder.Follow ? "" : order.ToString().ToUpper() + "  ";
        nav.state = what + (target != null ? (dist > stop ? "HUNT  " + target.name : "STRIKE") : (dist > stop ? (order == CompanionOrder.Hold ? "POST" : "HEEL") : "WAIT"));
        float fromHero = Vector3.Distance(player.transform.position, transform.position);
        lostTime = order != CompanionOrder.Hold && !pathing && fromHero > 6f + (order == CompanionOrder.Solo ? SoloDistance : 0f) ? lostTime + dt : 0f;

        if (order != CompanionOrder.Hold && (dist > 25f || lostTime > 2f))     // explicit safe recovery after a floor change
        {
            if (TryHeel(player, out Vector3 socket, order == CompanionOrder.Solo ? SoloDistance : 1.3f))
            {
                if (!pathing || !nav.Teleport(socket)) transform.position = socket;
                lostTime = 0f;
            }
        }
        else if (target != null && dist <= stop + ReachSlack)
        {
            // Within reach of his target: bite when ready. (Closing to exactly the stopping
            // distance leaves him a hair outside it; without this slack he crept forever.)
            if (attackCd <= 0f) Strike(player, target, order);
        }
        else if (dist > stop)
        {
            if (pathing) nav.MoveTo(goal, stop, speed);
            else
            {
                WorldSupport.TryMove(transform, to / dist * Mathf.Min(dist - stop, speed * dt), 0.35f, 1f);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * dt);
            }
        }

        // Hop along.
        float hop = Mathf.Abs(Mathf.Sin(phase * 6f));
        model.localPosition = Vector3.up * hop * 0.25f;
        model.localScale = Vector3.Scale(baseScale, new Vector3(1.12f - hop * 0.2f, 0.82f + hop * 0.3f, 1.12f - hop * 0.2f));
    }

    void Strike(PlayerController player, Enemy target, CompanionOrder order)
    {
        attackCd = order == CompanionOrder.Assist ? AssistCooldown : FollowCooldown;
        var result = target.ReceiveHit(new DamageRequest(player.stats.damage * 0.35f, transform.position, 4f, source: player));
        phase = 0f;
        if (!result.Damaged) return;
        Strikes++;
        Fx.Sparks(target.transform.position + Vector3.up * 0.5f, Vector3.up, new Color(0.5f, 0.8f, 1f), 3);
        // Assisting, his bump knocks the hero's target off its feet for a moment. The big ones shrug it off.
        if (order == CompanionOrder.Assist && target.kind != Enemy.Kind.Brute && !target.IsBoss && target.CombatEligible) target.Stagger(AssistFlinch);
    }
}
