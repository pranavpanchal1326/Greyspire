using UnityEngine;

// The learning path between climbs: a newcomer back in Brushwick is told the one most useful thing
// to do next, in order: spend stat points, learn at the Field desk, report the courier, accept Lady
// Vane's thanks, take a commission. The objective line keeps saying where the story goes; this says
// what the town can do for the hero first. Every suggestion is read from the save, so it disappears
// the moment it is done, and nothing here grants anything.
public partial class Game
{
    public string NextStep()
    {
        if (!InTown) return null;
        int points = Save.StatPoints;
        if (points > 0)
            return points + " stat point" + (points == 1 ? "" : "s") + " to spend: " + ControlGuide.Key(ControlAction.Status) + " opens Status";
        if (Save.BestFloor >= 1 && PilotProgress.Rank < PilotProgress.Budget)
            return "The Field desk can teach you a technique (" + (PilotProgress.Budget - PilotProgress.Rank) + " to learn), " + Where("Field desk");
        if (PilotProgress.Choice != 0 && !PilotProgress.Callback && Save.BestFloor >= PilotProgress.Floor)
            return "Report the courier at the Field desk, " + Where("Field desk");
        if (VaneThanksDue) return "Lady Vane is waiting to thank you for Rhea, " + Where("Vane");
        // The first visit is for the tower alone; work is suggested once a floor has been cleared.
        if (Save.BestFloor >= 1 && ActiveMission == null && CommissionOnOffer()) return "Nix of the Guild, " + Where(GuildDesk) + ", has work: a commission pays Sparks and XP";
        return null;
    }

    // Which way a person or place lies from the well at the centre of the square, read from where it
    // actually stands in town, so the words can never disagree with the map.
    string Where(string name)
    {
        Spot spot = spots.Find(s => s.name.Contains(name));
        if (spot == null) return "in town";
        Vector3 at = spot.who != null ? spot.who.transform.position : spot.pos;
        return Compass(new Vector2(at.x, at.z)) + " of the square";
    }

    public static string Compass(Vector2 offset)
    {
        if (offset.sqrMagnitude < 4f) return "by the well";
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        float angle = Mathf.Repeat(Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg + 22.5f, 360f);
        return names[Mathf.FloorToInt(angle / 45f) % 8];
    }

    // For a playtest: each change of objective or next step is logged, so a session shows how long a
    // newcomer spent on each instruction (and where one was left standing too long).
    string loggedObjective, loggedNext;
    float guidanceLogT;
    void TickGuidanceLog(float udt)
    {
        if (PlaytestLog.Active == null || Player == null || (guidanceLogT -= udt) > 0f) return;
        guidanceLogT = 0.5f;
        if (State != GameState.Playing && State != GameState.Hub) return;
        string objective = Objective(), next = NextStep();
        if (objective != loggedObjective) { loggedObjective = objective; PlaytestLog.Emit("objective", note: objective); }
        if (next != loggedNext) { loggedNext = next; if (next != null) PlaytestLog.Emit("next_step", note: next); }
    }

    void DrawNextStep(float x, float y)
    {
        string next = NextStep();
        if (next == null) return;
        UI.Fill(new Rect(x, y + 4, 4, 16), UI.Green);
        UI.Text(new Rect(x + 12, y, 620, 24), "NEXT   " + next, 14, new Color(0.78f, 1f, 0.82f), TextAnchor.MiddleLeft);
    }
}
