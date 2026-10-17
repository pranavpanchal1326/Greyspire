using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Plays a character's animation clips by name. Loop() is the ongoing state
// (idle, run); Once() is a one-shot (attack, dodge, hit) that takes over until
// it finishes; Move() blends idle, walk, jog and sprint by ground speed.
// Upper() plays a clip on the arms, chest and head only, over whatever the legs
// are doing: a sword swung at a run, a shield held up at a walk, a nod in passing.
//
// Two kinds of character are supported behind the same calls:
//  - cartoon characters whose clips ship inside their own FBX (Legacy Animation);
//  - realistic humans and monsters with a Humanoid avatar, driven from the shared
//    animation libraries in Resources/Humans/Anim through a Playables mixer.
[DefaultExecutionOrder(100)]
public class CharAnim : MonoBehaviour
{
    string current;
    float busyUntil;

    float actorClock;
    float ActorNow => animator != null ? actorClock : Time.time;
    public bool Busy => ActorNow < busyUntil;
    public bool Human => animator != null;

    // ------------------------------------------------------------ legacy backend

    Animation anim;
    readonly Dictionary<string, AnimationState> states = new Dictionary<string, AnimationState>();

    // ------------------------------------------------------------ humanoid backend

    class Slot
    {
        public AnimationClipPlayable playable;
        public int index;
        public float weight, target, fade = 0.15f, speed = 1f, length;
        public bool loop;
    }

    static Dictionary<string, AnimationClip> library;

    // Ground speed, in metres per second, that each gait covers at normal playback, and the phase
    // at which its left foot plants: measured on the real rig and recorded in the gait ledger
    // (Resources/Humans/gait_ledger.json, written by GaitAuthoring). Playing a gait faster or slower
    // than its pace is what makes feet slide. The fallbacks are the ledger's own values.
    [System.Serializable] class GaitEntry { public string clip; public float paceForward, paceRight, leftPlant, length; public bool loop; }
    [System.Serializable] class GaitLedger { public GaitEntry[] clips; }
    static Dictionary<string, GaitEntry> gaits;
    static GaitEntry Gait(string clip)
    {
        if (gaits == null)
        {
            gaits = new Dictionary<string, GaitEntry>();
            var text = Resources.Load<TextAsset>("Humans/gait_ledger");
            if (text != null)
            {
                var ledger = JsonUtility.FromJson<GaitLedger>(text.text);
                if (ledger != null && ledger.clips != null) foreach (var e in ledger.clips) gaits[e.clip] = e;
            }
        }
        return gaits.TryGetValue(clip, out var found) ? found : null;
    }
    static float Pace(string clip, float fallback)
    {
        var e = Gait(clip);
        if (e == null) return fallback;
        float pace = Mathf.Sqrt(e.paceForward * e.paceForward + e.paceRight * e.paceRight);
        return pace > 0.05f ? pace : fallback;
    }
    static float Plant(string clip) => Gait(clip)?.leftPlant ?? 0f;
    static float WalkPace => Pace("Walk_Loop", 0.97f);
    static float JogPace => Pace("Jog_Fwd_Loop", 6.35f);
    static float SprintPace => Pace("Sprint_Loop", 8.06f);
    // The directional set (package A01). A clip missing from the build falls back to the forward gait.
    public const string Easy = "Gait_Jog_Easy", EasyBack = "Gait_Jog_Easy_Bwd";
    public const string WalkBack = "Gait_Walk_Bwd", JogBack = "Gait_Jog_Bwd", SideRight = "Gait_Walk_Right", SideLeft = "Gait_Walk_Left",
        TurnStep = "Gait_Turn_Step", JogStop = "Gait_Jog_Stop", JogStart = "Gait_Jog_Start";
    // Off, Move falls back to the forward gaits (and the walk reversed for backing away): the
    // before-A01 behaviour, kept so the acceptance test can measure the difference.
    public static bool Directional = true;
    // Gait playback stays within these rates: beyond them a stride stops reading as a stride.
    const float GaitMinRate = 0.55f, GaitMaxRate = 1.55f;
    // The fastest a backstep can go with its feet planted (the authored backstep at its top rate).
    public static float BackstepTop => Pace(WalkBack, 0.97f) * GaitMaxRate;

    // What the gait blend is doing this frame, for tests and the review capture.
    public Vector2 GaitDirection { get; private set; } = Vector2.up;
    public float GaitRate { get; private set; }
    public string GaitLead { get; private set; } = "";

    public string walkClip = "Walk_Loop";   // people with a walk of their own (a patrol, a load to carry)
    public string idleClip = "Idle";        // how they stand when still: at ease, on guard, swaying
    string idleNow = "Idle";

    // The game asks for moves by the cartoon pack's clip names; these are the
    // matching clips in the humanoid animation libraries.
    static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
    {
        { "Idle", "Idle_Loop" }, { "Walking_A", "Walk_Loop" }, { "Running_A", "Jog_Fwd_Loop" },
        { "Throw", "OverhandThrow" }, { "Hit_Heavy", "Hit_Knockback" }, { "Jump_Air", "NinjaJump_Start" },
        { "Jump_Idle", "Jump_Loop" }, { "Dodge_Forward", "Roll" },
        { "Hit_A", "Hit_Chest" }, { "Hit_B", "Hit_Head" }, { "Death_A", "Death01" },
        { "Cheer", "Yes" }, { "Taunt", "Idle_No_Loop" }, { "Use_Item", "Consume" },
        { "Blocking", "Idle_Shield_Loop" }, { "Block_Hit", "Hit_Chest" }, { "Block_Attack", "Shield_OneShot" },
        { "2H_Melee_Idle", "Sword_Idle" }, { "2H_Melee_Attack_Chop", "Sword_Attack" },
        { "1H_Melee_Attack_Chop", "Sword_Regular_A" }, { "1H_Melee_Attack_Stab", "Sword_Dash_RM" },
        { "Shield_Bash", "Shield_Dash_RM" },
        { "Unarmed_Melee_Attack_Kick", "Melee_Hook" }, { "Unarmed_Melee_Attack_Punch_A", "Punch_Cross" },
        { "Spellcast_Shoot", "Spell_Simple_Shoot" }, { "Spellcast_Raise", "Spell_Simple_Enter" },
        { "Jump_Full_Long", "NinjaJump_Start" }, { "Spawn_Ground", "LayToIdle" },
        { "Sit_Floor_Down", "Sitting_Enter" }, { "Lie_Down", "Sitting_Enter" },
    };

    Animator animator;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();

    // The upper-body layer: its own mixer, laid over the first through a mask of chest, head and arms.
    AnimationLayerMixerPlayable layers;
    AnimationMixerPlayable upper;
    readonly Dictionary<string, Slot> upperSlots = new Dictionary<string, Slot>();
    Slot upperNow;
    float upperWeight, upperUntil, upperIn = 0.06f;
    bool upperHold;
    static AvatarMask upperMask;

    static AvatarMask UpperMask
    {
        get
        {
            if (upperMask != null) return upperMask;
            upperMask = new AvatarMask();
            for (int p = 0; p < (int)AvatarMaskBodyPart.LastBodyPart; p++)
            {
                var part = (AvatarMaskBodyPart)p;
                upperMask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                    || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            }
            return upperMask;
        }
    }

    // Returns null when the model has nothing to animate (e.g. the Blender-made props).
    public static CharAnim Attach(GameObject host, Transform model, params string[] hideParts)
    {
        var a = model.GetComponentInChildren<Animation>();
        var human = model.GetComponentInChildren<Animator>();
        bool isHuman = human != null && human.avatar != null && human.avatar.isValid && human.avatar.isHuman;
        if (!isHuman && (a == null || a.GetClipCount() == 0)) return null;

        foreach (string part in hideParts)
        {
            Transform t = LimbAnimator.FindDeep(model, part);
            if (t != null) t.gameObject.SetActive(false);
        }

        var c = host.AddComponent<CharAnim>();
        if (isHuman)
        {
            c.StartGraph(human);
            LimbPlacement.Bind(host, human);
        }
        else
        {
            c.anim = a;
            a.playAutomatically = false;
            a.cullingType = AnimationCullingType.AlwaysAnimate;
            a.Stop();
            foreach (AnimationState s in a)
            {
                // Clips arrive named "Rig|Idle"; index them by the short name.
                string key = s.name.Contains("|") ? s.name.Substring(s.name.LastIndexOf('|') + 1) : s.name;
                c.states[key] = s;
            }
        }
        c.Loop("Idle", 1f, 0f);
        return c;
    }

    void StartGraph(Animator a)
    {
        if (library == null)
        {
            library = new Dictionary<string, AnimationClip>();
            foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("Humans/Anim"))
            {
                if (clip.name.StartsWith("__preview__")) continue;
                string key = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
                library[key] = clip;
            }
        }
        animator = a;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("CharAnim");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, 0);
        upper = AnimationMixerPlayable.Create(graph, 0);
        layers = AnimationLayerMixerPlayable.Create(graph, 2);
        graph.Connect(mixer, 0, layers, 0);
        graph.Connect(upper, 0, layers, 1);
        layers.SetInputWeight(0, 1f);
        layers.SetInputWeight(1, 0f);
        layers.SetLayerMaskFromAvatarMask(1, UpperMask);
        AnimationPlayableOutput.Create(graph, "pose", animator).SetSourcePlayable(layers);
        graph.Play();
    }

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    static AnimationClip Find(string clip)
    {
        if (library.TryGetValue(clip, out AnimationClip direct)) return direct;
        if (Alias.TryGetValue(clip, out string other) && library.TryGetValue(other, out AnimationClip mapped)) return mapped;
        return null;
    }

    Slot GetSlot(string clip) => GetSlot(clip, mixer, slots);

    Slot GetSlot(string clip, AnimationMixerPlayable into, Dictionary<string, Slot> held)
    {
        if (held.TryGetValue(clip, out Slot s)) return s;
        AnimationClip source = Find(clip);
        if (source == null) return null;
        s = new Slot { playable = AnimationClipPlayable.Create(graph, source), index = into.GetInputCount(), length = Mathf.Max(0.05f, source.length) };
        // Humanoid foot IK would pull feet toward each clip's recorded goals, and the authored gaits
        // carry none (their feet were solved into the muscles), so mixing the two drags the feet off
        // the ground. Every gait plays as the ledger measured it, without; LimbPlacement (A02) puts
        // the feet on the actual ground.
        s.playable.SetApplyFootIK(false);
        into.SetInputCount(s.index + 1);
        graph.Connect(s.playable, 0, into, s.index);
        into.SetInputWeight(s.index, 0f);
        held[clip] = s;
        return s;
    }

    // Makes one clip the whole pose, fading everything else out.
    void Solo(Slot only, float fade)
    {
        foreach (Slot s in slots.Values)
        {
            s.target = s == only ? 1f : 0f;
            s.fade = fade;
        }
        if (fade <= 0f) foreach (Slot s in slots.Values) s.weight = s.target;
    }

    void Update()
    {
        bool presentingPlayer = GetComponent<PlayerController>() != null && Game.I != null && Game.I.State != Game.GameState.Playing && Game.I.State != Game.GameState.Hub;
        float dt = presentingPlayer ? GameClocks.PresentationDelta : GameClocks.WorldDelta, total = 0f;
        if (animator == null) { if (anim != null) anim.enabled = !GameClocks.HardPause && (presentingPlayer || GameClocks.WorldDelta > 0f); return; }
        actorClock += dt;
        // Weights travel at a steady rate but are eased before they reach the pose, so one
        // movement melts into the next instead of sliding across in a straight line.
        foreach (Slot s in slots.Values)
        {
            s.weight = s.fade <= 0f ? s.target : Mathf.MoveTowards(s.weight, s.target, dt / s.fade);
            total += Ease(s.weight);
        }
        foreach (Slot s in slots.Values)
        {
            mixer.SetInputWeight(s.index, total > 0.001f ? Ease(s.weight) / total : 0f);
            Advance(s);
        }

        // The upper body: one clip at a time, cross-faded, the whole layer eased in and out.
        bool on = upperNow != null && (upperHold || ActorNow < upperUntil);
        upperWeight = Mathf.MoveTowards(upperWeight, on ? 1f : 0f, dt / (on ? upperIn : 0.2f));
        layers.SetInputWeight(1, Ease(upperWeight));
        if (upperSlots.Count == 0) { graph.Evaluate(dt); return; }
        total = 0f;
        foreach (Slot s in upperSlots.Values)
        {
            s.weight = Mathf.MoveTowards(s.weight, s == upperNow ? 1f : 0f, dt / 0.1f);
            total += Ease(s.weight);
        }
        foreach (Slot s in upperSlots.Values)
        {
            upper.SetInputWeight(s.index, total > 0.001f ? Ease(s.weight) / total : 0f);
            Advance(s);
        }
        graph.Evaluate(dt);
    }

    static float Ease(float w) => w * w * (3f - 2f * w);

    // No clip is played faster than this, or slower than MinPace: beyond that a movement stops
    // looking like a movement. A clip that would have to be squeezed harder is not squeezed: its
    // middle is played at the limit, so the part that matters still lands when the move does.
    const float MaxPace = 1.6f, MinPace = 0.55f, MinFade = 0.09f;

    static void Advance(Slot s)
    {
        s.playable.SetSpeed(s.speed);
        // Imported clips are not flagged as looping, so wrap the time ourselves.
        double t = s.playable.GetTime();
        if (s.loop && (t >= s.length || t < 0)) s.playable.SetTime((t % s.length + s.length) % s.length);
        else if (!s.loop && t > s.length) s.playable.SetTime(s.length);
    }

    // ------------------------------------------------------------ upper body

    // Plays a clip on the upper body for a set time while the legs carry on. speed 0 fits the
    // whole clip to the time. False if this character cannot do it (no such clip, or not a human rig).
    public bool Upper(string clip, float seconds, float speed = 0f, float fadeIn = 0.06f)
    {
        if (animator == null) return false;
        Slot slot = GetSlot(clip, upper, upperSlots);
        if (slot == null) return false;
        slot.playable.SetTime(0);
        slot.speed = Mathf.Clamp(speed > 0f ? speed : slot.length / Mathf.Max(0.05f, seconds), MinPace, 2f);
        slot.playable.SetSpeed(slot.speed);
        slot.loop = false;
        if (upperWeight < 0.05f) foreach (Slot s in upperSlots.Values) s.weight = s == slot ? 1f : 0f;
        upperNow = slot;
        upperUntil = ActorNow + seconds;
        upperHold = false;
        upperIn = Mathf.Max(0.02f, fadeIn);
        return true;
    }

    // Holds a looping clip on the upper body (a raised shield) until UpperStop(). Gives way to a
    // full-body one-shot while one is playing.
    public bool UpperLoop(string clip, float fadeIn = 0.1f)
    {
        if (animator == null || Busy) return false;
        Slot slot = GetSlot(clip, upper, upperSlots);
        if (slot == null) return false;
        if (upperNow == slot && upperHold) return true;
        slot.speed = 1f;
        slot.loop = true;
        if (upperWeight < 0.05f) foreach (Slot s in upperSlots.Values) s.weight = s == slot ? 1f : 0f;
        upperNow = slot;
        upperHold = true;
        upperIn = Mathf.Max(0.02f, fadeIn);
        return true;
    }

    public void UpperStop()
    {
        upperHold = false;
        upperUntil = 0f;
    }

    // ------------------------------------------------------------ shared API

    public bool Has(string clip) => animator != null ? Find(clip) != null : states.ContainsKey(clip);
    public float Length(string clip)
    {
        if (animator != null) { var found = Find(clip); return found != null ? found.length : 0f; }
        return states.TryGetValue(clip, out var state) ? state.length : 0f;
    }

    // A contact plan starts at frame zero and uses exactly the same pace for
    // full/upper-body and legacy playback. No skipped wind-up hides damage.
    public void Contact(string clip, GrowthPolicy.ContactPlan plan, bool upperBody = false)
    {
        if (!Has(clip)) return;
        if (upperBody && Upper(clip, plan.duration, plan.clipSpeed, 0.04f)) return;
        if (animator != null)
        {
            Slot slot = GetSlot(clip);
            slot.speed = plan.clipSpeed; slot.loop = false;
            slot.playable.SetTime(0); slot.playable.SetSpeed(plan.clipSpeed);
            Solo(slot, 0.04f); UpperStop();
        }
        else
        {
            var state = states[clip]; state.speed = plan.clipSpeed;
            state.time = 0f; state.wrapMode = WrapMode.Once; anim.CrossFade(state.name, 0.04f);
        }
        current = clip; busyUntil = ActorNow + plan.duration;
    }

    // Idle, walk, jog and sprint mixed by how fast the character is really moving.
    // Each gait plays at the rate that matches the ground passing under it, so a
    // stroll, a jog and a flat-out run all keep their feet planted.
    public void Move(float metresPerSecond, float fade = 0.2f, bool backwards = false) =>
        Move(metresPerSecond, backwards ? Vector2.down : Vector2.up, fade);

    // The same, in any direction relative to where the character faces (x right, y forward), with
    // the turn rate in degrees per second for stepping round on the spot. Forward, back, left and
    // right gaits are blended by the angle of travel, each at the rate that matches its own pace, and
    // all of them are held in step by their measured left-foot plant.
    public void Move(float metresPerSecond, Vector2 localDirection, float fade = 0.2f, float turnRate = 0f)
    {
        if (animator == null)
        {
            Locomotion(Mathf.Clamp01(metresPerSecond / 6.8f));
            return;
        }
        if (Busy) return;
        float v = Mathf.Max(0f, metresPerSecond);
        Vector2 dir = localDirection.sqrMagnitude > 1e-4f ? localDirection.normalized : Vector2.up;
        if (!Directional) dir = dir.y < -0.2f ? Vector2.down : Vector2.up;
        GaitDirection = dir;
        float idle = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.9f, v));
        float moving = 1f - idle;

        // Four directions, each weighted by how close the travel is to it.
        float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        float fwd = Mathf.Max(0f, 1f - Mathf.Abs(angle) / 90f);
        float back = Mathf.Max(0f, 1f - (180f - Mathf.Abs(angle)) / 90f);
        float right = Mathf.Max(0f, 1f - Mathf.Abs(angle - 90f) / 90f);
        float left = Mathf.Max(0f, 1f - Mathf.Abs(angle + 90f) / 90f);

        // Within forward and back, one gait by speed: whichever keeps its playback rate nearest 1.
        Slot i = GetSlot(idleClip) ?? GetSlot("Idle"), w = GetSlot(walkClip) ?? GetSlot("Walk_Loop"),
            e = GetSlot(Easy), j = GetSlot("Jog_Fwd_Loop"), s = GetSlot("Sprint_Loop"),
            wb = GetSlot(WalkBack), eb = GetSlot(EasyBack), jb = GetSlot(JogBack), sr = GetSlot(SideRight), sl = GetSlot(SideLeft), ts = GetSlot(TurnStep);
        if (!Directional) { e = eb = wb = jb = sr = sl = ts = null; }
        var forwardSet = e != null
            ? new[] { (w, WalkPace), (e, Pace(Easy, 3.15f)), (j, JogPace), (s, SprintPace) }
            : new[] { (w, WalkPace), (j, JogPace), (s, SprintPace) };
        var backSet = eb != null && jb != null
            ? new[] { (wb, Pace(WalkBack, WalkPace)), (eb, Pace(EasyBack, 3.15f)), (jb, Pace(JogBack, JogPace)) }
            : new[] { (wb, Pace(WalkBack, WalkPace)) };
        float[] fw = Ladder(v, forwardSet, ref forwardGait), bw = Ladder(v, backSet, ref backGait);

        // Without the directional set, the forward walk plays reversed as before.
        bool reverseWalk = wb == null && back > 0f;
        if (reverseWalk) { fwd += back; back = 0f; }
        if (sr == null) { fwd += right; right = 0f; }
        if (sl == null) { fwd += left; left = 0f; }
        if (reverseWalk) { fw = new float[forwardSet.Length]; fw[0] = 1f; }

        // The pace of the whole mix, and the one rate that makes it match the ground.
        float pace = right * Pace(SideRight, 0.57f) + left * Pace(SideLeft, 0.57f);
        for (int k = 0; k < forwardSet.Length; k++) pace += fwd * fw[k] * forwardSet[k].Item2;
        for (int k = 0; k < backSet.Length; k++) pace += back * bw[k] * backSet[k].Item2;
        float rate = Mathf.Clamp(v / Mathf.Max(0.1f, pace), GaitMinRate, GaitMaxRate);
        GaitRate = rate;
        if (reverseWalk) rate = -rate;

        // Turning on the spot: the feet step round.
        float turning = ts == null ? 0f : idle * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 200f, Mathf.Abs(turnRate)));
        float stand = idle * (1f - turning);

        // A change of stance eases from the old one to the new.
        if (idleNow != idleClip)
        {
            if (slots.TryGetValue(idleNow, out Slot was)) { was.target = 0f; was.fade = 0.3f; }
            idleNow = idleClip;
        }
        if (current != "move")
        {
            foreach (Slot other in slots.Values) { other.target = 0f; other.fade = fade; }
            current = "move";
        }
        Blend(i, stand, 1f, fade);
        Blend(ts, turning, Mathf.Clamp(Mathf.Abs(turnRate) / 150f, 0.8f, 1.5f), fade);
        for (int k = 0; k < forwardSet.Length; k++) Blend(forwardSet[k].Item1, fw[k] * fwd * moving, rate, fade);
        for (int k = 0; k < backSet.Length; k++) Blend(backSet[k].Item1, bw[k] * back * moving, rate, fade);
        Blend(sr, right * moving, rate, fade);
        Blend(sl, left * moving, rate, fade);

        // Keep every gait in step with whichever one is leading, so blends never cross legs.
        var gaitSlots = new[] { (w, "Walk_Loop"), (e, Easy), (j, "Jog_Fwd_Loop"), (s, "Sprint_Loop"), (wb, WalkBack), (eb, EasyBack), (jb, JogBack), (sr, SideRight), (sl, SideLeft) };
        Slot lead = null; string leadName = ""; float best = 0f;
        foreach (var (slot, name) in gaitSlots)
            if (slot != null && slot.target > best) { best = slot.target; lead = slot; leadName = name; }
        GaitLead = leadName;
        if (lead == null) return;
        double phase = (lead.playable.GetTime() / lead.length % 1.0 + 1.0) % 1.0;
        double planted = phase - Plant(leadName);
        foreach (var (slot, name) in gaitSlots)
            if (slot != null && slot != lead) Follow(slot, lead, ((planted + Plant(name)) % 1.0 + 1.0) % 1.0);
    }

    // Weights over a ladder of gaits ordered by pace: the one gait whose playback rate would sit
    // nearest its own pace (in proportion) takes the legs; the weights' fade carries the change. A
    // new choice must be clearly better (Hysteresis) so a speed near the boundary does not flicker.
    const float Hysteresis = 0.12f;
    static float[] Ladder(float v, (Slot, float)[] set, ref int chosen)
    {
        var w = new float[set.Length];
        int best = 0; float bestCost = float.MaxValue;
        for (int k = 0; k < set.Length; k++)
        {
            if (set[k].Item1 == null) continue;
            float cost = Mathf.Abs(Mathf.Log(Mathf.Max(0.05f, v) / Mathf.Max(0.05f, set[k].Item2)));
            if (cost < bestCost) { bestCost = cost; best = k; }
        }
        if (chosen >= 0 && chosen < set.Length && set[chosen].Item1 != null)
        {
            float keep = Mathf.Abs(Mathf.Log(Mathf.Max(0.05f, v) / Mathf.Max(0.05f, set[chosen].Item2)));
            if (keep <= bestCost + Hysteresis) best = chosen;
        }
        chosen = best;
        w[best] = 1f;
        return w;
    }
    int forwardGait = -1, backGait = -1;

    // A run brought to a stand: the authored stop, which a fresh start cuts short (Interrupt).
    public bool Stop()
    {
        if (animator == null || Busy || !Has(JogStop)) return false;
        Once(JogStop, -1f, 0.12f);
        return true;
    }

    // Ends a one-shot early if it is still the one playing (a stop broken by a new start).
    public void Interrupt(string clip)
    {
        if (current == clip) busyUntil = 0f;
    }

    static void Follow(Slot gait, Slot lead, double phase)
    {
        if (gait != null && gait != lead && gait.target > 0.01f) gait.playable.SetTime(phase * gait.length);
    }

    // The same blend by speed (0..1), for the cartoon characters and their own clips.
    public void Locomotion(float speed01, float fade = 0.16f)
    {
        if (Busy) return;
        float idle = Mathf.Clamp01(1f - speed01 / 0.22f);
        float run = Mathf.Clamp01((speed01 - 0.45f) / 0.4f);
        float walk = Mathf.Clamp01(1f - idle - run);

        if (animator != null)
        {
            Slot i = GetSlot("Idle"), w = GetSlot("Walking_A"), r = GetSlot("Running_A");
            if (current != "loco")
            {
                foreach (Slot s in slots.Values) { s.target = 0f; s.fade = fade; }
                current = "loco";
            }
            Blend(i, idle, 1f, fade);
            Blend(w, walk, 0.85f + speed01 * 0.9f, fade);
            Blend(r, run, 0.8f + speed01 * 0.4f, fade);
            if (w != null && r != null && walk > 0.05f && run > 0.05f)
                w.playable.SetTime(r.playable.GetTime() / r.length * w.length);
            return;
        }

        if (current != "loco")
        {
            // Whatever single clip was looping (a jump, say) fades out under the blend.
            if (current != null && states.TryGetValue(current, out AnimationState old)) anim.Blend(old.name, 0f, fade);
            current = "loco";
        }
        Mix("Idle", idle, 1f, fade);
        Mix("Walking_A", walk, 0.8f + speed01 * 1.3f, fade);
        Mix("Running_A", run, 0.75f + speed01 * 0.5f, fade);
        if (walk > 0.05f && run > 0.05f
            && states.TryGetValue("Walking_A", out AnimationState ws) && states.TryGetValue("Running_A", out AnimationState rs))
            ws.normalizedTime = rs.normalizedTime;
    }

    static void Blend(Slot s, float weight, float speed, float fade)
    {
        if (s == null) return;
        s.target = weight;
        s.speed = speed;
        s.fade = fade;
        s.loop = true;
    }

    void Mix(string clip, float weight, float speed, float fade)
    {
        if (!states.TryGetValue(clip, out AnimationState s)) return;
        s.wrapMode = WrapMode.Loop;
        s.speed = speed;
        anim.Blend(s.name, weight, fade);
    }

    public void Loop(string clip, float speed = 1f, float fade = 0.15f)
    {
        if (Busy) return;
        if (animator != null)
        {
            Slot slot = GetSlot(clip);
            if (slot == null) return;
            slot.speed = speed;
            slot.loop = true;
            if (current == clip) return;
            Solo(slot, fade);
            current = clip;
            return;
        }

        if (!states.TryGetValue(clip, out AnimationState s)) return;
        s.speed = speed;
        if (current == clip) return;
        s.wrapMode = WrapMode.Loop;
        anim.CrossFade(s.name, fade);
        current = clip;
    }

    // duration > 0 stretches or squeezes the clip to take exactly that long.
    public void Once(string clip, float duration = -1f, float fade = 0.06f, bool hold = false)
    {
        float len;
        if (animator != null)
        {
            Slot slot = GetSlot(clip);
            if (slot == null) return;
            len = slot.length;
            float pace = duration > 0f ? len / duration : 1f;
            slot.speed = Mathf.Clamp(pace, MinPace, MaxPace);
            // Squeezed past the limit: start far enough in that the clip's midpoint falls at the
            // move's midpoint. A clip that is held to its end (a death) plays through from the start.
            float from = pace > MaxPace && !hold ? len * 0.5f - MaxPace * duration * 0.5f : 0f;
            slot.playable.SetTime(Mathf.Max(0f, from));
            slot.loop = false;
            if (fade > 0f) fade = Mathf.Max(fade, MinFade);
            Solo(slot, fade);
            UpperStop();    // a full-body move takes the whole body
        }
        else
        {
            if (!states.TryGetValue(clip, out AnimationState s)) return;
            len = Mathf.Max(0.01f, s.length);
            s.speed = duration > 0f ? len / duration : 1f;
            s.wrapMode = hold ? WrapMode.ClampForever : WrapMode.Once;
            s.time = 0f;
            anim.CrossFade(s.name, fade);
        }
        current = clip;
        busyUntil = hold ? float.MaxValue : ActorNow + (duration > 0f ? duration : len) - 0.05f;
    }

    // Plays a clip at a set speed and keeps control for a set time. For clips with a
    // long wind-down: the strike plays at full pace and the rest blends away.
    public void OnceAt(string clip, float speed, float busySeconds, float fade = 0.05f)
    {
        if (!Has(clip)) return;
        Once(clip, -1f, fade);
        if (animator != null && slots.TryGetValue(clip, out Slot slot)) slot.speed = Mathf.Min(speed, 2f);
        else if (states.TryGetValue(clip, out AnimationState s)) s.speed = speed;
        busyUntil = ActorNow + busySeconds;
    }

    public void Release()
    {
        busyUntil = 0f;
    }
}
