using System.Collections.Generic;
using UnityEngine;

// Time of day. In town and in the Wilds the sun travels across the sky with the
// world's clock: sunrise at six, noon, a golden sunset at six, a moonlit night
// with stars. Tower floors keep their own fixed light so fights always read clearly.
public partial class Game
{
    static readonly Color NightTop = new Color(0.03f, 0.04f, 0.14f);
    static readonly Color NightBottom = new Color(0.1f, 0.13f, 0.32f);
    static readonly Color DuskTop = new Color(0.36f, 0.3f, 0.62f);
    static readonly Color DuskBottom = new Color(1f, 0.62f, 0.38f);
    static readonly Color DuskSun = new Color(1f, 0.66f, 0.4f);
    static readonly Color Moon = new Color(0.5f, 0.6f, 1f);
    static readonly Color NightAmbient = new Color(0.4f, 0.44f, 0.62f);

    // 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset.
    float dayT = 0.42f;

    // 0 in daylight, 1 in the dead of night. Lanterns and stars follow this.
    public float Night { get; private set; }

    // 1 while the sun sits on the horizon, morning or evening: the golden hours.
    public float Dusk { get; private set; }

    Material skyMat;
    LevelDef skyDef;
    Renderer starField;
    readonly List<Renderer> sunGlows = new List<Renderer>();
    Color skyBottomNow = new Color(0.72f, 0.9f, 1f);

    void TickSky(float udt)
    {
        Shader.SetGlobalVector("_HeroPos", Player.transform.position);

        bool outdoors = State == GameState.Hub || InWilds;
        if (!outdoors || skyMat == null || skyDef == null)
        {
            Night = 0f;
            Dusk = 0f;
            Village.Lamps(0f, clockHour);
            return;
        }

        // dayT follows the world's clock (GameClock.cs).
        float height = Mathf.Sin((dayT - 0.25f) * Mathf.PI * 2f);        // 1 at noon, -1 at midnight
        float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.22f, height));
        float high = Mathf.Clamp01(height * 2.5f);                       // 0 at the horizon, 1 well up
        Night = 1f - daylight;
        Dusk = daylight * (1f - high);

        // One light plays both parts: the sun by day, the moon by night.
        Quaternion sunRot = Quaternion.Euler(Mathf.Lerp(10f, 62f, Mathf.Clamp01(height)), Mathf.Lerp(-95f, 95f, Mathf.InverseLerp(0.25f, 0.75f, dayT)), 0f);
        Quaternion moonRot = Quaternion.Euler(48f, 150f, 0f);
        sun.transform.rotation = Quaternion.Slerp(moonRot, sunRot, daylight);
        sun.color = Color.Lerp(Moon * 0.68f, Color.Lerp(DuskSun, skyDef.sun, high), daylight);
        sun.shadowStrength = Mathf.Lerp(0.5f, 0.75f, daylight);

        Color top = Color.Lerp(NightTop, Color.Lerp(DuskTop, skyDef.skyTop, high), daylight);
        skyBottomNow = Color.Lerp(NightBottom, Color.Lerp(DuskBottom, skyDef.skyBottom, high), daylight);
        skyMat.SetColor("_Top", top);
        skyMat.SetColor("_Bottom", skyBottomNow);
        // Cloud takes the colour of the hour: white at noon, rose at dusk, slate under the moon.
        Color cloud = Color.Lerp(new Color(0.17f, 0.2f, 0.32f), Color.Lerp(new Color(1f, 0.7f, 0.52f), Color.Lerp(skyBottomNow, Color.white, 0.75f), high), daylight);
        skyMat.SetColor("_CloudLit", cloud);
        skyMat.SetColor("_CloudDark", Color.Lerp(cloud, top, 0.4f) * 0.8f);
        cam.backgroundColor = skyBottomNow;
        RenderSettings.ambientLight = Color.Lerp(NightAmbient, skyDef.ambient, daylight);
        if (!InWilds) RenderSettings.fogColor = skyBottomNow;

        // The glow in the sky sits wherever the light is coming from.
        Vector3 toLight = -sun.transform.forward;
        for (int i = 0; i < sunGlows.Count; i++)
        {
            if (sunGlows[i] == null) continue;
            Transform g = sunGlows[i].transform;
            g.localPosition = toLight * (0.42f - i * 0.01f);
            g.localRotation = Quaternion.LookRotation(toLight);
            Color c = Color.Lerp(new Color(0.85f, 0.9f, 1f), Color.Lerp(DuskSun, skyDef.sun, high), daylight);
            c.a = i == 0 ? 1f : Mathf.Lerp(0.16f, 0.28f + (1f - high) * 0.2f, daylight);
            Fx.Tint(sunGlows[i], c);
        }
        if (starField != null) Fx.Tint(starField, new Color(1f, 1f, 1f, Night));
        // Windows light as the day goes, and go dark house by house as people turn in.
        Village.Lamps(Night, clockHour);
    }

    // A few hundred stars on the inside of the sky dome, invisible until dusk.
    void BuildStars(Transform dome)
    {
        var rng = new System.Random(1701);
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int n = 0; n < 420; n++)
        {
            float az = (float)rng.NextDouble() * Mathf.PI * 2f;
            float el = Mathf.Asin(Mathf.Lerp(0.05f, 1f, (float)rng.NextDouble()));
            Vector3 dir = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(dir, right);
            float s = Mathf.Lerp(0.0016f, 0.0045f, (float)rng.NextDouble() * (float)rng.NextDouble());
            Vector3 c = dir * 0.44f;
            int i = verts.Count;
            verts.Add(c - right * s - up * s); verts.Add(c + right * s - up * s);
            verts.Add(c + right * s + up * s); verts.Add(c - right * s + up * s);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var mesh = new Mesh { name = "Stars" };
        GeneratedResources.Own(mesh, LevelRoot);
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        var go = new GameObject("Stars");
        go.transform.SetParent(dome, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        starField = go.AddComponent<MeshRenderer>();
        starField.sharedMaterial = Fx.Mote;
        starField.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Fx.Tint(starField, new Color(1f, 1f, 1f, 0f));
    }

    // A warm halo for lanterns and torches. Faint by day, strong at night.
    Glow AddGlow(Transform parent, float height, Color colour, float size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(go.GetComponent<Collider>());
        go.name = "Glow";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * height;
        go.transform.localScale = Vector3.one * size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Fx.Mote;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var glow = go.AddComponent<Glow>();
        glow.colour = colour;
        return glow;
    }
}
