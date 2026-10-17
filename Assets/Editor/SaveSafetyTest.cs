using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run via Tools/savetest.ps1: file saves are isolated and registry settings are restored.
[InitializeOnLoad]
public static class SaveSafetyTest
{
    const string Session = "GreyspireSaveSafetyTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    class Step { public float wait; public Action act; }
    [Serializable] class Envelope { public string payload, checksum; }
    static List<Step> steps;
    static int at, passed;
    static double since, deadline;
    static string offerKey;
    static Action thankCallback;
    static Game G => Game.I;
    static object Call(string name, params object[] args) => typeof(Game).GetMethod(name, Any).Invoke(G, args);
    static void Set(string name, object value) => typeof(Game).GetField(name, Any).SetValue(G, value);
    static object Get(string name) => typeof(Game).GetField(name, Any).GetValue(G);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
        Debug.Log("SAVE PASS " + message);
    }
    static int TotalXp()
    {
        int result = Save.Xp;
        for (int level = 1; level < Save.Level; level++) result += Game.XpNeeded(level);
        return result;
    }
    static void Reload() { JourneyStore.ReloadForTest(); JourneyStore.Ensure(); Upgrades.Reset(); }
    static void CleanFiles()
    {
        JourneyStore.BeforeReplaceForTest = JourneyStore.AfterMutationForTest = null;
        foreach (string suffix in new[] { "", ".bak", ".tmp", ".bak.tmp", ".damaged", ".campaign", ".campaign.tmp" })
            if (File.Exists(JourneyStore.FilePath + suffix)) File.Delete(JourneyStore.FilePath + suffix);
        JourneyStore.ReloadForTest();
    }
    static void Fresh() { Save.Wipe(); Upgrades.Reset(); }
    static void Fails(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (IOException) { failed = true; }
        finally { JourneyStore.BeforeReplaceForTest = JourneyStore.AfterMutationForTest = null; }
        Check(failed, message);
    }

    static SaveSafetyTest()
    {
        if (SessionState.GetBool(Session, false)) EditorApplication.update += Tick;
    }
    public static void Run()
    {
        try
        {
            Check(Array.IndexOf(Environment.GetCommandLineArgs(), "-greyspire-save-dir") >= 0, "dedicated save directory required");
            StorageTests();
            RewardTests();
            Fresh();
            Save.SeenIntro = true;
            Save.StoryFloor = Save.ClearStory = 30;
            Save.Pet = true;
            Save.Level = 2;
            Save.StatPoints = 3;
            Save.TowerFloor = Save.BestFloor = 4;
            Save.Flush();
            SessionState.SetInt(Session + "Passed", passed);
            SessionState.SetBool(Session, true);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogError("SAVE FAILED " + e); EditorApplication.Exit(1); }
    }

    static void StorageTests()
    {
        CleanFiles();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.SetInt("level", 4);
        PlayerPrefs.SetInt("xp", 12);
        PlayerPrefs.SetInt("bestFloor", 10);
        PlayerPrefs.SetInt("boon2", 1);
        PlayerPrefs.SetInt("rescued", 3);
        PlayerPrefs.SetInt("rescueAsk0", 2);
        PlayerPrefs.SetInt("missions", 1);
        PlayerPrefs.SetInt("mission", 3); // old active index 2
        PlayerPrefs.SetInt("perk1", 2);
        PlayerPrefs.SetInt("hero1", 1);
        PlayerPrefs.SetString("metAll", "Test Resident");
        PlayerPrefs.SetInt("metTest Resident", 1);
        Save.Sensitivity = 0.77f;
        Save.Volume = 0.42f;
        Save.CameraFov = 72f;
        Save.CameraMotion = 0.2f;
        Save.InvertLook = true;
        PlayerPrefs.Save();
        JourneyStore.Ensure();
        Check(Save.Level == 4 && Save.Xp == 12 && Save.TowerFloor == 10, "registry migration preserves level XP and fallback floor");
        Check(Save.Boon(2) == 1 && Save.PerkRank(1) == 2 && Heroes.Owned(1) && Save.Met("Test Resident"), "migration preserves boons perks heroes and met names");
        Check(Save.Mission == 2 && Save.MissionPaid("wolves-road"), "frozen commission indexes migrate to stable IDs");
        var missions = (Array)typeof(Game).GetField("Missions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        object first = missions.GetValue(0), third = missions.GetValue(2);
        try
        {
            missions.SetValue(third, 0);
            missions.SetValue(first, 2);
            Check(Save.Mission == 0 && Game.MissionId(2) == "wolves-road" && Save.MissionPaid(Game.MissionId(2)), "commission reorder preserves active and paid identity");
        }
        finally { missions.SetValue(first, 0); missions.SetValue(third, 2); }
        Check(Save.PendingGuardian == null && Save.GuardianPaid(0, 5) && Save.GuardianPaid(0, 10), "ambiguous legacy Guardian payments are never granted again");
        Check(Save.RescueChoice(0) == 2 && !Save.RescuePending(0) && Save.RescuePending(1), "legacy rescue keeps choice or queues unresolved follow-up");
        PlayerPrefs.SetInt("level", 99);
        Reload();
        Check(Save.Level == 4, "file snapshot is authoritative after migration");
        string oldCampaign = JourneyStore.Campaign;
        JourneyStore.SetInt("future-journey-key", 9);
        Save.CarriedSparks = 19;
        Save.Flush();
        Fresh();
        Check(JourneyStore.Campaign != oldCampaign && !JourneyStore.HasKey("future-journey-key") && Save.RescueChoice(0) == 0 && Save.CarriedSparks == 0, "new game resets every journey key and campaign identity");
        Check(Mathf.Abs(Save.Sensitivity - 0.77f) < 0.001f && Mathf.Abs(Save.Volume - 0.42f) < 0.001f && Save.CameraFov == 72f && Save.InvertLook, "new game preserves settings");
        Reload();
        Check(Save.Level == 1 && Save.TowerFloor == 0 && Save.Boon(2) == 0 && Save.Mission == -1, "new game never reimports old registry progress");
        File.WriteAllText(JourneyStore.FilePath, "truncated");
        Reload();
        Check(Save.Level == 1 && JourneyStore.Recovery != null && File.Exists(JourneyStore.FilePath + ".damaged"), "new-game backup recovery cannot resurrect the previous campaign");
        JourneyStore.Transaction(() => Save.Sparks = 77);
        Save.Flush();
        oldCampaign = JourneyStore.Campaign;
        JourneyStore.BeforeReplaceForTest = target => { if (target == JourneyStore.BackupPath) throw new IOException("Interrupted reset backup refresh"); };
        Save.Wipe();
        JourneyStore.BeforeReplaceForTest = null;
        string resetSnapshot = File.ReadAllText(JourneyStore.FilePath);
        Check(JourneyStore.Campaign != oldCampaign && Save.Sparks == 0, "new-game primary commits even when its redundant refresh fails");
        File.WriteAllText(JourneyStore.FilePath, "damaged new-game primary");
        JourneyStore.ReloadForTest();
        bool oldCampaignBlocked = false;
        try { JourneyStore.Ensure(); } catch (InvalidDataException) { oldCampaignBlocked = true; }
        Check(oldCampaignBlocked && File.ReadAllText(JourneyStore.FilePath) == "damaged new-game primary", "campaign marker refuses an old backup after interrupted reset refresh");
        File.WriteAllText(JourneyStore.FilePath, resetSnapshot);
        Reload();
        Save.Flush();
        Check(Save.Sparks == 0 && Save.Level == 1, "valid new-game primary recovers without historical progress");
        JourneyStore.Transaction(() => Save.Sparks = 17);
        Save.Flush();
        string saved = File.ReadAllText(JourneyStore.FilePath);
        JourneyStore.BeforeReplaceForTest = target => { if (target == JourneyStore.FilePath) throw new IOException("Injected write interruption"); };
        Fails(() => JourneyStore.Transaction(() => Save.Sparks = 999), "failed atomic replacement reports failure");
        Check(Save.Sparks == 17 && File.ReadAllText(JourneyStore.FilePath) == saved, "failed replacement rolls memory back and preserves disk snapshot");
        Reload();
        Check(Save.Sparks == 17, "orphan temporary file cannot supersede committed progress");
        oldCampaign = JourneyStore.Campaign;
        JourneyStore.BeforeReplaceForTest = target => { if (target == JourneyStore.FilePath) throw new IOException("Interrupted new game"); };
        Fails(Save.Wipe, "interrupted new game reports failure");
        Reload();
        Check(JourneyStore.Campaign == oldCampaign && Save.Sparks == 17, "failed new-game commit preserves the original journey");
        File.WriteAllText(JourneyStore.FilePath, "broken primary");
        Reload();
        Check(Save.Sparks == 17 && JourneyStore.Recovery != null, "corrupt primary recovers the complete committed backup");
        File.WriteAllText(JourneyStore.FilePath, "broken primary");
        File.WriteAllText(JourneyStore.BackupPath, "broken backup");
        JourneyStore.ReloadForTest();
        bool blocked = false;
        try { JourneyStore.Ensure(); } catch (Exception) { blocked = true; }
        Check(blocked && JourneyStore.Error != null && File.ReadAllText(JourneyStore.FilePath) == "broken primary", "two corrupt snapshots fail closed without wiping or reimporting");
        CleanFiles();
        JourneyStore.Ensure();
        var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(JourneyStore.FilePath));
        envelope.payload = envelope.payload.Replace("\"version\":" + JourneyStore.SchemaVersion, "\"version\":999");
        using (var hash = SHA256.Create()) envelope.checksum = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(envelope.payload)));
        File.WriteAllText(JourneyStore.FilePath, JsonUtility.ToJson(envelope));
        JourneyStore.ReloadForTest();
        blocked = false;
        try { JourneyStore.Ensure(); } catch (NotSupportedException) { blocked = true; }
        Check(blocked, "future schema cannot silently downgrade to an older backup");
        CleanFiles();
        JourneyStore.Ensure();
        File.Delete(JourneyStore.FilePath);
        File.Delete(JourneyStore.BackupPath);
        JourneyStore.ReloadForTest();
        blocked = false;
        try { JourneyStore.Ensure(); } catch (InvalidDataException) { blocked = true; }
        Check(blocked && File.Exists(JourneyStore.CampaignPath), "missing journey files never silently reimport stale registry progress");
        CleanFiles();
        JourneyStore.Ensure();
    }

    static void RewardTests()
    {
        foreach (string key in new[] { "sparks", "xp", "level", "statPoints", "mission:first-steps:paid", "missionId", "missionCount" })
        {
            Fresh();
            Save.StatPoints = 0;
            Save.Mission = Game.MissionIndex("first-steps");
            Save.BestFloor = 3; // real climb readiness is now enforced at payment
            Save.MissionCount = 1;
            Save.Flush();
            string snapshot = File.ReadAllText(JourneyStore.FilePath);
            JourneyStore.AfterMutationForTest = changed => { if (changed == key) throw new IOException("Interrupted at " + key); };
            Fails(() => Save.PayMission("first-steps", 30, 120), "commission interruption at " + key);
            Check(Save.Sparks == 0 && TotalXp() == 0 && Save.StatPoints == 0 && Save.MissionCount == 1 && !Save.MissionPaid("first-steps") && File.ReadAllText(JourneyStore.FilePath) == snapshot,
                "commission all-or-nothing at " + key);
            Reload();
        }
        Check(Save.PayMission("first-steps", 30, 120), "commission complete package commits");
        Reload();
        Check(Save.Sparks == 30 && TotalXp() == 120 && Save.Level == 3 && Save.StatPoints == 6 && Save.Mission == -1, "reload keeps all commission currency XP levels and points");
        Check(!Save.PayMission("first-steps", 30, 120) && Save.Sparks == 30 && TotalXp() == 120, "repeated commission claim is rejected");
        Fresh();
        JourneyStore.Transaction(() => Save.AwardXp(25));
        Reload();
        Check(Save.Level == 2 && Save.StatPoints == 3, "first level award never doubles lazily initialized stat points");
        Fresh();
        Check(Save.RecordFloorClear(0, 5), "Guardian milestone and entitlement commit together");
        var offer = Save.PendingGuardian;
        string choices = string.Join(",", offer.choices);
        Reload();
        Check(Save.TowerFloor == 5 && Save.PendingGuardian.key == offer.key && string.Join(",", Save.PendingGuardian.choices) == choices, "quit before cards preserves the originally offered choices");
        Check(!Save.RecordFloorClear(0, 5) && string.Join(",", Save.PendingGuardian.choices) == choices, "repeated clear cannot reroll an entitlement");
        Check(!Save.TakeGuardian(offer.key, 999), "unoffered boon is rejected");
        Save.CarriedSparks = 30;
        Save.Flush();
        Check(Save.RerollGuardian(offer.key), "paid Guardian reroll commits offer and cost together");
        choices = string.Join(",", Save.PendingGuardian.choices);
        Reload();
        Check(Save.CarriedSparks == 20 && Save.PendingGuardian.rerollCost == 20 && string.Join(",", Save.PendingGuardian.choices) == choices, "reroll choices cost and next price survive reload");
        string beforeReroll = File.ReadAllText(JourneyStore.FilePath);
        JourneyStore.BeforeReplaceForTest = target => { if (target == JourneyStore.FilePath) throw new IOException("Interrupted reroll"); };
        Fails(() => Save.RerollGuardian(offer.key), "reroll interrupted at disk commit");
        Reload();
        Check(Save.CarriedSparks == 20 && Save.PendingGuardian.rerollCost == 20 && File.ReadAllText(JourneyStore.FilePath) == beforeReroll, "failed reroll keeps the old offer and purse");
        int chosen = Save.PendingGuardian.choices[0];
        foreach (string key in new[] { "boon" + chosen, offer.key + ":paid", offer.key + ":offer" })
        {
            string snapshot = File.ReadAllText(JourneyStore.FilePath);
            JourneyStore.AfterMutationForTest = changed => { if (changed == key) throw new IOException("Interrupted boon claim"); };
            Fails(() => Save.TakeGuardian(offer.key, chosen), "Guardian claim interruption at " + key);
            Check(Save.Boon(chosen) == 0 && Save.PendingGuardian != null && !Save.GuardianPaid(0, 5) && File.ReadAllText(JourneyStore.FilePath) == snapshot, "Guardian claim rollback at " + key);
            Reload();
        }
        Check(Save.TakeGuardian(offer.key, chosen), "offered boon is consumed once");
        Reload();
        Check(Save.Boon(chosen) == 1 && Save.PendingGuardian == null && !Save.TakeGuardian(offer.key, chosen), "reload and duplicate confirm cannot award a second boon");
        Save.TowerFloor = 0;
        Save.GloomUnlocked = Save.GloomTier = 1;
        Save.Flush();
        Check(Save.RecordFloorClear(1, 5) && Save.PendingGuardian.tier == 1, "next Gloom climb has a distinct Guardian entitlement");
        Fresh();
        foreach (var u in Upgrades.All) Save.SetBoon(u.id, u.max);
        Save.Flush();
        Upgrades.Reset();
        Save.RecordFloorClear(0, 5);
        Reload();
        Check(Save.PendingGuardian == null && Save.GuardianPaid(0, 5) && Save.Sparks == 50, "exhausted boon pool settles a deterministic fallback once");
        Save.RecordFloorClear(0, 5);
        Check(Save.Sparks == 50, "exhausted-pool fallback cannot duplicate");
        Fresh();
        Check(Save.BeginRescue(0) && Save.StatPoints == 2, "rescue points and pending choice commit together");
        Reload();
        Check(Save.RescuePending(0) && Save.RescueChoice(0) == 0 && !Save.BeginRescue(0) && Save.StatPoints == 2, "interrupted rescue retains choice without duplicate points");
        Check(!Save.ResolveRescue(0, 2) && Save.ResolveRescue(0, 1), "rescue choice validates and commits");
        Reload();
        Check(Save.RescueChoice(0) == 2 && Save.RescuePending(0) && !Save.ResolveRescue(0, 0), "resolved rescue branch survives interruption and cannot change");
        Save.FinishRescue(0);
        Reload();
        Check(!Save.RescuePending(0) && Save.StatPoints == 2, "rescue presentation completion does not grant more points");
        Fresh();
        foreach (string key in new[] { "rescue:rhea:owned", "statPoints", "rescue:rhea:pending", "rescue:rhea:paid" })
        {
            JourneyStore.AfterMutationForTest = changed => { if (changed == key) throw new IOException("Interrupted rescue grant"); };
            Fails(() => Save.BeginRescue(0), "rescue interruption at " + key);
            Reload();
            Check(Save.RescuedMask == 0 && Save.StatPoints == 0 && !Save.RescuePending(0), "rescue grant rollback at " + key);
        }
        Fresh();
        JourneyStore.Transaction(() => Save.Sparks = 100);
        JourneyStore.AfterMutationForTest = key => { if (key == "perk0") throw new IOException("Interrupted forge purchase"); };
        Fails(() => Save.BuyPerk(0), "forge purchase interruption");
        Reload();
        Check(Save.Sparks == 100 && Save.PerkRank(0) == 0 && Save.BuyPerk(0), "forge purchase is atomic and retryable");
        Reload();
        Check(Save.Sparks == 75 && Save.PerkRank(0) == 1, "forge currency and rank migrate through the same snapshot");
        JourneyStore.Transaction(() => Save.OpenTowerChest(1, 31));
        bool bounded = false;
        try { Save.OpenTowerChest(1, 32); } catch (ArgumentOutOfRangeException) { bounded = true; }
        Reload();
        Check(Save.TowerChest(1, 31) && !Save.TowerChest(1, 0) && bounded, "bit-set boundary 31 is distinct and index 32 cannot alias zero");
        foreach (bool wilds in new[] { false, true })
        {
            Fresh();
            Save.BeginExpedition(wilds);
            JourneyStore.Transaction(() => Save.CarriedSparks = 11);
            Check(Save.SettleExpedition(false, true, 0), (wilds ? "Wilds" : "tower") + " death settles immediately");
            Reload();
            Check(Save.Sparks == (wilds ? 5 : 6) && Save.CarriedSparks == 0 && !Save.SettleExpedition(false, true, 0), "death reload preserves rounding and rejects duplicate settlement");
            int banked = Save.Sparks;
            Save.BankCarried();
            Check(Save.Sparks == banked && Save.Runs == (wilds ? 0 : 1), "quit after defeat cannot bank twice or change run count");
        }
        Fresh();
        Save.BeginExpedition(false);
        JourneyStore.Transaction(() => Save.CarriedSparks = 13);
        Reload();
        Save.BankCarried();
        Reload();
        Check(Save.Sparks == 13 && Save.CarriedSparks == 0 && Save.Runs == 0, "interrupted living expedition safely banks its persisted purse");
        Save.BeginExpedition(false);
        Save.SettleExpedition(true, false, 0);
        Reload();
        Check(Save.Wins == 1 && Save.Runs == 1 && Save.GloomUnlocked == 1 && Save.SettledGloomUnlocked && !Save.SettleExpedition(true, false, 0), "victory settlement and Gloom unlock commit once");
        var timings = new List<double>();
        for (int i = 0; i < 100; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            JourneyStore.Transaction(() => Save.CarriedSparks++);
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }
        timings.Sort();
        Debug.Log("SAVE TIMING 100 flushed transactions median=" + timings[49].ToString("F2") + "ms p95=" + timings[94].ToString("F2") + "ms max=" + timings[99].ToString("F2") + "ms");
    }

    static void Do(float wait, Action action) => steps.Add(new Step { wait = wait, act = action });
    static void SkipTalk()
    {
        var done = (Action)Get("dialogDone");
        Set("dialog", null);
        Set("dialogDone", null);
        G.Cam.EndCine();
        done?.Invoke();
    }
    static void Script()
    {
        Do(1f, () => Call("StartClimb"));
        Do(2f, () =>
        {
            Check(G.Floor == 5 && G.State == Game.GameState.Playing, "runtime enters the next Guardian floor");
            foreach (var enemy in new List<Enemy>(Enemy.All)) enemy.Vanish();
            ((IList)Get("timers")).Clear();
            Set("pending", 0);
            Call("FloorCleared");
            offerKey = Save.PendingGuardian.key;
            Check(Save.TowerFloor == 5 && Save.PendingGuardian != null && Get("cards") == null, "runtime clear commits entitlement before delayed presentation");
            ((IList)Get("timers")).Clear();
            Call("OnApplicationQuit");
            Reload();
            Call("EnterHub");
        });
        Do(1f, () => Set("introT", 0f));
        Do(0.3f, () =>
        {
            Check(G.State == Game.GameState.Cards && Save.PendingGuardian.key == offerKey, "return to town resumes pending Guardian cards");
            int id = Save.PendingGuardian.choices[0];
            Set("cardsT", 1f);
            Call("PickCard", 0);
            Call("PickCard", 0);
            Check(Save.Boon(id) == 1 && Save.PendingGuardian == null && G.State == Game.GameState.Hub, "runtime double-confirm grants one boon and resumes town");
            Save.Mission = Game.MissionIndex("first-steps");
            Save.Flush();
            int xp = TotalXp();
            Call("TurnInMission");
            thankCallback = (Action)Get("dialogDone");
            Check(Save.Sparks == 30 && TotalXp() == xp + 50 && G.State == Game.GameState.Story, "runtime commission XP is committed before thanks dialogue");
            int paidXp = TotalXp();
            Reload();
            thankCallback?.Invoke();
            thankCallback?.Invoke();
            Check(TotalXp() == paidXp && Save.Sparks == 30, "replayed thanks callback cannot duplicate rewards");
            Set("dialog", null);
            Set("dialogDone", null);
            Save.BeginRescue(0);
            Call("TickHub");
            SkipTalk();
            Check(G.Asking, "town follow-up asks an interrupted rescue choice");
            G.Answer(1);
            Check(Save.RescueChoice(0) == 2, "runtime rescue answer is durable before branch dialogue");
            Reload();
            SkipTalk();
            Check(!Save.RescuePending(0), "runtime resumed rescue finishes its presentation receipt");
            Save.BestFloor = 9;
            Save.Flush();
            Call("OpenHubPanel", 1);
        });
        Do(0.5f, () =>
        {
            Directory.CreateDirectory("Logs/save-safety");
            ScreenCapture.CaptureScreenshot("Logs/save-safety/rescue-recovery-gate.png");
        });
        Do(0.3f, () => Call("ReturnForRescue", 1));
        Do(1.2f, () =>
        {
            Check(G.Floor == 9 && !G.InTown && !(bool)Get("waveActive") && Save.TowerFloor == 5, "optional rescue recovery leaves active-climb progress unchanged");
            foreach (object spot in (IList)Get("spots"))
            {
                if ((string)spot.GetType().GetField("name").GetValue(spot) != "Treasure") continue;
                var open = (Action)spot.GetType().GetField("act").GetValue(spot);
                int purse = Save.CarriedSparks;
                open();
                open();
                Reload();
                Check(Save.CarriedSparks == purse + 24, "runtime chest callback and repeat claim pay exactly once");
                break;
            }
            JourneyStore.Transaction(() => Save.CarriedSparks = 11);
            int banked = Save.Sparks, runs = Save.Runs;
            G.Player.hp = 0;
            G.OnPlayerDied();
            Check(!G.Roaming, "defeat delay cannot reopen roaming or interaction");
            Call("TickFloor");
            Check(Get("nearSpot") == null, "defeated player cannot select a floor reward or exit");
            G.Player.Heal(1);
            G.Player.stats.lifestealEvery = 1;
            G.Player.OnKill();
            G.Collect(Pickup.Kind.Heart, 1);
            G.Collect(Pickup.Kind.Coin, 50);
            int xpBeforeDeathGrowth = TotalXp();
            G.GainXp(1000);
            Check(G.Player.Dead && !G.Roaming && G.DefeatPending && Save.CarriedSparks == 0,
                "post-defeat heart lifesteal level-up and coin events cannot reopen a settled expedition");
            Check(TotalXp() == xpBeforeDeathGrowth + 1000, "posthumous XP persists without reviving the defeated player");
            Check(Save.Sparks == banked + 6 && Save.CarriedSparks == 0 && Save.Runs == runs + 1, "runtime death commits loss before its delayed results");
            Call("OnApplicationQuit");
            Reload();
            Call("EndRun", false, true);
            Call("EndRun", false, true);
            Check(Save.Sparks == banked + 6 && Save.Runs == runs + 1 && (int)Get("sparksLost") == 5, "quit during defeat and repeated results cannot change settlement");
        });
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || G == null) return;
        try
        {
            if (steps == null)
            {
                passed = SessionState.GetInt(Session + "Passed", 0);
                Application.runInBackground = true;
                steps = new List<Step>();
                Script();
                since = EditorApplication.timeSinceStartup;
                deadline = since + 120;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Runtime deadline exceeded");
            if (JourneyStore.Error != null) throw new Exception(JourneyStore.Error);
            if (G.Player != null)
            {
                G.Player.hp = G.Player.maxHp;
                typeof(PlayerController).GetField("invuln", Any).SetValue(G.Player, 1f);
            }
            if (EditorApplication.timeSinceStartup - since < steps[at].wait) return;
            steps[at].act();
            since = EditorApplication.timeSinceStartup;
            if (++at < steps.Count) return;
            Debug.Log("SAVE DONE " + passed + " checks passed");
            SessionState.SetBool(Session, false);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("SAVE FAILED runtime step " + at + " " + e);
            SessionState.SetBool(Session, false);
            EditorApplication.Exit(1);
        }
    }
}
