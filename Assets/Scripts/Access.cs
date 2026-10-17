using UnityEngine;

// Comfort and access options (package U03). Guard and sprint can be held (the default) or
// toggled with one press; timing assist widens the parry window and lengthens the input buffer.
// The assist is disclosed on the settings page, in the HUD's corner tag and in the journal, and it
// changes no damage, no reward and no enemy: it only gives the player's own timing more room.
public static partial class Save
{
    public static bool GuardToggle { get => PlayerPrefs.GetInt("access.guardToggle", 0) == 1; set { PlayerPrefs.SetInt("access.guardToggle", value ? 1 : 0); Access.Reset(); } }
    public static bool SprintToggle { get => PlayerPrefs.GetInt("access.sprintToggle", 0) == 1; set { PlayerPrefs.SetInt("access.sprintToggle", value ? 1 : 0); Access.Reset(); } }
    public static bool TimingAssist { get => PlayerPrefs.GetInt("access.timingAssist", 0) == 1; set => PlayerPrefs.SetInt("access.timingAssist", value ? 1 : 0); }
    // Fades the commission, daily and clock panels while a fight is on (package U01).
    public static bool CombatFocusHud { get => PlayerPrefs.GetInt("access.focusHud", 1) == 1; set => PlayerPrefs.SetInt("access.focusHud", value ? 1 : 0); }
}

public static class Access
{
    public const float AssistParry = 1.6f, BufferSeconds = 0.2f, AssistBufferSeconds = 0.35f, SprintLetGo = 0.3f;

    public static float ParryScale => Save.TimingAssist ? AssistParry : 1f;
    public static float Buffer => Save.TimingAssist ? AssistBufferSeconds : BufferSeconds;

    static int frame = -1;
    static bool guardLatched, sprintLatched;
    static float idle;
    public static bool Guard { get; private set; }
    public static bool Sprint { get; private set; }

    // Read once a frame by the hero before movement and combat.
    public static void Tick(bool steering, bool town, float dt)
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        bool dash = GameInput.DashPressed;
        if (Save.GuardToggle)
        {
            if (GameInput.BlockPressed) guardLatched = !guardLatched;
            if (dash) guardLatched = false;
            Guard = guardLatched;
        }
        else { guardLatched = false; Guard = GameInput.BlockHeld; }

        if (Save.SprintToggle)
        {
            // In a fight every dash carries into a sprint; in town one press starts the run and another
            // ends it. Either way it lasts only while the hero keeps moving.
            if (dash) sprintLatched = !town || !sprintLatched;
            idle = steering ? 0f : idle + dt;
            if (idle > SprintLetGo) sprintLatched = false;
            Sprint = sprintLatched;
        }
        else { sprintLatched = false; Sprint = GameInput.DashHeld; }
    }

    // A deflect that runs out, a guard break, a revive or a change of setting drops any latch.
    public static void ReleaseGuard() { guardLatched = false; Guard = Save.GuardToggle ? false : Guard; }
    public static void Reset() { guardLatched = sprintLatched = false; Guard = Sprint = false; idle = 0f; frame = -1; }
}
