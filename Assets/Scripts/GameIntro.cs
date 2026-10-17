using UnityEngine;

// Arriving somewhere: a few seconds of camera over the place, wide-screen bars, the
// name of where you are, and the hero waiting at the end of it. No HUD and no
// controls until the camera has come down behind them. Any button skips it.
public partial class Game
{
    const float IntroBars = 0.115f;     // each bar, as a share of the screen height

    float introT, introDur;
    string introTitle = "", introLine = "";
    bool seenTown, seenWilds;       // the first arrival of a session is the long one

    // How long an arrival runs: the full sweep the first time, a shorter one after that.
    static float ArrivalTime(ref bool seen, float full)
    {
        float seconds = seen ? full * 0.6f : full;
        seen = true;
        return seconds;
    }

    public bool Intro => introT > 0f;

    void BeginIntro(string title, string line, Vector3 vantage, Vector3 lookAt, float seconds)
    {
        introTitle = title;
        introLine = line;
        introT = introDur = Save.CameraAuto <= 0f ? Mathf.Min(seconds, 1.2f) : seconds;
        bannerT = 0f;
        toastT = 0f;
        Cam.Entrance(vantage, lookAt, seconds);
    }

    void TickIntro(float udt)
    {
        if (introT <= 0f) return;
        introT -= udt;
        bool skip = GameInput.ConfirmPressed || GameInput.InteractPressed || GameInput.PausePressed;
        if (!skip || fadeDir != 0f || introDur - introT < 0.7f || introT < 0.7f) return;
        introT = 0.6f;
        Cam.SkipEntrance();
    }

    void DrawIntro(float w, float h)
    {
        float age = introDur - introT;
        float bars = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(age / 0.5f, introT / 0.6f)));
        float bar = h * IntroBars * bars;
        Color ink = new Color(0.014f, 0.015f, 0.018f, 1f);
        UI.Fill(new Rect(0, 0, w, bar), ink);
        UI.Fill(new Rect(0, h - bar, w, bar), ink);

        // The name of the place: a rule drawing out, the title over it, one line under it.
        float a = Mathf.Clamp01((age - 0.5f) / 0.7f) * Mathf.Clamp01((introT - 0.5f) / 0.7f);
        if (a <= 0f) return;
        float x = w * 0.075f, y = h * 0.62f;
        Color gold = new Color(1f, 0.86f, 0.45f, a), white = new Color(1f, 1f, 1f, a);
        UI.Text(new Rect(x, y - 78f, w, 78f), introTitle, 66, white, TextAnchor.MiddleLeft);
        UI.Fill(new Rect(x, y + 6f, 380f * Mathf.SmoothStep(0f, 1f, a), 3f), gold);
        UI.Text(new Rect(x, y + 16f, w, 34f), introLine, 23, gold, TextAnchor.MiddleLeft, false);
        if (age > 1.2f)
            UI.Text(new Rect(w - 400f, h - bar + 6f, 380f, 26f), ControlGuide.Key(ControlAction.Advance) + " skip arrival", 15, new Color(1f, 1f, 1f, 0.45f * a), TextAnchor.MiddleRight, false);
    }
}
