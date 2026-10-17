using UnityEngine;

// A Painter the Gloom has drained: every part of them washed to the grey of old
// stone. Thaw() lets the colour run back in.
public class Drained : MonoBehaviour
{
    static readonly Color Stone = new Color(0.52f, 0.53f, 0.6f);
    static readonly int WashId = Shader.PropertyToID("_Wash"), OutlineId = Shader.PropertyToID("_OutlineColor");

    Renderer[] rends;
    MaterialPropertyBlock block;
    float amount = 0.94f, target = 0.94f;

    void Start()
    {
        rends = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        Apply();
    }

    public void Thaw()
    {
        target = 0f;
    }

    void Update()
    {
        if (rends == null || Mathf.Approximately(amount, target)) return;
        amount = Mathf.MoveTowards(amount, target, GameClocks.PresentationDelta * 0.7f);
        if (amount > 0.001f)
        {
            Apply();
            return;
        }
        // Fully themselves again: hand the materials back untouched.
        foreach (Renderer r in rends) if (r != null) r.SetPropertyBlock(null);
        enabled = false;
    }

    void Apply()
    {
        block.SetColor(WashId, new Color(Stone.r, Stone.g, Stone.b, amount));
        block.SetColor(OutlineId, Color.Lerp(new Color(0.08f, 0.07f, 0.12f), new Color(0.3f, 0.3f, 0.36f), amount));
        foreach (Renderer r in rends) if (r != null) r.SetPropertyBlock(block);
    }
}
