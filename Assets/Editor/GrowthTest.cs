using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Runs against isolated journey data. The balance export reads the same stats,
// attack plans, imported clips and instantiated enemy health used by the game.
[InitializeOnLoad]
public static class GrowthTest
{
    const string Session = "GreyspireGrowthTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int passed, phase;
    static double readyAt;
    static readonly List<string> unexpectedErrors = new List<string>();
    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) unexpectedErrors.Add(message);
    }
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static readonly Vector3 Origin = new Vector3(1000f, 0.1f, 1000f);
    static object Get(object host, string field) => host.GetType().GetField(field, Any).GetValue(host);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        passed++; Debug.Log("GROWTH PASS " + message);
    }
    static void Reload() { JourneyStore.ReloadForTest(); JourneyStore.Ensure(); Upgrades.Reset(); }
    static void Fresh() { Save.Wipe(); Upgrades.Reset(); }
    static void Fails(Action action, Type type, string message)
    {
        bool failed = false;
        try { action(); }
        catch (Exception e) { failed = type.IsInstanceOfType(e.GetBaseException()); }
        finally { JourneyStore.AfterMutationForTest = JourneyStore.BeforeReplaceForTest = null; }
        Check(failed, message); Reload();
    }
    static void Storage()
    {
        Check(Environment.GetCommandLineArgs().Contains("-greyspire-save-dir"), "dedicated save directory required");
        Fresh(); Save.Level = 59; Save.StatPoints = 174; Save.Flush();
        int need = Game.XpNeeded(59);
        Check(Save.AwardXp(need + 77) == 1 && Save.Level == 60 && Save.StatPoints == 177 && Save.Sparks == 3 && Save.Xp == 17,
            "cap crossing grants the last three points and converts only surplus XP");
        Save.AwardXp(3); Reload();
        Check(Save.Level == 60 && Save.StatPoints == 177 && Save.Sparks == 4 && Save.Xp == 0, "fractional XP conversion survives reload without extra levels or points");
        Fresh(); Save.AwardXp(int.MaxValue); Reload();
        Check(Save.Level == 60 && Save.StatPoints == 177 && Save.Sparks > 0 && Save.Xp < 20, "largest legal XP award terminates with bounded power and preserved surplus");
        Fresh(); Save.Level = int.MaxValue; Save.StatPoints = 19; Save.SetStat(0, 1234); Save.Flush();
        Save.AwardXp(41); Reload();
        Check(Save.Level == int.MaxValue && Save.Stat(0) == 1234 && Save.StatPoints == 19 && Save.Sparks == 2 && Save.Xp == 1,
            "legacy level and allocations remain stored while new XP pays currency");
        Check(Game.XpNeeded(int.MaxValue) == 11530 && Game.XpNeeded(int.MinValue) == 25, "legacy and invalid XP thresholds cannot overflow");
        Fresh(); Save.StatPoints = 4; Save.SetStat(0, 59); Save.Flush();
        Check(Save.AllocateStat(0) && Save.Stat(0) == 60 && Save.StatPoints == 3, "last stat rank costs exactly one point");
        Check(!Save.AllocateStat(0) && !Save.AllocateStat(-1) && !Save.AllocateStat(4) && Save.StatPoints == 3,
            "capped and invalid allocations spend nothing");
        Save.SetStat(0, 65); Save.SetStat(1, 61); Save.SetStat(2, 7); Save.SetStat(3, 9); Save.StatPoints = 13; Save.Flush();
        Save.RespecStats(); Reload();
        Check(Save.StatPoints == 155 && Enumerable.Range(0, 4).All(i => Save.Stat(i) == 0), "town respec refunds every legacy allocation exactly");
        Save.RespecStats();
        Check(Save.StatPoints == 155, "repeating respec cannot mint points");
        Check(Save.ConvertPoint() && Save.StatPoints == 154 && Save.Sparks == 25, "conversion trades one point for exactly twenty-five banked Sparks");
        Save.StatPoints = 0; Save.Flush();
        Check(!Save.ConvertPoint() && Save.Sparks == 25, "empty point balance cannot buy Sparks");
        foreach (string operation in new[] { "allocate", "respec", "convert", "daily", "xp" })
        {
            Fresh(); Save.Level = 59; Save.StatPoints = 2; Save.SetStat(0, 2); Save.DailyCount = 1; Save.DailyDay = Save.Day; Save.Flush();
            string before = File.ReadAllText(JourneyStore.FilePath);
            JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected write failure"); };
            Fails(() =>
            {
                if (operation == "allocate") Save.AllocateStat(1);
                if (operation == "respec") Save.RespecStats();
                if (operation == "convert") Save.ConvertPoint();
                if (operation == "daily") Save.RecordDailyKill(2, 13);
                if (operation == "xp") Save.AwardXp(need + 20);
            }, typeof(IOException), operation + " reports failed disk commit");
            Check(File.ReadAllText(JourneyStore.FilePath) == before && Save.Level == 59 && Save.StatPoints == 2 && Save.Stat(0) == 2
                && Save.Stat(1) == 0 && Save.Sparks == 0 && Save.DailyGrowth == 0 && Save.DailyCount == 1,
                operation + " rollback preserves the complete committed snapshot");
        }
        Fresh();
        JourneyStore.Transaction(() =>
        {
            for (int day = 1; day <= 1000; day++)
            {
                Save.Day = day; Save.RecordDailyKill(2, 13); var reward = Save.RecordDailyKill(2, 13);
                if (!reward.complete || reward.point != (day <= 20) || Save.RecordDailyKill(2, 13).complete) throw new Exception("daily replay/budget");
            }
        });
        Reload();
        Check(Save.DailyGrowth == 20 && Save.StatPoints == 20 && Save.Sparks == 13000, "one thousand days pay at most twenty points with currency continuing and no same-day replay");
        Save.Day++; Save.Flush(); Save.RecordDailyKill(1, 13); Reload();
        Check(Save.DailyGrowth == 20 && Save.StatPoints == 20 && Save.Sparks == 13013, "reload and dawn cannot reset the lifetime daily budget");
        Fresh(); Save.StatPoints = int.MaxValue; Save.Flush();
        Fails(() => Save.AwardDailyPoint(), typeof(OverflowException), "daily point overflow is rejected atomically");
        Check(Save.DailyGrowth == 0 && Save.StatPoints == int.MaxValue, "rejected daily point retains its entitlement counter");
        Save.SetStat(0, 1); Save.Flush();
        Fails(() => Save.RespecStats(), typeof(OverflowException), "unrepresentable legacy respec refuses to erase allocations");
        Check(Save.Stat(0) == 1 && Save.StatPoints == int.MaxValue, "respec overflow retains all legacy records");
        Fresh(); Save.Sparks = int.MaxValue; Save.StatPoints = 1; Save.Level = 60; Save.Xp = 19; Save.Flush();
        Fails(() => Save.ConvertPoint(), typeof(OverflowException), "point conversion rejects wallet overflow");
        Check(Save.StatPoints == 1 && Save.Sparks == int.MaxValue, "failed conversion loses no point");
        Fails(() => Save.AwardXp(1), typeof(OverflowException), "XP conversion rejects wallet overflow");
        Check(Save.Xp == 19 && Save.Sparks == int.MaxValue, "failed XP conversion retains its remainder");
        string summary = Game.GrowthSummary(1, 60);
        Check(Game.SkillNames.All(summary.Contains) && new[] { "D / C / B / A / S", "+177 stat points", "20 = 1" }.All(summary.Contains),
            "multi-level summary includes every skill rank crossing points and cap conversion");
        Fresh(); Check(Save.DailyGrowth == 0, "new campaign gets its own daily budget");
    }
    class Loadout
    {
        public string name;
        public int level;
        public int[] stats, perks, boons;
        public Loadout(string name, int level, int[] stats, int[] perks, int[] boons) { this.name = name; this.level = level; this.stats = stats; this.perks = perks; this.boons = boons; }
    }
    static readonly int[] zeroStats = new int[4], zeroBoons = new int[14], forge = { 3, 3, 3, 3 };
    static readonly int[] legalBoons = { 5, 4, 3, 4, 2, 4, 3, 2, 0, 0, 0, 0, 2, 1 };
    static Loadout[] Loadouts() => new[]
    {
        new Loadout("starter", 1, zeroStats, zeroStats, zeroBoons),
        new Loadout("balanced-l16", 16, new[] { 15, 15, 15, 0 }, new[] { 1, 1, 1, 1 }, new[] { 2, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        new Loadout("strength-l60", 60, new[] { 60, 39, 39, 39 }, forge, legalBoons),
        new Loadout("agility-l60", 60, new[] { 39, 60, 39, 39 }, forge, legalBoons),
        new Loadout("sense-l60", 60, new[] { 39, 39, 39, 60 }, forge, legalBoons),
        new Loadout("legal-207", 60, new[] { 60, 60, 27, 60 }, forge, legalBoons),
        new Loadout("legacy-envelope", int.MaxValue, new[] { 999, 999, 999, 999 }, forge, Upgrades.All.Select(u => u.max).ToArray()),
    };
    static void Apply(Loadout build)
    {
        JourneyStore.Transaction(() =>
        {
            Save.Level = build.level; Save.StatPoints = 0;
            for (int i = 0; i < 4; i++) { Save.SetStat(i, build.stats[i]); JourneyStore.SetInt("perk" + i, build.perks[i]); }
            // Every boon id is set: ids beyond a loadout's list (newer boons) are untaken.
            for (int i = 0; i < Mathf.Max(build.boons.Length, Upgrades.All.Count); i++) Save.SetBoon(i, i < build.boons.Length ? build.boons[i] : 0);
        });
        Upgrades.Reset(); Call(P, "BuildStats"); P.weak = 1f;
        P.Revive(Origin, true); P.transform.rotation = Quaternion.identity;
    }
    static void Setup()
    {
        foreach (var enemy in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        G.enabled = P.enabled = false;
        ((IList)Get(G, "timers")).Clear();
        Set(G, "<DefeatPending>k__BackingField", false); Set(G, "<InTown>k__BackingField", false); Set(G, "<InWilds>k__BackingField", false);
        Set(G, "waveActive", false); Set(G, "fade", 0f); Set(G, "fadeDir", 0f); Set(G, "flashA", 0f);
        Call(G, "SetState", Game.GameState.Playing); Call(G, "ResetSystem");
        Juice.Reset(); Juice.Paused = false; Time.timeScale = 1f;
    }
    static AnimationClipPlayable ClipPlayable(CharAnim clips, string name, bool upper)
    {
        var slots = (IDictionary)Get(clips, upper ? "upperSlots" : "slots");
        return (AnimationClipPlayable)Get(slots[name], "playable");
    }
    static string Num(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
    static void RuntimeAndExport()
    {
        Setup();
        Check(legalBoons.Sum() == 30 && Loadouts().First(b => b.name == "legal-207").stats.Sum() == 207, "extreme fresh build respects thirty Guardian rewards and 177+20+10 points");
        var actionCsv = new StringBuilder("hero,build,action,clip,clip_length,duration,contact_seconds,clip_contact,clip_speed,impact_factor\n");
        var statCsv = new StringBuilder("hero,build,level,hearts,damage,crit_probability,crit_multiplier,attack_speed,move_speed,dash_cooldown,reach,burst_gain,coin_multiplier,light_dps,combo_seconds\n");
        var balanceCsv = new StringBuilder("hero,build,floor,gloom,target,target_hp,expected_light_dps,active_seconds_to_kill,light_hits_to_kill,incoming_light,incoming_heavy,hearts,survivable_light_hits,survivable_heavy_hits,core_xp_day,core_xp_night,style_bonus_sparks\n");
        var report = new StringBuilder("# Greyspire phase-four balance model\n\nGenerated by `GrowthTest` from runtime stats, attack plans, imported clips and actual instantiated enemy HP. Times are sustained active light-combo estimates: no travel, guard, miss, hitstop, cooldown, stagger, juggle, riposte, burst, healing or companion assistance. They are idealized expected damage estimates, not observed fight lengths.\n\n");
        var targets = new List<(int floor, int gloom, string name, float hp, int score, int coin, int harm)>();
        foreach (int floor in new[] { 1, 5, 10, 16, 25, 30 })
        for (int gloom = 0; gloom <= Save.MaxGloom; gloom++)
        {
            Set(G, "level", (floor - 1) / 10); Set(G, "wave", (floor - 1) % 10); Set(G, "<Gloom>k__BackingField", gloom);
            foreach (var kind in new[] { Enemy.Kind.Minion, Enemy.Kind.Brute, Enemy.Kind.Boss })
            {
                bool elite = kind == Enemy.Kind.Brute;
                var enemy = Enemy.Create(kind, Origin + Vector3.forward * 50f, (floor - 1) / 10, elite);
                targets.Add((floor, gloom, kind == Enemy.Kind.Brute ? "Guardian-envelope" : kind.ToString(), enemy.maxHp, enemy.scoreValue, enemy.coinValue, G.Harm));
                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
        }
        report.Append("Six floor snapshots (1, 5, 10, 16, 25, 30), all six Gloom tiers, all five heroes and seven builds. Guardian-envelope uses an elite Brute; it is hypothetical on non-Guardian floors. King envelopes below floor 30 are also hypothetical.\n\n| Hero | Legal 207 DPS | Combo seconds | Gloom 5 King HP | Active TTK seconds | Hearts | Heavy hits survived |\n|---|---:|---:|---:|---:|---:|---:|\n");
        for (int hero = 0; hero < Heroes.All.Length; hero++)
        {
            P.SetHero(hero); P.enabled = false;
            var clips = (CharAnim)Get(P, "clips");
            foreach (var build in Loadouts())
            {
                Apply(build); var s = P.stats;
                Check(s.damage > 0 && s.damage <= GrowthPolicy.MaxDamage && s.maxHearts >= 1 && s.maxHearts <= 30 && s.crit <= 0.75f && s.critMultiplier >= 2
                    && s.attackSpeed <= 1.8f && s.moveSpeed <= 1.08f && s.dashCooldown >= 0.2f && s.reach <= 2f && s.coinMult <= 5,
                    Heroes.All[hero].name + " / " + build.name + " has finite bounded effective power");
                int steps = build.level >= 2 ? 3 : 2; float duration = 0f, damage = 0f;
                bool plansValid = true, playbackValid = true;
                for (int step = 0; step < 3; step++)
                {
                    Set(P, "comboStep", step); Call(P, "StartAttack", Vector3.zero);
                    var plan = (GrowthPolicy.ContactPlan)Get(P, "attackPlan");
                    string clip = P.Hero.combo[step];
                    plansValid &= clips.Has(clip) && plan.duration >= 0.12f && plan.clipSpeed <= 2f && plan.power >= 1 && plan.power <= 1.5f
                        && Mathf.Abs(plan.duration * plan.fraction * plan.clipSpeed - plan.clipContact) < 0.0001f;
                    var playable = ClipPlayable(clips, clip, false);
                    playbackValid &= Math.Abs(playable.GetTime()) < 0.0001 && Math.Abs(playable.GetSpeed() - plan.clipSpeed) < 0.0001;
                    // Drive the real imported animation graph to the gameplay contact time.
                    ((PlayableGraph)Get(clips, "graph")).Evaluate(0f);
                    ((PlayableGraph)Get(clips, "graph")).Evaluate(plan.duration * plan.fraction);
                    if (Math.Abs(playable.GetTime() - plan.clipContact) >= 0.002)
                        Debug.Log("GROWTH SAMPLE " + hero + " " + build.name + " " + step + " expected=" + plan.clipContact + " actual=" + playable.GetTime() + " speed=" + playable.GetSpeed());
                    playbackValid &= Math.Abs(playable.GetTime() - plan.clipContact) < 0.002;
                    clips.Contact(clip, plan, true);
                    playable = ClipPlayable(clips, clip, true);
                    playbackValid &= Math.Abs(playable.GetTime()) < 0.0001 && Math.Abs(playable.GetSpeed() - plan.clipSpeed) < 0.0001;
                    actionCsv.AppendLine(string.Join(",", hero, build.name, "light" + step, clip, Num(clips.Length(clip)), Num(plan.duration), Num(plan.duration * plan.fraction), Num(plan.clipContact), Num(plan.clipSpeed), Num(plan.power)));
                    if (step < steps) { duration += plan.duration; damage += s.damage * P.Weapon.steps[step].damage * plan.power * (1 + s.crit * (s.critMultiplier - 1)); }
                }
                Check(plansValid, Heroes.All[hero].name + " / " + build.name + " contacts fit authored clips and playback budget");
                Check(playbackValid, Heroes.All[hero].name + " / " + build.name + " full and upper graphs start at windup and reach authored contact");
                float dps = damage / duration;
                statCsv.AppendLine(string.Join(",", hero, build.name, build.level, s.maxHearts, Num(s.damage), Num(s.crit), Num(s.critMultiplier), Num(s.attackSpeed), Num(s.moveSpeed), Num(s.dashCooldown), Num(s.reach), Num(s.burstGain), Num(s.coinMult), Num(dps), Num(duration)));
                foreach (var target in targets)
                {
                    int light = 1 + target.harm, heavy = 2 + target.harm;
                    balanceCsv.AppendLine(string.Join(",", hero, build.name, target.floor, target.gloom, target.name, Num(target.hp), Num(dps), Num(target.hp / dps), Num(target.hp / (damage / steps)), light, heavy, s.maxHearts,
                        (s.maxHearts - 1) / light, (s.maxHearts - 1) / heavy, target.name == "Boss" ? 600 : GrowthPolicy.CoreXp(target.score, target.floor, 0), target.name == "Boss" ? 600 : GrowthPolicy.CoreXp(target.score, target.floor, 1), target.name == "Boss" ? 0 : GrowthPolicy.StyleSparks(target.coin, 2)));
                }
                if (build.name == "legal-207")
                {
                    var king = targets.First(t => t.floor == 30 && t.gloom == 5 && t.name == "Boss");
                    report.AppendLine("| " + P.Hero.name + " | " + Num(dps) + " | " + Num(duration) + " | " + Num(king.hp) + " | " + Num(king.hp / dps) + " | " + s.maxHearts + " | " + (s.maxHearts - 1) / (2 + king.harm) + " |");
                    Check(king.hp / dps >= 6 && king.hp / dps < 30 && (s.maxHearts - 1) / (2 + king.harm) >= 2, P.Hero.name + " extreme legal build preserves a readable King health budget and survives multiple heavy hits");
                }
            }
            var max = Loadouts().First(b => b.name == "legacy-envelope"); Apply(max);
            foreach (string move in new[] { "Heavy", "Stab", "Kick", "Bash", "Throw" })
            {
                Call(P, "CancelOffense");
                if (move == "Heavy") Call(P, "StartHeavy", 1f);
                if (move == "Stab") Call(P, "StartStab", Vector3.forward);
                if (move == "Kick") Call(P, "StartKick");
                if (move == "Bash") Call(P, "StartBash");
                if (move == "Throw") Call(P, "StartThrow");
                var plan = (GrowthPolicy.ContactPlan)Get(P, "movePlan");
                Check(plan.duration >= 0.12f && plan.clipSpeed <= 2 && Math.Abs(plan.duration * plan.fraction * plan.clipSpeed - plan.clipContact) < 0.0001,
                    Heroes.All[hero].name + " " + move + " extreme move shares contact timing with animation");
                actionCsv.AppendLine(string.Join(",", hero, max.name, move, "move", "", Num(plan.duration), Num(plan.duration * plan.fraction), Num(plan.clipContact), Num(plan.clipSpeed), Num(plan.power)));
            }
        }
        report.Append("\nFresh permanent point supply: 59 levels × 3 = 177; lifetime daily points = 20; five rescue receipts × 2 = 10. Total 207, with 240 available allocation slots. Specialized builds here spend 177 and are attainable without daily/rescue points. Forge ranks stop at 3. There are thirty Guardian receipts across Gloom 0–5; all-forty boon ranks and allocations >60 belong only to the retained legacy envelope.\n\nStacking order: base → forge → hero → effective level/stats → bounded boon ranks → final bounds. Damage above 50 approaches 100 smoothly; agility/tempo and movement use smooth diminishing returns; clip overflow becomes at most 1.5× impact. Crit overflow above 75% raises critical damage. The full legacy record survives.\n\nStyle analysis: for the identical encounter at night/day, permanent XP is identical at all seven style ranks. Old style scaling gave up to twice the XP per kill; it now pays up to the enemy's base coin value in carried bonus Sparks (rounded to whole Sparks, before pickup multipliers). Night retains up to 25% XP for extra encounter risk. Daily farming pays exactly twenty additional permanent points over 1000 or more dawns; repeat days keep banked currency rewards. Max-level XP converts at 20 XP per banked Spark with exact remainder. Currency and optional point conversion fund finite forge/hero/companion unlocks and paid choices/rerolls; no later increase in stats is granted.\n\nRead the CSVs for starter weaknesses, ordinary/Guardian/King envelopes and all Gloom tiers. A starter facing the strongest King is a deliberately mismatched diagnostic, not campaign tuning. No claim of a human playtest, balanced enjoyment, controller review or target-hardware performance follows from this model. Authored contact timestamps retain existing action beats; this validates time agreement, not artistic judgment of the exact weapon pose.\n");
        Directory.CreateDirectory("Reports");
        File.WriteAllText("Reports/growth-actions.csv", actionCsv.ToString());
        File.WriteAllText("Reports/growth-stats.csv", statCsv.ToString());
        File.WriteAllText("Reports/growth-balance.csv", balanceCsv.ToString());
        report.Append(ExportPace());
        File.WriteAllText("Reports/growth-balance.md", report.ToString());
        Check(targets.Count == 108, "balance model captures actual health at six floors six Gloom tiers and three enemy envelopes");
        RuntimeRewards();
    }
    static string ExportPace()
    {
        var csv = new StringBuilder("gloom,floor,kills,current_xp,old_sss_xp,current_level,old_d_level,old_sss_level,current_level_points,old_sss_level_points\n");
        int slowLevel = 1, slowXp = 0, fastLevel = 1, fastXp = 0;
        Fresh();
        JourneyStore.Transaction(() =>
        {
            for (int gloom = 0; gloom <= Save.MaxGloom; gloom++)
            for (int floor = 1; floor <= Game.TowerFloors; floor++)
            {
                Set(G, "<Gloom>k__BackingField", gloom);
                object wave = Call(G, "FloorWave", (floor - 1) / 10, (floor - 1) % 10);
                int current = 0, oldFast = 0, kills = 0;
                void Add(int count, int score)
                {
                    kills += count;
                    current += count * GrowthPolicy.CoreXp(score, floor, 0);
                    oldFast += count * Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, score * 0.5f) * (1 + 0.1f * (floor - 1)) * 2));
                }
                if (floor == 30) { current = oldFast = 600; kills = 1; }
                else
                {
                    Add((int)Get(wave, "slimes") + 2 * (int)Get(wave, "bigSlimes"), 10);
                    Add((int)Get(wave, "minions"), 15); Add((int)Get(wave, "bigSlimes"), 30);
                    Add((int)Get(wave, "spitters"), 25); Add((int)Get(wave, "bombers"), 20);
                    int guardians = Game.GuardianFloor(floor) ? 1 : 0;
                    Add((int)Get(wave, "brutes") - guardians, 100); Add(guardians, 300);
                }
                Save.AwardXp(current);
                slowXp += current; fastXp += oldFast;
                while (slowLevel < 60 && slowXp >= Game.XpNeeded(slowLevel)) slowXp -= Game.XpNeeded(slowLevel++);
                while (fastLevel < 60 && fastXp >= Game.XpNeeded(fastLevel)) fastXp -= Game.XpNeeded(fastLevel++);
                csv.AppendLine(string.Join(",", gloom, floor, kills, current, oldFast, Save.Level, slowLevel, fastLevel, Save.StatPoints, 3 * (fastLevel - 1)));
            }
        });
        File.WriteAllText("Reports/growth-pace.csv", csv.ToString());
        Check(Save.Level == slowLevel && fastLevel > slowLevel && Save.StatPoints == 3 * (slowLevel - 1), "six deterministic tower climbs preserve struggling-player XP pace and remove style's extra permanent levels");
        return "\nPace projection (`growth-pace.csv`): sequential full tower clears at Gloom 0–5 use the real deterministic FloorWave counts, guaranteed elite Guardians, two Slime children per BigSlime, and the King's actual fixed 600 XP. Excludes optional Wilds, missions, daily/rescue points, random elites and boss summons; assumes all enemies are killed once. Current and old D style both finish at level " + slowLevel + "; old continuously-SSS style finishes at level " + fastLevel + ". This measures the old upper-bound feedback loop, not an observed player's style history. The struggling-player core XP schedule is unchanged.\n";
    }
    static void RuntimeRewards()
    {
        Setup(); Fresh(); Save.Level = 12; Save.StatPoints = 33; Save.Flush(); P.SetHero(0); P.enabled = false;
        P.Revive(Origin, true);
        var enemy = Enemy.Create(Enemy.Kind.Minion, Origin + Vector3.forward * 2f, 0); enemy.enabled = false;
        Set(G, "styleRank", 0); int xp0 = (int)Call(G, "KillXp", enemy);
        Set(G, "styleRank", 6); Set(G, "style", 800f);
        Check((int)Call(G, "KillXp", enemy) == xp0, "actual kill XP is independent of style rank");
        int carried = Save.CarriedSparks; G.OnEnemyKilled(enemy);
        Check(Save.CarriedSparks == carried + GrowthPolicy.StyleSparks(enemy.coinValue, 2), "style pays the carried wallet in the actual kill transaction");
        Set(G, "<DefeatPending>k__BackingField", true); carried = Save.CarriedSparks; G.OnEnemyKilled(enemy);
        Check(Save.CarriedSparks == carried, "post-defeat style rewards cannot reopen the settled purse");
        Set(G, "<DefeatPending>k__BackingField", false); Fresh(); Save.Level = 1; Save.DailyDay = Save.Day; Save.DailyCount = 9; Save.Flush(); Call(G, "ResetSystem");
        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        enemy = Enemy.Create(Enemy.Kind.Minion, Origin + Vector3.forward * 2f, 0); enemy.enabled = false;
        JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected kill commit failure"); };
        Fails(() => G.OnEnemyKilled(enemy), typeof(IOException), "actual combined kill commit reports disk failure");
        Check(Save.Xp == 0 && Save.DailyCount == 9 && Save.DailyGrowth == 0 && (string)Get(G, "systemTitle") == "",
            "rolled-back daily reward is never announced by a failed kill commit");
        Call(G, "ResetSystem"); Call(G, "SystemMessage", "GROWTH", Game.GrowthSummary(1, 13));
        Call(G, "SystemMessage", "DAILY", "+1 point"); Call(G, "SystemMessage", "COMMISSION", "+30 Sparks");
        Check((string)Get(G, "systemTitle") == "GROWTH" && ((ICollection)Get(G, "systemMessages")).Count == 2, "daily and commission messages cannot overwrite an active growth summary");
        Call(G, "TickSystem", 20f);
        Check((string)Get(G, "systemTitle") == "DAILY", "queued reward notices advance in committed order");
        Call(G, "TickSystem", 20f);
        Check((string)Get(G, "systemTitle") == "COMMISSION", "every queued reward notice remains available");
        float readingTime = (float)Get(G, "systemT"); Call(G, "SetState", Game.GameState.Paused); Call(G, "TickSystem", 20f);
        Check((float)Get(G, "systemT") == readingTime, "Status and pause cannot expire an unread growth notice");
        Call(G, "SetState", Game.GameState.Story); Call(G, "TickSystem", 20f);
        Check((float)Get(G, "systemT") == readingTime, "dialogue cannot expire queued reward notices");
        Call(G, "SetState", Game.GameState.Playing);
        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        var low = GrowthPolicy.Build(60, new[] { 59, 59, 0, 59 }, forge, legalBoons, Heroes.All[2]);
        var high = GrowthPolicy.Build(60, new[] { 60, 60, 0, 60 }, forge, legalBoons, Heroes.All[2]);
        Check(high.damage > low.damage && high.attackSpeed > low.attackSpeed && high.moveSpeed > low.moveSpeed && high.critMultiplier > low.critMultiplier,
            "last strength agility and sense points retain value after diminishing returns and crit cap");
        Call(P, "CancelOffense"); P.stats = high; P.weak = 1f;
        enemy = Enemy.Create(Enemy.Kind.Brute, Origin + Vector3.forward * 2f, 0); enemy.enabled = false;
        enemy.hp = enemy.maxHp = 10000f; Set(enemy, "guarding", false); Set(enemy, "openT", 0f); Physics.SyncTransforms();
        var hit = (HitResult)Call(P, "Hit", enemy, 10f, true, 0f, false, DamageKind.Physical, false, 0f, 0f);
        Check(Mathf.Abs(hit.damage - 10f * high.critMultiplier) < 0.001f, "critical overflow applies once to actual contact damage");
        // Disabled enemies stop updating and deregister; keep this target in the
        // real melee candidate list while driving its attack frames manually.
        Enemy.All.Add(enemy);
        enemy.hp = 10000f; Call(P, "CancelOffense"); Call(P, "StartHeavy", 1f);
        var heavyPlan = (GrowthPolicy.ContactPlan)Get(P, "movePlan");
        Call(P, "TickMove", heavyPlan.duration * 0.5f - 0.001f);
        Check(enemy.hp == 10000f, "extreme heavy attack cannot hurt before its visible contact phase");
        Call(P, "TickMove", 0.002f);
        Check(enemy.hp < 10000f, "extreme heavy attack hurts when its animation contact phase opens");
        float afterContact = enemy.hp; Call(P, "TickMove", 0.001f);
        Check(enemy.hp == afterContact, "heavy contact phase cannot reward duplicate damage on subsequent frames");
        Enemy.All.Remove(enemy);
        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        var legacyHost = new GameObject("Legacy contact fixture"); var legacyModel = new GameObject("Model"); legacyModel.transform.SetParent(legacyHost.transform);
        var animation = legacyModel.AddComponent<Animation>(); var legacyClip = new AnimationClip { legacy = true };
        legacyClip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1)); animation.AddClip(legacyClip, "Attack");
        var legacy = CharAnim.Attach(legacyHost, legacyModel.transform);
        var legacyPlan = new GrowthPolicy.ContactPlan(0.12f, 0.4f, 0.5f); legacy.Contact("Attack", legacyPlan, true);
        Check(animation["Attack"].time == 0 && animation["Attack"].speed <= 2 && Mathf.Abs(animation["Attack"].speed * legacyPlan.duration * 0.5f - 0.4f) < 0.001f,
            "legacy animation fallback uses the same windup contact and speed budget");
        UnityEngine.Object.DestroyImmediate(legacyHost); UnityEngine.Object.DestroyImmediate(legacyClip);
        P.SetHero(2); P.enabled = false; Apply(Loadouts().Last());
        Check(Save.Level == int.MaxValue && Save.Stat(0) == 999, "runtime effective-stat calculation never rewrites legacy growth");
        P.stats.secondWinds = 0; P.RefreshStats();
        Check(P.stats.secondWinds == 0, "refresh respec and level presentation cannot refill spent Second Winds");
        P.NewRun(); Check(P.stats.secondWinds == 2, "owned Second Wind charges arrive on the next expedition");
        Set(G, "<InTown>k__BackingField", true); Call(G, "SetState", Game.GameState.Hub); Call(G, "OpenPause", 1); Set(G, "fade", 0f);
        Call(G, "ResetSystem");
    }
    static GrowthTest()
    {
        if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
    }
    public static void Run()
    {
        try
        {
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            foreach (string capture in new[] { "Logs/growth-status.png", "Logs/growth-summary.png" }) if (File.Exists(capture)) File.Delete(capture);
            Storage(); Fresh(); Save.SeenIntro = true; Save.StoryFloor = Save.ClearStory = 30; Save.Flush();
            SessionState.SetInt(Session + "Passed", passed); SessionState.SetBool(Session, true);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogError("GROWTH FAILED " + e); EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup + 2;
        if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (phase == 0)
            {
                passed = SessionState.GetInt(Session + "Passed", 0); Application.runInBackground = true;
                RuntimeAndExport(); G.gameObject.AddComponent<GrowthTestOverlay>(); phase = 1; readyAt = EditorApplication.timeSinceStartup + 2; return;
            }
            if (phase == 1)
            {
                ScreenCapture.CaptureScreenshot("Logs/growth-status.png"); phase = 2; readyAt = EditorApplication.timeSinceStartup + 2; return;
            }
            if (phase == 2)
            {
                Call(G, "SetState", Game.GameState.Playing); Set(G, "<InTown>k__BackingField", false);
                Call(G, "ResetSystem"); Call(G, "SystemMessage", "GROWTH", Game.GrowthSummary(1, 60)); Call(G, "TickSystem", 0.5f);
                phase = 3; readyAt = EditorApplication.timeSinceStartup + 2; return;
            }
            if (phase == 3)
            {
                ScreenCapture.CaptureScreenshot("Logs/growth-summary.png"); phase = 4; readyAt = EditorApplication.timeSinceStartup + 2; return;
            }
            Check(JourneyStore.Error == null, "growth scenario leaves the journey store healthy");
            Check(unexpectedErrors.Count == 0, "runtime and rendered UI contain no unexpected errors" + (unexpectedErrors.Count > 0 ? ": " + unexpectedErrors[0] : ""));
            if (!Application.isBatchMode) Check(File.Exists("Logs/growth-status.png") && File.Exists("Logs/growth-summary.png"), "both growth UI captures were rendered");
            Debug.Log("GROWTH DONE " + passed + " checks passed");
            SessionState.SetBool(Session, false); EditorApplication.update -= Tick; EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("GROWTH FAILED " + e); SessionState.SetBool(Session, false); EditorApplication.update -= Tick; EditorApplication.Exit(1);
        }
    }
    public static void DrawFixtureUi() { if (G != null) Call(G, "OnGUI"); }
}

// Invokes the real UI while game/controller updates remain frozen for a capture.
public class GrowthTestOverlay : MonoBehaviour
{
    void OnGUI() => GrowthTest.DrawFixtureUi();
}
