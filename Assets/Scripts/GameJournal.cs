using System.Collections.Generic;
using UnityEngine;

// The journal (package U01): one pause tab that says where the journey stands. Every line is read
// from the saved records the game already keeps (commissions, the courier, rescues, the floor-5
// Guardian, Mop's order, the checkpoint), so it can never disagree with what the world does.
public partial class Game
{
    public struct JournalEntry { public string head, line; public Color color; }

    public List<JournalEntry> Journal()
    {
        var list = new List<JournalEntry>();
        void Add(string head, string line, Color color) => list.Add(new JournalEntry { head = head, line = line, color = color });

        Add("NOW", Objective(), new Color(1f, 0.95f, 0.75f));
        string next = NextStep();
        if (next != null) Add("NEXT", next, new Color(0.78f, 1f, 0.82f));

        Mission m = ActiveMission;
        int fulfilled = 0;
        foreach (var mission in Missions) if (Save.Consequence("commission:" + mission.id + ":fulfilled")) fulfilled++;
        string done = fulfilled > 0 ? "   (" + fulfilled + " fulfilled)" : "";
        if (m == null) Add("COMMISSION", "None taken. Nix of the Guild posts work, " + Where(GuildDesk) + "." + done, Soft);
        else if (MissionReady) Add("COMMISSION", m.title + ": done. Report to " + m.giver + " for " + m.sparks + " Sparks and " + m.xp + " XP." + done, UI.Green);
        else Add("COMMISSION", m.title + ": " + MissionTask(m) + "  " + MissionProgress(m) + " / " + MissionGoal(m) + "." + done, UI.Gold);

        if (PilotProgress.Choice == 1)
            Add("THE COURIER", "You gave the courier the " + (PilotProgress.Selected == "courier-medicine" ? "medicine" : "survey tools") + "; the crossing shortcut is open."
                + (Save.Consequence("pilot:courier:reported") ? " You reported it at the field desk." : " The field desk would like to hear of it."), UI.Green);
        else if (PilotProgress.Choice == 2)
            Add("THE COURIER", "You kept the courier's kit and sold it on for 24 Sparks. The crossing has no shortcut.", UI.Gold);
        else if (PilotProgress.OfferPending)
            Add("THE COURIER", "A courier on floor " + PilotProgress.Floor + " is waiting on your answer.", UI.Gold);

        for (int i = 0; i < Story.Rescues.Length; i++)
        {
            Lost lost = Story.Rescues[i];
            int choice = Save.RescueChoice(lost.id);
            bool rescued = (Save.RescuedMask & (1 << i)) != 0 || Save.RescuePending(lost.id) || choice != 0;
            if (!rescued)
            {
                if (Save.BestFloor >= lost.floor - 1) Add("MISSING", lost.name + " was last seen near floor " + lost.floor + ".", Soft);
                continue;
            }
            string line;
            if (choice == 1)
            {
                line = lost.name + ": sent straight home, leaving you a last draught (+" + DraughtHearts + " hearts).";
                if (lost.id == "rhea") line += VaneThanksPaid ? " Lady Vane paid her thanks." : VaneThanksDue ? " Lady Vane wants to thank you in town." : "";
            }
            else if (choice == 2)
            {
                line = lost.name + ": stayed to share what was seen above, and an ambush came for it.";
                if (lost.id == "rhea") line += " The Gatekeeper's pillars are marked. Lady Vane has not forgotten.";
            }
            else line = lost.name + ": found, and still to be brought home.";
            Add("RESCUE", line, choice == 0 ? UI.Gold : new Color(0.75f, 0.9f, 1f));
        }

        if (Save.BestFloor >= ChapterProgress.GatekeeperFloor) Add("GUARDIAN", "Grol the Gatekeeper has fallen. Floor " + ChapterProgress.GatekeeperFloor + " is clear.", UI.Green);
        else if (ChapterProgress.GatekeeperCheckpoint)
            Add("CHECKPOINT", "Floor " + ChapterProgress.GatekeeperFloor + "'s packs are beaten. A retry starts at the Gatekeeper's gate."
                + (Save.Consequence("rescue:rhea:guardian-intel") ? " Rhea's intel: put a marked pillar behind you and let him charge into the stone." : ""), UI.Gold);
        else if (Save.BestFloor >= ChapterProgress.GatekeeperFloor - 2)
            Add("GUARDIAN", "A Guardian holds floor " + ChapterProgress.GatekeeperFloor + "." + (Save.Consequence("rescue:rhea:guardian-intel") ? " Rhea's intel marks the pillars to bait his charge into." : ""), Soft);

        if (Save.Pet) Add("MOP", Companion.Describe(Companion.Order) + ".   " + ControlGuide.Key(ControlAction.Companion) + " changes it.", new Color(0.55f, 0.82f, 1f));
        if (Save.TimingAssist) Add("ASSIST", "Timing assist is on: a wider parry window and a longer input buffer. Settings, Access.", UI.Green);
        return list;
    }

    void DrawJournal(float w, float h, float cx)
    {
        var panel = new Rect(cx - 430f, 118f, 860f, Mathf.Min(560f, h - 200f));
        UI.Framed(panel, new Color(0.09f, 0.095f, 0.115f, 0.95f), UI.Gold, 3f);
        UI.Text(new Rect(panel.x, panel.y + 10, panel.width, 36), "Journal", 28, UI.Gold);
        float y = panel.y + 60f;
        foreach (var entry in Journal())
        {
            if (y > panel.yMax - 40f) break;
            UI.Text(new Rect(panel.x + 26, y, 150, 26), entry.head, 14, entry.color, TextAnchor.UpperLeft, false);
            UI.Text(new Rect(panel.x + 180, y, panel.width - 210, 52), entry.line, 15, Color.white, TextAnchor.UpperLeft, false);
            y += entry.line.Length > 92 ? 56f : 34f;
        }
    }
}
