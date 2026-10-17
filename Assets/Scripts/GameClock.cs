using UnityEngine;

// The world's clock. Time passes whenever the hero is on their feet, in town, in
// the Wilds and up the tower alike: a day runs dawn, morning, afternoon, evening
// and night, the day is counted, and the clock is kept in the save. The sky
// (GameSky.cs) and the people of Brushwick (GameLife.cs) both read it.
public partial class Game
{
    const float HourSeconds = 40f;      // real seconds to one hour of daylight: a full day is about a quarter of an hour
    const float NightPace = 1.6f;       // the small hours pass quicker

    float clockHour = 8f, clockSaveT;
    int clockPhase = -1;

    static readonly string[] PhaseNames = { "Night", "Dawn", "Morning", "Afternoon", "Evening" };
    static readonly string[] PhaseLines =
    {
        "Night falls. The Gloom is restless after dark.", "Dawn. A new day.", "Morning.", "Afternoon.", "Evening. The lamps are being lit.",
    };

    public float Hour => clockHour;
    public bool IsNight => clockHour >= 20f || clockHour < 5f;

    // 0 night, 1 dawn, 2 morning, 3 afternoon, 4 evening.
    public static int PhaseOf(float hour) => hour < 5f ? 0 : hour < 7f ? 1 : hour < 12f ? 2 : hour < 17f ? 3 : hour < 20f ? 4 : 0;

    public static string ClockText(float hour)
    {
        int h = Mathf.FloorToInt(hour) % 24, m = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
        return h.ToString("00") + ":" + (m / 5 * 5).ToString("00");
    }

    // Is this hour inside a shift? A shift that ends before it starts runs past midnight.
    public bool OnDuty(float from, float to)
    {
        if (to - from >= 24f) return true;
        to %= 24f;
        return from <= to ? clockHour >= from && clockHour < to : clockHour >= from || clockHour < to;
    }

    void LoadClock()
    {
        clockHour = Mathf.Repeat(Save.Hour, 24f);
        dayT = clockHour / 24f;
        clockPhase = PhaseOf(clockHour);
    }

    void SaveClock()
    {
        Save.Hour = clockHour;
    }

    void TickClock(float dt)
    {
        if ((State != GameState.Playing && State != GameState.Hub) || awakening) return;
        clockHour += dt / HourSeconds * (clockHour >= 21f || clockHour < 5f ? NightPace : 1f);
        if (clockHour >= 24f)
        {
            clockHour -= 24f;
            Save.Day++;
        }
        dayT = clockHour / 24f;

        int phase = PhaseOf(clockHour);
        if (phase != clockPhase)
        {
            // The turn of the day is worth a word, wherever the sky can be seen.
            bool first = clockPhase < 0;
            clockPhase = phase;
            if (!first && !Intro && (State == GameState.Hub || InWilds) && phase != 2 && phase != 3)
                Toast(PhaseLines[phase], phase == 0 ? new Color(0.7f, 0.75f, 1f) : UI.Gold);
        }
        if ((clockSaveT -= dt) <= 0f)
        {
            clockSaveT = 10f;
            SaveClock();
            Save.Flush();
        }
    }

    // Rest: the clock runs forward to the given hour, into tomorrow if it has to.
    void SkipTo(float hour)
    {
        if (hour <= clockHour) Save.Day++;
        clockHour = hour;
        dayT = clockHour / 24f;
        clockPhase = PhaseOf(clockHour);
        SaveClock();
        Save.Flush();
    }

    // Day, hour and part of the day: top right, with the sun or moon riding a small arc.
    void DrawClock(float w, float y)
    {
        var r = new Rect(w - 236, y, 210, 46);
        UI.Panel(r, new Color(0.045f, 0.047f, 0.058f, 0.62f), true);

        // The arc: the sun climbs from the left at six, stands overhead at noon, sets on the right at six.
        bool day = clockHour >= 6f && clockHour < 18f;
        float k = day ? (clockHour - 6f) / 12f : Mathf.Repeat(clockHour - 18f, 24f) / 12f;
        float ax = r.x + 14f + k * 34f, ay = r.y + 32f - Mathf.Sin(k * Mathf.PI) * 18f;
        UI.Fill(new Rect(r.x + 12, r.y + 34, 40, 2), new Color(1f, 1f, 1f, 0.35f));
        Color orb = day ? Color.Lerp(new Color(1f, 0.6f, 0.3f), new Color(1f, 0.92f, 0.5f), Mathf.Sin(k * Mathf.PI)) : new Color(0.75f, 0.82f, 1f);
        UI.Tex(UI.Centered(ax, ay, 20, 20), UI.Glow, new Color(orb.r, orb.g, orb.b, 0.6f));
        UI.Tex(UI.Centered(ax, ay, 11, 11), UI.Circle, orb);

        UI.Text(new Rect(r.x + 62, r.y + 3, 140, 22), "DAY " + Save.Day + "    " + ClockText(clockHour), 16, Color.white, TextAnchor.MiddleLeft);
        UI.Text(new Rect(r.x + 62, r.y + 23, 140, 20), PhaseNames[PhaseOf(clockHour)], 14, day ? new Color(1f, 0.9f, 0.6f) : new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleLeft, false);
    }
}
