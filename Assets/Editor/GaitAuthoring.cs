using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Package A01: the directional gait set, authored from the shared humanoid library by keyframe
// processing (no generative model). Each clip is built from the library's own walk, jog and
// idle muscle curves, then measured on the real body rig: ground pace, stance foot drift, the
// closest the feet come, and where the left foot plants. Side-step strength is chosen by that
// measurement, and every loop is rotated so its left plant lands on phase zero, so CharAnim can
// keep the whole set in step. The measurements are written as the animation ledger the master
// plan asks for (section 28): Assets/Resources/Humans/gait_ledger.json.
//
//   Unity -batchmode -quit -executeMethod GaitAuthoring.Build
public static class GaitAuthoring
{
    const string Library = "Assets/Resources/Humans/Anim/UAL1.fbx";
    const string OutDir = "Assets/Resources/Humans/Anim";
    const string LedgerPath = "Assets/Resources/Humans/gait_ledger.json";
    const float Fps = 30f;
    // A side step may not bring the feet closer than this, or the legs clip through each other.
    const float MinFootGap = 0.11f;

    [Serializable] public class Entry
    {
        public string clip, source, derivation;
        public float length, paceForward, paceRight, leftPlant, stanceSlip, stanceDrift, minFootGap, floorGap;
        public bool loop, footIK;
    }
    [Serializable] public class Ledger { public string note; public List<Entry> clips = new List<Entry>(); }

    class Source
    {
        public AnimationClip clip;
        public float length;
        public readonly Dictionary<string, AnimationCurve> curves = new Dictionary<string, AnimationCurve>();
        public readonly Dictionary<string, float> mean = new Dictionary<string, float>(), min = new Dictionary<string, float>();
        public float Get(string name, float t)
        {
            if (!curves.TryGetValue(name, out var c)) return 0f;
            return c.Evaluate(((t % length) + length) % length);
        }
    }

    static bool IkGoal(string p) => p.StartsWith("LeftFoot") || p.StartsWith("RightFoot") || p.StartsWith("LeftHand") || p.StartsWith("RightHand");

    static Source Load(AnimationClip[] all, string name)
    {
        var clip = all.First(c => c.name == name || c.name.EndsWith("|" + name));
        var s = new Source { clip = clip, length = clip.length };
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.type != typeof(Animator) || IkGoal(b.propertyName)) continue;
            var c = AnimationUtility.GetEditorCurve(clip, b);
            s.curves[b.propertyName] = c;
            float sum = 0f, lo = float.MaxValue;
            for (int i = 0; i < 60; i++) { float v = c.Evaluate(clip.length * i / 60f); sum += v; lo = Mathf.Min(lo, v); }
            s.mean[b.propertyName] = sum / 60f; s.min[b.propertyName] = lo;
        }
        return s;
    }

    // A pose is every curve's value at one moment.
    delegate float Pose(string curve, float t);

    static AnimationClip Make(string name, Source settingsFrom, IEnumerable<string> curves, float length, Pose pose, bool loop)
    {
        var clip = new AnimationClip { name = name, frameRate = Fps };
        int frames = Mathf.Max(2, Mathf.RoundToInt(length * Fps));
        var names = curves.ToList();
        var keys = names.ToDictionary(n => n, n => new Keyframe[frames + 1]);
        for (int f = 0; f <= frames; f++)
        {
            float t = length * f / frames;
            // The last key repeats the first so a loop closes exactly.
            float at = loop && f == frames ? 0f : t;
            float qx = pose("RootQ.x", at), qy = pose("RootQ.y", at), qz = pose("RootQ.z", at), qw = pose("RootQ.w", at);
            float qn = Mathf.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            foreach (var n in names)
            {
                float v = pose(n, at);
                if (n.StartsWith("RootQ.") && qn > 1e-5f) v /= qn;
                keys[n][f] = new Keyframe(t, v);
            }
        }
        foreach (var n in names)
        {
            var curve = new AnimationCurve(keys[n]);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), n), curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(settingsFrom.clip);
        settings.loopTime = loop;
        settings.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    static float Scale(Source s, string n, float t, float k) => s.mean.TryGetValue(n, out float m) ? m + (s.Get(n, t) - m) * k : s.Get(n, t);

    static bool ArmSwing(string n) => n.Contains("Arm Front-Back") || n.Contains("Forearm Stretch") || n.Contains("Shoulder Front-Back") || n.Contains("Arm Twist");

    // ------------------------------------------------------------ the derivations

    // A gait between two cycles: both are run on one clock, each from its own left plant, and their
    // poses mixed. The library has a walk (about 1 m/s) and a run (about 6 m/s) and nothing between;
    // this is the easy jog that covers the gap. Its real pace is measured, not assumed.
    static Pose Between(Source a, float plantA, Source b, float plantB, float mix, float length) => (n, t) =>
    {
        float phase = t / length;
        float ta = ((phase + plantA) % 1f) * a.length, tb = ((phase + plantB) % 1f) * b.length;
        return Mathf.Lerp(a.Get(n, ta), b.Get(n, tb), mix);
    };

    // A clip read back as a source, so it can be derived from in turn.
    static Source FromClip(AnimationClip clip)
    {
        var s = new Source { clip = clip, length = clip.length };
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var c = AnimationUtility.GetEditorCurve(clip, b);
            s.curves[b.propertyName] = c;
            float sum = 0f, lo = float.MaxValue;
            for (int i = 0; i < 60; i++) { float v = c.Evaluate(clip.length * i / 60f); sum += v; lo = Mathf.Min(lo, v); }
            s.mean[b.propertyName] = sum / 60f; s.min[b.propertyName] = lo;
        }
        return s;
    }

    static Pose Reverse(Source s, float arms) => (n, t) => ArmSwing(n) ? Scale(s, n, s.length - t, arms) : s.Get(n, s.length - t);

    // A side step. The upper body keeps the walk with its swing and twist calmed; the legs come
    // from SolveSideStep, which places each ball of the foot on a straight sideways path.
    static readonly string[] LegCurves =
    {
        "Left Upper Leg In-Out", "Left Upper Leg Front-Back", "Left Upper Leg Twist In-Out", "Left Lower Leg Stretch",
        "Left Lower Leg Twist In-Out", "Left Foot Up-Down", "Left Foot Twist In-Out",
        "Right Upper Leg In-Out", "Right Upper Leg Front-Back", "Right Upper Leg Twist In-Out", "Right Lower Leg Stretch",
        "Right Lower Leg Twist In-Out", "Right Foot Up-Down", "Right Foot Twist In-Out",
    };

    static Pose SideStep(Source s, Dictionary<string, float[]> legs) => (n, t) =>
    {
        if (legs.TryGetValue(n, out var keys))
        {
            float f = t / s.length * (keys.Length - 1);
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, keys.Length - 2);
            return Mathf.Lerp(keys[i], keys[i + 1], f - i);
        }
        if (n.EndsWith("Twist Left-Right") || n.EndsWith("Turn Left-Right")) return Scale(s, n, t, 0.2f);
        if (ArmSwing(n)) return Scale(s, n, t, 0.35f);
        return s.Get(n, t);
    };

    // Solves the legs of a side step frame by frame on the real rig. The walk supplies the timing
    // and the lift (its ball-of-foot height); each foot is then steered, through hip in-out, hip
    // front-back and knee, onto a straight sideways path on its own side of the body. The leg on
    // the side of travel reaches out and the other closes in to halfGap from the centre line, never
    // across it. stride is how far each planted foot travels per support.
    static Dictionary<string, float[]> SolveSideStep(Source walk, int dir, float stride, float halfGap, StringBuilder log)
    {
        int frames = Mathf.RoundToInt(walk.length * Fps);
        var handler = new HumanPoseHandler(animator.avatar, animator.transform);
        var pose = new HumanPose();
        var muscles = new float[frames + 1][];
        var bodyPos = new Vector3[frames + 1]; var bodyRot = new Quaternion[frames + 1];
        Transform[] toes =
        {
            animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot),
            animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot),
        };
        var start = new Vector3[2, frames + 1];
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        for (int f = 0; f <= frames; f++)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, walk.clip, walk.length * f / frames);
            AnimationMode.EndSampling();
            handler.GetHumanPose(ref pose);
            muscles[f] = (float[])pose.muscles.Clone(); bodyPos[f] = pose.bodyPosition; bodyRot[f] = pose.bodyRotation;
            for (int s = 0; s < 2; s++) start[s, f] = body.transform.InverseTransformPoint(toes[s].position);
        }
        AnimationMode.StopAnimationMode();
        {
            // Writing the walk's own pose back must reproduce its own feet.
            pose.muscles = (float[])muscles[0].Clone(); pose.bodyPosition = bodyPos[0]; pose.bodyRotation = bodyRot[0];
            handler.SetHumanPose(ref pose);
            float err = (body.transform.InverseTransformPoint(toes[0].position) - start[0, 0]).magnitude;
            log.AppendLine("pose round trip error " + err.ToString("F3") + " m, body " + bodyPos[0].ToString("F3") + " root " + animator.transform.position.ToString("F3")
                + " in-out index " + Array.IndexOf(HumanTrait.MuscleName, "Left Upper Leg In-Out"));
        }

        int Index(string name) => Array.IndexOf(HumanTrait.MuscleName, name);
        var u = new float[2, frames + 1]; var side = new float[2]; var rest = new float[2];
        for (int s = 0; s < 2; s++)
        {
            float lo = float.MaxValue, hi = float.MinValue, fsum = 0f, rsum = 0f;
            for (int f = 0; f <= frames; f++)
            {
                float fw = Vector3.Dot(start[s, f], forward);
                lo = Mathf.Min(lo, fw); hi = Mathf.Max(hi, fw); fsum += fw; rsum += Vector3.Dot(start[s, f], rightAxis);
            }
            float mid = (lo + hi) * 0.5f, half = Mathf.Max(0.01f, (hi - lo) * 0.5f);
            for (int f = 0; f <= frames; f++) u[s, f] = (Vector3.Dot(start[s, f], forward) - mid) / half;
            side[s] = Mathf.Sign(rsum);
            rest[s] = fsum / (frames + 1);
        }
        var legBones = new[]
        {
            new[] { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot) },
            new[] { animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot) },
        };
        // Where each ball of the foot should be, frame by frame.
        var goals = new Vector3[2, frames + 1];
        for (int f = 0; f <= frames; f++)
            for (int s = 0; s < 2; s++)
            {
                Vector3 goal = start[s, f];
                float r = side[s] * (halfGap + stride * 0.5f) + dir * stride * 0.5f * u[s, f];
                goal = goal - rightAxis * Vector3.Dot(goal, rightAxis) + rightAxis * r;
                goals[s, f] = goal - forward * Vector3.Dot(goal, forward) + forward * rest[s];
            }
        // Solve, play the clip back, and aim again by what playback missed, until it lands.
        var aim = (Vector3[,])goals.Clone();
        var result = LegCurves.Concat(new[] { "RootT.y" }).ToDictionary(n => n, n => new float[frames + 1]);
        float worst = 0f;
        for (int pass = 0; pass < 4; pass++)
        {
            for (int f = 0; f <= frames; f++)
            {
                pose.muscles = (float[])muscles[f].Clone(); pose.bodyPosition = bodyPos[f]; pose.bodyRotation = bodyRot[f];
                // The walk's own foot attitude this frame, and its ankle-to-ball offset.
                handler.SetHumanPose(ref pose);
                var footRotation = new Quaternion[2]; var ankleOffset = new Vector3[2];
                for (int s = 0; s < 2; s++) { footRotation[s] = legBones[s][2].rotation; ankleOffset[s] = legBones[s][2].position - toes[s].position; }
                // Start from the previous frame's legs, so the solve cannot flip a twist between frames.
                if (f > 0) foreach (var n in LegCurves) pose.muscles[Index(n)] = result[n][f - 1];
                handler.SetHumanPose(ref pose);
                // A leg reaching sideways needs more length than the walk's body height leaves it:
                // lower the body until the supporting foot reaches the floor.
                int stance = aim[0, f].y <= aim[1, f].y ? 0 : 1;
                for (int lower = 0; lower < 4; lower++)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        // The ankle goes where it must for the ball of the foot to land on the aim,
                        // the foot keeping the walk's own attitude.
                        Vector3 ankle = body.transform.TransformPoint(aim[s, f]) + ankleOffset[s];
                        TwoBoneIK.Solve(legBones[s][0], legBones[s][1], legBones[s][2], ankle, body.transform.TransformDirection(forward));
                        legBones[s][2].rotation = footRotation[s];
                    }
                    handler.GetHumanPose(ref pose);
                    handler.SetHumanPose(ref pose);
                    float high = body.transform.InverseTransformPoint(toes[stance].position).y - aim[stance, f].y;
                    if (high < 0.002f) break;
                    pose.bodyPosition.y -= high / Mathf.Max(0.1f, animator.humanScale);
                    handler.SetHumanPose(ref pose);
                }
                foreach (var n in LegCurves) result[n][f] = Mathf.Clamp(pose.muscles[Index(n)], -1f, 1f);
                result["RootT.y"][f] = pose.bodyPosition.y;
            }
            // A loop closes on its own first frame.
            foreach (var n in result.Keys.ToList()) result[n][frames] = result[n][0];
            float jump = 0f; string jumpAt = "";
            foreach (var n in result.Keys)
                for (int f = 1; f <= frames; f++)
                    if (Mathf.Abs(result[n][f] - result[n][f - 1]) > jump) { jump = Mathf.Abs(result[n][f] - result[n][f - 1]); jumpAt = n + " @" + f; }
            log.AppendLine("  side step dir " + dir + " pass " + pass + " largest key step " + jump.ToString("F2") + " (" + jumpAt + ")");
            var check = Make("check", walk, walk.curves.Keys, walk.length, SideStep(walk, result), true);
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            worst = 0f;
            for (int f = 0; f <= frames; f++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, check, walk.length * f / frames);
                AnimationMode.EndSampling();
                for (int s = 0; s < 2; s++)
                {
                    Vector3 miss = goals[s, f] - body.transform.InverseTransformPoint(toes[s].position);
                    worst = Mathf.Max(worst, miss.magnitude);
                    aim[s, f] += miss;
                }
            }
            AnimationMode.StopAnimationMode();
            UnityEngine.Object.DestroyImmediate(check);
            log.AppendLine("  side step dir " + dir + " pass " + pass + " playback miss " + worst.ToString("F3") + " m");
        }
        log.AppendLine("side step dir " + dir + " stride " + stride + " playback miss " + worst.ToString("F3") + " m");
        return result;
    }

    // Stepping on the spot, for a turn in place.
    static Pose TurnStep(Source s) => (n, t) =>
    {
        if (n.EndsWith("Upper Leg Front-Back")) return Scale(s, n, t, 0.18f);
        if (n.EndsWith("Lower Leg Stretch") && s.min.TryGetValue(n, out float lo)) return lo + (s.Get(n, t) - lo) * 0.7f;
        if (n.EndsWith("Foot Up-Down") || n.EndsWith("Toes Up-Down")) return Scale(s, n, t, 0.5f);
        if (ArmSwing(n) || n.EndsWith("Twist Left-Right")) return Scale(s, n, t, 0.3f);
        return s.Get(n, t);
    };

    // A run brought to a stop: the stride slows over a step and settles into the stance.
    // A run brought to a stand. The stride runs on only as far as the hero brakes (Brake 52 m/s^2
    // from a 6.8 m/s run covers 0.44 m), so the front foot plants and stays put; then the rear leg
    // lifts and steps in while the body settles into the idle stance. rearLeft says which leg trails
    // at the plant.
    public const float StopTravel = 0.44f, StopBrake = 0.14f;
    static Pose Stop(Source run, Source idle, float plant, float duration, bool rearLeft)
    {
        float advance = StopTravel / Mathf.Max(0.5f, Mathf.Abs(runPace));
        string rear = rearLeft ? "Left " : "Right ";
        return (n, t) =>
        {
            float k = Mathf.Clamp01(t / StopBrake);
            float p = plant * run.length + advance * (1f - (1f - k) * (1f - k));
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StopBrake * 0.7f, duration * 0.9f, t));
            float v = Mathf.Lerp(run.Get(n, p), idle.Get(n, 0f), w);
            float step = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - StopBrake * 0.7f) / (duration * 0.8f - StopBrake * 0.7f)));
            if (n == rear + "Lower Leg Stretch") v -= 0.8f * step;
            if (n == rear + "Upper Leg Front-Back") v += 0.35f * step;
            return v;
        };
    }
    static float runPace = 6.35f;

    // Keeps one ball of the foot where it stands from `from` seconds on, through a clip that would
    // otherwise move it, by solving that leg on the rig frame by frame (and correcting by what playback
    // misses, as SolveSideStep does). Returns the leg's curves, to lay over the base pose.
    static Dictionary<string, float[]> PinFoot(Source settings, Pose basePose, float length, bool left, Func<float, Vector3, Vector3> goalAt, StringBuilder log)
    {
        int frames = Mathf.RoundToInt(length * Fps);
        var baseClip = Make("pin base", settings, settings.curves.Keys, length, basePose, false);
        var handler = new HumanPoseHandler(animator.avatar, animator.transform);
        var pose = new HumanPose();
        int sideIndex = left ? 0 : 1;
        Transform toe = animator.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes) ?? animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        var bones = left
            ? new[] { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot) }
            : new[] { animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
        string[] names = LegCurves.Skip(sideIndex * 7).Take(7).ToArray();
        int Index(string name) => Array.IndexOf(HumanTrait.MuscleName, name);
        var muscles = new float[frames + 1][]; var bodyPos = new Vector3[frames + 1]; var bodyRot = new Quaternion[frames + 1];
        var goal = new Vector3[frames + 1];
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        for (int f = 0; f <= frames; f++)
        {
            float t = length * f / frames;
            AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(animator.gameObject, baseClip, t); AnimationMode.EndSampling();
            handler.GetHumanPose(ref pose);
            muscles[f] = (float[])pose.muscles.Clone(); bodyPos[f] = pose.bodyPosition; bodyRot[f] = pose.bodyRotation;
            Vector3 at = body.transform.InverseTransformPoint(toe.position);
            goal[f] = goalAt(t, at);
        }
        AnimationMode.StopAnimationMode();
        var aim = (Vector3[])goal.Clone();
        var result = names.ToDictionary(n => n, n => new float[frames + 1]);
        float worst = 0f;
        for (int pass = 0; pass < 4; pass++)
        {
            for (int f = 0; f <= frames; f++)
            {
                pose.muscles = (float[])muscles[f].Clone(); pose.bodyPosition = bodyPos[f]; pose.bodyRotation = bodyRot[f];
                handler.SetHumanPose(ref pose);
                Quaternion footRotation = bones[2].rotation; Vector3 ankleOffset = bones[2].position - toe.position;
                if (f > 0) foreach (var n in names) pose.muscles[Index(n)] = result[n][f - 1];
                handler.SetHumanPose(ref pose);
                TwoBoneIK.Solve(bones[0], bones[1], bones[2], body.transform.TransformPoint(aim[f]) + ankleOffset, body.transform.TransformDirection(forward));
                bones[2].rotation = footRotation;
                handler.GetHumanPose(ref pose);
                foreach (var n in names) result[n][f] = Mathf.Clamp(pose.muscles[Index(n)], -1f, 1f);
            }
            var check = Make("pin check", settings, settings.curves.Keys, length, Overlay(basePose, length, result), false);
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            worst = 0f;
            for (int f = 0; f <= frames; f++)
            {
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(animator.gameObject, check, length * f / frames); AnimationMode.EndSampling();
                Vector3 miss = goal[f] - body.transform.InverseTransformPoint(toe.position);
                worst = Mathf.Max(worst, miss.magnitude);
                aim[f] += miss;
            }
            AnimationMode.StopAnimationMode();
            UnityEngine.Object.DestroyImmediate(check);
        }
        UnityEngine.Object.DestroyImmediate(baseClip);
        log.AppendLine("pinned " + (left ? "left" : "right") + " foot, playback miss " + worst.ToString("F3") + " m");
        return result;
    }

    // A base pose with some curves replaced by per-frame values.
    static Pose Overlay(Pose basePose, float length, Dictionary<string, float[]> keys) => (n, t) =>
    {
        if (!keys.TryGetValue(n, out var k)) return basePose(n, t);
        float f = Mathf.Clamp01(t / length) * (k.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, k.Length - 2);
        return Mathf.Lerp(k[i], k[i + 1], f - i);
    };

    // How far the planted foot moves after the brake, the measure that matters for a stop.
    static float PlantedTravel(AnimationClip clip, bool plantedLeft)
    {
        Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        Vector3 Sample(Transform toe, float t)
        {
            AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(animator.gameObject, clip, t); AnimationMode.EndSampling();
            var p = body.transform.InverseTransformPoint(toe.position); p.y = 0f; return p;
        }
        Transform planted = plantedLeft ? lt : rt;
        Vector3 from = Sample(planted, StopBrake);
        float worst = 0f;
        for (float t = StopBrake; t <= clip.length; t += 1f / 120f) worst = Mathf.Max(worst, (Sample(planted, t) - from).magnitude);
        return worst;
    }

    // ------------------------------------------------------------ measurement on the real rig

    public struct Measure
    {
        public Vector3 pace;            // ground velocity the clip implies at normal playback, model space
        public float slip, drift, minGap, leftPlant, lowest;
    }

    static GameObject body;
    // The library walk's lowest ball-of-foot height: the floor every clip must reach.
    static float floorRef;
    static Animator animator;
    static Vector3 forward = Vector3.forward, rightAxis = Vector3.right;

    static string DumpTo;
    public static Measure Probe(AnimationClip clip, bool loop)
    {
        int n = Mathf.Max(24, Mathf.RoundToInt(clip.length * 120f));
        // The ball of the foot is what stays planted from foot-flat to toe-off.
        Transform Contact(HumanBodyBones toes, HumanBodyBones foot) => animator.GetBoneTransform(toes) ?? animator.GetBoneTransform(foot);
        var lt = Contact(HumanBodyBones.LeftToes, HumanBodyBones.LeftFoot);
        var rt = Contact(HumanBodyBones.RightToes, HumanBodyBones.RightFoot);
        var L = new Vector3[n]; var R = new Vector3[n];
        // The editor's own sampler poses a humanoid from its muscle curves outside play mode.
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        for (int i = 0; i < n; i++)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, clip.length * i / n);
            AnimationMode.EndSampling();
            L[i] = body.transform.InverseTransformPoint(lt.position);
            R[i] = body.transform.InverseTransformPoint(rt.position);
        }
        if (DumpTo != null)
        {
            var csv = new StringBuilder("i,lx,ly,lz,rx,ry,rz\n");
            for (int i = 0; i < n; i++) csv.AppendLine(i + "," + L[i].x.ToString("F3") + "," + L[i].y.ToString("F3") + "," + L[i].z.ToString("F3") + "," + R[i].x.ToString("F3") + "," + R[i].y.ToString("F3") + "," + R[i].z.ToString("F3"));
            File.WriteAllText(DumpTo, csv.ToString());
            DumpTo = null;
        }
        float dt = clip.length / n;
        float floor = Mathf.Min(L.Min(p => p.y), R.Min(p => p.y));
        // A foot counts as planted in the lowest fifth of its own lift (at least 2 cm), so a jog's
        // quick ground contact is found as reliably as a walk's long one.
        float band = Mathf.Max(0.02f, 0.2f * Mathf.Min(L.Max(p => p.y) - floor, R.Max(p => p.y) - floor));
        var m = new Measure { minGap = float.MaxValue, lowest = floor };
        // Support intervals, found around the loop (a stance may straddle the clip's end).
        var supports = new List<(Vector3[] foot, int start, int length)>();
        int leftStart = -1;
        foreach (var foot in new[] { L, R })
        {
            bool Down(int i) => foot[((i % n) + n) % n].y < floor + band;
            int s0 = -1;
            for (int i = 0; i < n && s0 < 0; i++) if (!Down(i)) s0 = i;
            if (s0 < 0) continue;
            int run = -1;
            for (int k = s0; k <= s0 + n; k++)
            {
                bool down = k < s0 + n && Down(k);
                if (down && run < 0) { run = k; if (foot == L && (leftStart < 0 || (k % n) < leftStart)) leftStart = k % n; }
                if (!down && run >= 0) { if (k - run >= 3) supports.Add((foot, run, k - run)); run = -1; }
            }
        }
        Vector3 At(Vector3[] foot, int k) => foot[((k % n) + n) % n];
        Vector3 sum = Vector3.zero; float total = 0f;
        var rates = new List<Vector3>();
        foreach (var (foot, start, length) in supports)
        {
            Vector3 v = (At(foot, start + length - 1) - At(foot, start)) / ((length - 1) * dt);
            v.y = 0f;
            rates.Add(v);
            sum += v * length; total += length;
        }
        Vector3 mean = total > 0f ? sum / total : Vector3.zero;
        m.pace = -mean;
        float sq = 0f;
        foreach (var v in rates) sq += (v - mean).sqrMagnitude;
        m.slip = rates.Count > 0 ? Mathf.Sqrt(sq / rates.Count) : 0f;
        // Drift: how far a planted foot strays from where steady travel at the clip's pace takes it.
        foreach (var (foot, start, length) in supports)
            for (int k = 0; k < length; k++)
            {
                Vector3 d = At(foot, start + k) - (At(foot, start) + mean * k * dt);
                d.y = 0f;
                m.drift = Mathf.Max(m.drift, d.magnitude);
            }
        for (int i = 0; i < n; i++) { var d = L[i] - R[i]; d.y = 0f; m.minGap = Mathf.Min(m.minGap, d.magnitude); }
        m.leftPlant = leftStart < 0 ? 0f : (float)leftStart / n;
        return m;
    }

    // Rotates a loop so that its left foot plants at phase zero.
    static AnimationClip Rotate(AnimationClip clip, float phase)
    {
        if (phase <= 0.001f || phase >= 0.999f) return clip;
        float shift = phase * clip.length;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            var c = AnimationUtility.GetEditorCurve(clip, b);
            var keys = new Keyframe[c.length];
            for (int i = 0; i < c.length; i++)
            {
                float t = c.keys[i].time;
                keys[i] = new Keyframe(t, c.Evaluate((t + shift) % clip.length));
            }
            keys[keys.Length - 1].value = keys[0].value;
            var rotated = new AnimationCurve(keys);
            for (int i = 0; i < rotated.length; i++) rotated.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, b, rotated);
        }
        return clip;
    }

    static Entry Record(string clipName, string source, string derivation, AnimationClip clip, bool loop, bool footIK, Measure m)
    {
        return new Entry
        {
            clip = clipName, source = source, derivation = derivation, length = clip.length, loop = loop, footIK = footIK,
            paceForward = Vector3.Dot(m.pace, forward), paceRight = Vector3.Dot(m.pace, rightAxis),
            leftPlant = m.leftPlant, stanceSlip = m.slip, stanceDrift = m.drift, minFootGap = m.minGap, floorGap = m.lowest - floorRef,
        };
    }

    static AnimationClip Save(AnimationClip clip)
    {
        string path = OutDir + "/" + clip.name + ".anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    public static void Build()
    {
        var log = new StringBuilder();
        try
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(Library).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Source walk = Load(all, "Walk_Loop"), jog = Load(all, "Jog_Fwd_Loop"), sprint = Load(all, "Sprint_Loop"), idle = Load(all, "Idle_Loop");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Humans/Body/Superhero_Male_FullBody.fbx");
            body = UnityEngine.Object.Instantiate(prefab);
            body.transform.position = Vector3.zero; body.transform.rotation = Quaternion.identity;
            animator = body.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) throw new Exception("body rig has no humanoid animator");

            // The library walk defines forward: its planted feet travel backward under the body.
            var walkM = Probe(walk.clip, true);
            floorRef = walkM.lowest;
            if (walkM.pace.magnitude < 0.3f) throw new Exception("the rig did not take the walk's pose (pace " + walkM.pace + ")");
            forward = walkM.pace.normalized;
            rightAxis = Vector3.Cross(Vector3.up, forward);
            log.AppendLine("forward " + forward.ToString("F3"));

            var ledger = new Ledger { note = "A01 gait ledger. Paces are metres per second at playback rate 1 in the character's own frame. Slip is the RMS spread of planted-foot velocity (m/s); drift the worst single-support stray from steady travel (m). Authored by Assets/Editor/GaitAuthoring.cs from the shared library; no generative model." };
            foreach (var (src, name) in new[] { (walk, "Walk_Loop"), (jog, "Jog_Fwd_Loop"), (sprint, "Sprint_Loop") })
            {
                DumpTo = "Logs/gait-" + name + ".csv";
                ledger.clips.Add(Record(name, "UAL1", "library", src.clip, true, true, Probe(src.clip, true)));
            }

            var curves = walk.curves.Keys.ToList();
            void Loop(string name, Source src, Pose pose, string derivation)
            {
                var clip = Make(name, src, curves, src.length, pose, true);
                DumpTo = "Logs/gait-" + name + ".csv";
                var m = Probe(clip, true);
                clip = Rotate(clip, m.leftPlant);
                m = Probe(clip, true);
                Save(clip);
                ledger.clips.Add(Record(name, src.clip.name, derivation, clip, true, false, m));
                log.AppendLine(name + " pace " + m.pace.ToString("F2") + " slip " + m.slip.ToString("F3") + " drift " + m.drift.ToString("F3") + " gap " + m.minGap.ToString("F3") + " plant " + m.leftPlant.ToString("F2"));
            }

            Loop("Gait_Walk_Bwd", walk, Reverse(walk, 0.7f), "walk cycle played in reverse, arm swing 70%");
            Loop("Gait_Jog_Bwd", jog, Reverse(jog, 0.7f), "jog cycle played in reverse, arm swing 70%");

            {
                float pw = ledger.clips.First(e => e.clip == "Walk_Loop").leftPlant, pj = ledger.clips.First(e => e.clip == "Jog_Fwd_Loop").leftPlant;
                float easyLength = (walk.length + jog.length) * 0.5f;
                var between = Make("Gait_Jog_Easy", walk, curves, easyLength, Between(walk, pw, jog, pj, 0.5f, easyLength), true);
                var easy = FromClip(between);
                Loop("Gait_Jog_Easy", easy, (n, t) => easy.Get(n, t), "walk and jog cycles run on one " + easyLength.ToString("0.00") + " s clock from their left plants, mixed half and half");
                Loop("Gait_Jog_Easy_Bwd", easy, Reverse(easy, 0.7f), "the easy jog played in reverse, arm swing 70%");
            }

            // The side step: the longest stride per support whose worse direction still keeps its
            // planted foot as steady as the library walk does (drift within 25% of the walk's).
            float walkDrift = ledger.clips.First(e => e.clip == "Walk_Loop").stanceDrift;
            float chosen = 0.24f;
            foreach (float stride in new[] { 0.24f, 0.3f, 0.36f, 0.42f, 0.48f })
            {
                float worst = 0f, slowest = float.MaxValue, gap = float.MaxValue;
                foreach (int dir in new[] { 1, -1 })
                {
                    var c = Make("trial", walk, curves, walk.length, SideStep(walk, SolveSideStep(walk, dir, stride, 0.07f, log)), true);
                    var m = Probe(c, true);
                    UnityEngine.Object.DestroyImmediate(c);
                    worst = Mathf.Max(worst, m.drift); gap = Mathf.Min(gap, m.minGap);
                    slowest = Mathf.Min(slowest, dir * Vector3.Dot(m.pace, rightAxis));
                }
                log.AppendLine("side step stride " + stride + " slowest " + slowest.ToString("F2") + " worst drift " + worst.ToString("F3") + " gap " + gap.ToString("F3"));
                if (gap >= MinFootGap && slowest > 0f && worst <= walkDrift * 1.25f) chosen = stride;
            }
            log.AppendLine("side step chosen stride " + chosen);
            Loop("Gait_Walk_Right", walk, SideStep(walk, SolveSideStep(walk, 1, chosen, 0.07f, log)), "walk timing and lift, feet solved onto a straight sideways path (" + chosen + " m per support), arm swing 35%");
            Loop("Gait_Walk_Left", walk, SideStep(walk, SolveSideStep(walk, -1, chosen, 0.07f, log)), "walk timing and lift, feet solved onto a straight sideways path (" + chosen + " m per support), arm swing 35%");

            Loop("Gait_Turn_Step", walk, TurnStep(walk), "walk cycle stepped on the spot: thigh swing 18%, knee lift 70%");

            // The stop. At the jog's left plant the front foot is found by sampling (whichever ball of
            // the foot is further forward); it is held on the ground, sliding back under the body only
            // by the braking distance, while the trailing leg steps in.
            var jogEntry = ledger.clips.First(e => e.clip == "Jog_Fwd_Loop");
            runPace = jogEntry.paceForward;
            {
                Pose stop = Stop(jog, idle, jogEntry.leftPlant, 0.55f, true);
                var probe = Make("stop probe", jog, curves, 0.55f, stop, false);
                Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(animator.gameObject, probe, 0f); AnimationMode.EndSampling();
                Vector3 l0 = body.transform.InverseTransformPoint(lt.position), r0 = body.transform.InverseTransformPoint(rt.position);
                AnimationMode.StopAnimationMode();
                UnityEngine.Object.DestroyImmediate(probe);
                bool frontLeft = Vector3.Dot(l0, forward) > Vector3.Dot(r0, forward);
                Vector3 plantedAt = frontLeft ? l0 : r0;
                stop = Stop(jog, idle, jogEntry.leftPlant, 0.55f, !frontLeft);
                float floorToe = 0.01f;
                Vector3 Hold(float t, Vector3 sampled)
                {
                    float k = Mathf.Clamp01(t / StopBrake);
                    float slide = StopTravel * (1f - (1f - k) * (1f - k));
                    return plantedAt - forward * slide + Vector3.up * (floorToe - plantedAt.y);
                }
                stop = Overlay(stop, 0.55f, PinFoot(jog, stop, 0.55f, frontLeft, Hold, log));
                var clip = Save(Make("Gait_Jog_Stop", jog, curves, 0.55f, stop, false));
                var e = Record("Gait_Jog_Stop", jog.clip.name, "jog stride at the plant; the front foot is held on the ground by IK, sliding back under the body only by the hero's braking distance (" + StopTravel + " m over " + StopBrake + " s), while the trailing leg steps in and the body settles toward the idle stance", clip, false, false, Probe(clip, false));
                e.stanceDrift = PlantedTravel(clip, frontLeft);
                ledger.clips.Add(e);
                log.AppendLine("Gait_Jog_Stop front foot " + (frontLeft ? "left" : "right") + ", travel after the brake " + e.stanceDrift.ToString("F3") + " m");
            }
            AssetDatabase.DeleteAsset(OutDir + "/Gait_Jog_Start.anim");

            File.WriteAllText(LedgerPath, JsonUtility.ToJson(ledger, true));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            File.WriteAllText("Logs/gait-authoring.txt", log.ToString());
            Debug.Log("GAIT AUTHORED " + ledger.clips.Count + " ledger entries\n" + log);
        }
        catch (Exception e)
        {
            Debug.LogError("GAIT FAILED " + e);
            File.WriteAllText("Logs/gait-authoring.txt", log + "\n" + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (body != null) UnityEngine.Object.DestroyImmediate(body);
        }
    }
}
