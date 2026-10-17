using System;
using UnityEngine;

// Additive schema-2 records. Identity is never a display position or a world index.
public static class PilotProgress
{
    public const int Version = 1, Floor = 3;
    public const string FloorId = "tower:03", EventId = "courier-crossing-v1";
    public static readonly string[] Branches = { "mobility", "duelist", "breaker" };
    public static string Branch => JourneyStore.GetString("pilot:mastery:branch");
    public static int Rank => JourneyStore.GetInt("pilot:mastery:rank");
    public static int Budget => Save.BestFloor >= 3 ? 2 : Save.BestFloor >= 1 ? 1 : 0;
    public static int Choice => JourneyStore.GetInt("pilot:event:choice");
    public static bool Shortcut => JourneyStore.GetInt("pilot:shortcut") == 1;
    public static bool Callback => JourneyStore.GetInt("pilot:event:callback") == 1;
    public static string Selected => JourneyStore.GetString("pilot:event:selected");
    public static bool OfferPending => JourneyStore.GetInt("pilot:event:offered") == 1 && Choice == 0;
    public static string Token => JourneyStore.Campaign + ":" + EventId;
    static bool Town => Game.I != null && Game.I.InTown && !Game.I.DefeatPending && Game.I.State == Game.GameState.Hub;
    public static bool Has(string branch, int rank = 1) => Branch == branch && Rank >= rank;
    public static void Validate()
    {
        if (JourneyStore.GetInt("pilotVersion") > Version) throw new NotSupportedException("Future pilot records; use the newer game build.");
        string branch = Branch, selected = Selected;
        if ((branch.Length > 0 && Array.IndexOf(Branches, branch) < 0) || Rank < 0 || Rank > 2 || (branch.Length == 0) != (Rank == 0)
            || Choice < 0 || Choice > 2 || (selected.Length > 0 && selected != "courier-tools" && selected != "courier-medicine"))
            throw new NotSupportedException("Unrecognized pilot records; existing journey files are retained.");
    }
    static void Mark() => JourneyStore.SetInt("pilotVersion", Version);
    public static void BeginOffer() => JourneyStore.Transaction(() =>
    {
        Validate(); if (Selected.Length == 0 || Choice != 0) return;
        Mark(); JourneyStore.SetInt("pilot:event:offered", 1);
    });
    public static bool Learn(string branch) => JourneyStore.Transaction(() =>
    {
        Validate();
        if (!Town || Array.IndexOf(Branches, branch) < 0 || Rank >= Budget || (Branch.Length > 0 && Branch != branch)) return false;
        Mark(); JourneyStore.SetString("pilot:mastery:branch", branch); JourneyStore.SetInt("pilot:mastery:rank", Rank + 1); return true;
    });
    public static bool Respec() => JourneyStore.Transaction(() =>
    {
        Validate(); if (!Town || Rank == 0) return false;
        Mark(); JourneyStore.SetString("pilot:mastery:branch", ""); JourneyStore.SetInt("pilot:mastery:rank", 0); return true;
    });
    public static string SelectEvent(int floor) => JourneyStore.Transaction(() =>
    {
        Validate(); if (floor != Floor || Save.BestFloor < 1) return "";
        if (Selected.Length > 0) return Selected;
        string selected = ProductionRandom.Stream("pilot-event", FloorId + ":v1").Range(0, 2) == 0 ? "courier-tools" : "courier-medicine";
        Mark(); JourneyStore.SetString("pilot:event:selected", selected); return selected;
    });
    public static bool Resolve(int choice, string token) => JourneyStore.Transaction(() =>
    {
        Validate();
        if (token != Token || Selected.Length == 0 || Choice != 0 || choice < 1 || choice > 2) return false;
        Mark(); JourneyStore.SetInt("pilot:event:choice", choice);
        JourneyStore.SetInt("consequence:pilot:courier:" + (choice == 1 ? "helped" : "kit-kept"), 1);
        if (choice == 1) JourneyStore.SetInt("pilot:shortcut", 1);
        else Save.Sparks = checked(Save.Sparks + 24);
        PlaytestLog.Emit("courier_choice", value: choice);
        return true;
    });
    public static bool OpenShortcut() => JourneyStore.Transaction(() =>
    {
        Validate(); if (Shortcut) return false; Mark(); JourneyStore.SetInt("pilot:shortcut", 1); return true;
    });
    public static bool CompleteCallback(string token) => JourneyStore.Transaction(() =>
    {
        Validate(); if (!Town || token != Token || Choice == 0 || Callback || Save.BestFloor < Floor) return false;
        Mark(); JourneyStore.SetInt("pilot:event:callback", 1);
        JourneyStore.SetInt("consequence:pilot:courier:reported", 1);
        if (Choice == 1) Save.Sparks = checked(Save.Sparks + 20);
        return true;
    });
}
