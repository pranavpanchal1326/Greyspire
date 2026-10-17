using UnityEngine;

// A local practice actor: no kills, drops, XP, floor progress or saved rewards.
public sealed class PilotTraining : MonoBehaviour
{
    public Enemy target;
    public int AcceptedStrikes { get; private set; }
    float clock;
    bool warned;
    void Update()
    {
        var g = Game.I;
        if (g == null || !g.TrainingActive || target == null || !g.Roaming) return;
        clock += GameClocks.SimulationDelta;
        if (!warned && clock >= 2.2f)
        {
            warned = true; target.CloseOpening();
            g.Toast("PRACTICE STRIKE: guard late or evade", UI.Gold);
            Fx.Ring(target.transform.position, UI.Gold, 0.8f, 4f, 0.2f);
        }
        if (clock < 3f) return;
        clock = 0f; warned = false;
        if (Vector3.Distance(g.Player.transform.position, target.transform.position) > 4f
            || !CombatContact.CanContact(target.transform.position + Vector3.up * 1.1f, g.Player)) return;
        // Rest before each single practice blow; this fixture cannot defeat the hero.
        g.Player.Heal(g.Player.stats.maxHearts);
        var result = g.Player.ReceiveHit(new DamageRequest(1f, target.transform.position, 0f, source: target));
        if(result.outcome!=HitOutcome.Miss)AcceptedStrikes++;
        g.Toast(result.outcome == HitOutcome.Parried ? "PARRY: follow with a riposte" : result.outcome == HitOutcome.Immune ? "EVADED: try a light counter" : "Try the guard or evade timing again", UI.Blue);
    }
}
