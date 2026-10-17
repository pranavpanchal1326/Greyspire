using UnityEngine;

// Authored paired score loops for three battle environments, plus the recorded
// town theme. Banks share a DSP clock; new scenes enter on a bar and crossfade.
// Combat adds actual percussion/lead material without restarting the bed.
public class Music : MonoBehaviour
{
    const int Rate = 22050;
    const float Step = 0.125f;      // one 16th note at 120 bpm
    const int StepsPerChord = 16;

    // Chord roots (MIDI) and whether each is minor, per track.
    static readonly int[][] Roots =
    {
        new[] { 60, 57, 53, 55 },   // menu: C Am F G
        new[] { 60, 55, 57, 53 },   // meadow: C G Am F
        new[] { 62, 58, 60, 57 },   // dunes: Dm Bb C Am
        new[] { 57, 53, 50, 52 },   // hollow: Am F Dm E
    };
    static readonly bool[][] Minor =
    {
        new[] { false, true, false, false },
        new[] { false, false, true, false },
        new[] { true, false, false, true },
        new[] { true, false, true, false },
    };

    [HideInInspector] public float intensity;
    class Bank { public AudioSource bed, drive; public bool recorded; public int track; }
    readonly Bank[] banks = new Bank[2];
    readonly AudioClip[] bedClips = new AudioClip[4], driveClips = new AudioClip[4];
    int active, requested = -1, track = -1, incoming;
    bool transition;
    double clock, transitionAt;
    float driveVol;
    const double Bar = 2.0, Fade = 1.0;
    public int ActiveTrack => track;
    public int RequestedTrack => requested;
    public bool Transitioning => transition;
    public static string ScoreId(int index) => new[] { "brushwick-recorded", "greenreach-score", "sunscorch-score", "hollow-score" }[Mathf.Clamp(index, 0, 3)];
    public static float TransitionGain(double now, double start) => Mathf.Clamp01((float)((now - start) / Fade));
    public AudioClip BedClip(int index) { if (bedClips[index] == null) Build(index); return bedClips[index]; }
    public AudioClip DriveClip(int index) { if (driveClips[index] == null) Build(index); return driveClips[index]; }
    void Awake()
    {
        for (int i = 0; i < banks.Length; i++)
        {
            banks[i] = new Bank { bed = gameObject.AddComponent<AudioSource>(), drive = gameObject.AddComponent<AudioSource>() };
            foreach (var source in new[] { banks[i].bed, banks[i].drive })
            { source.loop = true; source.playOnAwake = false; source.spatialBlend = 0f; source.volume = 0f; source.priority = 180; }
        }
    }
    void Prepare(Bank bank, int index, double start)
    {
        bank.track = index;
        var recorded = index == 0 ? Resources.Load<AudioClip>("Audio/Music/TownTheme") : null;
        bank.recorded = recorded != null;
        bank.bed.clip = recorded != null ? recorded : BedClip(index);
        bank.drive.clip = bank.recorded ? null : DriveClip(index);
        bank.bed.volume = bank.drive.volume = 0f;
        bank.bed.PlayScheduled(start);
        if (bank.drive.clip != null) bank.drive.PlayScheduled(start);
    }
    public void SetTrack(int index)
    {
        requested = Mathf.Clamp(index, 0, 3);
        if (track >= 0) return;
        clock = AudioSettings.dspTime + 0.05;
        track = requested; Prepare(banks[active], track, clock);
    }
    void Update() => TickMix(AudioSettings.dspTime, Time.unscaledDeltaTime);
    public void TickMix(double now, float delta)
    {
        if (track < 0) return;
        if (!transition && requested != track)
        {
            incoming = 1 - active;
            transitionAt = clock + System.Math.Ceiling((now + 0.1 - clock) / Bar) * Bar;
            Prepare(banks[incoming], requested, transitionAt);
            transition = true;
        }
        driveVol = Mathf.MoveTowards(driveVol, Mathf.Clamp01(intensity), Mathf.Max(0f, delta) * 0.8f);
        float mix = Save.MusicVolume * (Save.QuietMix ? 0.65f : 1f);
        float t = transition ? TransitionGain(now, transitionAt) : 0f;
        float oldGain = transition ? Mathf.Cos(t * Mathf.PI * 0.5f) : 1f;
        Apply(banks[active], mix * oldGain);
        if (!transition) return;
        Apply(banks[incoming], mix * Mathf.Sin(t * Mathf.PI * 0.5f));
        if (t < 1f) return;
        banks[active].bed.Stop(); banks[active].drive.Stop();
        active = incoming; track = banks[active].track; transition = false;
    }
    void Apply(Bank bank, float gain)
    {
        bank.bed.volume = gain * (bank.recorded ? 0.42f : 0.35f);
        bank.drive.volume = bank.recorded ? 0f : gain * 0.3f * driveVol;
        // Fixed pitch keeps the two loops locked through hit-stop and pause.
        bank.bed.pitch = bank.drive.pitch = 1f;
    }
    void OnDestroy()
    {
        foreach (var clip in bedClips) if (clip != null) Destroy(clip);
        foreach (var clip in driveClips) if (clip != null) Destroy(clip);
    }

    void Build(int index)
    {
        int[] roots = Roots[index];
        int steps = roots.Length * StepsPerChord;
        int len = (int)(steps * Step * Rate);
        var a = new float[len];
        var b = new float[len];
        var rng = new System.Random(index * 31 + 7);

        for (int s = 0; s < steps; s++)
        {
            int chord = s / StepsPerChord;
            int local = s % StepsPerChord;
            int root = roots[chord];
            int third = Minor[index][chord] ? 3 : 4;
            int[] tones = { 0, third, 7, 12 };
            int start = (int)(s * Step * Rate);

            // Bed: bass pulse and a gentle arpeggio.
            if (local == 0 || local == 6 || local == 8 || local == 14)
                Note(a, start, Freq(root - 24), 0.3f, 0.22f, 0);
            if (local % 2 == 0)
                Note(a, start, Freq(root - 12 + tones[(local / 2) % 3 + (local % 8 == 6 ? 1 : 0)]), 0.28f, 0.1f, 1);

            // Drive: kick, hats and a busier lead an octave up.
            if (local % 4 == 0) Kick(b, start);
            if (local % 4 == 2) Hat(b, start, rng, 0.07f);
            if (local % 8 == 4) Hat(b, start, rng, 0.12f);
            int[] lead = { 0, 2, 1, 3, 2, 1, 0, 2 };
            Note(b, start, Freq(root + tones[lead[local % 8]]), 0.12f, local % 4 == 0 ? 0.09f : 0.06f, 2);
        }

        bedClips[index] = Clip(ScoreId(index) + "-bed", a);
        driveClips[index] = Clip(ScoreId(index) + "-drive", b);
    }

    static AudioClip Clip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Freq(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }

    // wave: 0 = sine + a little second harmonic, 1 = triangle, 2 = soft square.
    // Tails wrap round the end of the buffer so the loop is seamless.
    static void Note(float[] buf, int start, float freq, float dur, float vol, int wave)
    {
        int n = (int)(dur * Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float ph = t * freq;
            float frac = ph - Mathf.Floor(ph);
            float s;
            if (wave == 0) s = Mathf.Sin(ph * Mathf.PI * 2f) + 0.3f * Mathf.Sin(ph * Mathf.PI * 4f);
            else if (wave == 1) s = 4f * Mathf.Abs(frac - 0.5f) - 1f;
            else s = Mathf.Clamp(Mathf.Sin(ph * Mathf.PI * 2f) * 3f, -1f, 1f) * 0.7f;
            float k = i / (float)n;
            float env = Mathf.Min(1f, i / 120f) * (1f - k) * (1f - k);
            buf[(start + i) % buf.Length] += s * env * vol;
        }
    }

    static void Kick(float[] buf, int start)
    {
        int n = (int)(0.16f * Rate);
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            ph += Mathf.Lerp(130f, 42f, k) / Rate;
            buf[(start + i) % buf.Length] += Mathf.Sin(ph * Mathf.PI * 2f) * (1f - k) * 0.5f;
        }
    }

    static void Hat(float[] buf, int start, System.Random rng, float vol)
    {
        int n = (int)(0.04f * Rate);
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            buf[(start + i) % buf.Length] += ((float)rng.NextDouble() * 2f - 1f) * (1f - k) * (1f - k) * vol;
        }
    }
}
