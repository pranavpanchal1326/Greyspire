using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

// First chapter route proof with the real Game/Player updates and controller
// inputs. Enemy cleanup is a disclosed fixture, not a balance/playability test.
public static partial class FeedbackTest
{
    static bool chapterAuto;
    static float pulseUntil, nextStoryPulse;
    static Enemy chapterTarget;
    static int chapterStrikes, chapterDashes;
    static float chapterHp, chapterJumpY;
    static float mapReadingTime;
    static Vector3 chapterStart;
#if ENABLE_INPUT_SYSTEM
    static void Pulse(GamepadButton button) { Pad(button); pulseUntil = Time.unscaledTime + 0.1f; }
#endif
    static void ChapterInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (!chapterAuto || G == null) return;
        if (pulseUntil > 0 && Time.unscaledTime >= pulseUntil) { Pad(); pulseUntil = 0; }
        if (G.State != Game.GameState.Story || Time.unscaledTime < nextStoryPulse || pulseUntil > 0) return;
        bool advance = G.Asking;
        var lines = (Line[])Get(G, "dialog"); int index = (int)Get(G, "dialogIndex");
        if (lines != null && index < lines.Length && lines[index].who == Story.Title) advance = true;
        Pulse(advance ? GamepadButton.South : GamepadButton.North);
        nextStoryPulse = Time.unscaledTime + 0.55f;
#endif
    }
    static void ClearChapterEnemies()
    {
        // Keep this separate from controller contact proof. It shortens five
        // complete encounter budgets without making a human difficulty claim.
        for (int pass = 0; pass < 6 && Enemy.All.Count > 0; pass++)
            foreach (var e in Enemy.All.ToArray()) e.TakeDamage(99999, e.transform.position + Vector3.back, 0);
        // Floor 5's Gatekeeper cannot be hurt until the hero walks into his arena (package C03):
        // the quiet floor opens his gate, walking in seals it and wakes him, then the same shortcut.
        if (G.Keeper != null && G.Keeper.Dormant)
        {
            Call(G, "TickArena");
            P.Teleport(Game.ArenaCentre + new Vector3(0f, 0.1f, -4f));
            Call(G, "TickArena");
            for (int pass = 0; pass < 6 && Enemy.All.Count > 0; pass++)
                foreach (var e in Enemy.All.ToArray()) e.TakeDamage(99999, e.transform.position + Vector3.back, 0);
        }
    }
    static void ApproachSpot(string name)
    {
        var spot = ((IList)Get(G, "spots")).Cast<object>().First(s => (string)Get(s, "name") == name);
        var point = (Vector3)Get(spot, "pos");
        P.Revive(point + Vector3.forward * 2.3f + Vector3.up * 0.1f, true); P.transform.rotation = Quaternion.Euler(0, 180, 0); G.Cam.Face(180); G.Cam.Snap();
    }
    static void Chapter()
    {
#if ENABLE_INPUT_SYSTEM
        Do(0.2f, () =>
        {
            Save.Wipe(); Save.SeenIntro = true; Save.Level = 20; Save.StatPoints = 0; Save.ReducedEffects(); Upgrades.Reset();
            Call(G, "AbandonStory"); State(Game.GameState.Hub); Set(G, "prologueDue", false); Call(G, "BuildHub");
            P.NewRun(); P.Revive(new Vector3(0, 0.1f, 22), true); P.transform.rotation = Quaternion.identity; G.Cam.Face(0); G.Cam.Snap();
            Juice.Reset(); Time.timeScale = 1; G.enabled = P.enabled = true; chapterAuto = true; Pad();
        });
        Do(0.4f, () => Pulse(GamepadButton.North));
        Do(0.3f, () => { Check((int)Get(G, "hubPanel") == 1, "first chapter: controller interaction opens the real tower gate"); Pulse(GamepadButton.South); });
        Do(2.6f, () =>
        {
            Check(G.Floor == 1 && G.State == Game.GameState.Playing && Enemy.All.Count > 0, "first chapter: controller entry and story skip reach the live first floor");
            foreach (var e in Enemy.All.ToArray()) { e.enabled = false; if (!Enemy.All.Contains(e)) Enemy.All.Add(e); }
            P.Revive(new Vector3(0, 0.1f, -36), true); P.transform.rotation = Quaternion.identity; G.Cam.Face(0); G.Cam.Snap();
            chapterTarget = Enemy.Create(Enemy.Kind.Slime, new Vector3(0, 0.1f, -33.7f), 0); chapterTarget.hp = chapterTarget.maxHp = 1000;
            chapterTarget.enabled = false; Enemy.All.Add(chapterTarget);
            chapterHp = chapterTarget.hp; chapterStrikes = P.strikes; chapterDashes = P.dashes;
        });
        Do(0.35f, () => Pulse(GamepadButton.RightStick));
        Do(0.2f, () => { Check(P.LockedTarget == chapterTarget, "first chapter: controller stick-click locks the visible threat"); Pulse(GamepadButton.West); });
        Do(0.45f, () => { Check(P.strikes == chapterStrikes + 1 && chapterTarget.hp < chapterHp, "first chapter: controller attack lands real accepted contact"); Pulse(GamepadButton.East); });
        Do(0.3f, () => { Check(P.dashes == chapterDashes + 1, "first chapter: controller dodge executes after contact"); });
        Do(0.45f, () => { chapterJumpY = P.transform.position.y; Pulse(GamepadButton.South); });
        Do(0.2f, () => { Check(P.transform.position.y > chapterJumpY + 0.25f, "first chapter: controller jump leaves supported terrain"); });
        Do(0.7f, () => { chapterStart = P.transform.position; InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }); });
        Do(0.4f, () => { Check(Vector3.Distance(P.transform.position, chapterStart) > 0.8f, "first chapter: controller stick moves the live CharacterController"); Pad(); ClearChapterEnemies(); });
        for (int floor = 1; floor <= 5; floor++)
        {
            int expected = floor;
            if (floor > 1)
                Do(2f, () => { Check(G.Floor == expected && G.State == Game.GameState.Playing && Enemy.All.Count > 0, "first chapter: controller stairs enter floor " + expected); ClearChapterEnemies(); });
            Do(3.6f, () =>
            {
                Check(Save.TowerFloor == expected && (bool)Get(G, "stairsOpen"), "first chapter: floor " + expected + " clear commits and opens its actual route");
                Check(FeedbackPolicy.Flash((float)Get(G, "flashA")) == 0 && Save.CameraMotion == 0, "first chapter: floor " + expected + " clear respects reduced effects");
            });
            if (floor == 4)
            {
                Do(0.15f, () => { ApproachSpot("A drained Painter"); });
                Do(0.3f, () => Pulse(GamepadButton.North));
                Do(4f, () => { Check(Save.RescueChoice("rhea") == 1 && !Save.RescuePending("rhea") && Save.Consequence("rescue:rhea:home"), "first chapter: controller handles both Timeline skips, required rescue choice and return"); });
            }
            if (floor == 5)
            {
                Do(1.1f, () =>
                {
                    Check(G.State == Game.GameState.Cards && Save.PendingGuardian != null && (float)Get(G, "cardsT") >= 0.5f, "first chapter: Guardian produces its durable boon offer after the reveal lock");
                    Check(UiNavigation.FocusedLabel == ((System.Collections.Generic.List<Upgrade>)Get(G, "cards"))[0].name, "funded Guardian reveal defaults to a boon rather than spending Sparks on a reroll");
                    Pulse(GamepadButton.South);
                });
                Do(0.3f, () => { Check(Save.GuardianPaid(0, 5) && Save.PendingGuardian == null && G.State == Game.GameState.Playing, "first chapter: controller consumes the Guardian offer and returns to the route"); ApproachSpot("The Way Down"); });
                Do(0.3f, () => Pulse(GamepadButton.North));
                Do(1.5f, () =>
                {
                    Check(G.InTown && G.State == Game.GameState.Hub && Save.TowerFloor == 5, "first chapter: controller returns to town with five clears and rescue intact");
                    Check(errors.Count == 0 && JourneyStore.Error == null, "live controller chapter completes without unexpected errors or save failure");
                    chapterAuto = false; Pad(); ScreenCapture.CaptureScreenshot("Logs/feedback-chapter.png");
                });
                Do(0.5f, () => Check(File.Exists("Logs/feedback-chapter.png"), "first chapter controller return capture produced"));
                Do(0.3f, () => { ApproachSpot("The Wilds"); Pad(GamepadButton.North); });
                Do(0.2f, () => Pad());
                Do(2.6f, () => { Check(G.InWilds && G.State == Game.GameState.Playing, "controller interaction reaches the live Wilds"); Pad(GamepadButton.Select); });
                Do(0.2f, () =>
                {
                    Check((bool)Get(G, "showMap") && !G.Roaming && Time.timeScale == 0, "controller map entry suspends both movement and the live fight");
                    Route("Wilds map");
                    mapReadingTime = (float)Get(G, "systemT");
                    chapterDashes = P.dashes; Pad(); ScreenCapture.CaptureScreenshot("Logs/feedback-map.png");
                });
                Do(0.3f, () => { Check(Math.Abs((float)Get(G, "systemT") - mapReadingTime) < 0.001f, "map preserves queued message reading time while it covers the HUD"); Pad(GamepadButton.East); });
                Do(0.2f, () =>
                {
                    Check(!(bool)Get(G, "showMap") && G.State == Game.GameState.Playing && !Juice.Paused && P.dashes == chapterDashes, "controller map back resumes without a stray dodge or pause menu");
                    Check(errors.Count == 0 && JourneyStore.Error == null, "controller chapter and Wilds controls leave runtime and save state healthy"); Pad();
                });
                Do(0.2f, () => typeof(JourneyStore).GetField("<Error>k__BackingField", Any).SetValue(null, "Isolated recovery-screen fixture"));
                Do(0.25f, () =>
                {
                    Route("save attention recovery");
                    Check(!G.Roaming && UiNavigation.FocusedLabel == "CLOSE GAME", "save attention suspends gameplay and exposes a controller-accessible close action"); Pad(GamepadButton.East);
                });
                Do(0.2f, () =>
                {
                    Check(G.State == Game.GameState.Playing && Time.timeScale == 0, "back input cannot reopen play or flush a failed journey");
                    typeof(JourneyStore).GetField("<Error>k__BackingField", Any).SetValue(null, null); Pad(); G.enabled = P.enabled = false; State(Game.GameState.Menu);
                    File.WriteAllText("Reports/controller-routes.csv", routes.ToString());
                    Check(errors.Count == 0 && JourneyStore.Error == null, "recovery-screen input produces no unexpected exception");
                });
            }
            else
            {
                Do(0.15f, () => ApproachSpot("Stairs"));
                Do(0.3f, () => Pulse(GamepadButton.North));
            }
        }
#endif
    }
}
