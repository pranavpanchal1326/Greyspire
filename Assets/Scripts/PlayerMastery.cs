using UnityEngine;

public partial class PlayerController
{
    float masteryCounter;
    public float MasteryCounter => masteryCounter;
    float MasteryHeavyTime => PilotProgress.Has("mobility") || PilotProgress.Has("breaker") ? 1.2f : 1f;
    void MasteryEvade()
    {
        if (!PilotProgress.Has("mobility")) return;
        masteryCounter = 1.2f;
        Game.I.Toast("EVADE COUNTER: next light within 1.2s", UI.Blue);
    }
    float MasteryDamage(bool riposte, out bool counter)
    {
        counter = masteryCounter > 0f && PilotProgress.Has("mobility") && move == Move.None && attackT >= 0f;
        return riposte && PilotProgress.Has("duelist") ? 2.7f / 2.2f : counter ? 1.3f : 1f;
    }
}
