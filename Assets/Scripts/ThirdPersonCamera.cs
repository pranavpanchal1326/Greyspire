using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    const float BaseSensitivity = 2.2f;
    const float Distance = 5.7f;

    public Transform target;

    Camera cam;
    Vector3 focus, focusVel;
    float yaw, pitch = 22f, shake, kick, dist = Distance, side, speedFov;
    Vector2 lookEased;
    readonly RaycastHit[] boomHits = new RaycastHit[48];
    float lockYawVelocity, lockPitchVelocity;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    public void Shake(float amount)
    {
        shake = Mathf.Max(shake, amount);
    }

    // Momentary field-of-view punch: dashes and the Colour Burst.
    public void Kick(float degrees)
    {
        kick = Mathf.Max(kick, degrees);
    }

    // A shove in one direction that springs straight back: the camera feeling a killing blow land.
    Vector3 punch;

    public void Punch(Vector3 worldDir, float metres)
    {
        punch = worldDir.normalized * metres;
    }

    // Cinematic framing for dialogue: hold a shot on a point, optionally drifting round it.
    bool cine;
    Vector3 cineFocus;
    float cineYaw, cinePitch, cineDist, cineOrbit;

    public void Cine(Vector3 focusPoint, float yawDeg, float pitchDeg, float distance, float orbitPerSecond)
    {
        cine = true;
        cineFocus = focusPoint; cineYaw = yawDeg; cinePitch = pitchDeg; cineDist = distance; cineOrbit = orbitPerSecond;
    }

    public void EndCine()
    {
        cine = false;
    }

    public void Snap()
    {
        if (target != null) focus = target.position + Vector3.up * 1.5f;
        focusVel = Vector3.zero;
        dist = Distance;
        punch = Vector3.zero;
        lockYawVelocity = lockPitchVelocity = 0f;
    }

    // Points the gameplay camera: yaw 0 looks north, from behind a hero who faces north.
    public void Face(float yawDeg)
    {
        yaw = yawDeg;
        pitch = 22f;
        lookEased = Vector2.zero;
    }

    // An arrival: the camera opens high over a place, looking at it, drifts a little way
    // round, then comes down to its usual spot behind the hero.
    Vector3 entFrom, entLook;
    float entT, entDur, entHold, entDrift;

    public bool Arriving => entT > 0f;

    public void Entrance(Vector3 vantage, Vector3 lookAt, float seconds)
    {
        if (Save.CameraAuto <= 0f) { entT = 0f; Snap(); cam.fieldOfView = Save.CameraFov; return; }
        entFrom = vantage;
        entLook = lookAt;
        entT = entDur = seconds;
        entHold = 0.38f;        // share of the time spent on the opening view before descending
        entDrift = 16f;         // degrees of slow orbit over the whole move
        transform.position = vantage;
        transform.rotation = Quaternion.LookRotation(lookAt - vantage);
        cam.fieldOfView = 44f;
    }

    // Cuts an arrival short: from wherever the camera has got to, straight down to the hero.
    public void SkipEntrance()
    {
        if (entT <= 0f) return;
        entFrom = transform.position;
        entLook = transform.position + transform.forward * 12f;
        entT = entDur = 0.6f;
        entHold = 0f;
        entDrift = 0f;
    }

    void LateUpdate()
    {
        if (target == null || GameClocks.HardPause) return;
        float udt = Time.unscaledDeltaTime;

        Game.GameState state = Game.I.State;
        if (cine && state == Game.GameState.Story)
        {
            cineYaw += cineOrbit * udt * Save.CameraAuto;
            Quaternion shotRot = Quaternion.Euler(cinePitch, cineYaw, 0);
            Vector3 pos = cineFocus - shotRot * Vector3.forward * cineDist;
            pos.y = Mathf.Max(pos.y, 0.4f);
            if (Save.CameraAuto <= 0f)
            {
                transform.position = pos; transform.rotation = shotRot; cam.fieldOfView = Save.CameraFov;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, pos, 2.5f * udt);
                transform.rotation = Quaternion.Slerp(transform.rotation, shotRot, 2.5f * udt);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 48f, 3f * udt);
            }
            // Keep the gameplay camera lined up behind the shot so the cut back is gentle.
            yaw = cineYaw;
            focus = target.position + Vector3.up * 1.5f;
            return;
        }
        if (Game.I.Roaming)
        {
            // Ease the raw mouse movement very slightly so the view glides instead of stepping.
            Vector2 look = GameInput.Look * (BaseSensitivity * Mathf.Lerp(0.3f, 1.7f, Save.Sensitivity));
            lookEased = Vector2.Lerp(lookEased, look, 1f - Mathf.Exp(-udt * 34f));
            Enemy locked = Game.I.Player.LockedTarget;
            if (locked != null && Save.CameraAuto > 0f)
            {
                Vector3 toward = locked.transform.position + Vector3.up * Mathf.Clamp(locked.height * 0.65f, 0.5f, 1.6f)
                    - (target.position + Vector3.up * 1.5f);
                float heading = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                float elevation = Mathf.Clamp(18f - Mathf.Atan2(toward.y, new Vector2(toward.x, toward.z).magnitude) * Mathf.Rad2Deg, -8f, 38f);
                yaw = Mathf.SmoothDampAngle(yaw, heading, ref lockYawVelocity, 0.12f, 540f * Save.CameraAuto, udt);
                pitch = Mathf.SmoothDampAngle(pitch, elevation, ref lockPitchVelocity, 0.16f, 240f * Save.CameraAuto, udt);
                lookEased = Vector2.zero;
            }
            else
            {
                lockYawVelocity = lockPitchVelocity = 0f;
                yaw += lookEased.x;
                pitch = Mathf.Clamp(pitch - lookEased.y * (Save.InvertLook ? -1f : 1f), -10f, 70f);
            }
        }
        else if (state == Game.GameState.Menu || state == Game.GameState.Shrine || state == Game.GameState.Results)
        {
            yaw += 8f * udt * Save.CameraAuto;
            pitch = Mathf.MoveTowards(pitch, 18f, 20f * udt * Save.CameraAuto);
        }

        bool lockedOn = Game.I.Roaming && Game.I.Player.LockedTarget != null;
        Vector3 lookAhead = Game.I.Roaming && !lockedOn ? Vector3.ClampMagnitude(Game.I.Player.GroundVelocity * 0.055f, 0.4f) : Vector3.zero;
        Vector3 want = target.position + Vector3.up * 1.5f + lookAhead;
        if (lockedOn)
        {
            Enemy locked = Game.I.Player.LockedTarget;
            Vector3 enemyFocus = locked.transform.position + Vector3.up * Mathf.Clamp(locked.height * 0.65f, 0.5f, 1.6f);
            want += Vector3.ClampMagnitude(enemyFocus - want, 4f) * (0.22f * Save.CameraAuto);
        }
        focus = Vector3.SmoothDamp(focus, want, ref focusVel, 0.08f, 100f, udt);

        bool showcase = state == Game.GameState.Menu || state == Game.GameState.Shrine || state == Game.GameState.Codex;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
        float wantDist = showcase ? 3.9f : lockedOn ? 6.2f : Distance;
        dist = Mathf.MoveTowards(dist, wantDist, 5f * udt);

        // On menu-style screens the hero stands to one side of the buttons: to the right of the
        // title screen's column, to the left of the forge and the archive.
        side = Mathf.MoveTowards(side, state == Game.GameState.Menu ? -1.9f : showcase ? 1.5f : lockedOn ? 0f : 0.38f, 6f * udt);

        Vector3 seat = focus - rot * Vector3.forward * dist + rot * Vector3.right * side;
        Vector3 boom = seat - focus;
        float safeDistance = SafeBoom(focus, boom);
        float requestedDistance = boom.magnitude;
        if (safeDistance < requestedDistance)
        {
            seat = focus + boom.normalized * safeDistance;
            dist = Mathf.Min(dist, safeDistance);
        }
        kick = Mathf.MoveTowards(kick, 0f, udt * 30f);
        speedFov = Mathf.Lerp(speedFov, Game.I.Roaming ? Game.I.Player.Speed01 * 4f : 0f, 3f * udt);

        if (entT > 0f && Save.CameraAuto <= 0f) entT = 0f;
        if (entT > 0f)
        {
            entT -= udt;
            float k = 1f - Mathf.Clamp01(entT / entDur);
            float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(entHold, 1f, k));
            Vector3 vantage = entLook + Quaternion.AngleAxis(k * entDrift, Vector3.up) * (entFrom - entLook);
            Vector3 at = Vector3.Lerp(vantage, seat, down);
            Vector3 looking = Vector3.Lerp(entLook, focus, down);
            transform.position = at;
            transform.rotation = Quaternion.Slerp(Quaternion.LookRotation(looking - at), rot, down * down);
            cam.fieldOfView = Mathf.Lerp(44f, Save.CameraFov, down);
            return;
        }

        punch = Vector3.Lerp(punch, Vector3.zero, 1f - Mathf.Exp(-udt * 11f));
        // Shake is a smooth, fast wander, not a new random place every frame: a jolt, not static.
        float st = Time.unscaledTime * 22f;
        Vector3 wander = new Vector3(Mathf.PerlinNoise(st, 3.1f) - 0.5f, Mathf.PerlinNoise(7.7f, st) - 0.5f, Mathf.PerlinNoise(st, st * 0.7f + 11f) - 0.5f) * 2f;
        float motion = Save.CameraMotion;
        Vector3 effectSeat = seat + (punch + wander * shake * 0.8f) * motion;
        Vector3 effectBoom = effectSeat - focus;
        transform.position = focus + effectBoom.normalized * SafeBoom(focus, effectBoom);
        transform.rotation = rot * Quaternion.Euler(wander.y * shake * 3.5f * motion, wander.x * shake * 3.5f * motion, wander.z * shake * 2f * motion);
        shake = Mathf.MoveTowards(shake, 0f, udt * 1.6f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Save.CameraFov + (kick + speedFov) * motion, 1f - Mathf.Exp(-14f * udt));
    }

    // Check the whole boom, including its shoulder offset. Actors and trigger volumes are not walls.
    public int SaturatedQueries { get; private set; }
    public float SafeBoom(Vector3 origin, Vector3 boom)
    {
        float distance = boom.magnitude;
        if (distance < 0.001f) return 0f;
        int count = Physics.SphereCastNonAlloc(origin, 0.24f, boom / distance, boomHits, distance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = boomHits;
        if (count == boomHits.Length)
        {
            SaturatedQueries++;
            hits = Physics.SphereCastAll(origin, 0.24f, boom / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        float safe = distance;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = hits[i].collider;
            if (!CombatContact.Solid(obstacle)) continue;
            safe = Mathf.Min(safe, Mathf.Max(0.3f, hits[i].distance - 0.08f));
        }
        return safe;
    }
}
