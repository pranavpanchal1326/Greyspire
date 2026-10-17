using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Measures the delivered dash implementation before selecting an optimization.
[InitializeOnLoad]
public static class ProductionBaseline
{
    const string Session = "GreyspireProductionBaseline";
    static double next;
    static int samples, peak, meshes;
    static readonly List<double> costs = new List<double>();
    static ProductionBaseline() { if (SessionState.GetBool(Session, false)) EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SessionState.SetBool(Session, true); EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Game.I == null || Game.I.Player == null) return;
        if (next == 0) { next = EditorApplication.timeSinceStartup + 2; return; }
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var model = Game.I.Player.GetComponentInChildren<SkinnedMeshRenderer>().transform.parent;
            if (samples < 50)
            {
                var watch = Stopwatch.StartNew(); Fx.Ghost(model, UI.Blue); watch.Stop();
                costs.Add(watch.Elapsed.TotalMilliseconds); samples++;
                var ghosts = UnityEngine.Object.FindObjectsByType<GhostFade>(); peak = Mathf.Max(peak, ghosts.Length);
                foreach (var ghost in ghosts) if (ghost.ownsMesh) meshes++;
                next = EditorApplication.timeSinceStartup + 0.025; return;
            }
            if (samples == 50) { samples++; next = EditorApplication.timeSinceStartup + 1; return; }
            costs.Sort(); Directory.CreateDirectory("Reports");
            string report = "calls,median_ms,p95_ms,peak_live_pieces,remaining_pieces\n50," + costs[25].ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "," + costs[47].ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "," + peak + "," + UnityEngine.Object.FindObjectsByType<GhostFade>().Length;
            File.WriteAllText("Reports/dash-baseline.csv", report);
            UnityEngine.Debug.Log("BASELINE DONE " + report.Replace('\n', ' ')); SessionState.SetBool(Session, false); EditorApplication.Exit(0);
        }
        catch (Exception e) { UnityEngine.Debug.LogError("BASELINE FAILED " + e); SessionState.SetBool(Session, false); EditorApplication.Exit(1); }
    }
}
