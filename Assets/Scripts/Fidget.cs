using UnityEngine;

// Gives a standing character some life: every so often they gesture, then
// settle back into their idle.
public class Fidget : MonoBehaviour
{
    static readonly string[] Moves = { "Interact", "Cheer", "Use_Item" };

    public CharAnim clips;
    public string idle = "Idle";    // what they do between gestures (standing, talking, working)
    public bool gestures = true;    // people busy with a job just keep at it

    float wait;

    void Start()
    {
        wait = ProductionRandom.Cosmetic.Range(3f, 9f);
    }

    void Update()
    {
        if (clips == null) return;
        wait -= GameClocks.WorldDelta;
        if (wait <= 0f && gestures)
        {
            wait = ProductionRandom.Cosmetic.Range(7f, 14f);
            clips.Once(Moves[ProductionRandom.Cosmetic.Range(0, Moves.Length)]);
        }
        else if (!clips.Busy) clips.Loop(idle);
    }
}
