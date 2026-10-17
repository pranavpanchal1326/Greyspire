using System;

// Additive schema-2 chapter receipts. These never grant floor clears or rewards.
public static class ChapterProgress
{
    public const int Version = 1, MillraceFloor = 2, GatekeeperFloor = 5;
    const string MillraceKey = "chapter:tower:02:millrace-gate-v1";
    // Floor 5's packs are beaten and only the Gatekeeper stands: a retry starts at his gate.
    // It is cleared again when he falls. It records progress, never a reward.
    const string GatekeeperKey = "chapter:tower:05:gatekeeper-checkpoint-v1";
    public static bool MillraceOpen => JourneyStore.GetInt(MillraceKey) == 1;
    public static string MillraceToken => JourneyStore.Campaign + ":" + MillraceKey;
    public static bool GatekeeperCheckpoint => JourneyStore.GetInt(GatekeeperKey) == 1;
    public static string GatekeeperToken => JourneyStore.Campaign + ":" + GatekeeperKey;

    public static void Validate()
    {
        int version = JourneyStore.GetInt("chapterVersion"), gate = JourneyStore.GetInt(MillraceKey), keeper = JourneyStore.GetInt(GatekeeperKey);
        if (version < 0 || version > Version || gate < 0 || gate > 1 || keeper < 0 || keeper > 1)
            throw new NotSupportedException("Unsupported chapter records; existing journey files are retained.");
    }

    public static bool OpenMillrace(string token) => JourneyStore.Transaction(() =>
    {
        Validate();
        if (token != MillraceToken || MillraceOpen) return false;
        JourneyStore.SetInt("chapterVersion", Version);
        JourneyStore.SetInt(MillraceKey, 1);
        return true;
    });

    // The floor's packs have fallen with the Gatekeeper still standing. A token from another
    // campaign (a callback outliving its journey) changes nothing.
    public static bool ReachGatekeeper(string token) => JourneyStore.Transaction(() =>
    {
        Validate();
        if (token != GatekeeperToken || GatekeeperCheckpoint) return false;
        JourneyStore.SetInt("chapterVersion", Version);
        JourneyStore.SetInt(GatekeeperKey, 1);
        return true;
    });

    // The Gatekeeper has fallen: floor 5 is cleared through the ordinary record, and the
    // checkpoint has done its job.
    public static bool PassGatekeeper(string token) => JourneyStore.Transaction(() =>
    {
        Validate();
        if (token != GatekeeperToken || !GatekeeperCheckpoint) return false;
        JourneyStore.SetInt(GatekeeperKey, 0);
        return true;
    });
}
