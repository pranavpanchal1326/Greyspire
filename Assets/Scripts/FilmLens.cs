using UnityEngine;

// A camera lens for filmed shots: depth of field. Only the film rig (Assets/Editor/CineFilm.cs)
// adds one; the game itself never does, so play is unaffected.
[RequireComponent(typeof(Camera))]
public class FilmLens : MonoBehaviour
{
    public float focus = 6f;        // metres from the camera that are sharp
    public float range = 6f;        // how gently sharpness falls away either side of it
    public float blur;              // strongest blur, in pixels at 1080p; 0 switches the lens off

    Material mat;

    void Awake()
    {
        Shader s = Resources.Load<Shader>("FilmLens");
        if (s != null && s.isSupported) mat = GeneratedResources.Own(new Material(s), transform);
        GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null || blur <= 0.01f)
        {
            Graphics.Blit(src, dst);
            return;
        }
        mat.SetFloat("_Focus", focus);
        mat.SetFloat("_Range", Mathf.Max(0.05f, range));
        mat.SetFloat("_MaxBlur", blur * src.height / 1080f);
        Graphics.Blit(src, dst, mat);
    }
}
