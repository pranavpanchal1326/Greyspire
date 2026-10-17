using UnityEngine;

// Owns Time.timeScale. Hit-stop freezes the action for a few frames when a
// blow lands; slow-mo rewards a perfect dodge and sells the killing blow.
public static class Juice
{
    public static bool Paused;

    static float hitstop, slow;

    public static bool Slowed => slow > 0f;

    public static void Hitstop(float seconds)
    {
        hitstop = Mathf.Max(hitstop, seconds);
    }

    public static void Slow(float seconds)
    {
        slow = Mathf.Max(slow, seconds);
    }

    public static void Reset()
    {
        hitstop = 0f;
        slow = 0f;
    }

    public static void Tick()
    {
        float dt = Time.unscaledDeltaTime;
        if (Paused)
        {
            Time.timeScale = 0f;
        }
        else if (hitstop > 0f)
        {
            hitstop -= dt;
            Time.timeScale = 0.02f;
        }
        else if (slow > 0f)
        {
            slow -= dt;
            Time.timeScale = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(slow / 0.2f));
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}
