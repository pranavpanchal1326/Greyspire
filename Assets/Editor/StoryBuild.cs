using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Timeline;

// Builds the assets behind the rescue scenes (see StoryScene.cs and GameStory.cs):
//   Painter.controller      the Animator Controller every drained Painter runs on
//   Rescue / Tell / Depart  three Timelines: the thaw, the telling, the walk home
//   RescueCam ...           the camera moves those Timelines play
//   Thaw / Go signals       the moments a Timeline hands back to the game
// Everything lands in Assets/Resources/Story. Run it again after changing a number here:
//   Tools\unity.ps1 StoryBuild.Build     (or the menu: Greyspire > Build Story Assets)
public static class StoryBuild
{
    const string Dir = "Assets/Resources/Story";

    static Dictionary<string, AnimationClip> library;

    [MenuItem("Greyspire/Build Story Assets")]
    public static void Build()
    {
        if (AssetDatabase.IsValidFolder(Dir)) AssetDatabase.DeleteAsset(Dir);
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        library = new Dictionary<string, AnimationClip>();
        foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("Humans/Anim"))
        {
            if (clip.name.StartsWith("__preview__")) continue;
            string key = clip.name.Contains("|") ? clip.name.Substring(clip.name.LastIndexOf('|') + 1) : clip.name;
            library[key] = clip;
        }

        Controller();

        SignalAsset thaw = Signal("Thaw"), go = Signal("Go");
        var restore = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/restore_1.ogg");

        // The stage is laid out round the Painter: they stand at the origin and the hero is along +Z.
        // Each key: time, where the camera is, what it looks at.
        AnimationClip rescueCam = CameraMove("RescueCam",
            (0f, new Vector3(4.6f, 1.55f, 1.5f), new Vector3(0f, 1.1f, 1.15f)),    // from the side: hero and stone, face to face
            (1.2f, new Vector3(4.0f, 1.5f, 1.45f), new Vector3(0f, 1.15f, 1.1f)),  // the colour arrives
            (3.4f, new Vector3(2.0f, 1.55f, 2.5f), new Vector3(0f, 1.35f, 0.2f)),  // round towards the Painter
            (6.5f, new Vector3(0.8f, 1.6f, 1.75f), new Vector3(0f, 1.5f, 0f)));    // and close on the face
        AnimationClip tellCam = CameraMove("TellCam",
            (0f, new Vector3(1.0f, 1.62f, 2.0f), new Vector3(0f, 1.5f, 0f)),
            (4.5f, new Vector3(0.55f, 1.6f, 1.35f), new Vector3(0f, 1.52f, 0f)));
        AnimationClip departCam = CameraMove("DepartCam",
            (0f, new Vector3(3.4f, 1.6f, 0.4f), new Vector3(0f, 1.25f, 0.9f)),     // the nod, both in frame
            (1.5f, new Vector3(3.6f, 1.65f, 0.6f), new Vector3(-0.3f, 1.2f, 0.8f)),
            (5f, new Vector3(4.4f, 1.9f, 1.6f), new Vector3(-3.4f, 1.0f, 0.1f)));  // watching her go

        // Rescue: stone, the break, standing free.
        TimelineAsset rescue = Timeline("Rescue");
        Actor(rescue, ("Idle_Shield_Loop", 0f, 1.4f, 0.04f), ("Idle_Shield_Break", 1.2f, 1.3f, 1f), ("Idle_Loop", 2.3f, 4.2f, 1f));
        Camera(rescue, rescueCam, 6.5f);
        Window<ActivationTrack>(rescue, "Colour ring", 1.2f, 2.8f);
        if (restore != null)
        {
            TimelineClip sound = rescue.CreateTrack<AudioTrack>(null, "Sound").CreateClip(restore);
            sound.start = 1.2f;
            sound.displayName = "restore";
        }
        Events(rescue, (1.2f, thaw));

        // Tell: the Painter talks, the camera leans in.
        TimelineAsset tell = Timeline("Tell");
        Actor(tell, ("Idle_Talking_Loop", 0f, 4.5f, 1f));
        Camera(tell, tellCam, 4.5f);
        Window<ActivationTrack>(tell, "Memory light", 0.3f, 4.2f);

        // Depart: a nod, then the walk to the Way Down.
        TimelineAsset depart = Timeline("Depart");
        Actor(depart, ("Yes", 0f, 1.6f, 1f), ("Walk_Loop", 1.35f, 3.65f, 1f));
        Camera(depart, departCam, 5f);
        Events(depart, (1.4f, go));

        foreach (TimelineAsset t in new[] { rescue, tell, depart }) EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("STORY built: controller, 3 timelines, 3 camera clips, 2 signals. DONE");
    }

    // ------------------------------------------------------------ Animator Controller

    // Stone -> Break -> Idle <-> Talk, a Nod from either, and Walk from anywhere.
    static void Controller()
    {
        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(Dir + "/Painter.controller");
        c.AddParameter("Thaw", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Talking", AnimatorControllerParameterType.Bool);
        c.AddParameter("Nod", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Leaving", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = c.layers[0].stateMachine;
        sm.entryPosition = new Vector3(20, 170);
        sm.anyStatePosition = new Vector3(20, 300);
        sm.exitPosition = new Vector3(20, 40);

        AnimatorState stone = State(sm, "Stone", "Idle_Shield_Loop", 240, 170);
        stone.speed = 0f;                               // caught with their guard up, and held there
        AnimatorState brk = State(sm, "Break", "Idle_Shield_Break", 460, 170);
        AnimatorState idle = State(sm, "Idle", "Idle_Loop", 680, 170);
        AnimatorState talk = State(sm, "Talk", "Idle_Talking_Loop", 680, 40);
        AnimatorState nod = State(sm, "Nod", "Yes", 460, 40);
        AnimatorState walk = State(sm, "Walk", "Walk_Loop", 460, 300);
        sm.defaultState = stone;

        Link(stone.AddTransition(brk), 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Thaw");
        AnimatorStateTransition free = brk.AddTransition(idle);
        free.hasExitTime = true; free.exitTime = 0.85f; free.duration = 0.25f; free.hasFixedDuration = true;

        Link(idle.AddTransition(talk), 0.25f).AddCondition(AnimatorConditionMode.If, 0f, "Talking");
        Link(talk.AddTransition(idle), 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");
        Link(idle.AddTransition(nod), 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Nod");
        Link(talk.AddTransition(nod), 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Nod");
        AnimatorStateTransition nodded = nod.AddTransition(idle);
        nodded.hasExitTime = true; nodded.exitTime = 0.5f; nodded.duration = 0.3f; nodded.hasFixedDuration = true;

        AnimatorStateTransition leave = sm.AddAnyStateTransition(walk);
        Link(leave, 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Leaving");
        leave.canTransitionToSelf = false;
        EditorUtility.SetDirty(c);
    }

    static AnimatorState State(AnimatorStateMachine sm, string name, string clip, float x, float y)
    {
        AnimatorState s = sm.AddState(name, new Vector3(x, y));
        s.motion = library[clip];
        return s;
    }

    static AnimatorStateTransition Link(AnimatorStateTransition t, float seconds)
    {
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = seconds;
        return t;
    }

    // ------------------------------------------------------------ Timelines

    static TimelineAsset Timeline(string name)
    {
        var t = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(t, Dir + "/" + name + ".playable");
        t.editorSettings.frameRate = 30;
        return t;
    }

    static SignalAsset Signal(string name)
    {
        var s = ScriptableObject.CreateInstance<SignalAsset>();
        AssetDatabase.CreateAsset(s, Dir + "/" + name + ".signal");
        return s;
    }

    // The Painter's animation track: clips in order, each overlapping the last so they blend.
    static void Actor(TimelineAsset timeline, params (string clip, float start, float length, float speed)[] clips)
    {
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Painter");
        track.trackOffset = TrackOffset.ApplySceneOffsets;      // stay where the floor put them
        TimelineClip last = null;
        foreach ((string clip, float start, float length, float speed) in clips)
        {
            TimelineClip c = track.CreateClip(library[clip]);
            c.displayName = clip;
            c.start = start;
            c.duration = length;
            c.timeScale = speed;
            if (last != null && last.end > c.start)
            {
                double overlap = last.end - c.start;
                last.blendOutDuration = overlap;
                c.blendInDuration = overlap;
            }
            last = c;
        }
    }

    static void Camera(TimelineAsset timeline, AnimationClip move, float length)
    {
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Camera");
        TimelineClip c = track.CreateClip(move);
        c.displayName = move.name;
        c.start = 0;
        c.duration = length;
    }

    static void Window<T>(TimelineAsset timeline, string name, float start, float length) where T : TrackAsset, new()
    {
        TimelineClip c = timeline.CreateTrack<T>(null, name).CreateDefaultClip();
        c.displayName = "Active";
        c.start = start;
        c.duration = length;
    }

    static void Events(TimelineAsset timeline, params (float time, SignalAsset signal)[] events)
    {
        SignalTrack track = timeline.CreateTrack<SignalTrack>(null, "Events");
        foreach ((float time, SignalAsset signal) in events)
        {
            SignalEmitter e = track.CreateMarker<SignalEmitter>(time);
            e.asset = signal;
            e.name = signal.name;
        }
    }

    // A camera move for the rig's "Eye": position keys, and rotations worked out from what to look at.
    static AnimationClip CameraMove(string name, params (float time, Vector3 from, Vector3 at)[] keys)
    {
        var clip = new AnimationClip { name = name, frameRate = 30 };
        var pos = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        var rot = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        Quaternion previous = Quaternion.identity;
        for (int i = 0; i < keys.Length; i++)
        {
            (float time, Vector3 from, Vector3 at) = keys[i];
            Quaternion q = Quaternion.LookRotation(at - from);
            if (i > 0 && Quaternion.Dot(previous, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            previous = q;
            for (int a = 0; a < 3; a++) pos[a].AddKey(time, from[a]);
            for (int a = 0; a < 4; a++) rot[a].AddKey(time, q[a]);
        }
        string[] axes = { "x", "y", "z", "w" };
        for (int a = 0; a < 3; a++)
        {
            Smooth(pos[a]);
            clip.SetCurve("Eye", typeof(Transform), "localPosition." + axes[a], pos[a]);
        }
        for (int a = 0; a < 4; a++)
        {
            Smooth(rot[a]);
            clip.SetCurve("Eye", typeof(Transform), "localRotation." + axes[a], rot[a]);
        }
        clip.EnsureQuaternionContinuity();
        AssetDatabase.CreateAsset(clip, Dir + "/" + name + ".anim");
        return clip;
    }

    static void Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
    }
}
