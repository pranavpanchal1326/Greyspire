using UnityEngine;

// Full-screen finishing pass: soft bloom on bright things (sparks, rings,
// highlights), shafts of light from the sun, plus a gentle colour grade.
[RequireComponent(typeof(Camera))]
public class PostFx : MonoBehaviour
{
    public float threshold = 0.88f;
    public float intensity = 0.7f;
    public float saturation = 1f;
    public float contrast = 1.07f;
    public float vignette = 0.5f;
    public Color tint = new Color(1f, 0.99f, 0.96f);

    // Filmic tone curve: bright colours roll off toward white instead of clipping into neon.
    public float filmic = 0.85f;
    public float exposure = 0.82f;

    // Moments, all 0..1 and eased back to 0 by the game: an ink frame on a heavy blow,
    // speed lines round the edge of the screen, and a split of the colour channels.
    public float impact, lines, aberration;

    // Ambient occlusion (0 = off) and film grain.
    public float occlusion = 0.9f;
    public float grain = 0.022f;

    // Light shafts: the direction the light comes from, and how strong the beams are (0 = off).
    public Vector3 sunDir = Vector3.up;
    public float rays;

    Material mat;
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Shader s = Resources.Load<Shader>("Post");
        if (s != null && s.isSupported) mat = GeneratedResources.Own(new Material(s), transform);
        else enabled = false;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        int w = Mathf.Max(1, src.width / 4), h = Mathf.Max(1, src.height / 4);
        RenderTexture a = RenderTexture.GetTemporary(w, h, 0, src.format);
        RenderTexture b = RenderTexture.GetTemporary(w, h, 0, src.format);
        RenderTexture shafts = null;

        mat.SetFloat("_Threshold", threshold);
        Graphics.Blit(src, a, mat, 0);
        for (int i = 1; i <= 3; i++)
        {
            mat.SetVector("_Dir", new Vector2(i, 0));
            Graphics.Blit(a, b, mat, 1);
            mat.SetVector("_Dir", new Vector2(0, i));
            Graphics.Blit(b, a, mat, 1);
        }

        // Beams only show while the camera is looking toward the light.
        Vector3 sun = cam.WorldToViewportPoint(cam.transform.position + sunDir * 500f);
        float facing = Mathf.Clamp01(Vector3.Dot(cam.transform.forward, sunDir) * 1.6f);
        float strength = sun.z > 0f ? rays * facing : 0f;
        if (strength > 0.01f)
        {
            shafts = RenderTexture.GetTemporary(w, h, 0, src.format);
            mat.SetVector("_SunPos", new Vector4(sun.x, sun.y, 0.035f, 0f));
            Graphics.Blit(a, b, mat, 3);
            mat.SetVector("_SunPos", new Vector4(sun.x, sun.y, 0.07f, 0f));
            Graphics.Blit(b, shafts, mat, 3);
            mat.SetTexture("_RayTex", shafts);
        }
        else mat.SetTexture("_RayTex", Texture2D.blackTexture);

        // Ambient occlusion at half size, softened.
        RenderTexture ao = null;
        bool shade = occlusion > 0.01f;
        cam.depthTextureMode = shade ? DepthTextureMode.Depth : DepthTextureMode.None;
        if (shade)
        {
            int aw = Mathf.Max(1, src.width / 2), ah = Mathf.Max(1, src.height / 2);
            ao = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.Default);
            RenderTexture tmp = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.Default);
            Matrix4x4 proj = cam.projectionMatrix;
            mat.SetVector("_AoParams", new Vector4(0.7f, occlusion, proj.m00, proj.m11));
            Graphics.Blit(src, ao, mat, 4);
            for (int i = 0; i < 2; i++)
            {
                mat.SetVector("_Dir", new Vector2(1, 0));
                Graphics.Blit(ao, tmp, mat, 1);
                mat.SetVector("_Dir", new Vector2(0, 1));
                Graphics.Blit(tmp, ao, mat, 1);
            }
            RenderTexture.ReleaseTemporary(tmp);
            mat.SetTexture("_AoTex", ao);
        }
        else mat.SetTexture("_AoTex", Texture2D.whiteTexture);
        mat.SetFloat("_Ao", shade ? 1f : 0f);
        mat.SetFloat("_Grain", grain);

        mat.SetTexture("_BloomTex", a);
        mat.SetFloat("_Rays", strength);
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_Saturation", saturation);
        mat.SetFloat("_Contrast", contrast);
        mat.SetFloat("_Vignette", vignette);
        mat.SetFloat("_Filmic", filmic);
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Impact", FeedbackPolicy.Impact(impact));
        mat.SetFloat("_Lines", FeedbackPolicy.Lines(lines));
        mat.SetFloat("_Aberr", FeedbackPolicy.Aberr(aberration));
        mat.SetFloat("_Aspect", (float)src.width / Mathf.Max(1, src.height));
        mat.SetColor("_Tint", tint);
        Graphics.Blit(src, dst, mat, 2);

        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
        if (shafts != null) RenderTexture.ReleaseTemporary(shafts);
        if (ao != null) RenderTexture.ReleaseTemporary(ao);
    }
}
