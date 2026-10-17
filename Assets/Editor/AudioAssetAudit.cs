using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build-time and manual inventory use the same runtime cue catalog. Numbered
// gaps are explicit, valid later takes remain usable, unknown source IDs fail.
public class AudioAssetAudit : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Validate();
    public static int[] MissingSlots(IEnumerable<int> numbers)
    {
        var present = new HashSet<int>(numbers);
        return present.Count == 0 ? Array.Empty<int>() : Enumerable.Range(1, present.Max()).Where(n => !present.Contains(n)).ToArray();
    }
    [MenuItem("Greyspire/Validate Audio Assets")]
    public static void Validate()
    {
        GameAudio.RefreshRecordings();
        var errors = new List<string>();
        var csv = new StringBuilder("id,requirement,priority,bus,takes,gaps,status\n");
        foreach (var cue in GameAudio.Catalog)
        {
            var paths = Directory.GetFiles("Assets/Resources/Audio", cue.id + "_*.*", SearchOption.TopDirectoryOnly)
                .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray();
            var slots = new List<int>();
            foreach (var path in paths)
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(path).Substring(cue.id.Length + 1), out int slot) || slot < 1 || slot > 32)
                { errors.Add("Unsupported numbered take: " + path); continue; }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/'));
                if (clip == null || clip.samples <= 0) errors.Add("Unreadable sound: " + path);
                else slots.Add(slot);
            }
            var gaps = MissingSlots(slots);
            bool missing = slots.Count == 0 && cue.requirement != CueRequirement.Optional;
            if (missing) errors.Add("Missing " + cue.requirement + " sound: " + cue.id);
            string status = missing ? "missing" : slots.Count == 0 ? "optional absent" : gaps.Length > 0 ? "gap usable" : "ok";
            csv.AppendLine(cue.id + "," + cue.requirement + "," + cue.priority + "," + (cue.ui ? "UI" : "world") + "," + slots.Count + "," + string.Join("|", gaps) + "," + status);
        }
        var known = new HashSet<string>(GameAudio.Catalog.Select(c => c.id));
        foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs"))
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "ToonKit\\.Sfx\\(\\s*\"([^\"]+)\""))
                if (!known.Contains(match.Groups[1].Value)) errors.Add("Uncatalogued source cue: " + match.Groups[1].Value + " in " + file);
        var wind = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/ambience_meadow_1.ogg");
        if (wind == null || wind.samples <= 0) errors.Add("Missing ambience bed: ambience_meadow_1");
        csv.AppendLine("ambience_meadow_1,Required,Ambience,bed,1,," + (wind == null ? "missing" : "ok"));
        var town = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Music/TownTheme.mp3");
        if (town == null || town.samples <= 0) errors.Add("Missing recorded town theme");
        csv.AppendLine("TownTheme,Required,Music,score,1,," + (town == null ? "missing" : "ok"));
        foreach (int index in new[] { 1, 2, 3 }) csv.AppendLine(Music.ScoreId(index) + ",Required,Music,paired score,2,,runtime authored");
        Directory.CreateDirectory("Reports"); File.WriteAllText("Reports/audio-assets.csv", csv.ToString());
        if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors.Distinct()));
        Debug.Log("AUDIO AUDIT DONE " + GameAudio.Catalog.Length + " cues; no missing required assets; optional hover/talk absence disclosed");
    }
}
