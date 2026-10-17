using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Paired review captures for the first-zone visual pass (V01): the same cameras, seed, lighting
// phase and comfort settings for every run, written to Logs/env/<label>/. Run once before a pass
// (-greyspire-env-label baseline) and once after (candidate), then compare side by side.
// Ordinary fog stays on; reduced effects are enabled, as the art guide asks.
[InitializeOnLoad]
public static class EnvironmentShowcase
{
    const string Session = "GreyspireEnvironmentShowcase";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static Game G => Game.I;
    static PlayerController P => G.Player;
    static object Call(object host, string name, params object[] args) => host.GetType().GetMethod(name, Any).Invoke(host, args);
    static void Set(object host, string name, object value) => (host.GetType().GetField(name, Any) ?? host.GetType().GetField("<" + name + ">k__BackingField", Any)).SetValue(host, value);

    struct Shot { public string name; public Vector3 eye, look; public Vector3 hero; }
    struct Place { public string name; public int floor; public Shot[] shots; }

    static float PathX(float z) => 9f * Mathf.Cos(z * Mathf.PI / 76f);

    static readonly Place[] Places =
    {
        new Place { name = "floor1", floor = 1, shots = new[]
        {
            new Shot { name = "arrival", hero = new Vector3(0f, 0.1f, -37f), eye = new Vector3(0.6f, 3.2f, -42.5f), look = new Vector3(0f, 2f, -20f) },
            new Shot { name = "heart", hero = new Vector3(PathX(-8f), 0.1f, -8f), eye = new Vector3(PathX(-8f) + 1f, 3.2f, -14f), look = new Vector3(0f, 2f, 6f) },
            new Shot { name = "stairs", hero = new Vector3(PathX(26f), 0.1f, 26f), eye = new Vector3(PathX(26f) + 1f, 3.2f, 20f), look = new Vector3(0f, 3f, 38f) },
        } },
        new Place { name = "floor2", floor = 2, shots = new[]
        {
            new Shot { name = "approach", hero = new Vector3(0f, 0.1f, -33f), eye = new Vector3(1.5f, 3.4f, -39f), look = new Vector3(0f, 4f, -6f) },
            new Shot { name = "underpass", hero = new Vector3(-12f, 0.1f, 0f), eye = new Vector3(-17.5f, 2.3f, -1.5f), look = new Vector3(4f, 1.6f, 0.5f) },
            new Shot { name = "deck", hero = new Vector3(0f, 5.5f, -4f), eye = new Vector3(0.8f, 8.4f, -10f), look = new Vector3(0f, 5.5f, 10f) },
            new Shot { name = "winch", hero = new Vector3(-9f, 0.1f, -2f), eye = new Vector3(-12f, 2.8f, -6f), look = new Vector3(-6f, 1.2f, 2f) },
        } },
        new Place { name = "floor3", floor = 3, shots = new[]
        {
            new Shot { name = "ruin", hero = new Vector3(0f, 0.1f, -30f), eye = new Vector3(1f, 3.4f, -36f), look = new Vector3(0f, 4f, -6f) },
            new Shot { name = "overlook", hero = new Vector3(-20f, 3.3f, -8f), eye = new Vector3(-21f, 6.2f, -14f), look = new Vector3(-20f, 3.5f, 8f) },
        } },
        new Place { name = "floor4", floor = 4, shots = new[]
        {
            new Shot { name = "painter", hero = new Vector3(PathX(2f), 0.1f, 2f), eye = new Vector3(PathX(2f) - 1.2f, 3.2f, -3.5f), look = new Vector3(PathX(12f) + 5.5f, 1.5f, 12f) },
            new Shot { name = "arrival", hero = new Vector3(0f, 0.1f, -37f), eye = new Vector3(0.6f, 3.2f, -42.5f), look = new Vector3(0f, 2f, -20f) },
        } },
        new Place { name = "floor5", floor = 5, shots = new[]
        {
            new Shot { name = "gate", hero = new Vector3(0f, 0.1f, 9f), eye = new Vector3(1.2f, 3.4f, 3.5f), look = new Vector3(0f, 2f, 25f) },
        } },
        new Place { name = "town", floor = 0, shots = new[]
        {
            new Shot { name = "square", hero = new Vector3(0f, 0.1f, -9f), eye = new Vector3(0.8f, 3.2f, -14.5f), look = new Vector3(0f, 2.5f, 10f) },
            // Town residents stand by angle and distance from the square: the Guild desk is at -30 degrees, 10 m.
            new Shot { name = "fielddesk", hero = new Vector3(5f, 0.1f, -3.2f), eye = new Vector3(2.5f, 2.8f, -0.5f), look = new Vector3(7.8f, 1.1f, -5.6f) },
            new Shot { name = "guild", hero = new Vector3(1.5f, 0.1f, 5f), eye = new Vector3(4.5f, 2.8f, 2.5f), look = new Vector3(-5.5f, 1.4f, 9.5f) },
        } },
    };

    static string label = "candidate";
    static int place = -1, shot;
    static double readyAt, nextAt;

    static EnvironmentShowcase()
    {
        if (SessionState.GetBool(Session, false)) EditorApplication.update += Tick;
    }

    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-greyspire-env-label") label = args[i + 1];
        SessionState.SetString(Session + "Label", label);
        Save.Wipe();
        Save.SeenIntro = true; Save.Level = 8; Save.BestFloor = 5; Save.TowerFloor = 4; Save.StoryFloor = 5; Save.Hour = 10.5f; Save.Pet = false;
        Save.ReducedEffects();
        JourneyStore.SetInt("productionSeed", 734912); JourneyStore.SetInt("seedPolicy", 1);
        Save.Flush();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Build(Place p)
    {
        foreach (var e in Enemy.All.ToArray()) UnityEngine.Object.DestroyImmediate(e.gameObject);
        G.enabled = true; P.enabled = false;
        Call(G, "SetState", Game.GameState.Playing);
        Set(G, "InWilds", false);
        if (p.floor == 0)
        {
            Call(G, "BuildHub");
            Set(G, "InTown", true);
            Call(G, "SetState", Game.GameState.Hub);
            return;
        }
        var def = FloorCatalog.At(p.floor);
        Set(G, "level", def.theme); Set(G, "wave", def.layout);
        Call(G, "BuildFloor", def.theme, def.layout);
        Set(G, "InTown", false);
    }

    static void Capture(Place p, Shot s)
    {
        P.Revive(s.hero, true);
        Vector3 face = s.look - s.hero; face.y = 0f;
        if (face.sqrMagnitude > 0.01f) P.transform.rotation = Quaternion.LookRotation(face);
        var go = new GameObject("Environment evidence camera");
        var cam = go.AddComponent<Camera>();
        var main = G.Cam.GetComponent<Camera>();
        cam.CopyFrom(main);
        cam.transform.position = s.eye;
        cam.transform.LookAt(s.look);
        cam.fieldOfView = 58f;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            string dir = "Logs/env/" + label;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(dir + "/" + p.name + "_" + s.name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; cam.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; label = SessionState.GetString(Session + "Label", label); }
        try
        {
            if (G == null || P == null || EditorApplication.timeSinceStartup < readyAt || EditorApplication.timeSinceStartup < nextAt) return;
            Application.runInBackground = true;
            if (place < 0 || shot >= Places[place].shots.Length)
            {
                if (++place >= Places.Length)
                {
                    Debug.Log("ENVIRONMENT DONE " + Directory.GetFiles("Logs/env/" + label, "*.png").Length + " images in Logs/env/" + label);
                    Finish(0);
                    return;
                }
                shot = 0;
                Build(Places[place]);
                nextAt = EditorApplication.timeSinceStartup + 2.5;   // let grass, shadows and props settle
                return;
            }
            Capture(Places[place], Places[place].shots[shot++]);
            nextAt = EditorApplication.timeSinceStartup + 0.6;
        }
        catch (Exception e)
        {
            Debug.LogError("ENVIRONMENT FAILED place=" + place + " shot=" + shot + " " + e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Session, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
