using UnityEngine;

// The world's quiet bed under the score (package S01): wind over the meadow floors, softer in
// town. It ducks while a fight is on and further while a threat is sounding, so it never covers
// the cues that matter. Built by Tools/build_audio.ps1 from synthesised noise.
public class Ambience : MonoBehaviour
{
    public const float FloorLevel = 0.32f, TownLevel = 0.16f, FightDuck = 0.55f, ThreatDuck = 0.4f, Glide = 0.8f;

    AudioSource source;
    public float Target { get; private set; }
    public float Level => source != null ? source.volume : 0f;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>("Audio/ambience_meadow_1");
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 200;
        source.volume = 0f;
        if (source.clip != null) source.Play();
    }

    // What the bed should be now: the place, then how much of a fight is going on.
    public static float Wanted(bool town, bool tower, bool fighting, int threats, bool quiet)
    {
        float level = town ? TownLevel : tower ? FloorLevel : 0f;
        if (fighting) level *= FightDuck;
        if (threats > 0) level *= ThreatDuck;
        return quiet ? level * 0.6f : level;
    }

    public void Tick(bool town, bool tower, bool fighting, float dt)
    {
        Target = Wanted(town, tower, fighting, GameAudio.ActiveThreats, Save.QuietMix);
        if (source != null) source.volume = Mathf.MoveTowards(source.volume, Target, Glide * Mathf.Max(0f, dt));
    }
}
