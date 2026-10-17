using UnityEngine;

// Someone who lives in Brushwick and keeps hours. While their shift runs they are
// at their post doing whatever they do there; when it ends they walk home and go
// indoors; when it comes round again they walk back out. The hours are read off
// the world's clock, so the town empties at night and fills again in the morning.
public class Resident : MonoBehaviour
{
    const float Pace = 1.45f;

    public CharAnim clips;
    public Transform model;         // the visible body: hidden while they are indoors
    public Vector3 post, home;      // where they stand on duty, and their front door
    public Quaternion facing;       // which way they face at the post
    public float from, to;          // on duty between these hours (a shift that ends before it starts runs past midnight)
    public Behaviour[] routines;    // what they do at their post (fidgeting, strolling): switched off while they walk
    public bool wanders;            // strollers have no fixed post: they pick up their round wherever they are
    public GameObject[] extras;     // things they carry that are not part of the body (a watchman's lantern)

    // Speech that hangs over their head for a moment when the hero walks by.
    [HideInInspector] public string bark;
    [HideInInspector] public float barkT, barkWait;

    enum Where { Post, Leaving, Home, Arriving }
    Where where;
    Collider body;
    NavFollower nav;

    public bool AtPost => where == Where.Post;
    public bool Indoors => where == Where.Home;

    void Awake()
    {
        body = GetComponent<Collider>();
    }

    // Puts them where the hour says they should be, at once: used when the town is built and after a rest.
    public void Snap()
    {
        if (body == null) body = GetComponent<Collider>();
        bool due = Game.I.OnDuty(from, to);
        if (due)
        {
            if (!wanders || where != Where.Post) transform.SetPositionAndRotation(post, facing);
            Show(true);
        }
        else
        {
            transform.position = home;
            Show(false);
        }
        Routines(due);
        where = due ? Where.Post : Where.Home;
    }

    void Update()
    {
        bool due = Game.I.OnDuty(from, to);
        switch (where)
        {
            case Where.Post:
                if (!due)
                {
                    Routines(false);
                    where = Where.Leaving;
                }
                break;
            case Where.Leaving:
                if (due) where = Where.Arriving;
                else if (Walk(home))
                {
                    Show(false);
                    where = Where.Home;
                }
                break;
            case Where.Home:
                if (due)
                {
                    transform.position = home;
                    Show(true);
                    where = Where.Arriving;
                }
                break;
            case Where.Arriving:
                if (!due) where = Where.Leaving;
                else if (wanders || Walk(post))
                {
                    if (!wanders) transform.rotation = facing;
                    Routines(true);
                    where = Where.Post;
                }
                break;
        }
    }

    // One step toward a place. True once they are there.
    bool Walk(Vector3 target)
    {
        Vector3 to = target - transform.position;
        float dist = to.magnitude;
        if (dist < 0.3f)
        {
            if (clips != null) clips.Move(0f);
            return true;
        }
        float dt = GameClocks.WorldDelta;
        to.y = 0f;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 6f * dt);
        if (nav == null)
        {
            nav = gameObject.GetComponent<NavFollower>();
            if (nav == null) nav = NavFollower.AddTo(gameObject);
            nav.label = name;
        }
        Vector3 before = transform.position;
        if (nav.Attach()) nav.MoveTo(target, 0.25f, Pace);
        else WorldSupport.TryMove(transform, to.normalized * Mathf.Min(dist, Pace * dt), 0.4f, 1.8f);
        if (clips != null) clips.Move(dt > 0f ? (transform.position - before).magnitude / dt : 0f);
        return false;
    }

    void Show(bool on)
    {
        if (model != null && model.gameObject.activeSelf != on) model.gameObject.SetActive(on);
        if (body != null) body.enabled = on;
        if (extras != null) foreach (GameObject e in extras) if (e != null) e.SetActive(on);
        if (!on) barkT = 0f;
    }

    void Routines(bool on)
    {
        if (routines == null) return;
        foreach (Behaviour b in routines) if (b != null) b.enabled = on;
    }
}
