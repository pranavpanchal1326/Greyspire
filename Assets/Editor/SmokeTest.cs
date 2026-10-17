using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Automated play-through used from the command line:
//   Unity -projectPath . -executeMethod SmokeTest.Run
// Enters play mode, walks through every screen and saves screenshots to
// Logs/smoke, so the game can be reviewed without anyone at the keyboard.
// Besides the screens it takes studio portraits of every character, and
// freeze-frames of the hero mid-stride and mid-swing to check the animation.
// It starts from a wiped save (Tools/smoke.ps1 keeps the player's own save safe).
[InitializeOnLoad]
public static class SmokeTest
{
    const string Key = "SmokeTestStage";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const int PortraitLayer = 31;

    class Step
    {
        public float wait;          // seconds after the previous step
        public Func<bool> until;    // and then until this is true (or 25 seconds pass)
        public Action act;
    }

    static List<Step> steps;
    static int at;
    static float since;
    static bool immortal, brawl;
    static Vector3 drive;           // while non-zero, the hero is marched along at this velocity
    static Enemy downed, fallen;

    static PlayerController P => Game.I.Player;

    static SmokeTest()
    {
        if (SessionState.GetInt(Key, 0) > 0) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        Save.Wipe();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetInt(Key, 1);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.I == null) return;
        float now = Time.realtimeSinceStartup;
        if (steps == null)
        {
            // Play mode stops whenever the editor window is not in front unless this is set: a run left
            // in the background would stall while this script carried on against a frozen game.
            Application.runInBackground = true;
            steps = new List<Step>();
            Script();
            at = 0;
            since = now;
        }

        try
        {
            if (immortal)
            {
                // Nothing lands at all: a single blow can be worth more hearts than the hero has.
                P.hp = P.maxHp;
                PField("invuln").SetValue(P, 1f);
            }
            if (drive != Vector3.zero)
            {
                PField("planar").SetValue(P, drive);
                P.transform.rotation = Quaternion.LookRotation(drive);
            }
            // While fighting, cycle through the hero's moves so combat effects show up in the shots.
            if (brawl && Game.I.State == Game.GameState.Playing && Time.frameCount % 20 == 0)
            {
                int which = Time.frameCount / 20 % 6;
                ResetMove();
                if (which == 1) Move("StartHeavy", 0.8f);
                else if (which == 2) Move("StartKick");
                else if (which == 3) Move("StartStab", Vector3.zero);
                else if (which == 4) Move("StartBash");
                else if (which == 5) Move("StartThrow");
                else Move("StartAttack", Vector3.zero);
            }

            Step s = steps[at];
            float waited = now - since;
            if (waited < s.wait) return;
            if (s.until != null && !s.until() && waited < s.wait + 25f) return;
            s.act();
            at++;
            since = now;
            if (at < steps.Count) return;

            Debug.Log("SMOKE DONE");
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("SMOKE FAILED at step " + at + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    static void Do(float wait, Action act) => steps.Add(new Step { wait = wait, act = act });

    static void When(float wait, Func<bool> until, Action act) => steps.Add(new Step { wait = wait, until = until, act = act });

    // ------------------------------------------------------------ the play-through

    static void Script()
    {
        Do(2.5f, () => Shot("1_menu"));

        // A line of dialogue frames the hero in close-up: the best look at the character model.
        Do(0.3f, () => Say(Story.Meet("Bram the Smith")));
        Do(2.5f, () =>
        {
            Shot("1a_hero_closeup");
            Portrait("p_hero_front", P.transform, 0f, 2.7f, 1f);
            Portrait("p_hero_back", P.transform, 180f, 2.7f, 1f);
            Portrait("p_hero_face", P.transform, 20f, 1.1f, 1.55f);
        });
        Do(1.5f, () =>
        {
            DropDialog();
            SetState(Game.GameState.Menu);
        });

        // Every hero, front on.
        for (int i = 1; i < Heroes.All.Length; i++)
        {
            int hero = i;
            Do(0.3f, () => P.SetHero(hero));
            Do(0.8f, () => Portrait("p_hero" + hero + "_" + Heroes.All[hero].name.Replace(" ", "_"), P.transform, 15f, 2.7f, 1f));
        }
        Do(0.3f, () => P.SetHero(0));

        // The tower, on a fresh save: the chapter card, the first fight and the awakening, then a floor to explore.
        Do(0.5f, () =>
        {
            immortal = true;
            Call("StartRun");
        });
        Do(2.5f, () => Shot("2_story_chapter"));
        Do(0.3f, () => ShowLine(1));
        Do(1.6f, () => Shot("2_story_speech"));
        Do(0.5f, SkipDialog);
        Do(2.5f, () =>
        {
            Shot("3_first_fight");
            brawl = true;
        });
        When(0.5f, () => Game.I.State == Game.GameState.Story, () =>
        {
            if (Game.I.State != Game.GameState.Story) Debug.Log("SMOKE awakening never triggered");
        });
        Do(0.4f, () => ShowLine(2));
        Do(1.6f, () =>
        {
            brawl = false;
            if (Game.I.State != Game.GameState.Story) return;
            Shot("3b_awakening_system");
            SkipDialog();
        });
        Do(2.5f, () => Shot("t1_floor_start"));
        Do(0.3f, () =>
        {
            // Walk up to the nearest pack: they should wake and come.
            var packs = (List<Vector2>)GameField("packSpots").GetValue(Game.I);
            Debug.Log("SMOKE floor packs=" + packs.Count + " enemies=" + Enemy.All.Count);
            if (packs.Count > 0) P.Teleport(new Vector3(packs[0].x, 0.1f, packs[0].y - 9f));
        });
        Do(2.5f, () => Shot("t2_pack"));
        Do(0.3f, () =>
        {
            P.Teleport(new Vector3(0f, 0.1f, 28f));
            CamField("yaw").SetValue(Game.I.Cam, 0f);
        });
        Do(1.6f, () => Shot("t3_stairs_sealed"));
        Do(0.3f, KillAll);
        Do(1.2f, () => Shot("t4_floor_cleared"));
        Do(2.2f, () =>
        {
            // The clearing scene: Lumi and the System have something to say.
            Debug.Log("SMOKE floor 1 cleared: state=" + Game.I.State + " towerFloor=" + Save.TowerFloor + " level=" + Save.Level + " points=" + Save.StatPoints);
            Settle();
        });
        Do(1f, () =>
        {
            Shot("t4_stairs_open");
            Debug.Log("SMOKE stairs open=" + GameField("stairsOpen").GetValue(Game.I) + " enemies=" + Enemy.All.Count);
        });
        Do(0.5f, () => Call("ClimbStairs"));
        Do(2.5f, () =>
        {
            Debug.Log("SMOKE floor=" + Game.I.Floor + " state=" + Game.I.State);
            if (Game.I.State == Game.GameState.Story) ShowLine(2);
        });
        Do(1.4f, () =>
        {
            Shot("t5_floor_two_system");
            Settle();
        });
        Do(2.2f, () =>
        {
            Shot("t5_floor_two");
            Debug.Log("SMOKE floor=" + Game.I.Floor + " enemies=" + Enemy.All.Count + " state=" + Game.I.State);
        });

        // A drained Painter on the fourth floor: found, the floor cleared, painted back.
        Do(0.5f, () =>
        {
            JourneyStore.SetInt("towerFloor", 3);
            JourneyStore.SetInt("storyFloor", 3);
            Call("StartRun");
        });
        Do(2.5f, () => Shot("s0_statue_story"));
        Do(0.3f, Settle);
        Do(1f, () =>
        {
            P.Teleport(new Vector3(9.5f, 0.1f, 7f));
            CamField("yaw").SetValue(Game.I.Cam, 38f);
            CamField("pitch").SetValue(Game.I.Cam, 12f);
        });
        Do(2f, () => Shot("s1_statue"));
        Do(0.3f, KillAll);
        Do(3.4f, Settle);
        Do(0.5f, () => UseSpot("A drained Painter"));
        Do(2.2f, () =>
        {
            Shot("s2_rescue");
            Debug.Log("SMOKE rescue: state=" + Game.I.State + " rescued=" + Save.RescuedMask);
            Settle();
        });

        // A Guardian floor: the warning, the Guardian itself, and the boon it leaves.
        Do(0.5f, () =>
        {
            JourneyStore.SetInt("towerFloor", 4);
            JourneyStore.SetInt("storyFloor", 4);
            Call("StartRun");
        });
        Do(2.5f, () => Shot("g1_guardian_warning"));
        Do(0.3f, Settle);
        Do(1.2f, () => Shot("g2_guardian_banner"));
        Do(0.3f, () =>
        {
            P.Teleport(new Vector3(0f, 0.1f, 17f));
            CamField("yaw").SetValue(Game.I.Cam, 0f);
            CamField("pitch").SetValue(Game.I.Cam, 14f);
        });
        Do(2.6f, () =>
        {
            Shot("g3_guardian");
            Debug.Log("SMOKE floor=" + Game.I.Floor + " guardian floor, enemies=" + Enemy.All.Count);
        });
        Do(0.3f, KillAll);
        Do(3.2f, () =>
        {
            Debug.Log("SMOKE floor 5 cleared: state=" + Game.I.State);
            if (Game.I.State == Game.GameState.Story) SkipDialog();
        });
        Do(1.5f, () =>
        {
            Shot("5_cards");
            Debug.Log("SMOKE boon offered: state=" + Game.I.State);
            Settle();
        });
        Do(0.6f, () => Debug.Log("SMOKE state=" + Game.I.State + " boons=" + Upgrades.Owned + " hearts=" + P.maxHp));

        // Things in flight: a bolt of gloom, a thrown knife and the hero's blade, crossing in front of the camera.
        Do(0.5f, () =>
        {
            Transform c = Game.I.Cam.transform;
            Vector3 fwd = new Vector3(c.forward.x, 0f, c.forward.z).normalized, right = c.right;
            Vector3 o = P.transform.position + Vector3.up * 1.3f - fwd * 2.2f - right * 2.6f;
            Projectile.Fire(o + Vector3.up * 0.8f, right, 2.2f);
            Projectile.Fire(o + Vector3.up * 0.2f - right * 0.4f, right, 2.2f, true);
            Projectile.Throw(o - Vector3.up * 0.4f, right, 2.2f, 0f);
        });
        Do(0.35f, () => Shot("fx_projectiles"));
        // A burst, held: the clock is stopped just after it goes off so the picture catches it.
        Do(1.2f, () =>
        {
            Transform c = Game.I.Cam.transform;
            Fx.Burst(P.transform.position + Vector3.up * 1.6f - new Vector3(c.forward.x, 0f, c.forward.z).normalized * 1.5f + c.right * 1.4f, new Color(0.78f, 0.36f, 0.95f), 12, 4f);
        });
        Do(0.1f, () =>
        {
            Juice.Paused = true;
            Shot("fx_burst");
        });
        Do(0.9f, () => Juice.Paused = false);

        // A mixed crowd, so every kind of tower monster is on the floor to be photographed.
        Do(0.5f, () =>
        {
            GameField("satTarget").SetValue(Game.I, 1f);
            P.Teleport(new Vector3(0f, 0.1f, -22f));
            Vector3 p = P.transform.position;
            for (int i = 0; i < 17; i++)
            {
                Enemy.Kind kind = i < 10 ? Enemy.Kind.Minion : i < 16 ? Enemy.Kind.Spitter : Enemy.Kind.Brute;
                Enemy.Create(kind, p + Quaternion.Euler(0, i * 21f, 0) * Vector3.forward * 7f, 0);
            }
        });
        Do(3f, () =>
        {
            var seen = new HashSet<string>();
            string roll = "";
            foreach (Enemy e in Enemy.All)
            {
                string id = e.kind + "_" + e.transform.GetChild(0).name;
                roll += " " + id;
                var ca = e.GetComponent<CharAnim>();
                if (ca == null || !ca.Human || !seen.Add(id)) continue;
                Portrait("p_enemy_" + id, e.transform, 0f, 1.6f + e.height, e.height * 0.5f);
            }
            Debug.Log("SMOKE crowd:" + roll);
        });

        // The slimes, the sapper and Mop, close up.
        Do(0.2f, () =>
        {
            Enemy.Kind[] kinds = { Enemy.Kind.Slime, Enemy.Kind.BigSlime, Enemy.Kind.Bomber };
            for (int i = 0; i < kinds.Length; i++)
            {
                Enemy e = Enemy.Create(kinds[i], P.transform.position + new Vector3(-9f - i * 3f, 0f, -6f), 0);
                e.aggro = 0f;
                e.name = "Sample_" + kinds[i];
            }
        });
        Do(1.4f, () =>
        {
            foreach (Enemy e in Enemy.All)
                if (e.name.StartsWith("Sample_")) Portrait("p_enemy_" + e.kind, e.transform, 25f, 2f + e.height * 1.6f, e.height * 0.45f);
            GameObject mop = GameObject.Find("Mop");
            if (mop != null) Portrait("p_mop", mop.transform, 25f, 2f, 0.25f);
        });

        // A monster killed: falling, then fading where it lies.
        Do(0.2f, () =>
        {
            foreach (Enemy e in Enemy.All)
            {
                if (e.kind != Enemy.Kind.Minion) continue;
                fallen = e;
                e.TakeDamage(99999f, e.transform.position + e.transform.forward, 0f);
                break;
            }
        });
        Do(0.8f, () => { if (fallen != null) Portrait("m_enemy_dying", fallen.transform, 40f, 3.6f, 0.7f); });
        Do(1.45f, () => { if (fallen != null) Portrait("m_enemy_fading", fallen.transform, 40f, 3.6f, 0.5f); });

        // A monster knocked flat, then getting back up.
        Do(0.2f, () =>
        {
            foreach (Enemy e in Enemy.All)
            {
                if (e.kind != Enemy.Kind.Minion) continue;
                downed = e;
                e.Stagger(1.6f);
                break;
            }
        });
        Do(0.55f, () => { if (downed != null) Portrait("m_enemy_down", downed.transform, 40f, 3.6f, 0.7f); });
        Do(0.7f, () => { if (downed != null) Portrait("m_enemy_rising", downed.transform, 40f, 3.6f, 0.7f); });
        Do(0.2f, () => brawl = true);
        Do(2.5f, () => Shot("4_combat"));
        Do(1.5f, () =>
        {
            Shot("4_combat_2");
            Debug.Log("SMOKE state=" + Game.I.State + " enemies=" + Enemy.All.Count + " hp=" + P.hp + " level=" + Save.Level + " floor=" + Game.I.Floor);
            brawl = false;
            Settle();
        });

        // Freeze-frames of each move, a beat after it starts.
        Freeze("m_combo1", () => Swing(0));
        Freeze("m_combo2", () => Swing(1));
        Freeze("m_combo3", () => Swing(2), 0.22f);
        Freeze("m_heavy", () => Move("StartHeavy", 0.5f), 0.26f);
        Freeze("m_kick", () => Move("StartKick"));
        Freeze("m_stab", () => Move("StartStab", Vector3.zero));
        Freeze("m_block", () => Move("StartBlock"), 0.3f);
        Freeze("m_bash", () => Move("StartBash"));
        Freeze("m_throw", () => Move("StartThrow"), 0.2f);
        Freeze("m_slide", () => Move("StartSlide"), 0.3f);
        Freeze("m_dash", () => Move("StartDash", Vector3.zero), 0.1f);
        Do(0.4f, ResetMove);

        // A swing at a full run: the legs should still be running under it. Then the arm coming back to guard.
        Do(0.3f, () => drive = new Vector3(0f, 0f, 6.8f));
        Do(0.7f, () =>
        {
            ResetMove();
            PField("comboStep").SetValue(P, 0);
            Move("StartAttack", Vector3.forward);
        });
        Do(0.14f, () => Portrait("m_run_swing", P.transform, 60f, 3.4f, 1f));
        Do(0.2f, () => Portrait("m_run_recover", P.transform, 60f, 3.4f, 1f));
        Do(0.1f, () =>
        {
            drive = Vector3.zero;
            P.Teleport(new Vector3(0f, 0.1f, -22f));
        });
        // The cut drawn in the air, as the game shows it.
        Do(0.5f, () =>
        {
            ResetMove();
            Swing(0);
        });
        Do(0.13f, () => Shot("fx_slash"));
        Do(0.6f, () =>
        {
            ResetMove();
            Swing(2);
        });
        Do(0.2f, () => Shot("fx_spin"));

        // The screen moments: an impact frame, speed lines, the cut-in.
        Do(0.4f, () =>
        {
            Settle();
            Game.I.Impact(1f);
        });
        Do(0.02f, () => Shot("fx_impact"));
        Do(0.8f, () =>
        {
            Game.I.Lines(1f);
            Game.I.CutIn("COLOUR BURST", UI.Gold);
        });
        Do(0.18f, () => Shot("fx_cutin"));

        // Pause, and the Status window.
        Do(1.2f, () => Invoke(Game.I, "OpenPause", 0));
        Do(0.7f, () => Shot("6_pause"));
        Do(0.3f, () => GameField("pauseTab").SetValue(Game.I, 1));
        Do(0.7f, () => Shot("6_status"));
        Do(0.3f, () => Invoke(Game.I, "SpendStat", 0));
        Do(0.3f, () =>
        {
            Debug.Log("SMOKE status: points=" + Save.StatPoints + " str=" + Save.Stat(0) + " damage=" + P.stats.damage);
            Call("ClosePause");
            immortal = false;
            Invoke(Game.I, "EndRun", false, true);
        });
        Do(2.5f, () => Shot("7_results"));

        // Town at mid-morning: the arrival, the prologue, the square, its people, the boards.
        Do(0.5f, () =>
        {
            JourneyStore.SetInt("bestFloor", 25);
            JourneyStore.SetInt("rescued", 31);
            SetHour(10f);
            Call("EnterHub");
        });
        Do(1.6f, () => Shot("1b0_town_arrival"));
        Do(2.2f, () => Shot("1b0_town_arrival_2"));
        When(0.5f, () => !Game.I.Intro, () => { });
        When(0.5f, () => Game.I.State == Game.GameState.Story, () => { });
        Do(1.5f, () => Shot("0_prologue_card"));
        Do(0.3f, () => { if (Game.I.State == Game.GameState.Story) ShowLine(1); });
        Do(1.5f, () => Shot("0_prologue_caption"));
        Do(0.3f, Settle);
        Do(1.5f, () => Shot("1b_town"));
        // The buildings: up the road to the tower, then the houses either side of the square.
        Do(0.3f, () => View(new Vector3(0f, 0.1f, 15f), 0f, -9f));
        Do(1.6f, () => Shot("1h_gloom_tower"));
        Do(0.3f, () => View(new Vector3(4f, 0.1f, 2f), -72f, 8f));
        Do(1.6f, () => Shot("1h_west_side"));
        Do(0.3f, () => View(new Vector3(-4f, 0.1f, 2f), 78f, 8f));
        Do(1.6f, () => Shot("1h_east_side"));
        Do(0.3f, () => View(new Vector3(0f, 0.1f, 6f), 180f, 8f));
        Do(1.6f, () => Shot("1h_south_side"));
        Do(0.3f, () => View(new Vector3(0f, 0.1f, -9f), 0f, 22f));
        // The whole town in view, measured over three seconds.
        Do(1f, MarkFps);
        Do(3f, () => Debug.Log("SMOKE town fps (3 s average): " + Fps()));
        Do(0.2f, TownPortraits);
        Do(0.3f, () => UseSpot("Rhea Vane"));
        Do(2.2f, () =>
        {
            Shot("1f_rescued_talk");
            Settle();
        });
        Do(0.5f, () => Invoke(Game.I, "OpenBoard", "Nix of the Guild"));
        Do(1f, () => Shot("1d_guild_board"));
        Do(0.3f, () => Invoke(Game.I, "AcceptMission", 0));
        Do(1f, () => Shot("1d_guild_accepted"));
        Do(0.3f, () => Invoke(Game.I, "OpenBoard", "Lady Aurelia Vane"));
        Do(1f, () => Shot("1e_house_vane"));
        Do(0.3f, () => Invoke(Game.I, "OpenHubPanel", 0));
        Do(0.2f, () => drive = new Vector3(0f, 0f, 1.5f));
        Do(1.4f, () => Portrait("m_walk", P.transform, 90f, 3f, 0.95f));
        Do(0.1f, () => drive = new Vector3(-3.7f, 0f, 0f));
        Do(1f, () => Portrait("m_jog", P.transform, 90f, 3f, 0.95f));
        Do(0.1f, () => drive = new Vector3(6.8f, 0f, 0f));
        Do(0.6f, () => Portrait("m_run", P.transform, 90f, 3f, 0.95f));
        Do(0.1f, () =>
        {
            drive = Vector3.zero;
            P.Teleport(new Vector3(0f, 0.1f, -9f));
            P.transform.rotation = Quaternion.identity;
        });
        Do(1f, () =>
        {
            // Weapons put away: sword at the hip, shield on the back.
            Portrait("p_hero_town_back", P.transform, 180f, 2.7f, 1f);
            Portrait("p_hero_town_side", P.transform, -90f, 2.7f, 1f);
            Shot("1b1_town_walkers");
        });

        // The day turning: late afternoon light, the lamps at dusk, the square at night, the small hours.
        Do(0.3f, () => SetHour(17.3f, true));
        Do(1.4f, () => Shot("1b2_town_sunset"));
        Do(0.2f, LookAtSun);
        Do(1.5f, () => Shot("1b2_town_sunset_shafts"));
        Do(0.2f, () =>
        {
            CamField("yaw").SetValue(Game.I.Cam, 0f);
            CamField("pitch").SetValue(Game.I.Cam, 22f);
        });
        Do(0.3f, () => SetHour(21.5f, true));
        Do(0.3f, () => View(new Vector3(4f, 0.1f, 2f), -72f, 8f));
        Do(1.6f, () => Shot("1h_west_side_night"));
        Do(0.3f, () => View(new Vector3(0f, 0.1f, -9f), 0f, 22f));
        Do(1.6f, () =>
        {
            Shot("1b3_town_night");
            Debug.Log("SMOKE town 21:30 residents out=" + ResidentsOut());
        });
        Do(0.3f, () => SetHour(2f, true));
        Do(1.6f, () =>
        {
            Shot("1b4_town_small_hours");
            Debug.Log("SMOKE town 02:00 residents out=" + ResidentsOut());
        });
        Do(0.3f, () => UseSpot("Bram the Smith"));
        Do(1f, () => Shot("1b5_smith_away"));
        Do(1.8f, () => Invoke(Game.I, "OpenHubPanel", 4));
        Do(0.7f, () => Shot("1g_rest"));
        Do(0.3f, () => Invoke(Game.I, "Rest", 6f));
        Do(2.6f, () =>
        {
            Shot("1g_morning_after");
            Debug.Log("SMOKE rested: day=" + Save.Day + " hour=" + Game.I.Hour.ToString("0.0") + " residents out=" + ResidentsOut());
        });
        Do(0.3f, () => SetHour(11f, true));
        Do(0.3f, () => Invoke(Game.I, "OpenHubPanel", 1));
        Do(0.7f, () => Shot("1c_gate"));

        // The Wilds: the arrival, then each region photographed from its edge, looking in.
        Do(0.3f, () =>
        {
            Invoke(Game.I, "OpenHubPanel", 0);
            Call("EnterWilds");
            immortal = true;
        });
        Do(2f, () => Shot("w00_arrival"));
        Do(0.1f, () => Debug.Log("SMOKE wilds arrival fps: " + (1f / Time.smoothDeltaTime).ToString("0")));
        Do(2.4f, () => Shot("w00_arrival_2"));
        When(0.5f, () => !Game.I.Intro, () => { });
        const int Regions = 10;
        for (int i = 0; i < Regions; i++)
        {
            int region = i;
            Do(i == 0 ? 1.5f : 0.4f, () =>
            {
                if (region >= Game.I.RegionCount) return;
                P.Teleport(Game.I.RegionSpot(region, out float yaw));
                CamField("yaw").SetValue(Game.I.Cam, yaw);
                CamField("pitch").SetValue(Game.I.Cam, 16f);
            });
            Do(4f, () =>
            {
                if (region >= Game.I.RegionCount) return;
                Shot("w" + (region + 1).ToString("00"));
                Debug.Log("SMOKE wilds region " + region + " enemies=" + Enemy.All.Count + " fps=" + (1f / Time.smoothDeltaTime).ToString("0") + " level=" + Save.Level);
            });
        }
        // What the frame costs, measured in one place (the woods): as shipped, then without the
        // ground's mottling, then without the finishing pass, then without the game's update and HUD.
        Do(0.4f, () =>
        {
            P.Teleport(Game.I.RegionSpot(1, out float yaw));
            CamField("yaw").SetValue(Game.I.Cam, yaw);
        });
        Do(2.5f, MarkFps);
        Do(3f, () =>
        {
            Debug.Log("SMOKE fps as shipped: " + Fps());
            SetGround(0f);
            MarkFps();
        });
        Do(3f, () =>
        {
            Debug.Log("SMOKE fps without ground mottling: " + Fps());
            SetGround(1f);
            Game.I.Cam.GetComponent<PostFx>().enabled = false;
            MarkFps();
        });
        Do(3f, () =>
        {
            Debug.Log("SMOKE fps without the finishing pass: " + Fps());
            Game.I.Cam.GetComponent<PostFx>().enabled = true;
            Game.I.enabled = false;
            MarkFps();
        });
        Do(3f, () =>
        {
            Debug.Log("SMOKE fps with the game's update and HUD off: " + Fps());
            Game.I.enabled = true;
        });

        Do(0.3f, () => GameField("showMap").SetValue(Game.I, true));
        Do(1f, () => Shot("w_map"));
        Do(0.3f, () => GameField("showMap").SetValue(Game.I, false));

        // The bandits, close up: one of each kind found in the camps of the autumn vale.
        Do(0.3f, () => P.Teleport(Game.I.RegionSpot(6, out float _)));
        Do(1.2f, () =>
        {
            var seen = new HashSet<string>();
            foreach (Enemy e in Enemy.All)
            {
                string id = e.kind + "_" + e.transform.GetChild(0).name;
                if (!e.IsBandit || !e.transform.GetChild(0).gameObject.activeSelf) continue;
                var ca = e.GetComponent<CharAnim>();
                if (ca == null || !ca.Human || !seen.Add(id)) continue;
                Portrait("p_bandit_" + id, e.transform, 0f, 1.6f + e.height, e.height * 0.5f);
                if (seen.Count == 1) Portrait("p_bandit_camp", e.transform, 30f, 11f, 1.2f, false);
            }
        });

        // Late light through the trees, looking toward the sun; then the Wilds after dark.
        Do(0.3f, () => SetHour(16.6f));
        Do(1.5f, LookAtSun);
        Do(1.5f, () => Shot("w_sunset_shafts"));
        Do(0.3f, () => SetHour(23f));
        Do(1.6f, () => Shot("w_night"));
        Do(0.3f, () => SetHour(10f));

        // The other screens.
        Do(0.5f, () =>
        {
            immortal = false;
            SetState(Game.GameState.Codex);
        });
        Do(0.7f, () => Shot("9_codex"));
        Do(0.3f, () => Say(Story.LevelIntro[2]));
        Do(2f, () => Shot("10_chapter"));
        Do(0.3f, () =>
        {
            Enemy.Create(Enemy.Kind.Boss, new Vector3(0, 0.1f, 11f), 2);
            Say(Story.BossIntro);
        });
        Do(3f, () =>
        {
            Shot("11_boss_intro");
            foreach (Enemy e in Enemy.All)
                if (e.IsBoss) Portrait("p_enemy_Boss", e.transform, 0f, 6f, 1.9f);
        });
        Do(0.3f, () =>
        {
            GameField("dialog").SetValue(Game.I, null);
            GameField("endingChoice").SetValue(Game.I, true);
        });
        Do(0.7f, () => Shot("12_choice"));
        Do(0.5f, () => SetState(Game.GameState.Shrine));
        Do(0.7f, () => Shot("8_shrine"));

        // Back to the title screen, now with a journey to continue.
        Do(0.5f, () =>
        {
            GameField("endingChoice").SetValue(Game.I, false);
            Call("ToMenu");
        });
        Do(2.5f, () => Shot("1_menu_continue"));
        Do(0.3f, () => GameField("confirmNew").SetValue(Game.I, true));
        Do(0.7f, () => Shot("1_menu_newgame"));
        Do(2.5f, () => { });
    }

    // Starts a move from a clean slate, then photographs the hero a moment later.
    static void Freeze(string name, Action start, float after = 0.16f)
    {
        Do(0.45f, () =>
        {
            ResetMove();
            start();
        });
        Do(after, () => Portrait(name, P.transform, 40f, 3.2f, 1f));
    }

    static void Swing(int comboStep)
    {
        PField("comboStep").SetValue(P, comboStep);
        Move("StartAttack", Vector3.zero);
    }

    static void KillAll()
    {
        // More than once: a big slime leaves two small ones behind.
        for (int pass = 0; pass < 3 && Enemy.All.Count > 0; pass++)
            foreach (Enemy e in new List<Enemy>(Enemy.All)) e.TakeDamage(99999f, e.transform.position + Vector3.back, 0f);
    }

    // Gets past whatever the game is waiting on: a conversation, a boon to choose.
    static void Settle()
    {
        for (int i = 0; i < 12; i++)
        {
            // A rescue scene: skip its Timelines and take the first answer.
            if (Game.I.InCutscene && Game.I.Scene != null) Game.I.Scene.Skip();
            else if (Game.I.Asking) Game.I.Answer(0);
            else if (Game.I.State == Game.GameState.Story) SkipDialog();
            else if (Game.I.State == Game.GameState.Cards)
            {
                GameField("cardsT").SetValue(Game.I, 1f);
                Invoke(Game.I, "PickCard", 0);
            }
            else break;
        }
    }

    // Puts a given line of the conversation in progress on screen, fully typed out.
    static void ShowLine(int index)
    {
        var lines = (Line[])GameField("dialog").GetValue(Game.I);
        if (lines == null || index >= lines.Length) return;
        GameField("dialogIndex").SetValue(Game.I, index);
        GameField("dialogChars").SetValue(Game.I, 999f);
        Invoke(Game.I, "ApplyShot", lines[index].shot);
    }

    // Presses E on a named place or person, wherever the hero happens to be.
    static void UseSpot(string name)
    {
        foreach (object spot in (IEnumerable)GameField("spots").GetValue(Game.I))
        {
            Type t = spot.GetType();
            if ((string)t.GetField("name", Any).GetValue(spot) != name) continue;
            ((Action)t.GetField("act", Any).GetValue(spot)).Invoke();
            return;
        }
        Debug.Log("SMOKE no such spot: " + name);
    }

    // Moves the world's clock. In town, everyone is put where that hour finds them.
    static void SetHour(float hour, bool snap = false)
    {
        GameField("clockHour").SetValue(Game.I, hour);
        GameField("dayT").SetValue(Game.I, hour / 24f);
        if (snap) Call("SnapResidents");
    }

    // Stands the hero somewhere and points the camera.
    static void View(Vector3 at, float yaw, float pitch)
    {
        P.Teleport(at);
        P.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        CamField("yaw").SetValue(Game.I.Cam, yaw);
        CamField("pitch").SetValue(Game.I.Cam, pitch);
    }

    static int fpsFrame;
    static float fpsTime;

    static void MarkFps()
    {
        fpsFrame = Time.frameCount;
        fpsTime = Time.realtimeSinceStartup;
    }

    static string Fps() => ((Time.frameCount - fpsFrame) / Mathf.Max(0.01f, Time.realtimeSinceStartup - fpsTime)).ToString("0");

    // Switches the ground shader's mottling and fraying on or off everywhere.
    static void SetGround(float on)
    {
        var block = new MaterialPropertyBlock();
        foreach (Transform t in Game.I.LevelRoot)
        {
            if (t.name != "Ground") continue;
            var r = t.GetComponent<Renderer>();
            r.GetPropertyBlock(block);
            block.SetFloat("_Ground", on);
            r.SetPropertyBlock(block);
        }
    }

    static int ResidentsOut()
    {
        int n = 0;
        foreach (Resident r in UnityEngine.Object.FindObjectsByType<Resident>()) if (!r.Indoors) n++;
        return n;
    }

    // Turns the camera to face the sun (or moon), low enough to have the horizon in frame.
    static void LookAtSun()
    {
        Vector3 toSun = -UnityEngine.Object.FindAnyObjectByType<Light>().transform.forward;
        CamField("yaw").SetValue(Game.I.Cam, Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg);
        CamField("pitch").SetValue(Game.I.Cam, 2f);
    }

    // Close-ups of the named townspeople, and full-length shots of everyone else.
    static void TownPortraits()
    {
        var count = new Dictionary<string, int>();
        foreach (Transform t in Game.I.LevelRoot)
        {
            if (t.GetComponent<CharAnim>() == null || t.childCount == 0 || !t.GetChild(0).gameObject.activeSelf) continue;
            count.TryGetValue(t.name, out int n);
            count[t.name] = n + 1;
            if (n >= 3) continue;
            string id = "p_town_" + t.name.Replace(" ", "_") + (n > 0 ? "_" + (n + 1) : "");
            bool named = t.name.Contains(" ");
            if (named) Portrait(id, t, 15f, 1.2f, 1.55f);
            else Portrait(id, t, 25f, 3f, 0.9f);
            // The benches: one shot with the furniture in it, to check people sit on it and not in it.
            if (t.name == "Seated" && n == 0) Portrait("p_town_bench", t, 35f, 5f, 0.8f, false);
        }
    }

    // ------------------------------------------------------------ reaching into the game

    static FieldInfo GameField(string name) => typeof(Game).GetField(name, Any);

    static FieldInfo PField(string name) => typeof(PlayerController).GetField(name, Any);

    static FieldInfo CamField(string name) => typeof(ThirdPersonCamera).GetField(name, Any);

    static void Call(string method) => typeof(Game).GetMethod(method, Any).Invoke(Game.I, null);

    static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Any).Invoke(target, args);

    static void SetState(Game.GameState state) => Invoke(Game.I, "SetState", state);

    static void Say(Line[] lines) => Invoke(Game.I, "Say", lines, (Action)(() => { }));

    static void Move(string method, params object[] args) => Invoke(P, method, args);

    static void DropDialog()
    {
        GameField("dialog").SetValue(Game.I, null);
        GameField("dialogDone").SetValue(Game.I, null);
    }

    static void SkipDialog()
    {
        var done = (Action)GameField("dialogDone").GetValue(Game.I);
        DropDialog();
        Game.I.Cam.EndCine();
        done?.Invoke();
    }

    // Puts the hero back to neutral: no move in progress, nothing on cooldown.
    static void ResetMove()
    {
        Type pc = typeof(PlayerController);
        PField("move").SetValue(P, Enum.ToObject(pc.GetNestedType("Move", Any), 0));
        PField("attackT").SetValue(P, -1f);
        PField("dashT").SetValue(P, 0f);
        PField("dashCd").SetValue(P, 0f);
        PField("bashCd").SetValue(P, 0f);
        PField("throwCd").SetValue(P, 0f);
        var clips = P.GetComponent<CharAnim>();
        if (clips != null) clips.Release();
    }

    // ------------------------------------------------------------ pictures

    // A studio shot of one character from a camera of its own. yaw 0 looks at the side the
    // character is facing; 180 looks at its back. alone leaves everything else out of the
    // picture, so scenery can never stand in front of the subject.
    static void Portrait(string name, Transform who, float yaw, float distance, float height, bool alone = true)
    {
        var layers = new List<(GameObject, int)>();
        if (alone)
        {
            foreach (Transform t in who.GetComponentsInChildren<Transform>(true))
            {
                layers.Add((t.gameObject, t.gameObject.layer));
                t.gameObject.layer = PortraitLayer;
            }
        }

        var go = new GameObject("PortraitCam");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 40f;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.55f, 0.6f, 0.7f);
        if (alone) c.cullingMask = 1 << PortraitLayer;
        Vector3 focus = who.position + Vector3.up * height;
        go.transform.position = focus + Quaternion.Euler(0f, who.eulerAngles.y + yaw, 0f) * Vector3.forward * distance;
        go.transform.LookAt(focus);
        var rt = new RenderTexture(800, 800, 24);
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(800, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        c.targetTexture = null;
        Directory.CreateDirectory("Logs/smoke");
        File.WriteAllBytes("Logs/smoke/" + name + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(go);
        rt.Release();

        foreach ((GameObject obj, int layer) in layers) obj.layer = layer;
    }

    // Game-view screenshot, UI included (needs the editor window, not batch mode).
    static void Shot(string name)
    {
        Directory.CreateDirectory("Logs/smoke");
        ScreenCapture.CaptureScreenshot("Logs/smoke/" + name + ".png");
        Debug.Log("SMOKE captured " + name);
    }
}
