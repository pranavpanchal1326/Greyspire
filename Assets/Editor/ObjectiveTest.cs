using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ObjectiveTest
{
    const string Session = "GreyspireObjectiveTest";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static int passed, phase;
    static double readyAt;
    static readonly List<string> errors = new List<string>();
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Get(object host, string field) => host.GetType().GetField(field, Any).GetValue(host);
    static void Set(object host, string field, object value) => host.GetType().GetField(field, Any).SetValue(host, value);
    static object Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Any).Invoke(host, args);
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        passed++; Debug.Log("OBJECTIVE PASS " + message);
    }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
    static void Reload() { JourneyStore.ReloadForTest(); JourneyStore.Ensure(); Upgrades.Reset(); }
    static void Fresh(int level = 1) { Save.Wipe(); Upgrades.Reset(); Save.Level = level; Save.StatPoints = 0; Save.Flush(); }
    static void Fails(Action action, Type type, string message)
    {
        bool failed = false;
        try { action(); } catch (Exception e) { failed = type.IsInstanceOfType(e.GetBaseException()); }
        finally { JourneyStore.AfterMutationForTest = JourneyStore.BeforeReplaceForTest = null; }
        Check(failed, message);
    }
    [Serializable] class Entry { public string key, value; }
    [Serializable] class Snapshot { public int version; public string campaign; public List<Entry> entries; }
    [Serializable] class Envelope { public string payload, checksum; }
    static string Hash(string text) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text))); }
    static string WriteLegacy(int version = 1, int objectiveVersion = 0)
    {
        string campaign = "legacy-objective-proof";
        var entries = new Dictionary<string, string>
        {
            { "level", "17" }, { "xp", "41" }, { "statPoints", "27" }, { "sparks", "250" }, { "stat0", "14" },
            { "regions", ((1 << 0) | (1 << 15)).ToString() }, { "chests", ((1 << 1) | (1 << 31)).ToString() },
            { "rescued", "3" }, { "rescueAsk0", "2" }, { "rescuePending1", "1" },
            { "hero1", "1" }, { "heroPick", "1" }, { "missionId", "garrick" }, { "missionCount", "0" },
            { "mission:wolves-road:paid", "1" }, { "foreign-extension", "keep-me" }, { "objectiveVersion", objectiveVersion.ToString() }
        };
        string payload = JsonUtility.ToJson(new Snapshot { version = version, campaign = campaign, entries = entries.Select(p => new Entry { key = p.Key, value = p.Value }).ToList() });
        string json = JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) });
        Directory.CreateDirectory(JourneyStore.DirectoryPath);
        File.WriteAllText(JourneyStore.FilePath, json); File.WriteAllText(JourneyStore.BackupPath, json); File.WriteAllText(JourneyStore.CampaignPath, campaign);
        JourneyStore.ReloadForTest(); return json;
    }
    static Array Regions => (Array)typeof(Game).GetProperty("RegionDefinitions", Any).GetValue(null);
    static Array Missions => (Array)typeof(Game).GetField("Missions", Any).GetValue(null);
    static void Migration()
    {
        Check(Environment.GetCommandLineArgs().Contains("-greyspire-save-dir"), "dedicated save directory required");
        string before = WriteLegacy();
        JourneyStore.AfterMutationForTest = key => { if (key == "region:greenreach:owned") throw new IOException("injected migration interruption"); };
        Fails(JourneyStore.Ensure, typeof(IOException), "identity migration reports interruption");
        Check(File.ReadAllText(JourneyStore.FilePath) == before && File.ReadAllText(JourneyStore.BackupPath) == before, "failed migration retains both exact v1 snapshots");
        string[] archives = Directory.GetFiles(JourneyStore.DirectoryPath, "journey.json.v1.*.pre-objectives");
        Check(archives.Length > 0 && archives.Any(file => File.ReadAllText(file) == before), "exact pre-upgrade journey is archived independently of rotating recovery backups");
        Reload();
        var snapshot = JsonUtility.FromJson<Snapshot>(JsonUtility.FromJson<Envelope>(File.ReadAllText(JourneyStore.FilePath)).payload);
        Check(snapshot.version == 2 && Save.ObjectivesMigrated && JourneyStore.Campaign == "legacy-objective-proof", "v1 upgrades atomically to v2 without changing the campaign");
        Check(Save.Level == 17 && Save.Xp == 41 && Save.StatPoints == 27 && Save.Sparks == 250 && Save.Stat(0) == 14, "migration preserves every existing growth and currency value");
        Check(JourneyStore.GetInt("regions") == ((1 << 0) | (1 << 15)) && JourneyStore.GetString("foreign-extension") == "keep-me",
            "unknown reserved bits and extension records remain retained");
        Check(Save.RegionMask == 1 && Save.ChestMask == 2 && Save.RescuedMask == 3, "known legacy discovery treasure and rescue flags map exactly");
        Check(Save.RescueChoice(0) == 2 && Save.Consequence("rescue:rhea:guardian-intel") && Save.Consequence("rescue:rhea:home")
            && Save.RescuePending(1) && !Save.BeginRescue(0), "migration retains chosen consequences and unfinished rescue without paying twice");
        Check(Heroes.Selected == 1 && Heroes.Owned(1) && Save.Mission == Game.MissionIndex("garrick") && Save.Consequence("commission:wolves-road:fulfilled"),
            "hero selection ownership commission identity and fulfilled consequence survive migration");
        string committed = File.ReadAllText(JourneyStore.FilePath); Reload();
        Check(File.ReadAllText(JourneyStore.FilePath) == committed && Save.StatPoints == 27, "reload cannot repeat migration or mint rewards");
        File.WriteAllText(JourneyStore.FilePath, "damaged"); Reload();
        Check(Save.RescueChoice(0) == 2 && Save.RegionMask == 1, "v2 backup recovery keeps stable objective state");
        object meadow = Regions.GetValue(0), vale = Regions.GetValue(6), firstMission = Missions.GetValue(0), chiefMission = Missions.GetValue(6);
        Lost rhea = Story.Rescues[0], dorn = Story.Rescues[1]; HeroDef pip = Heroes.All[0], bram = Heroes.All[1];
        try
        {
            Regions.SetValue(vale, 0); Regions.SetValue(meadow, 6); Missions.SetValue(chiefMission, 0); Missions.SetValue(firstMission, 6);
            Story.Rescues[0] = dorn; Story.Rescues[1] = rhea; Heroes.All[0] = bram; Heroes.All[1] = pip;
            WriteLegacy(); Reload();
            Check(Save.RegionMask == (1 << 6) && Save.ChestMask == (1 << 13), "frozen migration ignores the newly reordered region and chest indexes");
            Check(Save.Mission == 0 && (int)chiefMission.GetType().GetProperty("region").GetValue(chiefMission) == 0,
                "active commission and its region reference follow IDs across reorder");
            Check(Save.RescueChoice(1) == 2 && Save.RescuePending(0) && !Save.BeginRescue(1), "rescue choice pending state and receipt follow the person across reorder");
            Check(Heroes.Selected == 0 && Heroes.Owned(0) && Heroes.Owned(1), "hero ownership and selected character follow identity across reorder");
            Check(Story.Saw(1).Any(l => l.text.Contains("Grol")) && Story.GoHome(0).Any(l => l.text.Contains("knees")), "rescue branch writing stays with its person after reorder");
        }
        finally
        {
            Regions.SetValue(meadow, 0); Regions.SetValue(vale, 6); Missions.SetValue(firstMission, 0); Missions.SetValue(chiefMission, 6);
            Story.Rescues[0] = rhea; Story.Rescues[1] = dorn; Heroes.All[0] = pip; Heroes.All[1] = bram;
        }
        Array oldRegions = Regions;
        Array expanded = Array.CreateInstance(oldRegions.GetType().GetElementType(), oldRegions.Length + 1);
        object inserted = oldRegions.GetValue(0).GetType().GetMethod("MemberwiseClone", Any).Invoke(oldRegions.GetValue(0), null);
        Set(inserted, "id", "inserted-region"); Set(inserted, "name", "Inserted Region"); expanded.SetValue(inserted, 0);
        for (int i = 0; i < oldRegions.Length; i++) expanded.SetValue(oldRegions.GetValue(i), i + 1);
        try
        {
            typeof(Game).GetField("regionDefinitions", Any).SetValue(null, expanded);
            Check(Save.RegionMask == 2 && Save.ChestMask == 8 && (Save.RegionMask & 1) == 0, "inserting content preserves old ownership without granting the new region");
        }
        finally { typeof(Game).GetField("regionDefinitions", Any).SetValue(null, oldRegions); }
        WriteLegacy(2, 2);
        string future = File.ReadAllText(JourneyStore.FilePath);
        Fails(JourneyStore.Ensure, typeof(NotSupportedException), "future objective revision refuses silent reinterpretation");
        Check(File.ReadAllText(JourneyStore.FilePath) == future, "unsupported objective data remains untouched");
        WriteLegacy(2, 0); string incomplete = File.ReadAllText(JourneyStore.FilePath);
        Fails(JourneyStore.Ensure, typeof(NotSupportedException), "incomplete v2 identity data refuses a destructive legacy reinterpretation");
        Check(File.ReadAllText(JourneyStore.FilePath) == incomplete, "incomplete v2 data is retained without rewriting progress");
        WriteLegacy(); Reload(); Fresh();
        Check(Save.RegionMask == 0 && Save.ChestMask == 0 && Save.RescuedMask == 0 && !Save.Consequence("rescue:rhea:guardian-intel") && Heroes.Selected == 0,
            "new journey clears migrated identities consequences and selection without importing legacy again");
    }
    static QuestEvent Packet(string region = "greenreach", string target = "", ObjectiveCause cause = ObjectiveCause.Kill, bool bandit = true, Enemy.Kind kind = Enemy.Kind.Minion)
        => new QuestEvent(Save.ActiveMissionId, Save.MissionAttempt, Save.ExpeditionId, Guid.NewGuid().ToString("N"), region, target, cause, bandit, kind);
    static void Quests()
    {
        Fresh(); Check(!Save.AcceptCommission("garrick") && !Save.AcceptCommission("missing-id"), "rank and missing-content checks prevent invalid acceptance");
        Check(Save.AcceptCommission("wolves-road") && Save.MissionStage("wolves-road") == CommissionStage.Active, "commission transitions from available to active atomically");
        Check(!Save.AcceptCommission("slime-season") && !Save.PayMission("wolves-road", 30, 40), "second acceptance and premature payment are rejected");
        Save.BeginExpedition(true);
        Check(!Save.RecordObjective(Packet("whispering-woods")) && !Save.RecordObjective(Packet(bandit: false)), "origin and bandit classification reject ineligible kills");
        var first = Packet(); Check(Save.RecordObjective(first) && Save.MissionCount == 1 && !Save.RecordObjective(first), "one kill event earns one objective increment");
        Reload(); Check(!Save.RecordObjective(first) && Save.MissionCount == 1, "kill deduplication survives reload");
        for (int i = 0; i < 4; i++) Check(Save.RecordObjective(Packet()), "eligible kill advances the current attempt " + i);
        Check(Save.MissionStage("wolves-road") == CommissionStage.Ready && !Save.RecordObjective(Packet()), "ready commissions freeze their count at the goal");
        Check(Save.PayMission("wolves-road", 30, 40) && Save.Sparks == 30 && Save.Consequence("commission:wolves-road:fulfilled"), "ready payment commits currency XP receipt and consequence together");
        Check(Save.MissionStage("wolves-road") == CommissionStage.Paid && !Save.PayMission("wolves-road", 30, 40) && !Save.AcceptCommission("wolves-road"), "paid cannot be paid or accepted again");
        Fresh(); Save.AcceptCommission("wolves-road"); Save.BeginExpedition(true); var stale = Packet(); string attempt = Save.MissionAttempt;
        Check(Save.AbandonCommission("wolves-road", attempt) && Save.AcceptCommission("wolves-road") && Save.MissionAttempt != attempt, "abandon and reaccept establish a new attempt");
        Check(!Save.RecordObjective(stale) && Save.MissionCount == 0, "old-attempt kill callbacks cannot advance a new commission");
        var oldExpedition = Packet(); Save.BeginExpedition(true);
        Check(!Save.RecordObjective(oldExpedition) && !Save.AbandonCommission("wolves-road", attempt), "old expedition and old abandon callbacks are fenced");
        foreach (string key in new[] { "mission:wolves-road:event:1", "mission:wolves-road:source:1", "mission:wolves-road:count", "disk" })
        {
            Fresh(); Save.AcceptCommission("wolves-road"); Save.BeginExpedition(true);
            var e = Packet(); string snapshot = File.ReadAllText(JourneyStore.FilePath);
            if (key == "disk") JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected objective write"); };
            else JourneyStore.AfterMutationForTest = changed => { if (changed == key) throw new IOException("injected objective mutation"); };
            Fails(() => Save.RecordObjective(e), typeof(IOException), "objective interruption reports failure at " + key);
            Check(Save.MissionCount == 0 && File.ReadAllText(JourneyStore.FilePath) == snapshot, "event receipt and count roll back together at " + key);
            Reload(); Check(Save.RecordObjective(e) && !Save.RecordObjective(e), "failed event can be retried exactly once at " + key);
            Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt); Save.AcceptCommission("wolves-road");
        }
        Fresh(4); Save.AcceptCommission("garrick"); Save.BeginExpedition(true);
        Check(!Save.RecordObjective(Packet("goldleaf", "chief:hobb", kind: Enemy.Kind.Brute)), "another chief in the same region cannot substitute for Garrick");
        Check(!Save.RecordObjective(Packet("frostfall", "chief:garrick", kind: Enemy.Kind.Brute)), "named chief from the wrong origin cannot substitute");
        Check(Save.RecordObjective(Packet("goldleaf", "chief:garrick", kind: Enemy.Kind.Brute)), "exact named-chief identity completes its commission");
        Fresh(); Save.AcceptCommission("vane-signet"); Save.BeginExpedition(true);
        Check(!Save.RecordObjective(Packet("whispering-woods", "item:vane-signet")) && !Save.RecordObjective(Packet("whispering-woods", "item:other", ObjectiveCause.Recover)), "recovery requires both the item identity and correct event type");
        Check(Save.RecordObjective(Packet("whispering-woods", "item:vane-signet", ObjectiveCause.Recover)) && Save.MissionStage("vane-signet") == CommissionStage.Ready, "recovery item commits readiness before presentation");
        Fresh(); Save.BestFloor = 3; Save.Flush(); Save.AcceptCommission("first-steps");
        Check(Save.MissionStage("first-steps") == CommissionStage.Ready, "prior floor progress counts when accepting a climb commission");
        Fresh(); Save.RegionMask = 15; Save.Flush(); Save.AcceptCommission("lay-land");
        Check(Save.MissionStage("lay-land") == CommissionStage.Ready, "prior stable discoveries count for exploration");
        Fresh(); JourneyStore.SetString("missionId", "removed-commission"); JourneyStore.SetInt("mission:removed-commission:count", 7); Save.Flush();
        Check(!Save.AcceptCommission("wolves-road") && !Save.PayMission("removed-commission", 999, 999) && Save.ActiveMissionId == "removed-commission" && Save.MissionCount == 7,
            "unavailable saved commission is retained and cannot silently switch or pay");
    }
    static void Rescues()
    {
        foreach (int branch in new[] { 0, 1 })
        {
            Fresh(); Check(Save.BeginRescue("rhea") && Save.StatPoints == 2, "rescue begins with one receipt and pending scene branch " + branch);
            string token = Save.RescueToken("rhea");
            Check(!Save.FinishRescue("rhea", token), "rescue cannot finish before its required choice branch " + branch);
            Reload(); Check(Save.RescuePending("rhea") && Save.RescueToken("rhea") == token && !Save.BeginRescue("rhea"), "interrupted rescue keeps its identity token and two points branch " + branch);
            string outcome = "rescue:rhea:" + (branch == 0 ? "safe-return" : "guardian-intel");
            JourneyStore.AfterMutationForTest = key => { if (key == "consequence:" + outcome) throw new IOException("injected rescue branch"); };
            Fails(() => Save.ResolveRescue("rhea", branch, token), typeof(IOException), "branch interruption reports failure " + branch);
            Check(Save.RescueChoice("rhea") == 0 && !Save.Consequence(outcome), "choice and consequence roll back together " + branch); Reload();
            Check(Save.ResolveRescue("rhea", branch, token) && Save.Consequence(outcome) && !Save.ResolveRescue("rhea", 1 - branch, token), "first committed rescue choice is immutable " + branch);
            Reload(); Check(Save.RescueChoice("rhea") == branch + 1 && Save.Consequence(outcome), "chosen branch and consequence survive reload " + branch);
            Check(Save.FinishRescue("rhea", token) && !Save.FinishRescue("rhea", token) && Save.Consequence("rescue:rhea:home") && Save.StatPoints == 2, "return callback commits once without extra reward " + branch);
            string words = string.Join(" ", Story.RescueTown(Story.RescueIndex("rhea")).Select(l => l.text));
            Check(words.Contains(branch == 0 ? "sent me straight home" : "You asked what I saw"), "later town dialogue reflects the actual branch " + branch);
            Fresh(); Save.BeginRescue("rhea");
            Check(!Save.ResolveRescue("rhea", branch, token) && !Save.FinishRescue("rhea", token), "old-campaign rescue callbacks cannot change the new rescue " + branch);
        }
    }
    static void WorldReceipts()
    {
        foreach (string key in new[] { "region:whispering-woods:owned", "carried", "xp", "disk" })
        {
            Fresh(); Save.BeginExpedition(true); string exp = Save.ExpeditionId, snapshot = File.ReadAllText(JourneyStore.FilePath);
            if (key == "disk") JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected discovery commit"); };
            else JourneyStore.AfterMutationForTest = changed => { if (changed == key) throw new IOException("injected discovery mutation"); };
            Fails(() => Save.DiscoverRegion("whispering-woods", exp, true), typeof(IOException), "discovery interruption reports failure at " + key);
            Check(Save.RegionMask == 0 && Save.CarriedSparks == 0 && Save.Xp == 0 && File.ReadAllText(JourneyStore.FilePath) == snapshot, "discovery receipt currency and XP roll back together at " + key);
            Reload(); Check(Save.DiscoverRegion("whispering-woods", exp, true) && !Save.DiscoverRegion("whispering-woods", exp, true) && Save.CarriedSparks == 14 && Save.Xp == 5,
                "interrupted discovery retries once with its full reward at " + key);
        }
        Fresh(); Save.BeginExpedition(true); string oldExp = Save.ExpeditionId;
        Check(Save.DiscoverRegion("greenreach", oldExp, false) && Save.CarriedSparks == 0 && Save.Xp == 0, "gate arrival discovery is committed without inventing its former skipped reward");
        JourneyStore.BeforeReplaceForTest = path => { if (path == JourneyStore.FilePath) throw new IOException("injected treasure commit"); };
        Fails(() => Save.OpenWildChest("greenreach", 0, oldExp), typeof(IOException), "treasure commit reports interruption");
        Check(!Save.WildChestOpened("greenreach", 0) && Save.CarriedSparks == 0, "failed treasure retains both chest and purse"); Reload();
        Check(Save.OpenWildChest("greenreach", 0, oldExp) && Save.CarriedSparks == 12 && !Save.OpenWildChest("greenreach", 0, oldExp), "one stable treasure slot pays once"); Reload();
        Check(Save.WildChestOpened("greenreach", 0) && !Save.OpenWildChest("greenreach", 0, oldExp), "treasure receipt survives reload");
        Save.BeginExpedition(true);
        Check(!Save.OpenWildChest("greenreach", 1, oldExp) && !Save.DiscoverRegion("mirror-lake", oldExp, true), "old expedition callbacks cannot claim new treasure or discovery");
        Save.BankCarried();
        Check(!Save.OpenWildChest("greenreach", 1, Save.ExpeditionId), "settled expeditions cannot reopen their purse through a chest callback");
    }
    static void Runtime()
    {
        Fresh(4); Save.SeenIntro = true; Save.AcceptCommission("garrick"); Save.BeginExpedition(true); Save.Flush();
        G.enabled = P.enabled = false; Set(G, "<InWilds>k__BackingField", true); Set(G, "<DefeatPending>k__BackingField", false);
        Call(G, "BuildWilds"); Call(G, "SetState", Game.GameState.Playing); Set(G, "fade", 0f); Set(G, "fadeDir", 0f); Set(G, "introT", 0f);
        foreach (var e in Enemy.All.ToArray()) { e.enabled = false; }
        var enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        Check(enemies.Length > 100 && enemies.All(e => e.OriginRegionId.Length > 0 && e.ObjectiveEvent.expedition == Save.ExpeditionId), "real Wilds spawner stamps every camp enemy with origin and expedition");
        var wanted = enemies.Where(e => e.ObjectiveId == "chief:garrick").ToArray();
        Check(wanted.Length == 1 && wanted[0].IsBandit && wanted[0].kind == Enemy.Kind.Brute && wanted[0].OriginRegionId == "goldleaf", "real camp spawner binds exactly the requested named chief");
        wanted[0].title = "Renamed display only"; Set(G, "regionNow", Game.RegionIndex("whispering-woods"));
        wanted[0].hp = 1; wanted[0].ReceiveHit(new DamageRequest(10, wanted[0].transform.position + Vector3.back, 0, source: P));
        Check(Save.MissionStage("garrick") == CommissionStage.Ready && Save.MissionCount == 1, "renamed chief counts across the player's region boundary");
        int level = Save.Level, xp = Save.Xp, daily = Save.DailyCount;
        G.OnEnemyKilled(wanted[0]); Check(Save.MissionCount == 1 && Save.Level == level && Save.Xp == xp && Save.DailyCount == daily, "runtime repeated chief callback cannot duplicate objective XP or daily progress");
        Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt); Save.AcceptCommission("wolves-road");
        var source = Enemy.Create(Enemy.Kind.Minion, new Vector3(-85f, 0.1f, -55f), 0, bandit: true); source.BindObjective("greenreach"); source.enabled = false;
        source.hp = 1; P.weak = 1f; P.stats = new PlayerStats(); P.Revive(source.transform.position + Vector3.back * 2f, true);
        var hit = source.ReceiveHit(new DamageRequest(10, P.transform.position, 0, source: P));
        Check(hit.outcome == HitOutcome.Killed && Save.MissionCount == 1, "actual lethal contact credits the target's home region after being chased across the boundary");
        var outsider = Enemy.Create(Enemy.Kind.Minion, P.transform.position + Vector3.forward * 2f, 0, bandit: true); outsider.BindObjective("whispering-woods"); outsider.enabled = false;
        Set(G, "regionNow", Game.RegionIndex("greenreach")); G.OnEnemyKilled(outsider);
        Check(Save.MissionCount == 1, "standing in the mission region cannot make an outsider count");
        Fails(() => outsider.BindObjective("greenreach"), typeof(InvalidOperationException), "enemy birthplace cannot be rebound to manufacture eligibility");
        Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt); Save.AcceptCommission("slime-season");
        var parent = Enemy.Create(Enemy.Kind.BigSlime, P.transform.position + Vector3.right * 8f, 0); parent.BindObjective("mirror-lake"); parent.enabled = false;
        Set(G, "wildFloor", 1); // Luring the parent cannot retune its split children.
        var before = new HashSet<Enemy>(UnityEngine.Object.FindObjectsByType<Enemy>()); parent.hp = 1;
        parent.ReceiveHit(new DamageRequest(10, P.transform.position, 0, source: P));
        var children = Enemy.All.Where(e => !before.Contains(e)).ToArray();
        Check(children.Length == 2 && children.All(e => e.OriginRegionId == "mirror-lake" && e.ObjectiveEvent.attempt == parent.ObjectiveEvent.attempt), "split children inherit objective origin and attempt rather than the player's location");
        Check(children.All(e => e.tier == parent.tier), "split-child danger and XP retain the parent's birth tier across a boundary");
        foreach (var child in children) { child.enabled = false; G.OnEnemyKilled(child); }
        Check(Save.MissionCount == 3, "parent and distinct split-child kills count as three real objective events");
        Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt); Save.AcceptCommission("vane-signet");
        object recovery = Missions.Cast<object>().First(m => (string)Get(m, "id") == "vane-signet");
        Call(G, "Strongbox", new Vector2(1000, 1000), recovery);
        var spots = (IList)Get(G, "spots"); object box = spots[spots.Count - 1]; Action oldBox = (Action)Get(box, "act");
        Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt); Save.AcceptCommission("wolves-road"); oldBox();
        Check(Save.MissionCount == 0, "stale real strongbox cannot complete a different active commission");
        Save.AbandonCommission(Save.ActiveMissionId, Save.MissionAttempt); Save.AcceptCommission("vane-signet"); oldBox();
        Check(Save.MissionCount == 0, "stale real strongbox cannot complete a reaccepted attempt");
        Call(G, "Strongbox", new Vector2(1005, 1000), recovery); object newBox = spots[spots.Count - 1]; Action claim = (Action)Get(newBox, "act"); claim(); claim(); Reload();
        Check(Save.MissionCount == 1 && Save.MissionStage("vane-signet") == CommissionStage.Ready, "fresh real strongbox commits once and remains ready after reload");
        foreach (var e in UnityEngine.Object.FindObjectsByType<Enemy>()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        Enemy.All.Clear();
        var oldCampaignEnemy = Enemy.Create(Enemy.Kind.Minion, P.transform.position + Vector3.forward * 3f, 0); oldCampaignEnemy.enabled = false;
        Fresh(); G.OnEnemyKilled(oldCampaignEnemy);
        Check(Save.Level == 1 && Save.Xp == 0 && Save.DailyCount == 0, "stale old-campaign kill callback cannot grant XP or daily progress to a new journey");
        UnityEngine.Object.DestroyImmediate(oldCampaignEnemy.gameObject);
        Fresh(); Save.SeenIntro = true; Save.BeginRescue("rhea"); string token = Save.RescueToken("rhea");
        Set(G, "<InTown>k__BackingField", true); Set(G, "<InWilds>k__BackingField", false);
        Call(G, "ResumeRescue", Story.RescueIndex("rhea"), (Action)(() => {}));
        Action done = (Action)Get(G, "dialogDone"); done();
        Check(G.Asking, "runtime interrupted rescue restores its required choice");
        G.Answer(1); Check(Save.RescueChoice("rhea") == 2 && Save.Consequence("rescue:rhea:guardian-intel"), "runtime answer persists the information consequence before dialogue");
        Action finish = (Action)Get(G, "dialogDone"); finish(); finish();
        Check(!Save.RescuePending("rhea") && Save.StatPoints == 2 && Save.Consequence("rescue:rhea:home"), "runtime duplicated branch callback returns once with no duplicate points");
        Fresh(); Save.BeginRescue("rhea"); finish();
        Check(Save.RescuePending("rhea") && Save.RescueChoice("rhea") == 0, "stale runtime story callback cannot finish a new campaign's rescue");
        Save.ResolveRescue("rhea", 1, Save.RescueToken("rhea")); Save.FinishRescue("rhea", Save.RescueToken("rhea"));
        Directory.CreateDirectory("Reports");
        var manifest = new StringBuilder("kind,id,region,target,description\n");
        foreach (object mission in Missions) manifest.AppendLine("commission," + Get(mission, "id") + "," + Get(mission, "regionId") + "," + Get(mission, "targetId") + "," + Get(mission, "job"));
        for (int i = 0; i < Game.RegionDefinitionCount; i++) manifest.AppendLine("region," + Game.RegionId(i) + ",,,discovery and two treasure slots");
        foreach (Lost rescue in Story.Rescues) manifest.AppendLine("rescue," + rescue.id + ",,,floor " + rescue.floor);
        foreach (HeroDef hero in Heroes.All) manifest.AppendLine("hero," + hero.id + ",,," + hero.name);
        File.WriteAllText("Reports/objective-identities.csv", manifest.ToString());
        Set(G, "clockHour", 12f); Call(G, "BuildHub"); Call(G, "SnapResidents");
        P.NewRun(); P.Revive(new Vector3(0, 0.1f, -5), true); Call(G, "SetState", Game.GameState.Hub);
        object rheaSpot = ((IList)Get(G, "spots")).Cast<object>().First(s => (string)Get(s, "name") == "Rhea Vane");
        ((Action)Get(rheaSpot, "act"))();
        Check(G.State == Game.GameState.Story && ((Line[])Get(G, "dialog")).Any(l => l.text.Contains("You asked what I saw")), "actual rescued town NPC offers the persisted consequence in conversation");
        Set(G, "dialogIndex", Story.RescueTown(0).Length - 1); Set(G, "dialogChars", 999f); Set(G, "fade", 0f);
        G.gameObject.AddComponent<ObjectiveTestOverlay>();
    }
    public static void DrawUi() { if (G != null) Call(G, "OnGUI"); }
    static ObjectiveTest() { if (SessionState.GetBool(Session, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; } }
    public static void Run()
    {
        try
        {
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            if (File.Exists("Logs/objective-consequence.png")) File.Delete("Logs/objective-consequence.png");
            Migration(); Quests(); Rescues(); WorldReceipts(); Fresh(); Save.SeenIntro = true; Save.Flush();
            SessionState.SetInt(Session + "Passed", passed); SessionState.SetBool(Session, true);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogError("OBJECTIVE FAILED " + e); EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup + 2;
        if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (phase == 0) { passed = SessionState.GetInt(Session + "Passed", 0); Runtime(); phase = 1; readyAt = EditorApplication.timeSinceStartup + 2; return; }
            if (phase == 1) { ScreenCapture.CaptureScreenshot("Logs/objective-consequence.png"); phase = 2; readyAt = EditorApplication.timeSinceStartup + 2; return; }
            Check(errors.Count == 0, "runtime objective and consequence scenarios contain no unexpected errors" + (errors.Count > 0 ? ": " + errors[0] : ""));
            Check(JourneyStore.Error == null, "objective scenario leaves the journey store healthy");
            if (!Application.isBatchMode) Check(File.Exists("Logs/objective-consequence.png"), "later consequence dialogue renders successfully");
            Debug.Log("OBJECTIVE DONE " + passed + " checks passed"); SessionState.SetBool(Session, false); EditorApplication.update -= Tick; EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("OBJECTIVE FAILED " + e); SessionState.SetBool(Session, false); EditorApplication.update -= Tick; EditorApplication.Exit(1); }
    }
}
public class ObjectiveTestOverlay : MonoBehaviour { void OnGUI() => ObjectiveTest.DrawUi(); }
