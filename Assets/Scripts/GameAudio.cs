using System;
using System.Collections.Generic;
using UnityEngine;

public enum CuePriority { Threat, Contact, Feedback, Foley }
public enum CueRequirement { Critical, Required, Optional }
public readonly struct AudioCue
{
    public readonly string id;
    public readonly CuePriority priority;
    public readonly CueRequirement requirement;
    public readonly bool ui;
    public AudioCue(string id, CuePriority priority, CueRequirement requirement, bool ui = false)
    { this.id = id; this.priority = priority; this.requirement = requirement; this.ui = ui; }
}

// A bounded world pool reserves four voices for threats. UI acknowledgements
// have their own pool and never consume those voices or acquire a world origin.
public static class GameAudio
{
    public static readonly AudioCue[] Catalog = BuildCatalog();
    static AudioCue[] BuildCatalog()
    {
        var result = new List<AudioCue>();
        // "tell" is an enemy's wind-up: the warning that a blow is coming (package S01).
        foreach (string id in new[] { "fuse", "roar", "slam", "boom", "shoot", "hurt", "perfect", "tell" })
            result.Add(new AudioCue(id, CuePriority.Threat, CueRequirement.Critical));
        foreach (string id in new[] { "hit", "crit", "kill", "swing", "burst", "spawn" })
            result.Add(new AudioCue(id, CuePriority.Contact, CueRequirement.Required));
        foreach (string id in new[] { "step", "land", "jump", "dash" })
            result.Add(new AudioCue(id, CuePriority.Foley, CueRequirement.Required));
        foreach (string id in new[] { "buy", "card", "click", "coin", "combo", "deny", "epic", "heart", "levelup", "lose", "ready", "restore", "wave", "win" })
            result.Add(new AudioCue(id, CuePriority.Feedback, CueRequirement.Required, true));
        result.Add(new AudioCue("hover", CuePriority.Feedback, CueRequirement.Optional, true));
        result.Add(new AudioCue("talk", CuePriority.Feedback, CueRequirement.Optional, true));
        return result.ToArray();
    }
    public static AudioCue Definition(string id)
    {
        foreach (var cue in Catalog) if (cue.id == id) return cue;
        return new AudioCue(id, CuePriority.Feedback, CueRequirement.Required, true);
    }
    static readonly Dictionary<string, AudioClip[]> recordings = new Dictionary<string, AudioClip[]>();
    static readonly HashSet<string> reported = new HashSet<string>();
    class Voice
    {
        public AudioSource source;
        public AudioLowPassFilter filter;
        public CuePriority priority;
        public string cue;
        public double until;
    }
    static Voice[] world, ui;
    static GameObject root;
    public const float ThreatDuck = 0.7f;
    public static int Dropped { get; private set; }
    public static int Stolen { get; private set; }
    public static int WorldVoiceCount => world == null ? 0 : world.Length;
    public static int UiVoiceCount => ui == null ? 0 : ui.Length;
    public static AudioSource LastSource { get; private set; }
    public static int ActiveThreats
    {
        get { int count = 0; if (world != null) foreach (var v in world) if (v.priority == CuePriority.Threat && v.until > AudioSettings.dspTime) count++; return count; }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { root = null; world = ui = null; RefreshRecordings(); Dropped = Stolen = 0; LastSource = null; }
    public static void RefreshRecordings() { recordings.Clear(); reported.Clear(); }
    public static AudioClip[] Takes(string id)
    {
        if (recordings.TryGetValue(id, out var cached)) return cached;
        // Scan each numbered slot independently: a missing _2 never hides _3.
        cached = DiscoverTakes(id, path => Resources.Load<AudioClip>(path));
        recordings[id] = cached;
        if (cached.Length == 0 && Definition(id).requirement != CueRequirement.Optional && reported.Add(id))
            Debug.LogWarning("AUDIO missing " + Definition(id).requirement + " cue: " + id);
        return cached;
    }
    public static AudioClip[] DiscoverTakes(string id, Func<string, AudioClip> load)
    {
        var clips = new List<AudioClip>();
        for (int n = 1; n <= 32; n++)
        {
            var clip = load("Audio/" + id + "_" + n);
            if (clip != null) clips.Add(clip);
        }
        return clips.ToArray();
    }
    static Voice[] Pool(int count, bool spatial)
    {
        var result = new Voice[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject((spatial ? "World " : "UI ") + i); go.transform.SetParent(root.transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = 2f; source.maxDistance = 32f; source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f; source.reverbZoneMix = spatial ? 0.35f : 0f;
            result[i] = new Voice { source = source, filter = spatial ? go.AddComponent<AudioLowPassFilter>() : null };
        }
        return result;
    }
    static void Ensure()
    {
        if (root != null) return;
        root = new GameObject("Game audio"); UnityEngine.Object.DontDestroyOnLoad(root);
        world = Pool(12, true); ui = Pool(4, false);
    }
    public static bool Play(string id, float volume = 1f, float pitch = 1f, Vector3? position = null)
    {
        var cue = Definition(id); var takes = Takes(id);
        if (takes.Length == 0) return false;
        Ensure();
        bool spatial = position.HasValue && !cue.ui;
        var pool = spatial ? world : ui;
        double now = AudioSettings.dspTime;
        // Repeated fuse calls from one bomber are a pulse, not a new voice each frame.
        foreach (var v in pool)
            if (v.until > now && v.cue == id && (!spatial || Vector3.Distance(v.source.transform.position, position.Value) < 0.5f)
                && v.until - now > v.source.clip.length / Mathf.Max(0.1f, v.source.pitch) - 0.12f) return false;
        Voice chosen = null;
        int start = spatial && cue.priority != CuePriority.Threat ? 4 : 0;
        for (int i = start; i < pool.Length; i++) if (pool[i].until <= now) { chosen = pool[i]; break; }
        if (chosen == null)
        {
            // Equal-priority cues may replace their oldest peer; lower-priority
            // requests never evict a stronger signal. Threats prefer stealing foley.
            for (int i = start; i < pool.Length; i++)
            {
                var candidate = pool[i];
                if ((int)candidate.priority < (int)cue.priority) continue;
                if (chosen == null || (int)candidate.priority > (int)chosen.priority ||
                    (candidate.priority == chosen.priority && candidate.until < chosen.until)) chosen = candidate;
            }
            if (chosen == null) { Dropped++; return false; }
            Stolen++;
        }
        chosen.source.Stop(); chosen.source.clip = takes[ProductionRandom.Audio.Range(0, takes.Length)];
        chosen.source.pitch = Mathf.Clamp(pitch, 0.4f, 2f) * ProductionRandom.Audio.Range(0.96f, 1.04f);
        chosen.source.priority = (int)cue.priority * 48;
        chosen.priority = cue.priority; chosen.cue = id;
        bool covered = spatial && Game.I != null && !CombatContact.ClearPath(Game.I.Cam.transform.position, position.Value);
        if (spatial) chosen.source.transform.position = position.Value;
        if (chosen.filter != null) chosen.filter.cutoffFrequency = covered ? 3000f : 22000f;
        float gain = Mathf.Clamp01(volume) * 0.6f;
        if (Save.QuietMix) gain = volume <= 0 ? 0 : Mathf.Lerp(0.18f, 0.36f, Mathf.Clamp01(volume));
        if (covered) gain *= cue.priority == CuePriority.Threat ? 0.8f : 0.55f;
        // While a threat is sounding, everything lesser in the world steps back, so the warning
        // carries over the din of a mixed fight.
        if (spatial && cue.priority != CuePriority.Threat && ActiveThreats > 0) gain *= ThreatDuck;
        chosen.source.volume = gain;
        chosen.until = now + chosen.source.clip.length / chosen.source.pitch;
        chosen.source.Play(); LastSource = chosen.source; return true;
    }
    public static void StopWorld()
    {
        if (world == null) return;
        foreach (var v in world) { v.source.Stop(); v.until = 0; }
    }
}
