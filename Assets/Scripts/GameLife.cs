using System.Collections.Generic;
using UnityEngine;

// Brushwick as a place people live in. Everyone keeps hours (Resident.cs): the
// smith opens at six, the benches fill at midday, the square empties at dusk, the
// windows light, and the night watch walks its round until dawn. People say a
// word as the hero goes by, and there is a bed at home to pass the time in.
public partial class Game
{
    readonly List<Resident> residents = new List<Resident>();
    readonly List<Vector3> doors = new List<Vector3>();
    float barkScanT;

    void ClearResidents()
    {
        residents.Clear();
        doors.Clear();
    }

    // Gives one of the townsfolk their hours and a door to go home through.
    Resident Settle(GameObject go, CharAnim clips, float from, float to, bool wanders, params Behaviour[] routines)
    {
        if (clips == null || go.transform.childCount == 0) return null;
        var r = go.AddComponent<Resident>();
        r.clips = clips;
        r.model = go.transform.GetChild(0);
        r.post = go.transform.position;
        r.facing = go.transform.rotation;
        r.home = NearestDoor(r.post);
        // Nobody leaves on the stroke of the hour: each keeps their own slightly different time.
        float slack = (residents.Count * 0.37f % 1f - 0.5f) * 0.6f;
        r.from = to - from >= 24f ? 0f : Mathf.Repeat(from + slack, 24f);
        r.to = to - from >= 24f ? 24f : Mathf.Repeat(to + slack, 24f);
        r.wanders = wanders;
        r.routines = routines;
        r.barkWait = 3f + residents.Count % 5 * 2f;
        residents.Add(r);
        return r;
    }

    Vector3 NearestDoor(Vector3 from)
    {
        Vector3 best = from + Vector3.back * 6f;
        float bestD = float.MaxValue;
        foreach (Vector3 d in doors)
        {
            float dist = (d - from).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    void SnapResidents()
    {
        foreach (Resident r in residents) if (r != null) r.Snap();
    }

    // ------------------------------------------------------------ a word in passing

    // People notice the hero. Every so often the nearest one says something: about the hour,
    // about the tower, about what he has done lately.
    void TickBarks(float dt)
    {
        foreach (Resident r in residents)
        {
            if (r == null) continue;
            r.barkT -= dt;
            r.barkWait -= dt;
        }
        if ((barkScanT -= dt) > 0f || hubPanel != 0) return;
        barkScanT = 0.6f;

        Vector3 me = Player.transform.position;
        Resident near = null;
        float best = 4.4f * 4.4f;
        foreach (Resident r in residents)
        {
            if (r == null || r.Indoors) continue;
            if (r.barkT > 0f) return;                   // someone is already talking
            float d = (r.transform.position - me).sqrMagnitude;
            if (d < best && r.barkWait <= 0f) { best = d; near = r; }
        }
        if (near == null) return;
        near.bark = Story.Bark(PhaseOf(clockHour), Save.BestFloor, Rescued(), near.AtPost);
        near.barkT = 4.2f;
        near.barkWait = ProductionRandom.Narrative.Range(22f, 40f);
        // A nod goes with it, on top of whatever they were doing.
        if (near.clips != null && near.AtPost) near.clips.Upper("Yes", 1.5f, 1.5f, 0.15f);
        // Nobody else pipes up straight after.
        foreach (Resident r in residents) if (r != null && r != near) r.barkWait = Mathf.Max(r.barkWait, 7f);
    }

    void DrawBarks()
    {
        foreach (Resident r in residents)
        {
            if (r == null || r.barkT <= 0f || string.IsNullOrEmpty(r.bark)) continue;
            if (!ToGui(r.transform.position + Vector3.up * 2.25f, out Vector2 p)) continue;
            float a = Mathf.Clamp01(r.barkT * 2f) * Mathf.Clamp01((4.2f - r.barkT) * 5f);
            float width = Mathf.Min(360f, 40f + r.bark.Length * 8.6f);
            var box = UI.Centered(p.x, p.y - 16f, width, r.bark.Length > 38 ? 54f : 34f);
            UI.Panel(UI.Grow(box, 2f), new Color(0.062f, 0.065f, 0.079f, a), true);
            UI.Panel(box, new Color(0.98f, 0.97f, 0.93f, a), true);
            UI.Prose(UI.Grow(box, -8f), r.bark, 16, new Color(0.078f, 0.082f, 0.1f, a), TextAnchor.MiddleCenter, false);
        }
    }

    // ------------------------------------------------------------ home

    // The hero's own door. Sleep until morning, or sit out the afternoon until the lamps are lit.
    void DrawRest(float w, float h, float cx)
    {
        Dim(w, h, 0.6f);
        UI.Text(new Rect(0, 120, w, 60), "HOME", 50, UI.Gold);
        UI.Text(new Rect(0, 184, w, 28), "A bed, a roof, and a door that shuts.     It is " + ClockText(clockHour) + ", " + PhaseNames[PhaseOf(clockHour)].ToLower() + " of day " + Save.Day + ".",
            18, Color.white, TextAnchor.MiddleCenter, false);
        if (UI.Button(UI.Centered(cx, 290, 460, 62), "SLEEP UNTIL MORNING  (06:00)", UI.Blue, 22)) Rest(6f);
        bool day = clockHour >= 5f && clockHour < 17.5f;
        if (UI.Button(UI.Centered(cx, 366, 460, 62), "REST UNTIL EVENING  (18:00)", new Color(0.95f, 0.6f, 0.3f), 22, day)) Rest(18f);
        UI.Text(new Rect(0, 410, w, 24), "Resting restores your hearts. The town keeps its own hours: shops shut at night.", 15,
            new Color(0.85f, 0.85f, 0.95f), TextAnchor.MiddleCenter, false);
        if (UI.Button(UI.Centered(cx, h - 110, 240, 52), "NOT NOW", new Color(0.4f, 0.4f, 0.5f))) OpenHubPanel(0);
    }

    void Rest(float hour)
    {
        Transition(() =>
        {
            SkipTo(hour);
            Player.NewRun();
            SnapResidents();
            OpenHubPanel(0);
            SetState(GameState.Hub);
            Banner("DAY " + Save.Day, 2.8f, PhaseNames[PhaseOf(hour)]);
            ToonKit.Sfx("restore", 0.6f);
        });
    }
}
