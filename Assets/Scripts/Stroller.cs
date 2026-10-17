using UnityEngine;

// A villager with somewhere to be: walks a loop of waypoints at an easy pace,
// stops now and then to look about, and waits for the hero instead of walking
// through them.
public class Stroller : MonoBehaviour
{
    public CharAnim clips;
    public Vector3[] route;
    public float pace = 1.3f;       // metres per second: a stroll
    public string rest = "Idle";    // what they do when they stop

    int next;
    float pause, speed;

    void Update()
    {
        if (clips == null || route == null || route.Length == 0) return;
        float dt = GameClocks.WorldDelta;
        if (dt <= 0f) return;
        Vector3 to = route[next] - transform.position;
        to.y = 0f;
        Vector3 hero = Game.I.Player.transform.position - transform.position;
        hero.y = 0f;
        bool blocked = hero.magnitude < 1.6f && Vector3.Dot(hero.normalized, transform.forward) > 0.2f;

        if (pause > 0f) pause -= dt;
        bool walking = pause <= 0f && !blocked;
        speed = Mathf.MoveTowards(speed, walking ? pace : 0f, 2.5f * dt);

        // They walk the way they are facing and turn toward the next point, so corners come out as curves.
        if (walking && to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 4f * dt);
        WorldSupport.TryMove(transform, transform.forward * (speed * dt), 0.4f, 1.8f);

        if (to.magnitude < 0.7f)
        {
            next = (next + 1) % route.Length;
            if (ProductionRandom.Cosmetic.value < 0.4f) pause = ProductionRandom.Cosmetic.Range(2.5f, 6f);
        }

        if (speed < 0.05f && pause > 0f) clips.Loop(rest, 1f, 0.3f);
        else clips.Move(speed);
    }
}
