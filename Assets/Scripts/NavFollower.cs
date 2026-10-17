using UnityEngine;
using UnityEngine.AI;

// Walks its owner across the floor's NavMesh. The owner decides where to go and why
// (see Companion); this decides how to get there: round the rocks, not through them.
// Destinations are sampled on their local support and require a complete route.
// The owner moves through checked collision; missing navigation never grants a warp.
[RequireComponent(typeof(NavMeshAgent))]
public class NavFollower : MonoBehaviour
{
    const float RepathEvery = 0.2f;     // seconds between new routes to a moving goal
    const float RepathIfMoved = 0.3f;   // and only once the goal has moved this far

    public string label = "Agent";
    public string state = "";

    NavMeshAgent agent;
    LineRenderer route;
    Transform flag;
    Vector3 lastGoal;
    float repath;
    NavMeshPath path;
    Vector3[] corners = new Vector3[0];
    int corner;
    float bodyRadius = 0.35f, bodyHeight = 1f;
    public bool RouteComplete { get; private set; }
    public Vector3[] RouteCorners => corners;
    public NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };

    public bool OnMesh => agent.enabled && agent.isOnNavMesh;

    public static NavFollower AddTo(GameObject owner)
    {
        bool active = owner.activeSelf;
        owner.SetActive(false);
        var component = owner.GetComponent<NavMeshAgent>();
        if (component == null) component = owner.AddComponent<NavMeshAgent>();
        component.enabled = false;
        var follower = owner.GetComponent<NavFollower>();
        if (follower == null) follower = owner.AddComponent<NavFollower>();
        owner.SetActive(active);
        return follower;
    }

    void Awake()
    {
        path = new NavMeshPath();
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;                      // nothing to stand on until a floor is baked
        agent.radius = 0.35f;
        agent.height = 1f;
        agent.baseOffset = 0.05f;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.acceleration = 30f;                   // quick off the mark, and
        agent.angularSpeed = 540f;                  // turns through corners instead of snapping
        agent.autoBraking = true;                   // eases into the stopping distance
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = 60;
        agent.autoRepath = true;
    }

    void OnEnable() => NavField.Agents.Add(this);

    void OnDisable()
    {
        NavField.Agents.Remove(this);
        if (route != null) Destroy(route.gameObject);
        if (flag != null) Destroy(flag.gameObject);
    }

    // Steps onto the NavMesh if there is one underfoot. False where none is baked.
    public bool Attach()
    {
        if (OnMesh) return true;
        var controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            Vector3 scale = transform.lossyScale;
            bodyRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            bodyHeight = controller.height * Mathf.Abs(scale.y);
        }
        else if (GetComponent<Resident>() != null || GetComponent<Drained>() != null) { bodyRadius = 0.4f; bodyHeight = 1.8f; }
        int profile = NavField.Profile(bodyRadius, bodyHeight);
        if (profile == -1 || NavField.Current == null || !NavField.Current.Ready)
        {
            if (agent.enabled) agent.enabled = false;
            return false;
        }
        agent.agentTypeID = profile; agent.radius = bodyRadius; agent.height = bodyHeight;
        if (!TryDestination(transform.position, out Vector3 destination)) return false;
        Vector3 original = transform.position;
        agent.enabled = true;
        if (!agent.Warp(destination)) { agent.enabled = false; transform.position = original; return false; }
        transform.position = original; // Attach must not move an actor between levels.
        agent.isStopped = true;
        GetComponent<Actor>()?.CaptureContactPose();
        repath = 0f;
        return agent.isOnNavMesh;
    }

    // Head for `goal` and stop `stop` metres short of it. Safe to call every frame:
    // a new route is only asked for when the goal has really moved.
    public void MoveTo(Vector3 goal, float stop, float speed)
    {
        Advance(goal, stop, speed, GameClocks.WorldDelta);
    }

    public bool TryDestination(Vector3 goal, out Vector3 destination)
    {
        destination = goal;
        if (!WorldSupport.TryGround(goal, out var support, 0.35f, 0.7f)
            || !NavMesh.SamplePosition(support.point, out var hit, 1.5f, Filter)
            || Mathf.Abs(hit.position.y - support.point.y) > 0.35f
            || !WorldSupport.TryStand(hit.position, bodyRadius, bodyHeight, out destination, transform)) return false;
        return true;
    }

    public Vector3 DirectionTo(Vector3 goal, float dt)
    {
        if (!OnMesh) return Vector3.zero;
        agent.nextPosition = transform.position;
        repath -= dt;
        if (repath <= 0f || (goal - lastGoal).magnitude >= RepathIfMoved)
        {
            RouteComplete = TryDestination(goal, out Vector3 destination)
                && NavMesh.CalculatePath(transform.position, destination, Filter, path) && path.status == NavMeshPathStatus.PathComplete;
            corners = RouteComplete ? path.corners : new Vector3[0]; corner = 1;
            if (RouteComplete) agent.SetPath(path); else agent.ResetPath();
            agent.isStopped = true;
            lastGoal = goal; repath = RepathEvery;
        }
        while (corner < corners.Length)
        {
            Vector3 gap = corners[corner] - transform.position;
            float heightGap = Mathf.Abs(gap.y); gap.y = 0f;
            if (gap.magnitude >= 0.3f || heightGap > 0.75f) break;
            corner++;
        }
        if (corner >= corners.Length) return Vector3.zero;
        Vector3 to = corners[corner] - transform.position;
        to.y = 0f;
        return to.normalized;
    }

    public void Advance(Vector3 goal, float stop, float speed, float dt)
    {
        if (!Attach() || dt <= 0f) return;
        Vector3 dir = DirectionTo(goal, dt);
        float dist = Vector3.Distance(goal, transform.position);
        if (dist > stop && dir.sqrMagnitude > 0f)
        {
            WorldSupport.TryMove(transform, dir * Mathf.Min(speed * dt, dist - stop), bodyRadius, bodyHeight);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 10f);
        }
        agent.nextPosition = transform.position;
    }

    public void Hold()
    {
        if (OnMesh) agent.isStopped = true;
    }

    // For when the owner has been left far behind (a floor change).
    public bool Teleport(Vector3 to)
    {
        if (!OnMesh || !TryDestination(to, out Vector3 feet) || !agent.Warp(feet)) return false;
        transform.position = feet;
        GetComponent<Actor>()?.CaptureContactPose();
        repath = 0f;
        return true;
    }

    public string Report()
    {
        if (!OnMesh) return "<b>" + label + "</b>   off the mesh";
        return "<b>" + label + "</b>   <color=#ffd070>" + state + "</color>\n"
            + agent.path.corners.Length + " corners   " + Remaining().ToString("0.0") + " m to go   "
            + agent.velocity.magnitude.ToString("0.0") + " m/s";
    }

    float Remaining()
    {
        Vector3[] c = agent.path.corners;
        float d = 0f;
        for (int i = 1; i < c.Length; i++) d += (c[i] - c[i - 1]).magnitude;
        return d;
    }

    // With F3 on, the route is drawn in the game: a line through every corner and a ring at the goal.
    void LateUpdate()
    {
        bool draw = NavField.Show && OnMesh && agent.hasPath;
        if (draw && route == null) BuildRoute();
        if (route == null) return;
        route.gameObject.SetActive(draw);
        flag.gameObject.SetActive(draw);
        if (!draw) return;

        Vector3[] c = agent.path.corners;
        route.positionCount = c.Length;
        for (int i = 0; i < c.Length; i++) route.SetPosition(i, c[i] + Vector3.up * 0.14f);
        flag.position = agent.destination + Vector3.up * 0.12f;
    }

    void BuildRoute()
    {
        route = new GameObject(label + " route").AddComponent<LineRenderer>();
        route.sharedMaterial = NavField.Tint(new Color(1f, 0.82f, 0.2f, 1f));
        route.widthMultiplier = 0.16f;
        route.numCornerVertices = 4;
        route.numCapVertices = 4;
        route.useWorldSpace = true;
        route.alignment = LineAlignment.TransformZ;
        route.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        route.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var ring = new GameObject(label + " goal").AddComponent<LineRenderer>();
        flag = ring.transform;
        ring.sharedMaterial = NavField.Tint(new Color(1f, 0.3f, 0.2f, 1f));
        ring.widthMultiplier = 0.12f;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.alignment = LineAlignment.TransformZ;
        ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ring.positionCount = 24;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.45f);
        }
    }

    // The same route in the Scene view.
    void OnDrawGizmos()
    {
        if (agent == null || !OnMesh || !agent.hasPath) return;
        Vector3[] c = agent.path.corners;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < c.Length; i++) Gizmos.DrawLine(c[i - 1], c[i]);
        foreach (Vector3 p in c) Gizmos.DrawSphere(p, 0.12f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(agent.destination, agent.stoppingDistance);
    }
}
