using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;

// Automated run of the rescue scenes, from the command line:
//   Tools\storytest.ps1        (Unity -projectPath . -executeMethod StoryTest.Run)
// Plays both branches: Rhea on floor 4 is asked what she saw (Rescue, Tell, Depart), Dorn on
// floor 9 is sent home with the first cutscene skipped. Pictures go to Logs/story: the game
// view at each beat, and the Timeline window while the scene is live.
[InitializeOnLoad]
public static class StoryTest
{
    const string Key = "StoryTestStage";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    class Step { public float wait; public Action act; }

    static List<Step> steps;
    static int at;
    static float since;
    static EditorWindow timelineWindow, animatorWindow;

    static PlayerController P => Game.I.Player;

    static StoryTest()
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
            Application.runInBackground = true;     // keep playing when the editor window is not in front
            steps = new List<Step>();
            Script();
            at = 0;
            since = now;
        }
        try
        {
            P.hp = P.maxHp;
            typeof(PlayerController).GetField("invuln", Any).SetValue(P, 1f);
            Step s = steps[at];
            if (now - since < s.wait) return;
            s.act();
            at++;
            since = now;
            if (at < steps.Count) return;

            Debug.Log("STORY DONE");
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("STORY FAILED at step " + at + ": " + e);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    static void Do(float wait, Action act) => steps.Add(new Step { wait = wait, act = act });

    static void Script()
    {
        // ---- Branch B: Rhea Vane, floor 4, "What did you see?"
        Do(2.5f, () => Climb(3));
        Do(2.5f, Settle);
        Do(1f, Settle);
        Do(0.5f, Approach);
        Do(1.8f, () => { Shot("a1_stone"); Report("stone"); });
        Do(1.6f, Smoke("KillAll"));
        Do(3.4f, Settle);
        Do(0.5f, () => UseSpot("A drained Painter"));
        Do(0.15f, OpenWindows);
        Do(0.15f, () => { Shot("c1_rescue_wide"); Report("rescue 0.6"); });
        Do(1.1f, () => { Shot("c2_rescue_thaw"); Report("rescue 1.7"); });
        Do(1.1f, () =>
        {
            Grab(timelineWindow, "e1_timeline_rescue");
            Print("Animator", "e2_animator_break");
            Report("rescue 2.8");
        });
        Do(0.9f, () => Shot("c3_rescue_free"));
        Do(1.7f, () => { Shot("c4_rescue_close"); Report("rescue 5.4"); });
        Do(1.2f, () => Print("Animator", "e3_animator_talk"));
        Do(1.2f, () => { Shot("d1_line"); Report("line"); });
        Do(1.6f, Smoke("SkipDialog"));
        Do(0.9f, () => { Shot("d2_choice"); Report("choice"); });
        Do(1.7f, () => Game.I.Answer(1));
        Do(0.3f, () => Retarget());
        Do(1.4f, () => { Shot("c5_tell"); Report("tell 1.7"); Grab(timelineWindow, "e4_timeline_tell"); });
        Do(4.0f, () => Line(1));
        Do(1.5f, () => { Shot("d3_saw"); Report("saw"); });
        Do(1.6f, Smoke("SkipDialog"));
        Do(0.3f, () => Retarget());
        Do(0.5f, () => { Shot("c6_depart_nod"); Report("depart 0.8"); });
        Do(1.9f, () =>
        {
            Shot("c7_depart_walk");
            Report("depart 2.7");
            Grab(timelineWindow, "e5_timeline_depart");
            Print("Animator", "e6_animator_walk");
        });
        Do(3.6f, () => { Shot("c8_rescued"); Report("after"); });

        // ---- Branch A: Dorn Half-Wall, floor 9, "Go home", with the first cutscene skipped.
        Do(1.8f, () => Climb(8));
        Do(2.5f, Settle);
        Do(1f, Settle);
        Do(0.5f, Approach);
        Do(1.5f, Smoke("KillAll"));
        Do(3.4f, Settle);
        Do(0.5f, () => UseSpot("A drained Painter"));
        Do(0.8f, () => { Game.I.Scene.Skip(); Report("skipped"); });
        Do(0.4f, Smoke("SkipDialog"));
        Do(0.9f, () => Game.I.Answer(0));
        Do(0.2f, () => Line(1));
        Do(1.5f, () => { Shot("b2_farewell"); Report("farewell"); });
        Do(1.6f, Smoke("SkipDialog"));
        Do(2.9f, () => { Shot("b3_depart_dorn"); Report("depart dorn"); });
        Do(3.2f, () => Report("end"));
    }

    // ------------------------------------------------------------ helpers

    static Action Smoke(string method) => () => typeof(SmokeTest).GetMethod(method, Any).Invoke(null, null);

    static void Settle() => Smoke("Settle")();

    static void UseSpot(string name) => typeof(SmokeTest).GetMethod("UseSpot", Any).Invoke(null, new object[] { name });

    static void Climb(int towerFloor)
    {
        JourneyStore.SetInt("towerFloor", towerFloor);
        JourneyStore.SetInt("storyFloor", towerFloor);
        typeof(Game).GetMethod("StartRun", Any).Invoke(Game.I, null);
    }

    static GameObject Statue() => GameObject.Find("Drained Painter");

    // Stand the hero in front of the statue, camera on both.
    static void Approach()
    {
        Transform s = Statue().transform;
        Vector3 front = s.forward; front.y = 0f; front.Normalize();
        P.Teleport(s.position + front * 2.3f + Vector3.up * 0.05f);
        P.transform.rotation = Quaternion.LookRotation(-front);
        ThirdPersonCamera cam = Game.I.Cam;
        typeof(ThirdPersonCamera).GetField("yaw", Any).SetValue(cam, Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg + 28f);
        typeof(ThirdPersonCamera).GetField("pitch", Any).SetValue(cam, 10f);
        cam.Snap();
    }

    // Shows line `index` of the conversation in progress, framed on whoever says it.
    static void Line(int index)
    {
        Game g = Game.I;
        typeof(Game).GetField("dialogIndex", Any).SetValue(g, index);
        typeof(Game).GetField("dialogChars", Any).SetValue(g, 0f);
        typeof(Game).GetField("dialogT", Any).SetValue(g, 0f);
        typeof(Game).GetMethod("ApplyShot", Any).Invoke(g, new object[] { "them" });
    }

    static Animator Actor()
    {
        GameObject s = Statue();
        return s != null ? s.GetComponentInChildren<Animator>() : null;
    }

    static string StateName(Animator a)
    {
        if (a == null) return "none";
        foreach (string n in new[] { "Stone", "Break", "Idle", "Talk", "Nod", "Walk" })
            if (a.GetCurrentAnimatorStateInfo(0).IsName(n)) return n + (a.IsInTransition(0) ? " (in transition)" : "");
        return "?";
    }

    static void Report(string when)
    {
        Game g = Game.I;
        StoryScene s = g.Scene;
        var d = s != null ? s.GetComponent<PlayableDirector>() : null;
        Debug.Log("STORY " + when + ": game=" + g.State + " cutscene=" + g.InCutscene + " asking=" + g.Asking
            + " timeline=" + (s != null && s.Playing ? s.Now + " @" + d.time.ToString("0.0") + "s" : "none")
            + " animator=" + StateName(Actor()) + " rescued=" + Save.RescuedMask
            + " ask4=" + JourneyStore.GetInt("rescueAsk0", 0) + " ask9=" + JourneyStore.GetInt("rescueAsk1", 0));
    }

    static void Shot(string name)
    {
        Directory.CreateDirectory("Logs/story");
        ScreenCapture.CaptureScreenshot("Logs/story/" + name + ".png", 2);
        Debug.Log("STORY captured " + name);
    }

    // ------------------------------------------------------------ editor windows

    // The Timeline window on the live director.
    static void OpenWindows()
    {
        var director = Game.I.Scene.GetComponent<PlayableDirector>();
        TimelineEditorWindow t = TimelineEditor.GetOrCreateWindow();
        t.SetTimeline(director);
        t.locked = true;
        t.position = new Rect(40, 60, 1180, 250);
        timelineWindow = t;

        // The Animator window follows the selection: the live Painter, so the current state is lit.
        Type tool = Type.GetType("UnityEditor.Graphs.AnimatorControllerTool, UnityEditor.Graphs");
        // A window left over from an earlier session comes back with an empty graph: start with a new one.
        foreach (UnityEngine.Object stale in Resources.FindObjectsOfTypeAll(tool)) ((EditorWindow)stale).Close();
        animatorWindow = (EditorWindow)ScriptableObject.CreateInstance(tool);
        animatorWindow.titleContent = new GUIContent("Animator");
        animatorWindow.Show();
        animatorWindow.position = new Rect(40, 360, 1180, 430);
        Selection.activeGameObject = Actor().gameObject;
        t.Repaint();
        animatorWindow.Repaint();
    }

    // Points the locked Timeline window at whatever the director is playing now.
    static void Retarget()
    {
        if (timelineWindow == null || Game.I.Scene == null) return;
        var t = (TimelineEditorWindow)timelineWindow;
        t.locked = false;
        t.SetTimeline(Game.I.Scene.GetComponent<PlayableDirector>());
        t.locked = true;
        t.Repaint();
    }

    // A picture of an editor window taken from the window's own surface (GUIView.GrabPixels),
    // never from the screen: whatever else is open on the desktop cannot end up in it.
    static void Grab(EditorWindow w, string name)
    {
        if (w == null) return;
        object view = typeof(EditorWindow).GetField("m_Parent", Any).GetValue(w);
        if (view == null) { Debug.Log("STORY no view for " + name); return; }
        MethodInfo repaint = view.GetType().GetMethod("RepaintImmediately", Any);
        MethodInfo grab = view.GetType().GetMethod("GrabPixels", Any);
        if (grab == null) { Debug.Log("STORY GrabPixels not found for " + name); return; }
        float k = EditorGUIUtility.pixelsPerPoint;
        int width = Mathf.RoundToInt(w.position.width * k), height = Mathf.RoundToInt(w.position.height * k);
        repaint?.Invoke(view, null);
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        grab.Invoke(view, new object[] { rt, new Rect(0, 0, width, height) });
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        // The view's surface comes back bottom row first.
        Color[] px = tex.GetPixels();
        var up = new Color[px.Length];
        for (int y = 0; y < height; y++) Array.Copy(px, y * width, up, (height - 1 - y) * width, width);
        tex.SetPixels(up);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory("Logs/story");
        File.WriteAllBytes("Logs/story/" + name + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        Debug.Log("STORY grabbed " + name + " " + width + "x" + height);
    }

    // ------------------------------------------------------------ one window, by handle

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public uint size; public int width, height; public ushort planes, bitCount;
        public uint compression, sizeImage; public int xPels, yPels; public uint clrUsed, clrImportant;
    }

    // A picture of one of this editor's own floating windows, asked of the window itself
    // (PrintWindow). It is the window's content only, whatever else is on the desktop.
    static void Print(string title, string name)
    {
        uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (pid != me || !IsWindowVisible(h)) return true;
            var text = new StringBuilder(200);
            GetWindowText(h, text, 200);
            if (text.ToString() == title) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        if (found == IntPtr.Zero) { Debug.Log("STORY no window titled " + title); return; }

        GetWindowRect(found, out RECT r);
        int w = r.right - r.left, hgt = r.bottom - r.top;
        IntPtr windowDc = GetWindowDC(found), dc = CreateCompatibleDC(windowDc), bmp = CreateCompatibleBitmap(windowDc, w, hgt);
        IntPtr old = SelectObject(dc, bmp);
        bool ok = PrintWindow(found, dc, 2);        // 2 = PW_RENDERFULLCONTENT
        SelectObject(dc, old);
        var info = new BITMAPINFOHEADER { size = 40, width = w, height = hgt, planes = 1, bitCount = 32 };
        var bits = new byte[w * hgt * 4];
        GetDIBits(dc, bmp, 0, (uint)hgt, bits, ref info, 0);    // bottom row first, BGRA: the order Unity wants
        DeleteObject(bmp); DeleteDC(dc); ReleaseDC(found, windowDc);

        var tex = new Texture2D(w, hgt, TextureFormat.BGRA32, false);
        tex.LoadRawTextureData(bits);
        tex.Apply();
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++) px[i].a = 255;
        tex.SetPixels32(px);
        tex.Apply();
        Directory.CreateDirectory("Logs/story");
        File.WriteAllBytes("Logs/story/" + name + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("STORY printed " + name + " " + w + "x" + hgt + " ok=" + ok);
    }
}
