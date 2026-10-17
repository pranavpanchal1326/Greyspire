using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Phase nine, package C01: three weapon families that change spacing and choices.
// Every scenario drives the hero's real attack, cancel, guard and contact code.
[InitializeOnLoad]
public static class WeaponFamilyTest
{
    const string Session = "GreyspireWeaponFamilyTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    static readonly string[] HeroIds = { "pip", "bram", "nix" };
    static int passed;
    static double readyAt, deadline;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static object Get(object host, string field) => host.GetType().GetField(field, Any).GetValue(host);
    static string Num(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
        Debug.Log("WEAPON PASS " + message);
    }

    static int HeroIndex(string id) => Array.FindIndex(Heroes.All, h => h.id == id);

    static void Reset(string hero)
    {
        foreach (var enemy in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        ((IList)Get(G, "timers")).Clear();
        G.enabled = P.enabled = false;
        Set(G, "<DefeatPending>k__BackingField", false);
        Set(G, "<InTown>k__BackingField", false);
        Set(G, "<InWilds>k__BackingField", false);
        Set(G, "level", 0); Set(G, "wave", 0); Set(G, "combo", 0); Set(G, "fadeDir", 0f);
        Call(G, "SetState", Game.GameState.Playing);
        if (P.Hero.id != hero) P.SetHero(HeroIndex(hero));
        P.enabled = false;
        P.stats = new PlayerStats();
        P.stats.crit = 0f;
        P.weak = 1f;
        P.Revive(Origin, true);
        P.transform.rotation = Quaternion.identity;
        P.strikes = 0;
        Set(P, "invuln", 0f);
        Set(P, "comboStep", 0);
        Set(P, "guardSpent", false);
        Set(P, "dashCd", 0f); Set(P, "dashT", 0f); Set(P, "dashGrace", 0f); Set(P, "bashCd", 0f);
        Call(P, "CancelOffense");
        Juice.Paused = false;
        Juice.Reset();
        Time.timeScale = 1f;
        Physics.SyncTransforms();
    }

    // An enemy at a bearing (degrees from the hero's forward) whose edge is `gap` metres away.
    static Enemy Target(float bearing, float gap, float facing = 180f)
    {
        var enemy = Enemy.Create(Enemy.Kind.Minion, Origin, 0);
        float centre = gap + enemy.radius;
        Vector3 dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
        enemy.transform.position = Origin + dir * centre;
        // facing 180 turns the enemy to look back along the line at the hero.
        enemy.transform.rotation = Quaternion.LookRotation(-dir) * Quaternion.Euler(0f, facing - 180f, 0f);
        enemy.hp = enemy.maxHp = 1000f;
        Set(enemy, "cooldown", 1000f);
        Set(enemy, "guarding", false);
        Physics.SyncTransforms();
        return enemy;
    }

    static bool StrikeHits(string hero, int step, float bearing, float gap)
    {
        Reset(hero);
        Set(P, "comboStep", step);
        var enemy = Target(bearing, gap);
        Call(P, "Strike");
        return enemy.hp < 1000f;
    }


    static string MoveName => Get(P, "move").ToString();

    static void Block(float seconds)
    {
        Type move = typeof(PlayerController).GetNestedType("Move", BindingFlags.NonPublic);
        Set(P, "move", Enum.Parse(move, "Block"));
        Set(P, "blockT", seconds);
    }

    static void Identity()
    {
        var legacy = WeaponFamily.SwordShield.steps;
        Check(legacy.Select(s => s.time).SequenceEqual(new[] { 0.34f, 0.38f, 0.7f })
            && legacy.Select(s => s.contact).SequenceEqual(new[] { 0.48f, 0.48f, 0.55f })
            && legacy.Select(s => s.damage).SequenceEqual(new[] { 1f, 1.15f, 1.9f })
            && legacy.Select(s => s.reach).SequenceEqual(new[] { 2.5f, 2.5f, 3.2f })
            && WeaponFamily.SwordShield.dodgeCancel == 0.55f && WeaponFamily.SwordShield.parryWindow == 0.25f,
            "sword and shield keeps the exact pre-C01 combo, cancel and parry tables");
        Check(WeaponFamily.BareHands.steps == WeaponFamily.SwordShield.steps && WeaponFamily.BareHands.CanBash,
            "Sol's bare-hands family is outside C01 and unchanged");
        Check(Heroes.All.First(h => h.id == "pip").family == WeaponFamily.SwordShield
            && Heroes.All.First(h => h.id == "cedric").family == WeaponFamily.SwordShield
            && Heroes.All.First(h => h.id == "bram").family == WeaponFamily.GreatAxe
            && Heroes.All.First(h => h.id == "nix").family == WeaponFamily.LightBlade,
            "each chapter hero carries the family that matches the weapon in their hand");
        Check(WeaponFamily.Chapter.Select(f => f.id).Distinct().Count() == 3 && WeaponFamily.Chapter.All(f => f.steps.Length == 3),
            "three distinct weapon families each define a complete three-blow combo");
        foreach (var family in WeaponFamily.Chapter)
            foreach (var step in family.steps)
                Check(step.Spin || Mathf.Abs(Mathf.Cos(step.ArcDegrees * 0.5f * Mathf.Deg2Rad) - step.minDot) < 0.01f || step.thrust,
                    family.name + " draws the same arc it hits with (" + Num(step.ArcDegrees) + " degrees)");
    }

    static void Spacing()
    {
        float shieldReach = WeaponFamily.SwordShield.steps[0].reach, axeReach = WeaponFamily.GreatAxe.steps[0].reach, bladeReach = WeaponFamily.LightBlade.steps[0].reach;
        Check(StrikeHits("pip", 0, 0f, shieldReach - 0.05f) && !StrikeHits("pip", 0, 0f, shieldReach + 0.05f), "sword reaches exactly its 2.5 m edge");
        Check(StrikeHits("bram", 0, 0f, axeReach - 0.05f) && !StrikeHits("bram", 0, 0f, axeReach + 0.05f), "axe reaches exactly its 2.6 m edge");
        Check(StrikeHits("nix", 0, 0f, bladeReach - 0.05f) && !StrikeHits("nix", 0, 0f, bladeReach + 0.05f), "light blade reaches exactly its 2.1 m edge");
        Check(StrikeHits("pip", 0, 0f, 2.3f) && !StrikeHits("nix", 0, 0f, 2.3f), "at 2.3 m the sword connects and the light blade must close in");

        // Arc: a target at 50 degrees, 75 degrees and 105 degrees off the hero's line.
        Check(StrikeHits("pip", 0, 50f, 1.5f) && StrikeHits("bram", 0, 50f, 1.5f) && StrikeHits("nix", 0, 50f, 1.5f), "every family catches an enemy 50 degrees off its line");
        Check(StrikeHits("pip", 0, 75f, 1.5f) && StrikeHits("bram", 0, 75f, 1.5f) && !StrikeHits("nix", 0, 75f, 1.5f), "at 75 degrees the light blade misses while sword and axe connect");
        Check(!StrikeHits("pip", 0, 105f, 1.5f) && StrikeHits("bram", 0, 105f, 1.5f) && !StrikeHits("nix", 0, 105f, 1.5f), "only the axe's wide arc catches an enemy at the hero's side (105 degrees)");
        Check(StrikeHits("pip", 0, -105f, 1.5f) == StrikeHits("pip", 0, 105f, 1.5f) && StrikeHits("bram", 0, -105f, 1.5f), "arcs are symmetric left and right");

        // Count of a ring of five enemies 1.5 m away at -100..100 degrees: how much of a crowd one blow holds off.
        int Crowd(string hero)
        {
            Reset(hero);
            var ring = new[] { -100f, -50f, 0f, 50f, 100f }.Select(b => Target(b, 1.5f)).ToList();
            Call(P, "Strike");
            return ring.Count(e => e.hp < 1000f);
        }
        int axe = Crowd("bram"), sword = Crowd("pip"), blade = Crowd("nix");
        Check(axe == 5 && sword == 3 && blade == 3 && axe > sword, "a crowd of five: axe catches " + axe + ", sword " + sword + ", blade " + blade);

        // Finishers: the sword and axe spin; the blade thrusts down a lane.
        Check(StrikeHits("pip", 2, 180f, 1.5f) && StrikeHits("bram", 2, 180f, 1.5f), "sword and axe finishers spin through an enemy behind the hero");
        Check(!StrikeHits("nix", 2, 180f, 1.5f) && StrikeHits("nix", 2, 0f, 3.0f) && !StrikeHits("pip", 2, 0f, 3.3f),
            "the blade's thrust finisher misses behind but outreaches the sword spin straight ahead (3.0 m)");
        Check(!StrikeHits("nix", 2, 40f, 2.4f), "the thrust is a narrow lane: an enemy 40 degrees off at 2.4 m is not hit");
    }

    static void GuardAndFlank()
    {
        // Guard break: only the axe's third blow carries the heavy flag.
        foreach (string hero in HeroIds)
        {
            Reset(hero);
            var guarded = Target(0f, 1.2f); Set(guarded, "guarding", true);
            Call(P, "Strike");
            Check(guarded.hp == 1000f && P.strikes == 0, hero + "'s first blow glances off a raised guard");
        }
        Reset("bram"); Set(P, "comboStep", 2);
        var shieldBearer = Target(0f, 1.2f); Set(shieldBearer, "guarding", true);
        Call(P, "Strike");
        Check(shieldBearer.hp < 1000f && shieldBearer.Open, "the axe's third blow breaks a raised guard and leaves the enemy open");
        Reset("pip"); Set(P, "comboStep", 2);
        shieldBearer = Target(0f, 1.2f); Set(shieldBearer, "guarding", true);
        Call(P, "Strike");
        Check(shieldBearer.hp == 1000f, "the sword's spin finisher does not break a guard (that is the shield bash's job)");

        // Flank: the light blade's weak-point bonus applies only outside the enemy's front.
        Reset("nix");
        var front = Target(0f, 1.2f, 180f);
        Call(P, "Strike");
        float frontDamage = 1000f - front.hp;
        Reset("nix");
        var back = Target(0f, 1.2f, 0f);
        Call(P, "Strike");
        float backDamage = 1000f - back.hp;
        Check(frontDamage > 0f && backDamage > frontDamage * 1.3f, "light blade from behind deals " + Num(backDamage) + " versus " + Num(frontDamage) + " from the front");
        Reset("nix");
        var guardedBack = Target(0f, 1.2f, 0f); Set(guardedBack, "guarding", true);
        Call(P, "Strike");
        Check(guardedBack.hp < 1000f, "a frontal guard does not stop the light blade striking from behind");
        Reset("pip");
        front = Target(0f, 1.2f, 180f); Call(P, "Strike"); float swordFront = 1000f - front.hp;
        Reset("pip");
        back = Target(0f, 1.2f, 0f); Call(P, "Strike"); float swordBack = 1000f - back.hp;
        Check(swordBack < swordFront * 1.25f && swordFront < swordBack * 1.25f, "the sword has no flank bonus: it is reliable from any side");
    }

    static void Tempo()
    {
        Type action = typeof(PlayerController).GetNestedType("BufferedAction", BindingFlags.NonPublic);
        float Duration(string hero, int step)
        {
            Reset(hero); Set(P, "comboStep", step);
            Call(P, "StartAttack", Vector3.forward);
            return (float)Get(P, "attackDur");
        }
        float sword = Duration("pip", 0), axe = Duration("bram", 0), blade = Duration("nix", 0);
        Check(axe > sword * 1.15f && blade < sword * 0.9f, "first-blow tempo: axe " + Num(axe) + " s, sword " + Num(sword) + " s, blade " + Num(blade) + " s");

        // Dodge cancel windows, measured in the real TickAttack/ConsumeBufferedAction path.
        foreach (string hero in HeroIds)
        {
            foreach (bool early in new[] { true, false })
            {
                Reset(hero);
                var enemy = Target(0f, 1.2f);
                Call(P, "StartAttack", Vector3.forward);
                float dur = (float)Get(P, "attackDur");
                float cancel = P.Weapon.dodgeCancel;
                Set(P, "attackT", dur * (early ? cancel - 0.12f : cancel - 0.01f));
                Call(P, "Buffer", Enum.Parse(action, "Dodge"), Vector3.back);
                int dashes = P.dashes;
                Call(P, "TickAttack", early ? 0.001f : dur * 0.03f, Vector3.back);
                bool dashed = P.dashes == dashes + 1;
                Check(early ? !dashed && P.Attacking : dashed && !P.Attacking, hero + (early ? " cannot dodge before its cancel window" : " dodges out at its cancel window") + " (" + Num(cancel) + ")");
                if (!dashed) continue;
                float hpAfterCancel = enemy.hp;
                Call(P, "TickAttack", 1f, Vector3.back);
                Check(enemy.hp == hpAfterCancel, hero + "'s cancelled swing never lands later");
                if (hero == "nix") Check(enemy.hp == 1000f && P.strikes == 0, "the light blade can feint out before its cut lands, forfeiting the hit");
                else Check(P.strikes == 1, hero + " has already landed its blow before the cancel window opens");
            }
        }
    }

    static void Guards()
    {
        foreach (string hero in HeroIds)
        {
            Reset(hero);
            float window = P.Weapon.parryWindow;
            Block(window - 0.02f);
            var hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
            Check(hit.outcome == HitOutcome.Parried, hero + " parries inside its " + Num(window) + " s window");
            Reset(hero); Block(window + 0.02f);
            hit = P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
            Check(hit.outcome == HitOutcome.Blocked && P.hp == P.maxHp, hero + " blocks, but no longer parries, just after its window");
        }
        Check(WeaponFamily.SwordShield.parryWindow > WeaponFamily.LightBlade.parryWindow && WeaponFamily.LightBlade.parryWindow > WeaponFamily.GreatAxe.parryWindow,
            "parry windows rank shield, blade deflection, axe haft");

        // Bash out of a guard: only a shield can.
        foreach (string hero in HeroIds)
        {
            Reset(hero);
            Block(0.5f);
            Type action = typeof(PlayerController).GetNestedType("BufferedAction", BindingFlags.NonPublic);
            Call(P, "Buffer", Enum.Parse(action, "Light"), Vector3.forward);
            Call(P, "ConsumeBufferedAction", Vector3.forward, float.NaN);
            Check(hero == "pip" ? MoveName == "Bash" : MoveName == "Block", hero + (hero == "pip" ? " drives a shield bash out of its guard" : " has no bash and keeps its guard"));
        }

        // A haft gives ground on a blocked blow; a shield stands firm.
        float Knock(string hero)
        {
            Reset(hero); Block(0.5f);
            P.ReceiveHit(new DamageRequest(1f, Origin + Vector3.forward * 4f, 0f));
            return ((Vector3)Get(P, "knock")).magnitude;
        }
        Check(Knock("bram") > Knock("pip") + 1f, "a blocked blow drives the axe-wielder further back than a shield-bearer");
    }

    // What the hero carries, and how the blow is timed and turned in the hand.
    static void Kit()
    {
        Transform Held(string prop) => P.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == prop);
        Reset("bram");
        Transform axe = Held("Greataxe_Iron");
        Check(axe != null && axe.parent.name.Contains("hand_r"), "Bram holds the great axe in his right hand");
        var axeMesh = axe.GetComponentInChildren<MeshFilter>();
        float axeLength = axeMesh.transform.TransformVector(axeMesh.sharedMesh.bounds.size).magnitude;
        Reset("nix");
        Transform seax = Held("Seax_Steel"), dagger = Held("Dagger_Steel");
        Check(seax != null && seax.parent.name.Contains("hand_r") && dagger != null && dagger.parent.name.Contains("hand_l"),
            "Nix holds the seax in her right hand and the parrying dagger in her left");
        var swordMesh = Held("Seax_Steel").GetComponentInChildren<MeshFilter>();
        float seaxLength = swordMesh.transform.TransformVector(swordMesh.sharedMesh.bounds.size).magnitude;
        Reset("pip");
        var sword = Held("Sword_Bronze").GetComponentInChildren<MeshFilter>();
        float swordLength = sword.transform.TransformVector(sword.sharedMesh.bounds.size).magnitude;
        Check(axeLength > swordLength * 1.15f && seaxLength < swordLength * 0.9f,
            "the props read as their families: axe " + Num(axeLength) + " m, sword " + Num(swordLength) + " m, seax " + Num(seaxLength) + " m");

        var clips = (CharAnim)Get(P, "clips");
        foreach (string hero in HeroIds)
        {
            HeroDef def = Heroes.All.First(h => h.id == hero);
            for (int step = 0; step < 3; step++)
            {
                float length = clips.Length(def.combo[step]);
                Check(def.comboContact != null && def.comboContact[step] > 0f && def.comboContact[step] <= length * 0.85f,
                    hero + " blow " + (step + 1) + " lands at a measured moment inside " + def.combo[step] + " (" + Num(def.comboContact[step]) + " of " + Num(length) + " s)");
            }
            if (def.comboTwist == null) continue;
            float previous = 0f;
            for (int step = 0; step < 3; step++)
            {
                AttackStep s = def.family.steps[step];
                float windup = s.time * s.contact;
                float turn = Mathf.Abs(def.comboTwist[step] - previous);
                Check(turn <= 1100f * windup, hero + " blow " + (step + 1) + " turns its edge in (" + Num(turn) + " degrees) before contact (" + Num(windup) + " s)");
                previous = def.comboTwist[step];
            }
        }

        // The wrist turn really reaches the weapon: drive one blow's wind-up.
        Reset("bram"); Set(P, "comboStep", 1);
        axe = Held("Greataxe_Iron");
        Quaternion rest = axe.localRotation;
        Call(P, "StartAttack", Vector3.forward);
        for (int i = 0; i < 10; i++) Call(P, "TickTwist", 0.02f);
        float chopTwist = Heroes.All.First(h => h.id == "bram").comboTwist[1];
        Check(Mathf.Abs(Quaternion.Angle(rest, axe.localRotation) - Mathf.Abs(chopTwist)) < 1f, "the axe turns " + Num(chopTwist) + " degrees in the hand for the downward chop");
        Call(P, "CancelOffense");
        for (int i = 0; i < 10; i++) Call(P, "TickTwist", 0.02f);
        Check(Quaternion.Angle(rest, axe.localRotation) < 0.5f, "the axe settles back to its rest grip after the blow");

        // Put away in town: the great axe is slung across the back, the dagger at the right hip.
        Reset("bram");
        Set(P, "builtFrame", -10);
        Call(P, "SetArmed", false);
        axe = Held("Greataxe_Iron");
        Check(axe.parent.name.Contains("spine"), "the stowed great axe is slung across the back (on " + axe.parent.name + ")");
        Call(P, "SetArmed", true);
        Check(axe.parent.name.Contains("hand_r"), "drawing returns the axe to the right hand");
        Reset("nix");
        Set(P, "builtFrame", -10);
        Call(P, "SetArmed", false);
        seax = Held("Seax_Steel"); dagger = Held("Dagger_Steel");
        Vector3 sideOf(Transform t) => P.transform.InverseTransformPoint(t.position);
        Check(sideOf(seax).x < 0f && sideOf(dagger).x > 0f, "stowed: the seax hangs at the left hip and the dagger at the right");
        Call(P, "SetArmed", true);
    }

    static void Feedback()
    {
        var fs = WeaponFamily.Chapter;
        Check(fs.Select(f => f.swingPitch).Distinct().Count() == 3 && fs.Select(f => f.edge).Distinct().Count() == 3,
            "each family has its own swing pitch and cut colour");
        Check(WeaponFamily.GreatAxe.impactLayer.Length > 0 && WeaponFamily.SwordShield.impactLayer.Length == 0
            && WeaponFamily.GreatAxe.swingLayerPitch < 1f && WeaponFamily.LightBlade.swingLayerPitch > 1f && WeaponFamily.SwordShield.swingLayer.Length == 0,
            "the axe adds a low rush and a thud, the blade a bright whip; the sword keeps its original sound");
        Check(WeaponFamily.GreatAxe.trailWidth > WeaponFamily.SwordShield.trailWidth && WeaponFamily.SwordShield.trailWidth > WeaponFamily.LightBlade.trailWidth,
            "weapon trails rank wide axe, sword, thin blade");
        Check(WeaponFamily.GreatAxe.hitstop > WeaponFamily.SwordShield.hitstop && WeaponFamily.SwordShield.hitstop > WeaponFamily.LightBlade.hitstop
            && WeaponFamily.GreatAxe.finisherShake > WeaponFamily.SwordShield.finisherShake && WeaponFamily.SwordShield.finisherShake > WeaponFamily.LightBlade.finisherShake,
            "impact weight (hitstop and shake) ranks axe, sword, blade");
        foreach (string hero in HeroIds)
        {
            for (int step = 0; step < 3; step++)
            {
                Reset(hero); Set(P, "comboStep", step);
                Target(0f, 1f);
                var before = UnityEngine.Object.FindObjectsByType<SlashFx>(FindObjectsSortMode.None);
                Call(P, "Strike");
                bool thrust = P.Weapon.steps[step].thrust;
                bool drawn = UnityEngine.Object.FindObjectsByType<SlashFx>(FindObjectsSortMode.None)
                    .Any(fx => !before.Contains(fx) && fx.name == (thrust ? "Thrust" : "Slash"));
                Check(drawn, hero + " blow " + (step + 1) + " draws a " + (thrust ? "thrust streak" : "slash arc"));
            }
        }
    }

    static void Balance()
    {
        var csv = new StringBuilder("hero,family,step,time,contact,damage,reach,arc_degrees,lunge,push,stagger,heavy,thrust,plan_duration,plan_power,dodge_cancel,parry_window,guard,flank_bonus\n");
        var dps = new float[HeroIds.Length];
        for (int h = 0; h < HeroIds.Length; h++)
        {
            Reset(HeroIds[h]);
            P.RefreshStats();
            float time = 0f, damage = 0f;
            for (int step = 0; step < 3; step++)
            {
                Set(P, "comboStep", step); Call(P, "StartAttack", Vector3.zero);
                var plan = (GrowthPolicy.ContactPlan)Get(P, "attackPlan");
                var s = P.Weapon.steps[step];
                time += plan.duration;
                damage += P.stats.damage * s.damage * plan.power * (1f + P.stats.crit * (P.stats.critMultiplier - 1f));
                csv.AppendLine(string.Join(",", HeroIds[h], P.Weapon.id, step + 1, Num(s.time), Num(s.contact), Num(s.damage), Num(s.reach), Num(s.ArcDegrees), Num(s.lunge), Num(s.push),
                    Num(s.stagger), s.heavy, s.thrust, Num(plan.duration), Num(plan.power), Num(P.Weapon.dodgeCancel), Num(P.Weapon.parryWindow), P.Weapon.guard, Num(P.Weapon.flankBonus)));
                Check(plan.clipSpeed <= 2f && plan.power >= 1f && plan.power <= 1.5f, HeroIds[h] + " blow " + (step + 1) + " fits its authored clip within the playback budget");
                Call(P, "CancelOffense");
            }
            dps[h] = damage / time;
        }
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/weapon-families.csv", csv.ToString());
        Debug.Log("WEAPON DPS sword=" + Num(dps[0]) + " axe=" + Num(dps[1]) + " blade=" + Num(dps[2]));
        Check(dps[1] > dps[0] * 0.8f && dps[1] < dps[0] * 1.3f && dps[2] > dps[0] * 0.8f && dps[2] < dps[0] * 1.3f,
            "front-on combo damage per second stays within 0.8-1.3x of the sword for both new families (identity comes from spacing, not raw output)");
    }

    // Diagnostic picture: each family's first blow and finisher against the same ring of dummies, from above.
    static void Capture()
    {
        if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        foreach (string hero in HeroIds)
        {
            foreach (int step in new[] { 0, 2 })
            {
                Reset(hero); Set(P, "comboStep", step);
                foreach (float b in new[] { -100f, -50f, 0f, 50f, 100f, 180f }) Target(b, 1.5f);
                Target(0f, 2.9f);
                Call(P, "Strike");
                var go = new GameObject("Weapon evidence camera");
                var cam = go.AddComponent<Camera>();
                cam.transform.position = Origin + new Vector3(0f, 9f, 0.6f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.fieldOfView = 50f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
                var rt = new RenderTexture(640, 640, 24); var previous = RenderTexture.active; var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
                try
                {
                    cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); pixels.Apply();
                    File.WriteAllBytes("Logs/weapon-" + hero + "-" + (step + 1) + ".png", pixels.EncodeToPNG());
                }
                finally
                {
                    cam.targetTexture = null; RenderTexture.active = previous; rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }
    }

    // The guard rule as the game applies it each frame, given the button held: the shield and haft
    // hold; the blade's deflection drops by itself and a still-held button cannot chain another.
    static void HeldGuard()
    {
        foreach (string hero in HeroIds)
        {
            Reset(hero);
            Call(P, "StartBlock");
            for (int i = 0; i < 10; i++) Call(P, "TickGuard", 0.1f, true);
            bool holding = MoveName == "Block";
            Check(hero == "nix" ? !holding : holding, hero + (hero == "nix" ? "'s deflection drops after " + Num(WeaponFamily.LightBlade.deflectHold) + " s though the button is held" : " holds its guard for a full second"));
        }
        Call(P, "StartBlock");
        Check(MoveName == "None", "a held button cannot chain a second deflection");
        // No key is held in the test editor: the frame's real input read sees the release.
        Call(P, "ReadCombatInput", Vector3.zero, 0.01f);
        Call(P, "StartBlock");
        Check(MoveName == "Block", "releasing and pressing again gives a fresh deflection");
        Call(P, "TickGuard", 0.05f, false);
        Check(MoveName == "None", "every guard drops the moment the button is released");
    }

    static WeaponFamilyTest()
    {
        if (SessionState.GetBool(Session, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Save.Wipe();
        Save.Level = 12;
        Save.StatPoints = 33;
        Save.SeenIntro = true;
        Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; deadline = readyAt + 180; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Weapon family test initialization timed out");
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
            Application.runInBackground = true;
            Save.BeginExpedition(false);
            Identity(); Spacing(); GuardAndFlank(); Tempo(); Guards(); HeldGuard(); Kit(); Feedback(); Balance(); Capture();
            Check(JourneyStore.Error == null, "weapon family scenarios leave the journey store healthy");
            Debug.Log("WEAPON DONE " + passed + " checks passed");
            SessionState.SetBool(Session, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("WEAPON FAILED " + e);
            SessionState.SetBool(Session, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
}
