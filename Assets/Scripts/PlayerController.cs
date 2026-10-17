using System.Collections.Generic;
using UnityEngine;

// The hero: movement, the sword combo and taking damage. The moves beyond the
// basic combo (slams, kicks, the shield bash, the throw, the slide) live in
// PlayerMoves.cs.
[RequireComponent(typeof(CharacterController))]
public partial class PlayerController : Actor
{
    // Ground speeds in metres per second. Town is strolled through; a fight is run.
    const float WalkSpeed = 1.5f;
    const float JogSpeed = 3.7f;
    const float RunSpeed = 6.8f;
    const float SprintBoost = 1.3f;

    const float Accel = 34f;
    const float Brake = 52f;
    const float TownAccel = 16f;
    const float AirAccel = 18f;
    const float JumpVelocity = 10f;
    const float Gravity = -28f;
    const float DashSpeed = 20f;
    const float DashTime = 0.18f;
    const float BurstRadius = 9f;
    const float WaveCooldown = 5f;

    // Three-hit combo. Its timing, reach, arc and guard come from the hero's weapon family.
    public WeaponFamily Weapon => Hero.family ?? WeaponFamily.SwordShield;
    AttackStep Step => Weapon.steps[comboStep];

    public PlayerStats stats = new PlayerStats();
    [HideInInspector] public float weak = 1f;       // damage multiplier; tiny before the System awakens
    float waveCd;

    public float WaveReady => 1f - Mathf.Clamp01(waveCd / WaveCooldown);
    [HideInInspector] public float burst;           // 0..100 Colour Burst charge
    [HideInInspector] public float movedTime;       // tutorial tracking
    [HideInInspector] public int strikes, dashes;

    CharacterController cc;
    LimbAnimator anim;      // procedural fallback (Blender hero)
    CharAnim clips;         // rigged character
    Transform model;
    TrailRenderer trail;
    readonly List<Enemy> dashHits = new List<Enemy>();

    Vector3 planar, dashDir, modelScale, lastSafe;
    int styleTag = -1;      // which move a hit came from, when it is not the sword combo
    float vy, coyote, jumpBuffer, dashT, dashGrace, dashCd, invuln, lunge, stepT;
    float attackT = -1f, attackDur, comboReset;
    float attackPower = 1f;
    GrowthPolicy.ContactPlan attackPlan;
    int comboStep, killsSinceHeal, airJumps;
    bool hitDone, queued, wasGrounded, perfectUsed, sprinting, wasBlocking;
    float gestureT, restT, yawLag;
    Quaternion tilt = Quaternion.identity;

    public float DashReady => 1f - Mathf.Clamp01(dashCd / stats.dashCooldown);
    public bool BurstReady => burst >= 100f;

    public HeroDef Hero { get; private set; }
    // The enemy the hero last landed a blow on, and when: what an assisting companion goes for.
    public Enemy LastStruck { get; private set; }
    public float LastStruckAt { get; private set; } = -999f;
    public float Speed01 { get; private set; }      // how fast the hero is moving, 0..1 (camera uses it)

    bool Acting => attackT >= 0f || move != Move.None;

    // How much of his pace the hero keeps while doing something: most of it through a light
    // swing or a throw, half behind a raised shield, little while committed to a heavy move.
    float ActingPace => !Acting ? 1f : move == Move.None || move == Move.Throw ? 0.62f : move == Move.Block ? 0.45f : 0.3f;

    Quaternion modelRot = Quaternion.identity, capeRest = Quaternion.identity;
    Transform cape;
    bool wasCharged;
    float lastYaw, bank;
    // A01: how far the hips are turned toward the direction of travel (the chest turns back by the
    // same, through LimbPlacement), the facing last frame for the turn-in-place step, and the speed
    // last frame for the stop.
    float hipYaw, gaitYaw;
    LimbPlacement limbs;
    public const float MaxHipYaw = 70f, MaxBackHipYaw = 50f;
    public float HipYaw => hipYaw;

    // What the hero carries, and the two places each piece can be: in the hand, or put away.
    class Gear
    {
        public Transform prop, hand, rest;
        public Vector3 handPos, restPos;
        public Quaternion handRot, restRot;
        public bool blade, placed;
        public float side = -1f;    // which hip a blade hangs at: -1 left, +1 right
    }

    readonly List<Gear> gear = new List<Gear>();

    // The wrist turning the weapon so its edge, not its flat, leads each blow (HeroDef.comboTwist).
    Transform held;
    Quaternion heldRest;
    Vector3 heldLength;
    float twist, twistTarget;
    const float TwistRate = 1100f;   // degrees a second: a quarter turn inside a fast wind-up
    bool armed = true;
    int builtFrame;

    // The hero has no line round him, until the Colour Burst is ready: then an aura in every colour.
    const float Ink = 0f, InkCharged = 0.03f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.height = 1.7f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.86f, 0);
        cc.stepOffset = 0.3f;
        cc.slopeLimit = 48f;
        cc.skinWidth = 0.035f;
        cc.minMoveDistance = 0f;
        SetHero(Heroes.Selected);
        InitActor(stats.maxHearts);
    }

    // Builds (or rebuilds) the visible character for the chosen hero.
    public void SetHero(int index)
    {
        Hero = Heroes.All[Mathf.Clamp(index, 0, Heroes.All.Length - 1)];
        if (model != null)
        {
            model.gameObject.SetActive(false);
            model.SetParent(null);
            Destroy(model.gameObject);
        }
        if (clips != null) Destroy(clips);
        if (anim != null) Destroy(anim);
        clips = null;
        anim = null;
        gear.Clear();
        armed = true;
        builtFrame = Time.frameCount;

        // Prefer the rigged character; fall back to the Blender hero if it is missing.
        Transform weapon = null;
        model = Hero.look != null
            ? Humans.Build(Hero.look, transform)
            : ToonKit.SpawnCharacter(Hero.model, Hero.texture, transform, 0.78f);
        if (model != null && Hero.look != null)
        {
            clips = CharAnim.Attach(gameObject, model);
            weapon = Humans.Hold(model, Hero.weapon, false, Humans.Grip, Humans.GripAngles);
            limbs = GetComponent<LimbPlacement>();
            if (limbs != null) limbs.GripWeapon = weapon;
            BindTwist(weapon);
            Transform shield = Humans.Hold(model, Hero.shield, true, Humans.ShieldGrip, Humans.ShieldAngles);
            Transform offhand = Humans.Hold(model, Hero.offhand, true, Humans.OffhandGrip, Humans.OffhandAngles);
            Stow(weapon, true);
            Stow(shield, false);
            Stow(offhand, true, 1f);
        }
        else if (model != null)
        {
            clips = CharAnim.Attach(gameObject, model, Hero.hide);
            weapon = LimbAnimator.FindDeep(model, Hero.weapon);
        }
        else
        {
            model = ToonKit.Spawn("Hero", transform);
            weapon = LimbAnimator.FindDeep(model, "Sword");
        }
        if (clips == null)
        {
            anim = gameObject.AddComponent<LimbAnimator>();
            anim.Bind(model);
        }
        modelScale = model.localScale;
        modelRot = model.localRotation;
        cape = LimbAnimator.FindDeep(model, Hero.model + "_Cape");
        if (cape != null) capeRest = cape.localRotation;

        // Weapon trail rides the striking end of the weapon: a blade near its tip, an axe at its
        // head (or the hand, for a hero who fights bare-handed).
        Transform trailOn = weapon != null ? weapon : LimbAnimator.FindDeep(model, "hand_r");
        var trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(trailOn != null ? trailOn : model, false);
        trailGo.transform.position = weapon != null ? StrikingPoint(weapon)
            : trailOn != null ? trailOn.position : model.position + Vector3.up + model.forward * 0.8f;
        trail = trailGo.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Fx.Flat;
        trail.time = Weapon.trailTime;
        trail.startWidth = Weapon.trailWidth;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        Color edge = Weapon.edge;
        trail.startColor = new Color(Mathf.Lerp(1f, edge.r, 0.5f), Mathf.Lerp(1f, edge.g, 0.5f), Mathf.Lerp(1f, edge.b, 0.5f), 0.3f);
        trail.endColor = new Color(edge.r * 0.7f, edge.g * 0.9f, edge.b, 0f);
        // Tapers from the blade to nothing, with rounded ends: a clean arc instead of a slab.
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0f));
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;
        RefreshRenderers();

        // The hero's eyes go where his attention is: the nearest threat, or whoever he is about to speak to.
        var look = GetComponent<HeadLook>();
        if (look == null) look = gameObject.AddComponent<HeadLook>();
        look.hero = true;
        look.Rebind();
    }

    void BindTwist(Transform weapon)
    {
        held = Hero.comboTwist != null ? weapon : null;
        twist = twistTarget = 0f;
        if (held == null) return;
        heldRest = held.localRotation;
        // The weapon's length, as a direction in its own space (the mesh may sit on a child).
        var filter = held.GetComponentInChildren<MeshFilter>();
        Vector3 s = filter.sharedMesh.bounds.size, axis = Vector3.zero;
        axis[s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2] = 1f;
        heldLength = held.InverseTransformDirection(filter.transform.TransformDirection(axis)).normalized;
    }

    void TickTwist(float dt)
    {
        if (held == null || !armed) return;
        if (attackT < 0f) twistTarget = 0f;
        twist = Mathf.MoveTowards(twist, twistTarget, TwistRate * dt);
        held.localRotation = heldRest * Quaternion.AngleAxis(twist, heldLength);
    }

    // Where a held weapon does its work: 85% of the way along its long axis from the grip
    // to its far end (a blade near the tip, an axe at its head).
    static Vector3 StrikingPoint(Transform weapon)
    {
        var filter = weapon.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return weapon.position;
        Bounds b = filter.sharedMesh.bounds;
        Vector3 grip = filter.transform.InverseTransformPoint(weapon.position);
        Vector3 size = b.size;
        int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        float reach = Mathf.Abs(b.max[longest] - grip[longest]) >= Mathf.Abs(b.min[longest] - grip[longest]) ? b.max[longest] : b.min[longest];
        Vector3 along = b.center;
        along[longest] = grip[longest] + (reach - grip[longest]) * 0.85f;
        return filter.transform.TransformPoint(along);
    }

    // Notes a held prop as something that can be put away.
    void Stow(Transform prop, bool blade, float side = -1f)
    {
        if (prop == null) return;
        gear.Add(new Gear { prop = prop, hand = prop.parent, handPos = prop.localPosition, handRot = prop.localRotation, blade = blade, side = side });
    }

    // In town the sword hangs at the hip and the shield rides on the back.
    void SetArmed(bool on)
    {
        if (armed == on) return;
        // A freshly built body needs a frame to strike its first pose before anything is hung on it.
        if (!on && Time.frameCount < builtFrame + 2) return;
        armed = on;
        foreach (Gear g in gear)
        {
            if (g.prop == null) continue;
            if (!on && !g.placed) g.placed = Humans.RestPose(model, g.prop, g.blade, out g.rest, out g.restPos, out g.restRot, g.side);
            if (!g.placed) continue;
            g.prop.SetParent(on ? g.hand : g.rest, true);
            g.prop.localPosition = on ? g.handPos : g.restPos;
            g.prop.localRotation = on ? g.handRot : g.restRot;
        }
    }

    // What each stat point is worth. The Status window quotes these.
    public const float StrDamage = 0.04f, AgiAttack = 0.012f, AgiMove = 0.006f, SenCrit = 0.01f, SenBurst = 0.04f;
    public const int VitPerHeart = 4;

    // Everything the hero is, worked out from scratch: the forge's perks, the hero's own bent,
    // level, the four stats, the skills learned so far, and the boons Guardians have given up.
    void BuildStats()
    {
        if (Upgrades.All.Count == 0) Upgrades.Reset();
        int[] allocation = new int[4], perks = new int[4], boons = new int[Upgrades.All.Count];
        for (int i = 0; i < 4; i++) { allocation[i] = Save.Stat(i); perks[i] = Save.PerkRank(i); }
        foreach (Upgrade boon in Upgrades.All) boons[boon.id] = Save.Boon(boon.id);
        stats = GrowthPolicy.Build(Save.Level, allocation, perks, boons, Hero);
    }

    // Setting out: into the tower, or through the gate to the Wilds. Rested and whole.
    public void NewRun()
    {
        masteryCounter = 0f;
        BuildStats();
        burst = 0f;
        killsSinceHeal = 0;
        movedTime = 0f; strikes = 0; dashes = 0;
        maxHp = hp = stats.maxHearts;
    }

    // A stat point spent, a boon taken: the numbers change under the hero without
    // touching the fight in progress. A heart gained arrives full.
    public void RefreshStats()
    {
        int winds = stats.secondWinds;
        float before = maxHp;
        BuildStats();
        stats.secondWinds = Mathf.Min(stats.secondWinds, Mathf.Max(winds, 0));
        maxHp = stats.maxHearts;
        hp = Mathf.Clamp(hp + Mathf.Max(0f, maxHp - before), Dead ? 0f : 1f, maxHp);
    }

    public void Revive(Vector3 pos, bool fullHeal)
    {
        Access.Reset();
        maxHp = stats.maxHearts;
        if (fullHeal || hp <= 0f) hp = maxHp;
        vy = 0f; dashT = 0f; dashCd = 0f; attackT = -1f; invuln = 1f; lunge = 0f;
        move = Move.None;
        charge = heldT = throwCd = bashCd = 0f;
        masteryCounter = 0f;
        sprinting = false;
        if (clips != null) clips.Release();
        comboStep = 0; queued = false;
        ClearIntent();
        ClearLock();
        coyote = dashGrace = gestureT = restT = 0f;
        wasGrounded = false;
        GroundVelocity = Vector3.zero;
        Speed01 = 0f;
        yawLag = bank = 0f;
        lastYaw = transform.eulerAngles.y;
        tilt = Quaternion.identity;
        model.localRotation = modelRot;
        planar = knock = Vector3.zero;
        trail.emitting = false;
        SetFlash(0f);
        Teleport(pos);
    }

    public void Teleport(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        CaptureContactPose();
        cc.enabled = true;
        lastSafe = pos;
        GroundVelocity = Vector3.zero;
        ClearIntent();
        ClearLock();
    }

    // Called by the System each time the hero gains a level, even mid-fight: a little stronger, and whole again.
    public void OnLevelUp()
    {
        RefreshStats();
        if (!Dead) hp = maxHp;
    }

    // Something done with the hands: opening a chest, drinking at a spring, greeting someone.
    public void Gesture(string clip, float seconds, Vector3 toward)
    {
        if (clips == null || Dead || !clips.Has(clip) || Acting || dashT > 0f) return;
        Vector3 to = Flat(toward - transform.position);
        if (to.sqrMagnitude > 0.04f) transform.rotation = Quaternion.LookRotation(to);
        gestureT = seconds * 0.8f;
        planar = Vector3.zero;
        clips.Release();
        clips.Once(clip, seconds, 0.12f);
    }

    public void Cheer()
    {
        if (clips == null) return;
        clips.Release();
        clips.Once("Cheer", -1f, 0.15f, true);
    }

    public void Heal(int amount)
    {
        if (Dead || Game.I.DefeatPending) return;
        hp = Mathf.Min(maxHp, hp + amount);
    }

    public void AddBurst(float amount)
    {
        if (!Game.HasSkill(Game.SkillBurst)) return;     // the meter does nothing until the skill is learned
        bool was = BurstReady;
        burst = Mathf.Min(100f, burst + amount * stats.burstGain);
        if (!was && BurstReady) ToonKit.Sfx("ready", position: transform.position + Vector3.up);
    }

    public void OnKill()
    {
        if (Dead || Game.I.DefeatPending) return;
        AddBurst(6f);
        if (stats.lifestealEvery > 0 && ++killsSinceHeal >= stats.lifestealEvery)
        {
            killsSinceHeal = 0;
            if (hp < maxHp)
            {
                Heal(1);
                ToonKit.Sfx("heart", position: transform.position + Vector3.up);
            }
        }
    }

    void Update()
    {
        if (GameClocks.HardPause) { ClearIntent(); return; }
        model.localScale = Vector3.Lerp(model.localScale, modelScale, 12f * GameClocks.WorldDelta);

        // Brushwick is home: nobody draws a sword there.
        bool town = Game.I.InTown;
        SetArmed(!town);
        TickTwist(GameClocks.WorldDelta);

        if (!Game.I.Roaming || Dead)
        {
            if (anim != null)
            {
                anim.move = 0f;
                anim.attack = -1f;
                anim.airborne = false;
            }
            else if (!Dead) clips.Move(0f);
            trail.emitting = false;
            planar = Vector3.zero;
            GroundVelocity = Vector3.zero;
            Speed01 = 0f;
            ClearIntent();
            if (Dead || town) ClearLock();
            return;
        }

        float dt = GameClocks.WorldDelta;
        if (dt <= 0f) return;
        masteryCounter = Mathf.Max(0f, masteryCounter - dt);
        TickTargeting(dt);
        dashCd -= dt; invuln -= dt; dashGrace -= dt; comboReset -= dt; waveCd -= dt; throwCd -= dt; bashCd -= dt;
        if (comboReset <= 0f && attackT < 0f) comboStep = 0;

        // A gesture (opening a chest, drinking) holds him where he stands until it is done.
        Vector2 mv = gestureT > 0f ? Vector2.zero : GameInput.Move;
        bool gesturing = gestureT > 0f;
        gestureT -= dt;
        float yaw = Game.I.Cam.transform.eulerAngles.y;
        Vector3 dir = Quaternion.Euler(0, yaw, 0) * new Vector3(mv.x, 0, mv.y);
        bool steering = dir.sqrMagnitude > 0.01f;
        if (steering) movedTime += dt;

        Access.Tick(steering, town, dt);
        if (town) sprinting = Access.Sprint && steering;
        else
        {
            if (!gesturing) ReadCombatInput(dir, dt);
            // Keep the dash button held after a dash to break into a sprint.
            sprinting = sprinting && Access.Sprint && steering && !Acting;
        }

        bool grounded = cc.isGrounded;
        if (dashT > 0f)
        {
            dashT -= dt;
            planar = dashDir * DashSpeed;
            if (LockedTarget == null) transform.rotation = Quaternion.LookRotation(dashDir);
            else Face(LockedTarget);
            if (stats.dashStrike) DashStrike();
            if ((ghostT -= dt) <= 0f)
            {
                ghostT = 0.09f;
                Fx.Ghost(model, new Color(0.6f, 0.82f, 1f, 0.2f));
            }
            if (dashT <= 0f)
            {
                planar = dashDir * RunSpeed * stats.moveSpeed;
                dashGrace = 0.12f;
                sprinting = Access.Sprint;
            }
        }
        else
        {
            // In town the pace is yours to pick: stroll, jog, or run. In a fight the hero runs.
            float pace = town
                ? (Access.Guard ? WalkSpeed : sprinting ? RunSpeed : JogSpeed)
                : RunSpeed * stats.moveSpeed * (sprinting ? SprintBoost : 1f) * ActingPace;
            if (LockedTarget != null && !sprinting)
            {
                pace *= 0.72f;
                // A locked backstep is a deliberate step back, no faster than the authored backstep
                // can carry with its feet planted (A01; it was 1.9 m/s against a reversed walk).
                if (Vector3.Dot(dir, transform.forward) < -0.2f) pace = Mathf.Min(pace, CharAnim.BackstepTop);
            }
            float accel = town ? (steering ? TownAccel : 25f) : grounded ? (steering ? Accel : Brake) : AirAccel;
            if (grounded && steering && Vector3.Dot(planar, dir) < 0f) accel *= 1.35f;
            planar = Vector3.MoveTowards(planar, dir * pace, accel * dt);
            if (steering && !Acting)
            {
                Vector3 face = LockedTarget != null && !sprinting ? Flat(LockedTarget.transform.position - transform.position) : dir;
                if (face.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), (town ? 420f : 760f) * dt);
            }
            else if (LockedTarget != null && !Acting) Face(LockedTarget);
        }

        // Jump with coyote time, input buffering and variable height; one more jump in mid-air.
        if (grounded && vy <= 0f)
        {
            coyote = 0.12f;
            airJumps = 1;
        }
        else coyote -= dt;
        jumpBuffer = !gesturing && GameInput.JumpPressed ? 0.14f : jumpBuffer - dt;

        bool canJump = !gesturing && !Acting && dashT <= 0f;
        if (jumpBuffer > 0f && coyote > 0f && canJump)
        {
            vy = JumpVelocity;
            coyote = jumpBuffer = 0f;
            if (clips == null) model.localScale = Vector3.Scale(modelScale, new Vector3(0.8f, 1.25f, 0.8f));
            Fx.Dust(transform.position, 4);
            if (clips != null && attackT < 0f) clips.Once("Jump_Start", 0.22f);
            ToonKit.Sfx("jump", position: transform.position + Vector3.up);
        }
        else if (GameInput.JumpPressed && !grounded && coyote <= 0f && airJumps > 0 && !town && canJump)
        {
            airJumps--;
            vy = JumpVelocity * 0.92f;
            jumpBuffer = 0f;
            if (clips == null) model.localScale = Vector3.Scale(modelScale, new Vector3(0.8f, 1.25f, 0.8f));
            Fx.Ring(transform.position, Color.white, 0.3f, 1.8f, 0.25f);
            if (clips != null && attackT < 0f) clips.Once("Jump_Air", 0.3f);
            ToonKit.Sfx("jump", 1f, 1.3f, position: transform.position + Vector3.up);
        }
        else if (grounded && vy <= 0f)
        {
            if (vy < 0f) vy = -2f;
        }
        else
        {
            vy += Gravity * dt * (vy > 0f && !GameInput.JumpHeld ? 2.2f : 1f);
        }
        lunge = Mathf.MoveTowards(lunge, 0f, 40f * dt);
        Vector3 vel = planar + transform.forward * lunge + knock;
        vel.y = vy;
        Vector3 beforeMove = transform.position;
        CollisionFlags collisions = cc.Move(vel * dt);
        GroundVelocity = Flat(transform.position - beforeMove) / dt;
        if ((collisions & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
        bool landedGround = (collisions & CollisionFlags.Below) != 0;
        if (landedGround && !wasGrounded && vy < 0f) Land();
        wasGrounded = landedGround;
        // Remove blocked momentum so leaving a wall does not launch the hero sideways.
        if ((collisions & CollisionFlags.Sides) != 0 && dashT <= 0f && !Acting)
            planar = Vector3.ClampMagnitude(GroundVelocity, planar.magnitude);

        if (attackT >= 0f) TickAttack(dt, dir);

        // Footsteps keep time with the gait: slow and soft at a walk, quick and dusty at a run.
        float mps = GroundVelocity.magnitude;
        float speed01 = Mathf.Clamp01(mps / RunSpeed);
        Speed01 = speed01;
        if (landedGround && mps > 0.5f && dashT <= 0f && move != Move.Slide)
        {
            stepT += mps * dt;
            float stride = Mathf.Lerp(0.72f, 1.55f, Mathf.InverseLerp(WalkSpeed, RunSpeed, mps));
            if (stepT >= stride)
            {
                stepT %= stride;
                if (mps > 4.5f) Fx.Dust(transform.position, 1);
                ToonKit.Sfx("step", mps > 4.5f ? 0.35f : 0.2f, position: transform.position + Vector3.up);
            }
        }
        else stepT = 0f;

        // How he stands: on guard with the blade up while anything is hunting him; at ease
        // otherwise; and in town, after a while with nothing to do, arms folded.
        restT = mps < 0.2f ? restT + dt : 0f;
        if (clips != null)
            clips.idleClip = town ? (restT > 7f ? "Idle_FoldArms_Loop" : "Idle") : Game.I.Threatened ? "Sword_Idle" : "Idle";

        bool airborne = !landedGround && (vy > 0.5f || vy < -3f);
        if (anim != null)
        {
            anim.move = speed01;
            anim.attack = attackT >= 0f ? attackT / attackDur : -1f;
            anim.attackStyle = comboStep;
            anim.airborne = airborne;
        }
        else
        {
            // The shield comes up on the upper body: the hero can still shuffle about behind it.
            // A great axe guards with the haft held up across the body in both hands (A02); a shield
            // or a blade raises the forearm.
            if (move == Move.Block && Weapon.guard != GuardStyle.Haft) clips.UpperLoop("Blocking", 0.08f);
            else if (move == Move.Block) clips.UpperStop();
            else if (wasBlocking) clips.UpperStop();
            wasBlocking = move == Move.Block;
            if (move == Move.Charge) clips.Loop("2H_Melee_Idle", 1f, 0.1f);
            else if (airborne) clips.Loop("Jump_Idle");
            else Gait(mps, steering, landedGround, dt);
        }

        // A full Colour meter shows on the hero: the outline cycles through the rainbow.
        bool charged = BurstReady && Game.HasSkill(Game.SkillBurst) && !town;
        if (charged)
        {
            block.SetColor("_OutlineColor", Save.Flashes > 0f ? Color.HSVToRGB(Time.time * 0.6f % 1f, 0.75f, 1f) : UI.Gold);
            block.SetFloat("_OutlineWidth", InkCharged);
            ApplyBlock();
        }
        else if (wasCharged)
        {
            block.SetColor("_OutlineColor", new Color(0.08f, 0.07f, 0.12f));
            block.SetFloat("_OutlineWidth", Ink);
            ApplyBlock();
        }
        wasCharged = charged;
        TickActor();

        // Body language: lean into the run, bank into turns, pitch forward in a dash.
        if (clips != null)
        {
            float yawNow = transform.eulerAngles.y;
            float turn = Mathf.DeltaAngle(lastYaw, yawNow) / Mathf.Max(dt, 0.0001f);
            lastYaw = yawNow;
            // A sudden change of facing (turning to strike, snapping round on a dash) is the game's
            // business and happens at once; the body is seen to swing round to it over a few frames.
            float snap = Mathf.DeltaAngle(yawNow - turn * dt, yawNow);
            if (Mathf.Abs(snap) > 20f) yawLag = Mathf.Clamp(yawLag - snap, -170f, 170f);
            yawLag = Mathf.MoveTowards(yawLag, 0f, Mathf.Max(540f, Mathf.Abs(yawLag) * 14f) * dt);
            bank = Mathf.Lerp(bank, Mathf.Clamp(-turn * 0.03f, -14f, 14f) * speed01 * (Mathf.Abs(snap) > 20f ? 0f : 1f), 8f * dt);
            bool backing = LockedTarget != null && Vector3.Dot(GroundVelocity, transform.forward) < -0.2f;
            float lean = speed01 * (backing ? -4f : 7f) + (sprinting ? 4f : 0f) + (dashT > 0f ? 14f : 0f);
            tilt = Quaternion.Slerp(tilt, Quaternion.Euler(lean, 0f, bank), 10f * dt);
            model.localRotation = Quaternion.Euler(0f, yawLag + hipYaw, 0f) * modelRot * tilt;

            // The cape never hangs dead still: a breath of wind at rest, a flutter at speed.
            if (cape != null)
            {
                float flutter = Mathf.Sin(Time.time * 2.1f) * 2.5f + Mathf.Sin(Time.time * 9f) * 5f * speed01;
                cape.localRotation = capeRest * Quaternion.Euler(flutter, 0f, Mathf.Sin(Time.time * 6.3f) * 3f * speed01);
            }
        }

        // Remember the last solid ground, so a fall through the world puts the hero back where
        // they were standing instead of somewhere they never walked to.
        if (landedGround && WorldSupport.TryGround(transform.position, out _, 0.35f, 0.5f)) lastSafe = transform.position;
        if (transform.position.y < lastSafe.y - 15f)
        {
            vy = 0f;
            Teleport(lastSafe + Vector3.up * 0.5f);
        }
    }

    // A01: the legs follow the direction of travel, not only the facing. Moving sideways or back
    // while locked on blends the side-step and backward gaits; past a walk the hips turn toward the
    // travel (up to MaxHipYaw, or MaxBackHipYaw when running backward) and the chest turns back so
    // the hero still faces the target. Turning on the spot steps round; a run brought to a stand
    // plays the authored stop. An action takes the body back to its own facing.
    void Gait(float mps, bool steering, bool grounded, float dt)
    {
        float facing = transform.eulerAngles.y;
        float turnRate = dt > 0f ? Mathf.DeltaAngle(gaitYaw, facing) / dt : 0f;
        gaitYaw = facing;
        float theta = GroundVelocity.sqrMagnitude > 0.04f ? Vector3.SignedAngle(transform.forward, GroundVelocity, Vector3.up) : 0f;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 2f, mps));
        float want = Mathf.Abs(theta) <= 110f ? Mathf.Clamp(theta, -MaxHipYaw, MaxHipYaw) * k
            : Mathf.Clamp(theta - Mathf.Sign(theta) * 180f, -MaxBackHipYaw, MaxBackHipYaw) * k;
        // Guarding keeps the shield on the target while the hips turn; any other action faces its own way.
        if ((Acting && move != Move.Block) || dashT > 0f || attackT >= 0f || !grounded || !CharAnim.Directional) want = 0f;
        hipYaw = Mathf.MoveTowards(hipYaw, want, 420f * dt);
        if (limbs != null)
        {
            limbs.TorsoYaw = -hipYaw;
            // The haft guard is posed by LimbPlacement (no two-handed guard in the clip library).
            limbs.HaftGuard = limbs.GripOn;
            // The great axe in both hands across the body when guarding or squared up to a threat; carried
            // relaxed it hangs from one hand, and the swings are the clips' own (one-handed: the left hand
            // cannot reach the haft through them, so it is not dragged after it).
            limbs.GripOn = armed && Weapon.guard == GuardStyle.Haft && dashT <= 0f && move != Move.Slide
                && attackT < 0f && (move == Move.Block || (move == Move.None && Game.I.Threatened));
        }
        float residual = (theta - hipYaw) * Mathf.Deg2Rad;
        Vector2 local = new Vector2(Mathf.Sin(residual), Mathf.Cos(residual));

        // A run let go of on the ground: the front foot plants and the trailing leg steps in. The stop
        // starts as the brake does, so its braking stride matches the body's (about 0.13 s, 0.44 m).
        if (steering) clips.Interrupt(CharAnim.JogStop);
        else if (mps > 3.2f && grounded && !Acting && dashT <= 0f && attackT < 0f && move == Move.None) clips.Stop();
        clips.Move(mps, local, 0.13f, turnRate);
    }

    // Everything the hero can do with a weapon in hand. None of it is read in town.
    void ReadCombatInput(Vector3 dir, float dt)
    {
        if (GameInput.AttackPressed) Buffer(BufferedAction.Light, dir);
        if (GameInput.HeavyPressed) Buffer(BufferedAction.Heavy, dir);
        if (GameInput.ThrowPressed) Buffer(BufferedAction.Throw, dir);
        if (GameInput.DashPressed) Buffer(BufferedAction.Dodge, dir);
        if (move != Move.None) TickMove(dt);
        ConsumeBufferedAction(dir);
        bool onGround = cc.isGrounded;
        // Keep the attack button held to wind up a charged slam.
        heldT = GameInput.AttackHeld && move == Move.None && attackT < 0f && dashT <= 0f && onGround ? heldT + dt : 0f;
        if (heldT > 0.3f) StartCharge();
        if (!Access.Guard) guardSpent = false;
        if (Access.Guard && move == Move.None && attackT < 0f && dashT <= 0f && onGround)
        {
            // At a sprint the guard button drops into a slide instead.
            if (sprinting && planar.magnitude > RunSpeed) StartSlide();
            else StartBlock();
        }
        if (GameInput.BurstPressed && BurstReady && Game.HasSkill(Game.SkillBurst)) ColourBurst();
        if (GameInput.SkillPressed && waveCd <= 0f && Game.HasSkill(Game.SkillWave) && dashT <= 0f) BrushWave();
    }

    void Land()
    {
        if (vy > -9f) return;
        if (clips == null) model.localScale = Vector3.Scale(modelScale, new Vector3(1.25f, 0.75f, 1.25f));
        Fx.Dust(transform.position, 6);
        if (clips != null && attackT < 0f && dashT <= 0f && move == Move.None) clips.Once("Jump_Land", 0.25f);
        ToonKit.Sfx("land", position: transform.position + Vector3.up);
    }

    // ------------------------------------------------------------ dash

    void StartDash(Vector3 dir)
    {
        BeginAction("StartDash");
        dashDir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
        dashT = DashTime;
        dashCd = stats.dashCooldown;
        attackT = -1f;
        queued = false;
        move = Move.None;
        bufferedAction = BufferedAction.None;
        charge = heldT = 0f;
        ghostT = 0f;
        perfectUsed = false;
        trail.emitting = true;
        dashHits.Clear();
        dashes++;
        Game.I.Lines(0.7f);
        if (clips == null) model.localScale = Vector3.Scale(modelScale, new Vector3(0.75f, 0.9f, 1.4f));
        if (clips != null)
        {
            clips.Release();
            // The tumble outlasts the burst of speed: he comes out of the roll already running.
            clips.Once("Dodge_Forward", DashTime + 0.12f);
        }
        Fx.Dust(transform.position, 5);
        Game.I.Cam.Kick(7f);
        ToonKit.Sfx("dash", position: transform.position + Vector3.up);
    }

    void DashStrike()
    {
        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.All[i];
            if (dashHits.Contains(e)) continue;
            if (Flat(e.transform.position - transform.position).magnitude > 1.4f + e.radius) continue;
            var result = Hit(e, stats.damage * 1.5f, false, 6f);
            if (result.outcome == HitOutcome.Miss) continue;
            dashHits.Add(e);
            if (result.Damaged) ToonKit.Sfx("hit", 1f, 1.3f, position: transform.position + Vector3.up);
        }
    }

    // ------------------------------------------------------------ sword

    void StartAttack(Vector3 inputDir)
    {
        BeginAction("StartAttack");
        attackT = 0f;
        AttackStep step = Step;
        float requested = step.time / stats.attackSpeed;
        float authoredPace = Hero.comboSpeed != null ? Hero.comboSpeed[comboStep] : 1f;
        float clipContact = Hero.comboContact != null ? Hero.comboContact[comboStep] : step.time * step.contact * authoredPace;
        if (clips != null && clips.Length(Hero.combo[comboStep]) > 0f)
            clipContact = Mathf.Min(clipContact, clips.Length(Hero.combo[comboStep]) * 0.85f);
        attackPlan = new GrowthPolicy.ContactPlan(requested, clipContact, step.contact);
        attackDur = attackPlan.duration; attackPower = attackPlan.power;
        hitDone = false;
        queued = false;
        lunge = step.lunge;
        if (Hero.comboTwist != null) twistTarget = Hero.comboTwist[comboStep];
        trail.emitting = true;
        if (clips != null)
        {
            // At a run the swing rides the upper body and the legs keep running; standing still, or
            // for the spin, the whole body goes into it.
            bool running = inputDir.sqrMagnitude > 0.01f && planar.magnitude > 2.5f && comboStep < 2 && Hero.comboSpeed != null;
            clips.Contact(Hero.combo[comboStep], attackPlan, running);
        }
        ToonKit.Sfx("swing", 1f, Weapon.swingPitch * (1f + comboStep * 0.12f), position: transform.position + Vector3.up);
        if (Weapon.swingLayer.Length > 0)
            ToonKit.Sfx(Weapon.swingLayer, Weapon.swingLayerVolume, Weapon.swingLayerPitch * (1f + comboStep * 0.06f), position: transform.position + Vector3.up);

        // Aim assist: face the nearest enemy in range, otherwise where the stick points.
        Enemy best = CombatTarget(step.reach * stats.reach + 1f, inputDir);
        Vector3 face = best != null ? Flat(best.transform.position - transform.position) : inputDir;
        if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
    }

    void TickAttack(float dt, Vector3 inputDir)
    {
        if (attackT < 0f || !CombatEligible || Game.I.DefeatPending) return;
        float previousAttackT = attackT;
        attackT += dt;
        if (!hitDone && attackT >= attackDur * Step.contact)
        {
            hitDone = true;
            Strike();
        }
        // A light blade may feint out before its cut lands (forfeiting the hit); heavier
        // families keep the swing until well after contact.
        float cancelAt = attackDur * Weapon.dodgeCancel;
        if (bufferedAction == BufferedAction.Dodge && attackT >= cancelAt)
        {
            float window = Mathf.Max(previousAttackT, cancelAt);
            ConsumeBufferedAction(inputDir, Time.time - Mathf.Max(0f, attackT - window));
            if (attackT < 0f) return;
        }
        if (attackT < attackDur) return;

        attackT = -1f;
        trail.emitting = dashT > 0f;
        if (clips != null && !queued)
        {
            // No follow-up: the arm carries the blade back to guard instead of snapping there.
            clips.Release();
            string recover = Hero.combo[comboStep] + "_Rec";
            if (clips.Has(recover)) clips.Upper(recover, 0.42f, 1.5f, 0.1f);
        }
        comboStep = (comboStep + 1) % (Game.HasSkill(Game.SkillFinisher) ? 3 : 2);   // the finisher has to be learned
        comboReset = 0.45f;
        if (queued) StartAttack(inputDir);
        else ConsumeBufferedAction(inputDir);
    }

    void Strike()
    {
        AttackStep step = Step;
        WeaponFamily weapon = Weapon;
        bool finisher = comboStep == 2;
        float reach = step.reach * stats.reach;
        int landed = 0;
        bool anyCrit = false, anyFlank = false, anyBreak = false;
        Vector3 fwd = transform.forward;

        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.All[i];
            if (!InSwing(e, step, reach, fwd)) continue;

            bool crit = ProductionRandom.Combat.value < stats.crit;
            bool flank = weapon.Flank(e.transform, transform.position);
            float dmg = stats.damage * weak * step.damage * ProductionRandom.Combat.Range(0.9f, 1.1f) * (flank ? weapon.flankBonus : 1f);
            var result = Hit(e, dmg, crit, step.push, step.heavy, stagger: step.stagger);
            if (!result.Damaged) continue;
            anyCrit |= crit;
            anyFlank |= flank;
            anyBreak |= result.guardBroken;
            landed++;
        }

        // The cut itself, drawn in the air with the same arc and reach that hit: a slanting arc for
        // the first two blows, a full circle for a spin, a streak for a thrust.
        Color edge = anyCrit || anyFlank ? UI.Gold : weapon.edge;
        Vector3 chest = transform.position + Vector3.up * 1.15f;
        if (step.thrust) Fx.Thrust(chest, fwd, reach + 0.5f, edge, 0.16f);
        else if (step.Spin) Fx.Slash(chest, transform.rotation, reach, 360f, new Color(edge.r, edge.g, edge.b, 0.8f), 0.24f, false);
        else Fx.Slash(chest, transform.rotation * Quaternion.Euler(0f, 0f, comboStep == 0 ? -24f : 20f), reach, step.ArcDegrees, edge, 0.16f, comboStep == 0);
        if (finisher && stats.shockFinisher) ShockFinisher(reach);
        if (landed == 0) return;

        strikes++;
        bool big = finisher || anyCrit;
        Juice.Hitstop(big ? weapon.finisherHitstop : weapon.hitstop);
        Game.I.Cam.Shake(big ? weapon.finisherShake : weapon.shake);
        if (step.heavy) Game.I.Cam.Kick(5f);
        if (anyFlank) Game.I.Word(chest + fwd * 1.2f + Vector3.up * 0.9f, "Weak point", UI.Gold, 28);
        if (anyBreak) Game.I.Word(chest + fwd * 1.2f + Vector3.up * 0.9f, "Guard broken", UI.Gold, 30);
        ToonKit.Sfx(anyCrit || anyFlank ? "crit" : "hit", 1f, weapon.swingPitch * (1f + comboStep * 0.15f), position: transform.position + Vector3.up);
        if (weapon.impactLayer.Length > 0)
            ToonKit.Sfx(weapon.impactLayer, weapon.impactLayerVolume * (finisher ? 1.3f : 1f), weapon.impactLayerPitch, position: transform.position + Vector3.up);
        Game.I.AddCombo(landed);
    }

    // Whether an enemy stands inside this blow: within reach and its arc, or along its lane for a thrust.
    // Anything pressed right against the hero is always caught, as before.
    bool InSwing(Enemy e, AttackStep step, float reach, Vector3 fwd)
    {
        Vector3 to = Flat(e.transform.position - transform.position);
        if (step.thrust)
        {
            float along = Vector3.Dot(to, fwd);
            return along >= -0.5f && along - e.radius <= reach && (to - fwd * along).magnitude <= 0.9f + e.radius;
        }
        float dist = to.magnitude - e.radius;
        if (dist > reach) return false;
        return step.Spin || dist <= 0.6f || Vector3.Dot(to.normalized, fwd) >= step.minDot;
    }

    void ShockFinisher(float innerReach)
    {
        Fx.Ring(transform.position, UI.Blue, innerReach, 6f, 0.35f);
        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            Enemy e = Enemy.All[i];
            float dist = Flat(e.transform.position - transform.position).magnitude - e.radius;
            if (dist > innerReach && dist < 6f) Hit(e, stats.damage * 1.2f, false, 10f);
        }
    }

    // heavy blows break a raised guard; light ones glance off it. An enemy left wide open
    // (staggered by a parry, a bash or a broken guard) takes a riposte: far more damage.
    HitResult Hit(Enemy e, float dmg, bool crit, float push, bool heavy = false, DamageKind kind = DamageKind.Physical, bool ignoreCover = false, float stagger = 0f, float launch = 0f)
    {
        if (!CombatContact.CanContact(transform.position + Vector3.up * 1.1f, e, 0.8f, ignoreCover)) return new HitResult(HitOutcome.Miss);
        Vector3 at = e.transform.position + Vector3.up * Mathf.Min(1.2f, e.height * 0.5f);
        Vector3 dir = Flat(e.transform.position - transform.position).normalized;
        bool riposte = e.Open;
        bool counter;
        dmg *= MasteryDamage(riposte, out counter);
        dmg *= move != Move.None ? movePower : attackT >= 0f ? attackPower : 1f;
        if (crit) dmg *= stats.critMultiplier;
        if (riposte)
        {
            dmg *= 2.2f * (stats.gatebreaker ? Upgrades.GatebreakerRiposte : 1f);
            crit = true;
        }
        bool air = e.Airborne;
        var result = e.ReceiveHit(new DamageRequest(dmg, transform.position, push, heavy || riposte, crit, this, kind, stagger, launch));
        if (!result.Damaged) return result;
        LastStruck = e; LastStruckAt = Time.time;
        // Gatebreaker: a heavy blow staggers even a brute or a Guardian, opening it for a riposte.
        if (stats.gatebreaker && heavy && !e.Dead && !e.Open && (e.kind == Enemy.Kind.Brute || e.IsBoss)) e.Stagger(Upgrades.GatebreakerStagger);
        if (counter) { masteryCounter = 0f; if (PilotProgress.Has("mobility", 2)) e.Stagger(0.6f); }
        if (riposte && PilotProgress.Has("duelist", 2)) AddBurst(12f);
        Fx.Sparks(at, dir + Vector3.up * 0.4f, crit ? UI.Gold : Color.white, crit ? 10 : 5);
        AddBurst(4f);

        // Fighting well is its own reward: varied moves, crits and juggles push the style rank up.
        int tag = styleTag >= 0 ? styleTag : move != Move.None ? 10 + (int)move : dashT > 0f ? 30 : comboStep;
        Game.I.StyleHit(tag, crit, air, riposte);
        if (crit || riposte) Game.I.Impact(riposte ? 0.9f : heavy ? 0.75f : 0.5f);
        if (e.Dead) Game.I.Cam.Punch(dir, heavy ? 0.5f : 0.3f);
        if (!riposte) return result;
        e.CloseOpening();
        Juice.Slow(0.3f);
        Game.I.Cam.Kick(6f);
        Game.I.Word(at + Vector3.up * 0.8f, "Riposte", UI.Gold, 34);
        ToonKit.Sfx("crit", 1f, 0.8f, position: transform.position + Vector3.up);
        return result;
    }

    // ------------------------------------------------------------ damage

    public override bool TakeDamage(float amount, Vector3 from, float push)
        => ReceiveHit(new DamageRequest(amount, from, push, heavy: amount >= 2f)).Damaged;

    public override HitResult ReceiveHit(DamageRequest request)
    {
        var result = ReceiveHitCore(request); PlaytestLog.Contact(this, request, result); return result;
    }
    HitResult ReceiveHitCore(DamageRequest request)
    {
        float amount = request.amount;
        Vector3 from = request.from;
        float push = request.push;
        bool guardBroken = false;
        if (!CombatEligible || Game.I.DefeatPending || Game.I.State != Game.GameState.Playing || !CombatContact.ValidDamage(amount)) return new HitResult(HitOutcome.Miss);

        // Dashing through an attack at the last moment is rewarded, not just forgiven.
        if (dashT > 0f || dashGrace > 0f)
        {
            if (!perfectUsed)
            {
                perfectUsed = true;
                MasteryEvade();
                dashCd = 0f;
                Juice.Slow(0.7f);
                AddBurst(20f);
                Game.I.Word(transform.position + Vector3.up * 2.3f, "Perfect dodge", UI.Blue, 32);
                Game.I.AddStyle(50f);
                Game.I.Lines(1f);
                ToonKit.Sfx("perfect", position: transform.position + Vector3.up);
            }
            return new HitResult(HitOutcome.Immune);
        }
        if (invuln > 0f) return new HitResult(HitOutcome.Immune);
        // A slide passes under bolts and quick swipes; only the heavy blows still connect.
        if (move == Move.Slide && !request.heavy) return new HitResult(HitOutcome.Immune);

        // Guarding: a block raised at the last instant is a parry.
        if (move == Move.Block)
        {
            Vector3 toSrc = Flat(from - transform.position);
            bool front = toSrc.sqrMagnitude < 0.01f || Vector3.Dot(toSrc.normalized, transform.forward) > -0.2f;
            Vector3 guard = transform.position + Vector3.up * 1.1f + transform.forward * 0.6f;
            if (front && blockT < Weapon.parryWindow * Access.ParryScale)
            {
                invuln = 0.5f;
                Juice.Slow(0.6f);
                AddBurst(15f);
                Game.I.Word(guard + Vector3.up * 0.9f, "Parry", UI.Gold, 36);
                Game.I.AddStyle(60f);
                Game.I.Impact(0.8f);
                Game.I.Cam.Shake(0.3f);
                Fx.Flash(guard, Color.white, 2.4f);
                Fx.Sparks(guard, transform.forward, UI.Gold, 8);
                ToonKit.Sfx("perfect", position: transform.position + Vector3.up);
                for (int i = Enemy.All.Count - 1; i >= 0; i--)
                {
                    Enemy e = Enemy.All[i];
                    if ((e.transform.position - transform.position).magnitude - e.radius > 3.6f
                        || !CombatContact.CanContact(transform.position + Vector3.up * 1.1f, e)) continue;
                    e.Stagger(1.6f);
                    e.Shove(transform.position, 7f);
                }
                if (clips != null) clips.Once("Block_Attack", 0.35f);
                return new HitResult(HitOutcome.Parried);
            }
            if (front && !request.heavy)
            {
                invuln = 0.2f;
                // An axe haft turns a blow aside but gives ground; a shield stands firm.
                knock = -transform.forward * (Weapon.guard == GuardStyle.Shield ? 4f : 6.5f);
                Fx.Sparks(guard, transform.forward, Color.white, 4);
                ToonKit.Sfx("hit", 0.8f, 1.6f, position: transform.position + Vector3.up);
                if (clips != null) clips.Once("Block_Hit", 0.25f);
                return new HitResult(HitOutcome.Blocked);
            }
            if (front)
            {
                // Heavy blows smash through a plain guard, but it still takes the edge off.
                amount -= 1f;
                guardBroken = true;
                Game.I.Toast("GUARD BROKEN", UI.Red);
            }
            End();
        }

        // High in the tower, and in the worst of the Wilds, everything hits harder.
        gestureT = 0f;
        CancelOffense();
        float actualDamage = Mathf.Max(0f, amount + Game.I.Harm);
        if (Game.I.TrainingActive && request.source is Enemy partner && partner.Training)
            actualDamage = Mathf.Min(actualDamage, Mathf.Max(0f, hp - 1f));
        if (actualDamage == 0f) return new HitResult(HitOutcome.Blocked, guardBroken: guardBroken);
        var result = ApplyDamage(request.WithAmount(actualDamage));
        if (!result.Damaged) return result;
        invuln = Mathf.Max(invuln, 1f);
        if (clips != null && !Dead && attackT < 0f)
        {
            clips.Release();
            clips.Once(request.heavy ? "Hit_B" : "Hit_A", request.heavy ? 0.45f : 0.35f);
        }
        Juice.Hitstop(0.12f);
        Game.I.Cam.Shake(0.35f);
        Game.I.OnPlayerHurt();
        ToonKit.Sfx("hurt", position: transform.position + Vector3.up);

        if (stats.thorns > 0f && !Dead)
        {
            Fx.Ring(transform.position, UI.Purple, 0.5f, 4.5f, 0.3f);
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemy.All[i];
                if (Flat(e.transform.position - transform.position).magnitude - e.radius < 4.5f
                    && CombatContact.CanContact(transform.position + Vector3.up * 1.1f, e))
                    e.ReceiveHit(new DamageRequest(stats.thorns, transform.position, 12f, source: this, kind: DamageKind.Magic));
            }
        }
        return new HitResult(result.outcome, result.damage, guardBroken);
    }

    protected override void Die()
    {
        if (stats.secondWinds > 0)
        {
            stats.secondWinds--;
            hp = Mathf.Min(2f, maxHp);
            invuln = 2.5f;
            Juice.Slow(0.8f);
            Fx.Ring(transform.position, UI.Gold, 0.5f, 8f, 0.5f);
            Game.I.Toast("SECOND WIND!", UI.Gold);
            Game.I.Flash(UI.Gold, 0.4f);
            ToonKit.Sfx("restore", position: transform.position + Vector3.up);
            foreach (var e in Enemy.All) e.Shove(transform.position, 18f);
            return;
        }
        Fx.Burst(transform.position + Vector3.up, new Color(0.16f, 0.55f, 0.95f), 16, 7f);
        trail.emitting = false;
        move = Move.None;
        if (clips != null)
        {
            clips.Release();
            clips.Once("Death_A", 1.5f, 0.12f, true);
        }
        Game.I.OnPlayerDied();
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
