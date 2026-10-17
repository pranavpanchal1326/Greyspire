using UnityEngine;

// Anything with health that can be hit: the player and every enemy.
public abstract class Actor : MonoBehaviour
{
    public float maxHp = 1f;
    public float hp = 1f;

    protected Vector3 knock;
    protected float flashStrength = 0.5f;    // how white a hit turns the model
    protected MaterialPropertyBlock block;
    Renderer[] rends;
    float flash;
    Vector3 previousContactPosition;
    bool hasContactPose;
    static readonly int FlashId = Shader.PropertyToID("_Flash");

    public bool Dead => hp <= 0f;
    int actionSequence;
    public string ActionId { get; private set; } = "";
    public void BeginAction(string action) { ActionId = PlaytestLog.Identity(this) + ":" + (++actionSequence) + ":" + action; PlaytestLog.Emit("action", source: this, note: action); }
    public virtual bool CombatEligible => !Dead && gameObject.activeInHierarchy;
    public Vector3 ContactMotion => hasContactPose ? transform.position - previousContactPosition : Vector3.zero;
    public void CaptureContactPose() { previousContactPosition = transform.position; hasContactPose = true; }
    protected void LateUpdate() => CaptureContactPose();

    protected void InitActor(float max)
    {
        maxHp = max;
        hp = max;
        rends = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        CaptureContactPose();
    }

    // Call after the visible model changes so hit flashes reach the new renderers.
    protected void RefreshRenderers()
    {
        rends = GetComponentsInChildren<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
    }

    public virtual bool TakeDamage(float amount, Vector3 from, float push)
        => ReceiveHit(new DamageRequest(amount, from, push)).Damaged;

    public virtual HitResult ReceiveHit(DamageRequest request)
    {
        var result = ApplyDamage(request);
        PlaytestLog.Contact(this, request, result);
        return result;
    }

    protected HitResult ApplyDamage(DamageRequest request)
    {
        if (!CombatEligible || !CombatContact.ValidDamage(request.amount)) return new HitResult(HitOutcome.Miss);
        float applied = Mathf.Min(hp, request.amount);
        hp = Mathf.Max(0f, hp - request.amount);
        bool lethal = hp <= 0f;
        SetFlash(flashStrength);
        Vector3 d = transform.position - request.from;
        d.y = 0f;
        knock = d.normalized * request.push;
        if (hp <= 0f) Die();
        HitOutcome result = Dead ? HitOutcome.Killed : !CombatEligible ? HitOutcome.Defeated : lethal ? HitOutcome.Revived : HitOutcome.Damaged;
        return new HitResult(result, applied);
    }

    // Pushes whatever is in the property block (outline colour, flash) to the renderers.
    protected void ApplyBlock()
    {
        if (rends == null) return;
        foreach (var r in rends)
            if (r != null) r.SetPropertyBlock(block);
    }

    protected void TickActor()
    {
        knock = Vector3.MoveTowards(knock, Vector3.zero, 35f * GameClocks.SimulationDelta);
        if (flash > 0f) SetFlash(Mathf.Max(0f, flash - GameClocks.SimulationDelta * 6f));
    }

    protected void SetFlash(float v)
    {
        flash = v;
        if (rends == null) return;
        block.SetFloat(FlashId, v);
        foreach (var r in rends)
            if (r != null) r.SetPropertyBlock(block);
    }

    protected abstract void Die();
}
