using UnityEngine;

// Comfort settings belong to preferences, never to a journey or a reward transaction.
public static partial class Save
{
    static float Comfort(string key, float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
    static void SetComfort(string key, float value) => PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
    public static float Flashes { get => Comfort("flashes", 0.5f); set => SetComfort("flashes", value); }
    public static float ImpactFrames { get => Comfort("impactFrames", 0.65f); set => SetComfort("impactFrames", value); }
    public static float MotionLines { get => Comfort("motionLines", 0.65f); set => SetComfort("motionLines", value); }
    public static float Aberration { get => Comfort("aberration", 0.35f); set => SetComfort("aberration", value); }
    public static float VignettePulses { get => Comfort("vignettePulses", 0.5f); set => SetComfort("vignettePulses", value); }
    public static float CameraAuto { get => Comfort("cameraAuto", 1f); set => SetComfort("cameraAuto", value); }
    public static bool QuietMix { get => PlayerPrefs.GetInt("quietMix", 0) == 1; set => PlayerPrefs.SetInt("quietMix", value ? 1 : 0); }

    public static void ReducedEffects()
    {
        CameraMotion = Flashes = ImpactFrames = MotionLines = Aberration = VignettePulses = CameraAuto = 0f;
        QuietMix = true;
        PlayerPrefs.Save();
    }
    public static void StandardEffects()
    {
        CameraMotion = 0.65f; Flashes = 0.5f; ImpactFrames = MotionLines = 0.65f;
        Aberration = 0.35f; VignettePulses = 0.5f; CameraAuto = 1f; QuietMix = false;
        PlayerPrefs.Save();
    }
}

public static class FeedbackPolicy
{
    // Both entry points and render-time values are gated: switching settings during
    // an effect removes it immediately instead of waiting for its timer to expire.
    public static float Flash(float strength) => Mathf.Clamp01(strength) * Save.Flashes;
    public static float Impact(float strength) => Mathf.Clamp01(strength) * Save.ImpactFrames;
    public static float Lines(float strength) => Mathf.Clamp01(strength) * Save.MotionLines;
    public static float Aberr(float strength) => Mathf.Clamp01(strength) * Save.Aberration;
    public static float Pulse(float strength) => Mathf.Clamp01(strength) * Save.VignettePulses;
}
