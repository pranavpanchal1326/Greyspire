using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Stages the story's Timeline cutscenes round one character. The Timelines themselves are
// assets (Resources/Story, built by StoryBuild); this lays out the stage they play on,
// binds each track to the right object, lends them the game camera, and hands the
// signals they raise back to whoever is telling the story (GameStory.cs).
//
// The stage sits at the actor's feet and faces the hero, so one camera move works on
// any floor: the actor is at the origin and the hero is somewhere along +Z.
public class StoryScene : MonoBehaviour, INotificationReceiver
{
    public Action<string> onSignal;

    PlayableDirector director;
    bool policyPaused;
    AnimatorUpdateMode originalActorClock;
    float originalActorSpeed, heldActorSpeed;
    bool actorClockHeld, actorRestored;
    void RestoreActor()
    {
        if (actorRestored) return; actorRestored = true;
        if (actor != null) { actor.updateMode = originalActorClock; actor.speed = originalActorSpeed; }
    }
    Animator actor, rig;
    Transform eye, actorRoot;
    GameObject ring, memory;
    AudioSource voice;
    ThirdPersonCamera gameCam;
    Camera lens;
    Action done;
    readonly List<(double time, string name)> pending = new List<(double, string)>();

    // What the script asks of the actor between Timelines.
    Quaternion turnTo;
    float turnLeft, turnTime, walkSpeed, stalled;
    Vector3 walkDir;

    public bool Playing => director != null && director.state == PlayState.Playing;
    public string Now { get; private set; } = "";

    public static StoryScene Stage(Transform actorRoot, Animator actor, Vector3 hero)
    {
        Vector3 at = actorRoot.position;
        if (WorldSupport.TryGround(at, out var support, 0.35f, 0.7f)) at = support.point;
        Vector3 to = hero - at;
        to.y = 0f;
        var go = new GameObject("Story Scene");
        go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to : Vector3.forward));

        var s = go.AddComponent<StoryScene>();
        s.actor = actor;
        if (actor != null) { s.originalActorClock = actor.updateMode; s.originalActorSpeed = actor.speed; actor.updateMode = AnimatorUpdateMode.UnscaledTime; }
        s.actorRoot = actorRoot;
        go.AddComponent<SignalReceiver>();
        s.voice = go.AddComponent<AudioSource>();
        s.voice.playOnAwake = false;
        s.voice.spatialBlend = 0f;
        s.director = go.AddComponent<PlayableDirector>();
        s.director.playOnAwake = false;
        s.director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        s.director.extrapolationMode = DirectorWrapMode.None;
        s.director.stopped += s.Stopped;

        // The camera rig: a Timeline animates "Eye", and the game camera is held to it.
        var rigGo = new GameObject("Camera Rig");
        rigGo.transform.SetParent(go.transform, false);
        s.rig = rigGo.AddComponent<Animator>();
        s.eye = new GameObject("Eye").transform;
        s.eye.SetParent(rigGo.transform, false);

        s.ring = s.Glow("Colour Ring", new Vector3(0f, 0.08f, 0f), new Color(1f, 0.82f, 0.3f), 1.15f, 3.2f);
        s.memory = s.Glow("Memory Light", new Vector3(0.2f, 2.1f, 0.7f), new Color(0.75f, 0.85f, 1f), 0f, 2.2f);
        return s;
    }

    // A warm light, and (with a radius) a ring on the ground: things an Activation track switches on.
    GameObject Glow(string name, Vector3 at, Color colour, float radius, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = at;
        var lamp = go.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = colour;
        lamp.range = 7f;
        lamp.intensity = intensity;
        if (radius > 0f)
        {
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = GeneratedResources.Own(NavField.Tint(colour), go.transform);
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.07f;
            line.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            line.positionCount = 40;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }
        go.SetActive(false);
        return go;
    }

    // Plays one Timeline from Resources/Story and calls back when it ends (or is skipped).
    public void Play(string timeline, Action whenDone)
    {
        var asset = Resources.Load<TimelineAsset>("Story/" + timeline);
        if (asset == null)
        {
            Debug.LogWarning("Story timeline not found: " + timeline + " (run StoryBuild.Build)");
            whenDone?.Invoke();
            return;
        }
        done = whenDone;
        Now = timeline;
        pending.Clear();
        director.playableAsset = asset;
        foreach (TrackAsset track in asset.GetOutputTracks())
        {
            if (track is AnimationTrack) director.SetGenericBinding(track, track.name == "Camera" ? rig : actor);
            else if (track is ActivationTrack) director.SetGenericBinding(track, track.name == "Memory light" ? memory : ring);
            else if (track is AudioTrack) director.SetGenericBinding(track, voice);
            else if (track is SignalTrack)
            {
                director.SetGenericBinding(track, GetComponent<SignalReceiver>());
                foreach (IMarker m in track.GetMarkers())
                    if (m is SignalEmitter e && e.asset != null) pending.Add((m.time, e.asset.name));
            }
        }

        if (gameCam == null)
        {
            gameCam = Game.I.Cam;
            lens = gameCam.GetComponent<Camera>();
        }
        gameCam.enabled = false;
        director.time = 0;
        director.Play();
        director.Evaluate();
        Hold();
    }

    // Jumps to the end. Anything the Timeline had still to signal is raised first, so skipping changes nothing.
    public void Skip()
    {
        if (!Playing) return;
        foreach ((double time, string name) in pending.ToArray()) Raise(name);
        director.Stop();
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is SignalEmitter e && e.asset != null) Raise(e.asset.name);
    }

    void Raise(string name)
    {
        int i = pending.FindIndex(p => p.name == name);
        if (i < 0) return;                      // already raised
        pending.RemoveAt(i);
        onSignal?.Invoke(name);
    }

    void Stopped(PlayableDirector d)
    {
        if (Now == "" || Game.I == null || Game.I.Player == null) return;     // nothing playing, or the game is closing
        Now = "";
        stalled = 0f;
        ring.SetActive(false);
        memory.SetActive(false);
        if (gameCam != null) gameCam.enabled = true;
        Action then = done;
        done = null;
        then?.Invoke();
    }

    // Turn the actor to face a point over `seconds`.
    public void TurnActor(Vector3 towards, float seconds)
    {
        Vector3 to = towards - actorRoot.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        turnTo = Quaternion.LookRotation(to);
        turnLeft = turnTime = seconds;
    }

    // Walk the actor off in a direction given in stage space.
    public void WalkActor(Vector3 stageDir, float metresPerSecond)
    {
        walkDir = transform.TransformDirection(stageDir.normalized);
        walkSpeed = metresPerSecond;
        TurnActor(actorRoot.position + walkDir, 0.6f);
    }

    void Update()
    {
        if (actorRoot == null)
        {
            Game.I.AbandonStory();
            return;
        }
        if (GameClocks.HardPause)
        {
            // The controller also runs between Timelines (idle/dialogue).
            // Pausing only the director would leave that pose animating.
            if (!actorClockHeld && actor != null) { heldActorSpeed = actor.speed; actor.speed = 0f; actorClockHeld = true; }
            if (Playing) { director.Pause(); voice.Pause(); policyPaused = true; }
            return; // A deliberate pause is not a stalled Timeline.
        }
        if (actorClockHeld) { actorClockHeld = false; if (actor != null) actor.speed = heldActorSpeed; }
        if (policyPaused) { policyPaused = false; director.Resume(); voice.UnPause(); }
        // A Timeline that should be running but is not (something paused the director) is
        // nudged on, and if that fails it is ended properly: the story must never be left hanging.
        if (Now != "" && !Playing)
        {
            stalled += Time.unscaledDeltaTime;
            if (stalled > 1f)
            {
                Debug.LogWarning("Story: timeline " + Now + " stalled and was ended");
                foreach ((double time, string name) in pending.ToArray()) Raise(name);
                director.Stop();
                Stopped(director);
            }
            else if (stalled > 0.25f)
            {
                director.Play();
                if (Playing) Debug.LogWarning("Story: timeline " + Now + " stalled and was resumed");
            }
        }
        else stalled = 0f;

        float dt = GameClocks.PresentationDelta;
        if (turnLeft > 0f)
        {
            actorRoot.rotation = Quaternion.Slerp(actorRoot.rotation, turnTo, Mathf.Clamp01(dt / turnLeft));
            turnLeft -= dt;
        }
        if (walkSpeed > 0f) WorldSupport.TryMove(actorRoot, walkDir * walkSpeed * dt, 0.4f, 1.8f);
    }

    void LateUpdate()
    {
        if (Playing && actorRoot != null && !GameClocks.HardPause) Hold();
    }

    void Hold()
    {
        gameCam.transform.SetPositionAndRotation(eye.position, eye.rotation);
        lens.fieldOfView = 42f;
    }

    public void Close()
    {
        director.stopped -= Stopped;
        RestoreActor();
        if (gameCam != null) gameCam.enabled = true;
        Destroy(gameObject);
    }
    void OnDestroy() => RestoreActor();
}
