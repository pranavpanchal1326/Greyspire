using UnityEngine;

// One piece of a dash afterimage: fades out, then cleans up after itself.
public class GhostFade : MonoBehaviour
{
    public Color colour = new Color(0.5f, 0.8f, 1f, 0.45f);
    public bool ownsMesh;       // baked meshes are ours to destroy; shared ones are not
    public bool pooled;
    public Mesh baked;
    MaterialPropertyBlock tint;
    public void ResetPose(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color c)
    {
        age = 0f; colour = c;
        transform.SetPositionAndRotation(position, rotation); transform.localScale = scale;
        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (rend == null) rend = GetComponent<Renderer>();
        gameObject.SetActive(true); Paint(c);
    }
    void Paint(Color c)
    {
        if (tint == null) tint = new MaterialPropertyBlock();
        tint.SetColor("_Color", c); rend.SetPropertyBlock(tint);
    }
    void OnDestroy() { if (ownsMesh) Destroy(GetComponent<MeshFilter>().sharedMesh); }

    const float Life = 0.28f;

    Renderer rend;
    float age;

    void Start()
    {
        rend = GetComponent<Renderer>();
        Paint(colour);
    }

    void Update()
    {
        age += GameClocks.EffectsDelta;
        if (age >= Life)
        {
            if (pooled) gameObject.SetActive(false); else Destroy(gameObject);
            return;
        }
        Color c = colour;
        c.a *= 1f - age / Life;
        Paint(c);
    }
}
