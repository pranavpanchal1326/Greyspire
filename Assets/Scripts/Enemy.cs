using System.Collections.Generic;
using UnityEngine;

// Every hostile thing. The tower is held by monsters: slimes, imps, Puglins, bombers,
// brutes, and the Gloom King at the top. Out in the Wilds there are monsters too, and
// bandits: people, with swords, shields and knives. How the sword-carriers duel is in
// EnemyDuel.cs.
public partial class Enemy : Actor
{
    public enum Kind { Slime, Minion, BigSlime, Spitter, Bomber, Brute, Boss }

    public static readonly List<Enemy> All = new List<Enemy>();

    static readonly Color Warn = new Color(1f, 0.25f, 0.2f);
    const float SleepDistance = 95f;

    public Kind kind;
    public float radius = 0.5f;
    public float height = 1f;
    public bool elite;
    public int coinValue = 1;
    public int scoreValue = 10;
    public string OriginRegionId { get; private set; } = "";
    public string ObjectiveId { get; private set; } = "";
    public bool KillRewardsCommitted { get; private set; }
    public string SpawnId { get; private set; }
    RandomStream aiRandom, appearanceRandom, dropRandom;
    public string BirthCampaign { get; private set; }
    public void CommitKillRewards() => KillRewardsCommitted = true;
    string questId = "", questAttempt = "", originExpedition = "";
    readonly string objectiveEventId = System.Guid.NewGuid().ToString("N");
    public QuestEvent ObjectiveEvent => new QuestEvent(questId, questAttempt, originExpedition, objectiveEventId, OriginRegionId, ObjectiveId, ObjectiveCause.Kill, IsBandit, kind);
    public void BindObjective(string region, string target = "", QuestEvent? parent = null)
    {
        if (OriginRegionId.Length > 0) throw new System.InvalidOperationException("Enemy objective origin is immutable");
        if (Game.RegionIndex(region) < 0) throw new System.ArgumentException("Unknown enemy region");
        OriginRegionId = region; ObjectiveId = target;
        questId = parent.HasValue ? parent.Value.quest : Save.ActiveMissionId;
        questAttempt = parent.HasValue ? parent.Value.attempt : Save.MissionAttempt;
        originExpedition = parent.HasValue ? parent.Value.expedition : Save.ExpeditionId;
    }
    [HideInInspector] public float aggro = 9999f;   // how close the hero must come before it notices
    bool provoked;                                  // it has been hit: it comes from much further off
    [HideInInspector] public string title;          // a name shown over its head: Guardians and wanted chiefs have one
    [HideInInspector] public int tier = 1;          // floor-equivalent strength of where it was born: sets its health and what it is worth
    bool alerted;

    public bool IsBandit => bandit;
    public bool Alerted => alerted;
    public bool Engaged => alerted || provoked;
    float airT, airDur;
    string meleeClip = "1H_Melee_Attack_Chop", castClip = "Spellcast_Shoot";

    public bool Airborne => airT > 0f;
    public bool Defeated => defeated;
    public override bool CombatEligible => base.CombatEligible && !defeated;

    // Staggered and wide open: the next hit that lands is a riposte.
    public bool Open => openT > 0f && !Dead && !defeated;
    float openT, riseT;

    public void CloseOpening() { openT = 0f; }
    [HideInInspector] public float shownHp;     // lags behind hp for the health bar

    CharacterController cc;
    NavFollower nav;
    Vector3 safeFeet;
    LimbAnimator anim;      // procedural (bomber, Gloom King)
    CharAnim clips;         // rigged characters
    float stun;
    Transform model;
    Vector3 baseScale, lungeDir, leapFrom, leapTo, slide;
    Transform keg;          // the sapper's powder keg
    float deadT, fadeAt;    // how long it has lain dead, and when the body starts to fade
    bool heavyBlow;         // the last hit that landed was a heavy one: decides how it falls
    Color color;
    float speed, cooldown, windup = -1f, lungeT, leapT = -1f, phase, strafe = 1f, punch, fuseTick;
    int level, bossPhase = 1, action;
    bool lungeHit, silent, defeated, bandit;
    string campIdle = "Idle";

    public bool IsBoss => kind == Kind.Boss;
    public bool Training;
    public float LastTrainingDamage { get; private set; }

    // bandit: a human robber instead of the monster of that kind (fighters, knife-throwers, chiefs).
    public static Enemy Create(Kind kind, Vector3 pos, int level, bool elite = false, bool bandit = false, int originTier = -1, string spawnId = null)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(Game.I.LevelRoot, false);
        go.transform.position = pos;
        var e = go.AddComponent<Enemy>();
        e.SpawnId = spawnId ?? ProductionRandom.NextSpawnId();
        e.aiRandom = ProductionRandom.Stream("ai", e.SpawnId);
        e.appearanceRandom = ProductionRandom.Stream("appearance", e.SpawnId);
        e.dropRandom = ProductionRandom.Stream("drops", e.SpawnId);
        e.bandit = bandit && (kind == Kind.Minion || kind == Kind.Spitter || kind == Kind.Brute);
        e.Init(kind, level, elite, originTier);
        if (WorldSupport.TryStand(pos, e.cc.radius * go.transform.localScale.x, e.cc.height * go.transform.localScale.y,
            out Vector3 feet, go.transform)) go.transform.position = feet;
        e.safeFeet = go.transform.position;
        e.CaptureContactPose();
        e.nav = NavFollower.AddTo(go); e.nav.label = kind.ToString();
        return e;
    }

    // What bandits do around their camp while nobody is bothering them.
    static readonly string[] CampIdles = { "Idle_Loop", "Idle_Talking_Loop", "Idle_FoldArms_Loop", "Idle_Loop" };

    void Init(Kind k, int lvl, bool isElite, int originTier)
    {
        BirthCampaign = JourneyStore.Campaign;
        kind = k;
        level = lvl;
        elite = isElite && k != Kind.Boss && k != Kind.Bomber;
        float baseHp;
        bool limbs = false;

        switch (k)
        {
            case Kind.BigSlime:
                model = Gel.Build(transform, 2f, new Color(0.2f, 0.27f, 0.09f), new Color(0.03f, 0.05f, 0.02f));
                baseHp = 70; speed = 2.5f; radius = 0.95f; height = 1.6f; coinValue = 3; scoreValue = 30;
                color = new Color(0.3f, 0.45f, 0.14f);
                break;
            case Kind.Minion:
                // The rank and file. In the tower they are imps: clawed, quick, and properly unpleasant.
                // On the road they are bandits with swords, and the better ones carry shields.
                if (bandit)
                {
                    float pick = appearanceRandom.value;
                    model = Humans.Build(pick < 0.4f ? Looks.Bandit : pick < 0.75f ? Looks.BanditWoman : Looks.Cutthroat, transform);
                    bandit = model != null;
                    if (bandit)
                    {
                        Humans.Hold(model, "Sword_Bronze", false, Humans.Grip, Humans.GripAngles);
                        meleeClip = appearanceRandom.value < 0.5f ? "Sword_Regular_A" : "Sword_Regular_B";
                        if (isElite || (Game.I.Floor >= 6 && aiRandom.value < 0.35f))
                        {
                            guards = true;
                            Humans.Hold(model, "Shield_Wooden", true, Humans.ShieldGrip, Humans.ShieldAngles);
                        }
                    }
                }
                if (model == null)
                {
                    model = Humans.Monster("Imp", appearanceRandom.Range(1, 4), transform, 1.55f);
                    if (model != null && !Humans.Animated(model)) { Destroy(model.gameObject); model = null; }
                    if (model != null) meleeClip = "Zombie_Scratch";
                }
                if (model == null)
                {
                    model = ToonKit.SpawnCharacter(appearanceRandom.value < 0.35f ? "Skeleton_Rogue" : "Skeleton_Minion", "skeleton_texture", transform, 0.72f);
                    if (model == null) model = ToonKit.Spawn("Slime", transform);
                    else ToonKit.Arm(model, "Skeleton_Blade", "skeleton_texture");
                }
                baseHp = 26; speed = 3.9f + lvl * 0.3f; radius = 0.45f; height = 1.7f; coinValue = 1; scoreValue = 15;
                color = new Color(0.8f, 0.8f, 0.75f);
                break;
            case Kind.Spitter:
                // The ones that fight from a distance. Monsters: Puglins, squat little things that lob
                // bolts overarm. Bandits: knife-throwers who keep well back.
                if (bandit)
                {
                    model = Humans.Build(Looks.Thrower, transform);
                    bandit = model != null;
                    if (bandit) castClip = "Throw";
                }
                if (model == null)
                {
                    model = Humans.Monster("Puglin", appearanceRandom.Range(1, 4), transform, 1.2f);
                    if (model != null && !Humans.Animated(model)) { Destroy(model.gameObject); model = null; }
                    if (model != null) castClip = "OverhandThrow";
                }
                if (model == null)
                {
                    model = ToonKit.SpawnCharacter("Skeleton_Mage", "skeleton_texture", transform, 0.8f);
                    if (model == null) model = ToonKit.Spawn("Spitter", transform);
                    else ToonKit.Arm(model, "Skeleton_Staff", "skeleton_texture");
                }
                baseHp = 30; speed = 2.8f; radius = 0.45f; height = 1.9f; coinValue = 2; scoreValue = 25;
                color = new Color(0.55f, 0.3f, 0.85f);
                break;
            case Kind.Bomber:
                // A Puglin sapper: a powder keg strapped to its back and a short fuse. It runs at you and does not stop.
                model = Humans.Monster("Puglin", 2, transform, 1.15f);
                if (model != null && !Humans.Animated(model)) { Destroy(model.gameObject); model = null; }
                if (model != null) keg = Humans.Strap(model, "Barrel", "spine_03", 0.5f);
                else
                {
                    model = ToonKit.Spawn("Bomber", transform);
                    limbs = true;
                }
                baseHp = 16; speed = 4.8f; radius = 0.45f; height = 1.2f; coinValue = 2; scoreValue = 20;
                color = new Color(1f, 0.5f, 0.15f);
                break;
            case Kind.Brute:
                // Monsters: an imp grown to twice the height of its kin. Bandits: the chief of the
                // camp, half again the size of a man, with an axe to match.
                if (bandit)
                {
                    model = Humans.Build(Looks.BanditChief, transform, 1.45f);
                    bandit = model != null;
                    if (bandit) Humans.Hold(model, "Axe_Bronze", false, Humans.Grip, Humans.GripAngles);
                }
                if (model == null)
                {
                    model = Humans.Monster("Imp", 3, transform, 2.9f);
                    if (model != null && !Humans.Animated(model)) { Destroy(model.gameObject); model = null; }
                }
                if (model == null)
                {
                    model = ToonKit.SpawnCharacter("Skeleton_Warrior", "skeleton_texture", transform, 1.3f);
                    if (model == null)
                    {
                        model = ToonKit.Spawn("Brute", transform, 1.6f);
                        limbs = true;
                    }
                    else ToonKit.Arm(model, "Skeleton_Axe", "skeleton_texture");
                }
                baseHp = 220; speed = 2.7f; radius = 0.85f; height = 3f; coinValue = 8; scoreValue = 100;
                color = new Color(0.85f, 0.25f, 0.22f);
                break;
            case Kind.Boss:
                // The Gloom King: a hooded giant, twice the height of a man, sword and shield in hand.
                model = Humans.Build(Looks.GloomKing, transform, 1.95f);
                if (model != null)
                {
                    Humans.Hold(model, "Sword_Bronze", false, Humans.Grip, Humans.GripAngles);
                    Humans.Hold(model, "Shield_Wooden", true, Humans.ShieldGrip, Humans.ShieldAngles);
                }
                else
                {
                    model = ToonKit.SpawnCharacter("Skeleton_Warrior", "skeleton_texture", transform, 1.55f, new Color(0.62f, 0.45f, 0.85f));
                    if (model == null) model = ToonKit.Spawn("GloomKing", transform, 2.3f);
                    else
                    {
                        ToonKit.Arm(model, "Skeleton_Axe", "skeleton_texture");
                        ToonKit.Arm(model, "Skeleton_Shield_Small_A", "skeleton_texture", true);
                    }
                    limbs = true;
                }
                baseHp = 1400; speed = 3.2f; radius = 1.2f; height = 4f; coinValue = 40; scoreValue = 1000;
                color = new Color(0.5f, 0.2f, 0.5f);
                break;
            default:
                model = Gel.Build(transform, 1.05f, Gel.Green, Gel.GreenDeep);
                baseHp = 20; speed = 3.4f + lvl * 0.3f; radius = 0.5f; height = 0.85f; coinValue = 1; scoreValue = 10;
                color = new Color(0.3f, 0.45f, 0.14f);
                break;
        }
        baseScale = model.localScale;
        clips = CharAnim.Attach(gameObject, model);
        duelist = k == Kind.Minion && clips != null;
        // Imps do not walk like men: they prowl, hunched, and stand swaying when there is nothing to kill.
        if (clips != null && clips.Human && !bandit && (k == Kind.Minion || k == Kind.Brute))
        {
            clips.walkClip = "Zombie_Walk_Fwd_Loop";
            clips.idleClip = "Zombie_Idle_Loop";
        }
        campIdle = CampIdles[appearanceRandom.Range(0, CampIdles.Length)];
        if (clips != null && !bandit)
        {
            // Monsters rise out of the ground before doing anything. Bandits are simply there.
            stun = 0.9f;
            clips.Once(clips.Has("Spawn_Ground") ? "Spawn_Ground" : "Idle", 0.9f, 0f);
        }
        if (limbs)
        {
            anim = gameObject.AddComponent<LimbAnimator>();
            anim.Bind(model);
        }

        cc = gameObject.AddComponent<CharacterController>();
        cc.radius = radius;
        cc.height = Mathf.Max(height, radius * 2f);
        cc.slopeLimit = WorldSupport.MaxWalkSlope;
        cc.stepOffset = 0.3f;
        cc.center = new Vector3(0, cc.height * 0.5f + 0.02f, 0);

        // How dangerous the ground it stands on is: the tower floor, or the tier of its region of the Wilds.
        tier = Mathf.Max(1, originTier > 0 ? originTier : Game.I.Floor);
        // Play-testers found the hero over-powered from floor 15: above floor 10 the tower's monsters
        // now toughen faster than a levelling hero's sword does. The Wilds keep the old curve, and so
        // does the Gloom King: his fight was tuned on its own and nobody has asked for it to be longer.
        bool steeper = !Game.I.InWilds && k != Kind.Boss;
        float climb = 0.03f * (tier - 1) + (steeper ? 0.07f * Mathf.Max(0, tier - 10) : 0f);
        float hpScale = (k == Kind.Boss ? 1f : 1f + 0.35f * lvl) * (1f + 0.25f * Game.I.Gloom) * (1f + climb);
        speed *= 1f + 0.04f * Game.I.Gloom;
        if (elite)
        {
            hpScale *= 2.2f;
            coinValue *= 3;
            scoreValue *= 3;
            transform.localScale = Vector3.one * 1.2f;
        }
        InitActor(baseHp * hpScale);
        shownHp = hp;
        if (k == Kind.Boss)
        {
            flashStrength = 0.35f;
            block.SetColor("_OutlineColor", new Color(0.75f, 0.3f, 1f));
            block.SetFloat("_OutlineWidth", 0.07f);
            SetFlash(0f);
        }
        if (elite)
        {
            block.SetColor("_OutlineColor", UI.Gold);
            block.SetFloat("_OutlineWidth", 0.05f);
            SetFlash(0f);
        }

        phase = appearanceRandom.value * 10f;
        strafe = aiRandom.value < 0.5f ? -1f : 1f;
        cooldown = aiRandom.Range(0.6f, 1.6f);
        punch = 1f;
        AssignRole();
        Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(0.4f, 0.3f, 0.6f), 6, 3f);
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); EncounterDirector.Release(this); }

    void Update()
    {
        if (Training)
        {
            float practiceDt = GameClocks.SimulationDelta;
            openT -= practiceDt; stun = Mathf.Max(0, stun - practiceDt);
            if (airT > 0f)
            {
                airT = Mathf.Max(0, airT - practiceDt);
                model.localPosition = Vector3.up * Mathf.Sin((1f - airT / Mathf.Max(0.01f, airDur)) * Mathf.PI) * 2.4f;
            }
            TickActor(); return;
        }
        shownHp = Mathf.MoveTowards(shownHp, hp, maxHp * 0.6f * GameClocks.SimulationDelta);
        if (Dead)
        {
            // The blow that killed it carries the body with it as it goes down.
            if (slide.sqrMagnitude > 0.01f)
            {
                transform.position += slide * GameClocks.SimulationDelta;
                slide = Vector3.MoveTowards(slide, Vector3.zero, 16f * GameClocks.SimulationDelta);
            }
            // The body lies a moment, then the Gloom lets go of it: it thins to nothing, shedding motes.
            deadT += GameClocks.SimulationDelta;
            if (fadeAt > 0f && deadT > fadeAt)
            {
                block.SetFloat("_Dissolve", Mathf.Clamp01((deadT - fadeAt) / 0.75f));
                ApplyBlock();
                if (ProductionRandom.Cosmetic.value < GameClocks.SimulationDelta * 16f)
                    Fx.Spark(transform.position + Vector3.up * 0.25f + ProductionRandom.Cosmetic.insideUnitSphere * radius, Vector3.up * ProductionRandom.Cosmetic.Range(0.6f, 1.6f),
                        new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.7f), 0.12f, 0.9f);
            }
            return;
        }
        if (Game.I.State != Game.GameState.Playing) return;

        float dt = GameClocks.SimulationDelta;
        if (dt <= 0f) return;
        TickRoles(dt);
        PlayerController player = Game.I.Player;
        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : transform.forward;
        Vector3 vel = Vector3.zero;
        Vector3 shape = Vector3.one;
        bool turn = true;
        // A rallied enemy recovers between attacks faster.
        cooldown -= dt * (Rallied ? RallyRecovery : 1f);
        phase += dt;
        openT -= dt;
        faceDir = Vector3.zero;

        // Knocked into the air: helpless until it lands (and it takes extra damage up there).
        if (airT > 0f)
        {
            airT -= dt;
            float k = 1f - Mathf.Clamp01(airT / airDur);
            model.localPosition = Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.4f;
            if (airT <= 0f)
            {
                model.localPosition = Vector3.zero;
                stun = Mathf.Max(stun, 0.35f);
                Fx.Dust(transform.position, 4);
            }
            if (cc.enabled) cc.SimpleMove(knock);
            TickActor();
            return;
        }

        // Once it has seen the hero it follows a good deal further before giving up; once it
        // has been hit, further still. Losing the hero puts it back to how it was.
        float reach = provoked ? Mathf.Max(aggro * 1.8f, 60f) : alerted ? aggro * 1.8f : aggro;
        // After dark the Gloom is restless: everything out in the Wilds notices the hero from further off.
        if (Game.I.InWilds) reach *= 1f + 0.3f * Game.I.Night;
        if (!alerted && dist <= reach)
        {
            // The rest of its pack or camp wakes with it.
            alerted = true;
            // It squares up: fists or blade come up as it turns to face him.
            if (clips != null && clips.Human) clips.Upper("Punch_Enter", 0.7f, 0f, 0.12f);
            foreach (Enemy mate in All)
                if (mate != this && (mate.transform.position - transform.position).sqrMagnitude < 100f) mate.alerted = true;
            OrganisePack();
        }
        if (dist > reach && !IsBoss)
        {
            alerted = false;
            provoked = false;
            guarding = false;
            duel = Duel.Approach;
            // Camps a long way off are not drawn or animated at all until the hero comes nearer.
            bool near = dist < SleepDistance;
            if (model.gameObject.activeSelf != near) model.gameObject.SetActive(near);
            if (!near) return;
            if (clips != null)
            {
                // Idle camps shift about: now and then one of them taunts at nothing. Bandits stand
                // around their fire, talking, arms folded, keeping watch.
                if (clips.Has("Taunt") && ProductionRandom.Cosmetic.value < dt * 0.05f) clips.Once("Taunt");
                else clips.Loop(bandit ? campIdle : clips.idleClip, 1f, 0.3f);
            }
            if (anim != null) anim.move = 0f;
            if (cc.enabled) cc.SimpleMove(knock);
            TickActor();
            return;
        }

        if (stun > 0f)
        {
            stun -= dt;
            // Knocked flat: the second half of the stagger is spent getting back up.
            if (riseT > 0f && stun <= riseT)
            {
                if (clips != null) clips.Once("Spawn_Ground", Mathf.Max(0.2f, stun), 0.12f);
                riseT = 0f;
            }
            // Once the flinch has played out, a stunned enemy sways on its feet rather than freezing.
            if (clips != null && !clips.Busy) clips.Loop("Idle");
            if (cc.enabled) cc.SimpleMove(knock);
            TickActor();
            return;
        }

        switch (kind)
        {
            case Kind.Minion:
                vel = duelist ? TickDuelist(player, dir, dist, dt, ref turn) : TickSlime(player, dir, dist, dt, ref shape, ref turn);
                break;
            case Kind.Slime:
            case Kind.BigSlime:
                vel = TickSlime(player, dir, dist, dt, ref shape, ref turn);
                break;
            case Kind.Spitter:
                vel = TickSpitter(player, dir, dist, dt, ref shape);
                break;
            case Kind.Bomber:
                vel = TickBomber(player, dir, dist, dt, ref shape);
                break;
            case Kind.Brute:
                vel = Gatekeeper ? TickGatekeeper(player, dir, dist, dt, ref turn) : TickBrute(player, dir, dist, dt, ref turn);
                break;
            case Kind.Boss:
                vel = TickBoss(player, dir, dist, dt, ref turn);
                break;
        }
        if (Dead) return;
        if (Rallied) vel *= RallySpeed;
        Footfall(vel, dt);

        punch = Mathf.MoveTowards(punch, 0f, dt * 5f);
        if (clips != null)
        {
            // Rigged characters keep their proportions; clips do the acting.
            shape = Vector3.one;
            if (leapT < 0f) model.localPosition = Vector3.zero;
            if (guarding) clips.Loop("Blocking", 1f, 0.12f);
            else if (clips.Human)
            {
                // Feet follow the direction of travel: a duelist circling its mark side-steps.
                Vector3 local = transform.InverseTransformDirection(vel);
                clips.Move(vel.magnitude, new Vector2(local.x, local.z));
            }
            else if (vel.sqrMagnitude > 0.2f) clips.Loop(kind == Kind.Minion ? "Running_A" : "Walking_A", kind == Kind.Minion ? 1.2f : 1f);
            else clips.Loop("Idle");
        }
        // Jelly bulges when it is struck. Flesh and bone do not.
        model.localScale = Vector3.Scale(baseScale, shape) * (1f + punch * (clips != null ? 0f : 0.2f));
        if (turn)
        {
            Vector3 face = faceDir.sqrMagnitude > 0.01f ? faceDir : dir;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 8f * dt);
        }
        if (cc.enabled)
        {
            // Never crowd right on top of the hero, and never ride on anyone's head.
            if (dist < radius + 0.55f) vel -= dir * 4f;
            Vector3 separation = Vector3.zero;
            for (int i = 0; i < All.Count; i++)
            {
                Enemy other = All[i];
                if (other == this || !other.CombatEligible) continue;
                Vector3 gap = transform.position - other.transform.position;
                if (Mathf.Abs(gap.y) > Mathf.Max(cc.height * transform.lossyScale.y,
                    (other.cc != null ? other.cc.height : other.height) * other.transform.lossyScale.y) + 0.2f) continue;
                gap.y = 0f;
                float room = radius * transform.lossyScale.x + other.radius * other.transform.lossyScale.x;
                if (gap.magnitude >= room) continue;
                Vector3 away = gap.sqrMagnitude > 0.0001f ? gap.normalized : (All.IndexOf(this) < i ? Vector3.left : Vector3.right);
                separation += away * Mathf.Min(4f, (room - gap.magnitude) * 6f);
            }
            if (nav != null && nav.Attach() && vel.sqrMagnitude > 0.01f && lungeT <= 0f && leapT < 0f
                && (dist > 4f || Mathf.Abs(player.transform.position.y - transform.position.y) > 1.2f))
                vel = nav.DirectionTo(player.transform.position, dt) * vel.magnitude;
            cc.SimpleMove(vel + knock + separation);
            if (WorldSupport.TryGround(transform.position, out _, 0.3f, 0.4f)) safeFeet = transform.position;
            else if (transform.position.y < safeFeet.y - 20f && WorldSupport.TryStand(safeFeet, radius, height, out var recovered, transform))
            { cc.enabled = false; transform.position = recovered; cc.enabled = true; CaptureContactPose(); }
        }
        TickActor();
    }

    // ------------------------------------------------------------ behaviours

    // Slimes hop after you, crouch (the tell), then pounce.
    Vector3 TickSlime(PlayerController player, Vector3 dir, float dist, float dt, ref Vector3 shape, ref bool turn)
    {
        if (lungeT > 0f)
        {
            lungeT -= dt;
            turn = false;
            shape = new Vector3(0.85f, 1.2f, 1.2f);
            if (!lungeHit && dist < radius + 0.9f)
            {
                lungeHit = true;
                HitPlayer(player, 1f, transform.position, 10f);
            }
            if (lungeT <= 0f) ReleaseCommit();
            return dist > radius + 0.7f ? lungeDir * 11f : Vector3.zero;
        }
        if (windup > 0f)
        {
            windup -= dt;
            turn = windup > 0.15f;
            shape = new Vector3(1.3f, 0.6f, 1.3f);
            if (windup <= 0f)
            {
                lungeT = 0.28f;
                lungeDir = transform.forward;
                lungeHit = false;
                cooldown = 1.4f;
            }
            return Vector3.zero;
        }
        if (dist < radius + 2.4f && cooldown <= 0f)
        {
            if (Commit())
            {
                BeginAction(kind.ToString());
                windup = 0.4f + UnseenWarning();
                Tell(kind == Kind.BigSlime ? 0.9f : 1.3f);
                SetFlash(0.6f);
                if (clips != null) clips.Once(meleeClip, 0.75f);
                return Vector3.zero;
            }
            // Not its turn: work round to the hero's flank and wait there.
            Vector3 side = Vector3.Cross(Vector3.up, dir) * strafe;
            float off = dist - (radius + 3f);
            return side * speed * 0.55f + dir * Mathf.Clamp(off, -1f, 1f) * speed * 0.5f;
        }

        float hop = Mathf.Abs(Mathf.Sin(phase * 5f));
        model.localPosition = Vector3.up * hop * 0.3f * baseScale.y;
        shape = new Vector3(1.15f - hop * 0.2f, 0.8f + hop * 0.3f, 1.15f - hop * 0.2f);
        return dist > radius + 0.9f ? dir * speed : Vector3.zero;
    }

    // Wizards keep their distance, strafe, and lob bolts.
    Vector3 TickSpitter(PlayerController player, Vector3 dir, float dist, float dt, ref Vector3 shape)
    {
        model.localPosition = Vector3.up * (0.25f + Mathf.Sin(phase * 2.5f) * 0.12f);
        if (windup <= 0f && TickCaller(dt)) return Vector3.zero;
        if (windup > 0f)
        {
            windup -= dt;
            shape = Vector3.one * (1f + (0.5f - windup) * 0.4f);
            if (windup <= 0f)
            {
                Vector3 origin = transform.position + Vector3.up * 1.2f + dir * 0.6f;
                Vector3 aim = (player.transform.position + Vector3.up * 0.9f - origin).normalized;
                // Monsters lob bolts of gloom; bandits throw knives, which fly faster and flatter.
                float fast = bandit ? 15f + level : 11f + level;
                Projectile.Fire(origin, aim, fast, bandit, this);
                if (elite)
                {
                    Projectile.Fire(origin, Quaternion.Euler(0, 18f, 0) * aim, fast, bandit, this);
                    Projectile.Fire(origin, Quaternion.Euler(0, -18f, 0) * aim, fast, bandit, this);
                }
                ToonKit.Sfx(bandit ? "swing" : "shoot", 1f, bandit ? 1.5f : 1f, position: transform.position + Vector3.up);
                // The shot in flight is still its commitment for a moment.
                ReleaseCommit(0.5f);
            }
            return Vector3.zero;
        }
        // It needs a clear line to the hero, and its turn.
        if (cooldown <= 0f && dist < 17f && WorldSupport.ClearSight(transform.position + Vector3.up * 1.2f, player.transform.position + Vector3.up * 1.1f) && Commit())
        {
            BeginAction(kind.ToString());
            windup = 0.5f + UnseenWarning();
            Tell(1.15f);
            cooldown = 2.4f;
            strafe = -strafe;
            SetFlash(0.5f);
            if (clips != null) clips.Once(castClip, 0.8f);
        }
        if (dist < 6f) return -dir * speed;
        if (dist > 11f) return dir * speed;
        return Vector3.Cross(Vector3.up, dir) * (speed * 0.6f * strafe);
    }

    // Bombers sprint at you, hiss, and blow up. Kill them first or get clear.
    Vector3 TickBomber(PlayerController player, Vector3 dir, float dist, float dt, ref Vector3 shape)
    {
        if (windup > 0f)
        {
            windup -= dt;
            if (anim != null) anim.move = 0f;
            float pulse = Mathf.PingPong(phase * 10f, 1f);
            // The fuse spits.
            if (keg != null) Fx.Spark(keg.position + Vector3.up * 0.3f, Vector3.up * 2f + ProductionRandom.Cosmetic.insideUnitSphere * 2f, new Color(2f, 1.2f, 0.4f), 0.1f, 0.3f);
            shape = Vector3.one * (1f + pulse * 0.15f + (0.9f - windup) * 0.3f);
            SetFlash(pulse * 0.8f);
            if ((fuseTick -= dt) <= 0f)
            {
                fuseTick = 0.15f;
                ToonKit.Sfx("fuse", 0.7f, 1.6f - windup, position: transform.position + Vector3.up);
            }
            if (windup <= 0f) Explode(player);
            return Vector3.zero;
        }
        if (dist < 2.4f)
        {
            Commit(true);
            BeginAction(kind.ToString());
            windup = 0.9f + UnseenWarning();
            Fx.Telegraph(transform.position, 3.4f, 0.9f, Warn);
            return Vector3.zero;
        }
        if (anim != null) anim.move = 1f;
        return dir * speed;
    }

    void Explode(PlayerController player)
    {
        Vector3 pos = transform.position;
        Fx.Burst(pos + Vector3.up * 0.5f, new Color(1f, 0.55f, 0.15f), 18, 9f, 1.4f);
        Fx.Ring(pos, new Color(1f, 0.7f, 0.2f), 0.5f, 3.6f, 0.3f);
        ToonKit.Sfx("boom", position: transform.position + Vector3.up);
        Game.I.Cam.Shake(0.35f);
        ReleaseCommit();
        if ((player.transform.position - pos).magnitude < 3.6f) HitPlayer(player, 2f, pos, 16f);

        // The blast doesn't care whose side you're on.
        for (int i = All.Count - 1; i >= 0; i--)
        {
            Enemy e = All[i];
            if (e != this && (e.transform.position - pos).magnitude - e.radius < 3.4f
                && CombatContact.CanContact(pos + Vector3.up * 1.1f, e))
                e.ReceiveHit(new DamageRequest(40f, pos, 12f, true, source: this));
        }
        if (silent || !CombatEligible) return;
        PlaytestLog.Emit("despawn", target: this, note: "silent removal");
        silent = true;
        hp = 0f;
        Die();
    }

    // Brutes lumber in and slam the ground in front of them.
    Vector3 TickBrute(PlayerController player, Vector3 dir, float dist, float dt, ref bool turn)
    {
        if (windup > 0f)
        {
            windup -= dt;
            turn = false;
            if (anim != null)
            {
                anim.move = 0f;
                anim.raise = 1f - Mathf.Clamp01(windup / 0.8f);
            }
            if (windup <= 0f)
            {
                Slam(player, transform.position + transform.forward * 1.6f, 3.6f, level >= 1 ? 1 : 0);
                ReleaseCommit(0.3f);
            }
            return Vector3.zero;
        }
        if (anim != null) anim.raise = Mathf.MoveTowards(anim.raise, 0f, dt * 8f);
        if (cooldown <= 0f && windup <= 0f && dist < 4.6f && !Training && !EncounterDirector.Allows(this, Role) || dist < 3.2f && cooldown <= 0f && !Commit())
        {
            // Not its turn: it owns the ground in front of it, a step outside its own reach.
            if (anim != null) anim.move = 0.3f;
            float hold = dist - 4.2f;
            return Mathf.Abs(hold) > 0.4f ? dir * Mathf.Sign(hold) * speed * 0.5f : Vector3.Cross(Vector3.up, dir) * strafe * speed * 0.25f;
        }
        if (dist < 3.2f && cooldown <= 0f)
        {
            BeginAction(kind.ToString());
            windup = 0.8f + UnseenWarning();
            Tell(0.72f);
            cooldown = 2.2f;
            // The overhead chop lands (about halfway through the clip) as the wind-up ends.
            if (clips != null) clips.Once("2H_Melee_Attack_Chop", 1.5f);
            Fx.Telegraph(transform.position + transform.forward * 1.6f, 3.6f, 0.8f, Warn);
            return Vector3.zero;
        }
        bool walk = dist > 2.2f;
        if (anim != null) anim.move = walk ? 0.6f : 0f;
        return walk ? dir * speed : Vector3.zero;
    }

    void Slam(PlayerController player, Vector3 at, float slamRadius, int shockwaves)
    {
        Fx.Burst(at + Vector3.up * 0.2f, new Color(0.95f, 0.85f, 0.6f), 16, 7f);
        Fx.Ring(at, Warn, 0.5f, slamRadius, 0.3f);
        ToonKit.Sfx("slam", position: transform.position + Vector3.up);
        Game.I.Cam.Shake(0.45f);
        Vector3 to = player.transform.position - at;
        if (to.y < 1.6f && new Vector2(to.x, to.z).magnitude < slamRadius) HitPlayer(player, 2f, at, 16f);
        for (int i = 0; i < shockwaves; i++) Fx.Shockwave(at, 13f, 9f - i * 3.5f, 1, this);
    }

    // The Gloom King: ground slam up close, leap slam from range, and in later
    // phases bolt fans and summoned Gloomlings.
    Vector3 TickBoss(PlayerController player, Vector3 dir, float dist, float dt, ref bool turn)
    {
        int wanted = hp < maxHp * 0.33f ? 3 : hp < maxHp * 0.66f ? 2 : 1;
        if (wanted > bossPhase)
        {
            bossPhase = wanted;
            punch = 1.5f;
            Fx.Ring(transform.position, UI.Purple, 1f, 12f, 0.6f);
            Game.I.Cam.Shake(0.6f);
            Game.I.Toast(Story.BossTaunts[Mathf.Min(bossPhase - 2, Story.BossTaunts.Length - 1)], UI.Purple);
            ToonKit.Sfx("roar", position: transform.position + Vector3.up);
            if (clips != null) clips.Once("Taunt", 0.9f);
            foreach (var e in All) if (e != this) e.Shove(transform.position, 6f);
        }

        if (leapT >= 0f)
        {
            leapT += dt / 0.9f;
            turn = false;
            float t = Mathf.Clamp01(leapT);
            Vector3 next = Vector3.Lerp(leapFrom, leapTo, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 6f;
            CollisionFlags contact = cc.Move(next - transform.position);
            model.localPosition = Vector3.zero;
            if (anim != null) anim.raise = 1f;
            if (leapT >= 1f || (contact & (CollisionFlags.Sides | CollisionFlags.Above)) != 0)
            {
                bool landed = leapT >= 1f;
                leapT = -1f;
                model.localPosition = Vector3.zero;
                cc.enabled = true;
                if (landed && Vector3.Distance(transform.position, leapTo) < 1f)
                    Slam(player, transform.position, 4.6f, bossPhase >= 3 ? 2 : 1);
                cooldown = BossCooldown();
            }
            return Vector3.zero;
        }

        if (windup > 0f)
        {
            windup -= dt;
            turn = action != 0;
            if (anim != null)
            {
                anim.move = 0f;
                anim.raise = Mathf.MoveTowards(anim.raise, 1f, dt * 3f);
            }
            if (windup <= 0f)
            {
                if (action == 0) Slam(player, transform.position + transform.forward * 2f, 4.6f, bossPhase >= 2 ? 1 : 0);
                else if (action == 1) BoltFan(player);
                else Summon();
                cooldown = BossCooldown();
            }
            return Vector3.zero;
        }

        if (anim != null) anim.raise = Mathf.MoveTowards(anim.raise, 0f, dt * 6f);
        if (cooldown <= 0f)
        {
            if (dist < 5f)
            {
                action = 0;
                BeginAction(kind.ToString());
                windup = 0.85f;
                if (clips != null) clips.Once("2H_Melee_Attack_Chop", 1.6f);
                Fx.Telegraph(transform.position + transform.forward * 2f, 4.6f, 0.85f, Warn);
            }
            else
            {
                float roll = aiRandom.value;
                if (bossPhase >= 2 && roll < 0.3f && All.Count < 7)
                {
                    action = 2;
                    BeginAction(kind.ToString());
                    windup = 0.8f;
                    if (clips != null) clips.Once("Spellcast_Raise", 1.1f);
                    SetFlash(0.7f);
                }
                else if (roll < (bossPhase >= 2 ? 0.65f : 0.3f))
                {
                    action = 1;
                    BeginAction(kind.ToString());
                    windup = 0.6f;
                    if (clips != null) clips.Once("Spellcast_Shoot", 0.9f);
                    SetFlash(0.7f);
                }
                else
                {
                    leapFrom = transform.position;
                    if (!WorldSupport.TryStand(player.transform.position, radius, height, out leapTo)
                        || !WorldSupport.ClearSight(leapFrom + Vector3.up * 2f, leapTo + Vector3.up * 2f))
                    { cooldown = 0.5f; return Vector3.zero; }
                    leapT = 0f;
                    if (clips != null) clips.Once("Jump_Full_Long", 1f);
                    Fx.Telegraph(leapTo, 4.6f, 0.9f, Warn);
                    ToonKit.Sfx("jump", 1f, 0.5f, position: transform.position + Vector3.up);
                }
            }
            return Vector3.zero;
        }

        bool walk = dist > 3.2f;
        if (anim != null) anim.move = walk ? 0.7f : 0f;
        return walk ? dir * (speed + bossPhase * 0.4f) : Vector3.zero;
    }

    float BossCooldown()
    {
        return bossPhase == 1 ? 2.1f : bossPhase == 2 ? 1.6f : 1.1f;
    }

    void BoltFan(PlayerController player)
    {
        Vector3 origin = transform.position + Vector3.up * 2.2f + transform.forward * 1.2f;
        Vector3 aim = (player.transform.position + Vector3.up * 0.9f - origin).normalized;
        int bolts = bossPhase >= 3 ? 9 : 5;
        for (int i = 0; i < bolts; i++)
        {
            float angle = Mathf.Lerp(-45f, 45f, i / (float)(bolts - 1));
            Projectile.Fire(origin, Quaternion.Euler(0, angle, 0) * aim, 12f, emitter: this);
        }
        ToonKit.Sfx("shoot", 1f, 0.7f, position: transform.position + Vector3.up);
    }

    void Summon()
    {
        ToonKit.Sfx("spawn", position: transform.position + Vector3.up);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f + phase;
            Vector3 p = transform.position + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 3.5f;
            if (WorldSupport.TryStand(p, 0.5f, 1.2f, out Vector3 spawn))
                Create(bossPhase >= 3 && i == 0 ? Kind.Bomber : Kind.Slime, spawn, level);
        }
    }

    // ------------------------------------------------------------ damage

    HitResult HitPlayer(PlayerController player, float amount, Vector3 from, float push)
    {
        if (!CombatContact.CanContact(from + Vector3.up * 1.1f, player)) return new HitResult(HitOutcome.Miss);
        var landed = player.ReceiveHit(new DamageRequest(amount, from, push, heavy: amount >= 2f, source: this));
        if (landed.Damaged) HitsLanded++;
        return landed;
    }

    // Knocks the enemy up. Heavy enemies only flinch.
    public void Launch(float seconds)
    {
        if (Dead || defeated) return;
        if (kind == Kind.Brute || IsBoss)
        {
            Stagger(0.45f);
            return;
        }
        airT = airDur = seconds;
        windup = -1f;
        lungeT = 0f;
        BreakOff();
        CancelCommitments();
        if (clips != null) clips.Once("Hit_B", 0.3f);
    }

    // Interrupts whatever it was doing and leaves it open for a moment.
    public void Stagger(float seconds)
    {
        if (Dead || defeated) return;
        if (IsBoss) seconds *= 0.35f;
        stun = Mathf.Max(stun, seconds);
        openT = Mathf.Max(openT, seconds);
        windup = -1f;
        lungeT = 0f;
        BreakOff();
        CancelCommitments();
        CancelGrolMove();
        SetFlash(0.5f);
        if (clips == null) return;
        // A long stagger puts it on the ground and it has to get up again; a short one is a flinch.
        if (seconds > 0.8f && clips.Has("Hit_Heavy") && clips.Has("Spawn_Ground"))
        {
            riseT = stun * 0.5f;
            clips.Once("Hit_Heavy", stun - riseT, 0.05f, true);
        }
        else clips.Once("Hit_A", Mathf.Min(seconds, 0.5f));
    }

    public void Shove(Vector3 from, float push)
    {
        if (IsBoss) return;
        Vector3 d = transform.position - from;
        d.y = 0f;
        knock = d.normalized * push;
    }

    public override HitResult ReceiveHit(DamageRequest request)
    {
        if (Training) hp = maxHp = 1000000f;
        var result = ReceiveHitCore(request);
        if (Training) { hp = maxHp; LastTrainingDamage = result.damage; }
        PlaytestLog.Contact(this, request, result); return result;
    }
    HitResult ReceiveHitCore(DamageRequest request)
    {
        bool crit = request.critical, heavy = request.heavy;
        float amount = request.amount, push = request.push;
        Vector3 from = request.from;
        if (!CombatEligible || !CombatContact.ValidDamage(amount)) return new HitResult(HitOutcome.Miss);
        if (GatekeeperImmune)
        {
            Fx.Sparks(transform.position + Vector3.up * 1.5f, (from - transform.position).normalized, Color.white, 4);
            ToonKit.Sfx("hit", 0.7f, 0.6f, position: transform.position + Vector3.up);
            return new HitResult(HitOutcome.Immune);
        }
        heavyBlow = heavy || crit;
        provoked = true;                                    // being hit gets its attention
        Vector3 facingSource = from - transform.position; facingSource.y = 0f;
        bool guardBroken = guarding && heavy && (facingSource.sqrMagnitude < 0.01f || Vector3.Dot(facingSource.normalized, transform.forward) >= 0.2f);
        if (guarding && !Guard(from, heavy)) return new HitResult(HitOutcome.Blocked);
        if (airT > 0f) amount *= 1.5f;                      // juggle bonus
        if (Training) amount = Mathf.Min(amount, hp - 1f);

        if (kind == Kind.Brute) push *= 0.25f;
        if (IsBoss) push = 0f;
        Game.I.HitNumber(transform.position + Vector3.up * (height + 0.3f), amount, crit);
        punch = 1f;
        var result = ApplyDamage(new DamageRequest(amount, from, push, heavy, crit, request.source, request.kind));
        if (!result.Damaged) return result;
        BreakCall();
        // Small enemies flinch; a hit can interrupt a slime's pounce wind-up.
        if (kind == Kind.Slime || kind == Kind.Minion || kind == Kind.Spitter) cooldown = Mathf.Max(cooldown, 0.5f);
        bool flinch = Flinch();
        if (clips != null && !Dead && stun <= 0f && windup <= 0f && lungeT <= 0f && flinch && kind != Kind.Brute)
        {
            clips.Once(heavy ? "Hit_B" : ProductionRandom.Cosmetic.value < 0.5f ? "Hit_A" : "Hit_B", heavy ? 0.4f : 0.3f);
        }
        bool staggered = guardBroken, launched = false;
        if (CombatEligible)
        {
            if (request.stagger > 0f) { Stagger(request.stagger); staggered = true; }
            if (request.launch > 0f)
            {
                Launch(request.launch);
                launched = kind != Kind.Brute && !IsBoss;
                staggered |= !launched;
            }
        }
        return new HitResult(result.outcome, result.damage, guardBroken, staggered, launched);
    }

    // Removed without rewards (when the boss falls, his minions go with him).
    public void Vanish()
    {
        if (silent || !CombatEligible) return;
        PlaytestLog.Emit("despawn", target: this, note: "silent removal");
        silent = true;
        hp = 0f;
        Die();
    }

    protected override void Die()
    {
        CancelCommitments();
        if (IsBoss && !silent && !defeated)
        {
            // Beaten, not destroyed: he stays for the final scene.
            defeated = true;
            hp = 0.01f;
            stun = float.MaxValue;
            cc.enabled = false;
            model.localPosition = Vector3.zero;
            if (clips != null)
            {
                clips.Release();
                clips.Once(clips.Has("Lie_Down") ? "Sit_Floor_Down" : "Death_A", 1.2f, 0.1f, true);
            }
            Game.I.OnBossDefeated(this);
            return;
        }
        All.Remove(this);
        Vector3 pos = transform.position + Vector3.up * 0.6f;
        Fx.Burst(pos, color, IsBoss ? 40 : kind == Kind.Brute ? 22 : 10, IsBoss ? 12f : 5f, IsBoss ? 2f : 1f);

        if (!silent)
        {
            ToonKit.Sfx("kill", 1f, kind == Kind.Brute || IsBoss ? 0.6f : 1f, position: transform.position + Vector3.up);
            int pickups = Mathf.Min(coinValue, 12);
            for (int i = 0; i < pickups; i++)
                Pickup.Spawn(Pickup.Kind.Coin, pos, Mathf.Max(1, coinValue / pickups));
            if (kind == Kind.Brute || IsBoss || dropRandom.value < 0.12f) Pickup.Spawn(Pickup.Kind.Heart, pos, 1);

            if (kind == Kind.BigSlime)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    var child = Create(Kind.Slime, transform.position + transform.right * i * 0.8f, level, originTier: tier, spawnId: ProductionRandom.ChildId(SpawnId, i));
                    if (OriginRegionId.Length > 0) child.BindObjective(OriginRegionId, parent: ObjectiveEvent);
                }
            }
            Game.I.OnEnemyKilled(this);
        }

        // Rigged characters collapse before they vanish; everything else just pops.
        if (clips != null && !silent)
        {
            cc.enabled = false;
            clips.Release();
            // A heavy blow throws it onto its back; anything else and it crumples where it stood.
            bool thrown = heavyBlow && !IsBoss && kind != Kind.Brute && clips.Has("Hit_Heavy");
            // Both are played at the pace they were made for: a fall takes the time a fall takes.
            if (thrown) clips.Once("Hit_Heavy", 0.75f, 0.1f, true);
            else clips.Once("Death_A", IsBoss ? 2.2f : 1.6f, 0.12f, true);
            slide = new Vector3(knock.x, 0f, knock.z) * (thrown ? 0.75f : 0.35f);
            fadeAt = IsBoss ? 2.6f : thrown ? 1.5f : 1.9f;
            Destroy(gameObject, fadeAt + 0.8f);
            return;
        }
        Destroy(gameObject);
    }
}
