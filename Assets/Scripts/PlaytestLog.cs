using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Opt-in local append-only evidence from authoritative decisions. No HP/list polling.
public sealed class PlaytestLog : MonoBehaviour
{
    public const int BatchSize = 64;
    public const long MaximumBytes = 8 * 1024 * 1024;
    public static PlaytestLog Active { get; private set; }
    public string FilePath { get; private set; }
    public string Failure { get; private set; }
    public int Kills { get; private set; }
    public int HitsTaken { get; private set; }
    public int Falls { get; private set; }
    public int Dropped { get; private set; }
    public int Pending => pending.Count;
    readonly List<string> pending = new List<string>(BatchSize);
    readonly string session = Guid.NewGuid().ToString("N");
    long sequence, bytes;
    float start, flushAt;
    bool ended;
    public const string Header = "seconds,event,floor,value,note,event_id,build,content,save_schema,seed_policy,root_seed,campaign,expedition,scope,source,target,action,outcome,simulation_seconds";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void FromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string name = null, directory = null;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-playtest") name = args[i + 1];
            if (args[i] == "-playtest-dir") directory = args[i + 1];
        }
        if (name != null) Begin(name, directory);
    }
    public static PlaytestLog Begin(string testerName, string directory = null)
    {
        if (Active != null) return Active;
        var go = new GameObject("Playtest Log"); DontDestroyOnLoad(go);
        var log = Active = go.AddComponent<PlaytestLog>(); log.start = Time.realtimeSinceStartup;
        try
        {
            directory = directory ?? Path.Combine(Application.persistentDataPath, "playtest");
            Directory.CreateDirectory(directory);
            string safe = string.Join("_", (string.IsNullOrWhiteSpace(testerName) ? "tester" : testerName).Split(Path.GetInvalidFileNameChars()));
            log.FilePath = Path.Combine(directory, safe + "_" + log.session + ".csv");
            File.WriteAllText(log.FilePath, Header + "\n", new UTF8Encoding(false));
            log.bytes = Encoding.UTF8.GetByteCount(Header + "\n");
            Emit("session_start", note: testerName); log.Flush();
        }
        catch (Exception e) { log.Disable(e); }
        return log;
    }
    static string Cell(string value)
    {
        value = value ?? "";
        if (value.Length > 1024) value = value.Substring(0, 1024);
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    public static string Identity(Actor actor) => actor == null ? "environment" : actor is Enemy e ? e.SpawnId : actor is PlayerController p ? "player:" + p.Hero.id : "fixture:" + actor.name;
    public static void Contact(Actor target, DamageRequest request, HitResult result)
        => Emit("contact", request.source, target, result.damage, request.kind.ToString(), result.outcome.ToString(), request.actionId);
    public static void Emit(string what, Actor source = null, Actor target = null, double value = 0, string note = "", string outcome = "", string actionId = null)
    {
        var log = Active;
        if (log == null || log.Failure != null || log.ended) return;
        try
        {
            if (what == "kill_reward") log.Kills++;
            if (what == "contact" && target is PlayerController && value > 0) log.HitsTaken++;
            if (what == "player_defeat") log.Falls++;
            var g = Game.I;
            string[] cells = { Number(Time.realtimeSinceStartup-log.start), Cell(what), (g != null ? g.Floor : 0).ToString(), Number(value), Cell(note),
                Cell(log.session + ":" + (++log.sequence)), Cell(ProductionRandom.BuildVersion), Cell(ProductionRandom.ContentVersion), JourneyStore.SchemaVersion.ToString(),
                JourneyStore.GetInt("seedPolicy").ToString(), JourneyStore.GetInt("productionSeed").ToString(CultureInfo.InvariantCulture), Cell(JourneyStore.Campaign), Cell(Save.ExpeditionId),
                Cell(ProductionRandom.Scope), Cell(Identity(source)), Cell(Identity(target)), Cell(actionId ?? (source != null ? source.ActionId : "")), Cell(outcome), Number(GameClocks.SimulationTime) };
            log.pending.Add(string.Join(",", cells) + "\n");
            if (log.pending.Count >= BatchSize || what == "scope_start" || what == "player_defeat") log.Flush();
        }
        catch (Exception e) { log.Disable(e); }
    }
    public void Flush()
    {
        if (Failure != null || pending.Count == 0) return;
        try
        {
            string batch = string.Concat(pending); int size = Encoding.UTF8.GetByteCount(batch);
            if (bytes + size > MaximumBytes) { Dropped += pending.Count; pending.Clear(); Failure = "Local telemetry reached its 8 MiB limit."; return; }
            File.AppendAllText(FilePath, batch, new UTF8Encoding(false)); bytes += size; pending.Clear(); flushAt = Time.realtimeSinceStartup + 1f;
        }
        catch (Exception e) { Disable(e); }
    }
    void Disable(Exception e) { Dropped += pending.Count; pending.Clear(); Failure = e.Message; }
    void Update() { if (Time.realtimeSinceStartup >= flushAt) Flush(); }
    public void End()
    {
        if (ended) return;
        Emit("summary_kills", value: Kills); Emit("summary_hits_taken", value: HitsTaken); Emit("summary_falls", value: Falls);
        Emit("session_end", note: "dropped=" + Dropped); Flush(); ended = true;
    }
    void OnApplicationQuit() => End();
    void OnDestroy() { End(); if (Active == this) Active = null; }
}
