using System;
using System.Collections.Generic;
using UnityEngine;

// Small immediate-mode UI kit drawn on a virtual 720-high canvas: dark glass
// panels with a hairline edge, quiet buttons that light when the pointer is on
// them, bars and sliders. Three typefaces: an inscriptional capital face for
// titles, a book serif for headings, names and anything spoken, and a plain
// humanist face for everything that is read at a glance. All textures are
// generated, so the UI needs no image assets.
public static class UI
{
    public const float H = 720f;
    public static float W { get; private set; }
    public static float Scale { get; private set; }

    public static Texture2D Round, RoundSmall, Circle, Heart, Vignette, Glow;

    // A restrained palette: old gold, oxblood, steel, sage, heather. Ink is the dark of every panel.
    public static readonly Color Ink = new Color(0.06f, 0.065f, 0.08f);
    public static readonly Color Gold = new Color(0.88f, 0.73f, 0.42f);
    public static readonly Color Red = new Color(0.76f, 0.24f, 0.24f);
    public static readonly Color Blue = new Color(0.46f, 0.64f, 0.8f);
    public static readonly Color Green = new Color(0.5f, 0.72f, 0.47f);
    public static readonly Color Purple = new Color(0.6f, 0.49f, 0.76f);
    public static readonly Color Paper = new Color(0.93f, 0.91f, 0.86f);

    const int HeadingSize = 26;     // text this big or bigger is set in the serif face
    const int DisplaySize = 40;     // and this big or bigger in the titling capitals

    static GUIStyle body, heading, display, panel, panelSmall;
    static readonly Dictionary<int, float> hover = new Dictionary<int, float>();

    public static bool Repaint => Event.current.type == EventType.Repaint;
    public static Vector2 Mouse => Event.current.mousePosition;

    // Multiplies the opacity of everything drawn while it is below one (the fight-focus HUD fade).
    public static float Fade = 1f;

    public static void Begin(string context = "default")
    {
        if (body == null) Init();
        Fade = 1f;
        Scale = Screen.height / H;
        W = Screen.width / Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        GUI.color = Color.white;
        UiNavigation.Begin(context);
    }

    static void Init()
    {
        Round = Make(64, (x, y) => RoundedBox(x, y, 0.5f, 0.12f));
        RoundSmall = Make(32, (x, y) => RoundedBox(x, y, 0.5f, 0.3f));
        Circle = Make(64, (x, y) => Mathf.Clamp01((0.48f - Mathf.Sqrt(x * x + y * y)) * 64f));
        Heart = Make(64, HeartShape);
        Vignette = Make(128, (x, y) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, Mathf.Sqrt(x * x + y * y))));
        Glow = Make(64, (x, y) =>
        {
            float d = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2f);
            return d * d;
        });

        // Each face is the first of its list that this machine has. Family names only: a weight
        // in the name ("Segoe UI Semibold") is not a family, and asking for one fails on every draw.
        var have = new HashSet<string>(Font.GetOSInstalledFontNames());
        string Pick(params string[] families)
        {
            foreach (string f in families) if (have.Contains(f)) return f;
            return "Arial";
        }
        Font titling = Font.CreateDynamicFontFromOSFont(Pick("Felix Titling", "Palatino Linotype", "Georgia", "Times New Roman"), 64);
        Font serif = Font.CreateDynamicFontFromOSFont(Pick("Palatino Linotype", "Constantia", "Georgia", "Times New Roman"), 48);
        Font plain = Font.CreateDynamicFontFromOSFont(Pick("Segoe UI", "Verdana", "Tahoma"), 32);
        body = new GUIStyle { font = plain, wordWrap = true };
        heading = new GUIStyle { font = serif, wordWrap = true };
        display = new GUIStyle { font = titling, wordWrap = true };
        panel = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
        panel.normal.background = Round;
        panelSmall = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
        panelSmall.normal.background = RoundSmall;
    }

    // ------------------------------------------------------------ primitives

    // outline: a soft dark shadow under the text so it reads over the world. Without it the text is drawn bare.
    public static void Text(Rect r, string text, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter, bool outline = true)
    {
        if (!Repaint) return;
        c.a *= Fade;
        GUIStyle style = size >= DisplaySize ? display : size >= HeadingSize ? heading : body;
        // Small print inside a panel is set light; labels, and anything laid over the world, bold.
        if (style == body) style.fontStyle = outline || size >= 18 ? FontStyle.Bold : FontStyle.Normal;
        heading.fontStyle = FontStyle.Normal;
        Draw(style, r, text, size, c, anchor, outline);
    }

    // Running text in the book face at any size: what people say, and what the story tells. Narration is italic.
    public static void Prose(Rect r, string text, int size, Color c, TextAnchor anchor = TextAnchor.UpperLeft, bool italic = false)
    {
        if (!Repaint) return;
        heading.fontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
        Draw(heading, r, text, size, c, anchor, false);
        heading.fontStyle = FontStyle.Normal;
    }

    static void Draw(GUIStyle style, Rect r, string text, int size, Color c, TextAnchor anchor, bool outline)
    {
        style.fontSize = size;
        style.alignment = anchor;
        if (outline)
        {
            float o = Mathf.Max(1f, size * 0.035f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.5f * c.a);
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, style);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * c.a);
            GUI.Label(new Rect(r.x, r.y + o * 1.5f, r.width, r.height), text, style);
        }
        style.normal.textColor = c;
        GUI.Label(r, text, style);
    }

    public static void Panel(Rect r, Color c, bool small = false)
    {
        if (!Repaint) return;
        c.a *= Fade;
        GUI.color = c;
        GUI.Box(r, GUIContent.none, small ? panelSmall : panel);
        GUI.color = Color.white;
    }

    // A panel with a hairline edge in the given colour.
    public static void Framed(Rect r, Color fill, Color border, float thickness = 4f)
    {
        float edge = Mathf.Min(thickness, 1.5f);
        Panel(new Rect(r.x, r.y + 5f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f * fill.a));
        Panel(r, new Color(border.r, border.g, border.b, Mathf.Min(1f, border.a) * 0.85f));
        Panel(Grow(r, -edge), fill);
    }

    public static void Tex(Rect r, Texture2D tex, Color c)
    {
        if (!Repaint) return;
        c.a *= Fade;
        GUI.color = c;
        GUI.DrawTexture(r, tex);
        GUI.color = Color.white;
    }

    public static void Fill(Rect r, Color c)
    {
        Tex(r, Texture2D.whiteTexture, c);
    }

    public static void Bar(Rect r, float frac, Color fill, Color back)
    {
        Fill(r, new Color(back.r, back.g, back.b, Mathf.Max(back.a, 0f) * 0.85f));
        frac = Mathf.Clamp01(frac);
        if (frac <= 0.001f) return;
        Rect inner = Grow(r, -2f);
        inner.width *= frac;
        Fill(inner, fill);
    }

    // A quiet dark button with an edge in its colour; the colour floods in under the pointer.
    public static bool Button(Rect r, string text, Color color, int size = 24, bool enabled = true)
    {
        int focusId = UiNavigation.Register(r, text, enabled);
        int id = text.GetHashCode() ^ ((int)r.y * 397) ^ (int)r.x;
        bool selected = UiNavigation.Focused(focusId);
        bool over = enabled && (r.Contains(Mouse) || selected);
        hover.TryGetValue(id, out float h);
        if (Repaint)
        {
            if (over && h <= 0f) ToonKit.Sfx("hover");
            h = Mathf.MoveTowards(h, over ? 1f : 0f, Time.unscaledDeltaTime * 9f);
            hover[id] = h;
        }

        Color edge = enabled ? color : new Color(0.36f, 0.37f, 0.4f);
        Color fill = Color.Lerp(new Color(Ink.r, Ink.g, Ink.b, 0.9f), new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.96f), enabled ? 0.22f + h * 0.6f : 0f);
        Panel(new Rect(r.x, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f));
        Panel(r, new Color(edge.r, edge.g, edge.b, enabled ? 0.75f + h * 0.25f : 0.5f));
        Panel(Grow(r, -1.5f), fill);
        Fill(new Rect(r.x + 1.5f, r.y + 6f, 3f, r.height - 12f), new Color(edge.r, edge.g, edge.b, enabled ? 1f : 0.4f));
        // Buttons are labels, not headings: keep them in the plain face.
        Text(r, text, Mathf.Min(size, HeadingSize - 1), enabled ? Color.Lerp(Paper, Color.white, h) : new Color(0.6f, 0.6f, 0.63f), TextAnchor.MiddleCenter, false);
        if (selected) FocusRing(r);

        if ((enabled && UiNavigation.Activate(focusId)) || GUI.Button(r, GUIContent.none, GUIStyle.none))
        {
            ToonKit.Sfx(enabled ? "click" : "deny");
            return enabled;
        }
        return false;
    }

    public static float Slider(Rect r, float value, Color color, string label = "slider")
    {
        int focusId = UiNavigation.Register(r, label, true, true);
        value = Mathf.Clamp01(value + UiNavigation.Adjust(focusId));
        Bar(r, value, color, Ink);
        if (GameInput.UsingGamepad && UiNavigation.Focused(focusId)) FocusRing(Grow(r, 4f));
        Event e = Event.current;
        Rect grab = Grow(r, 8f);
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && grab.Contains(e.mousePosition))
        {
            value = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
            e.Use();
        }
        return value;
    }

    public static void FocusRing(Rect r)
    {
        Fill(new Rect(r.x - 5, r.y - 5, r.width + 10, 3), Paper);
        Fill(new Rect(r.x - 5, r.yMax + 2, r.width + 10, 3), Paper);
        Fill(new Rect(r.x - 5, r.y - 5, 3, r.height + 10), Paper);
        Fill(new Rect(r.xMax + 2, r.y - 5, 3, r.height + 10), Paper);
    }
    public static bool SelectArea(Rect r, string label, bool enabled)
    {
        int id = UiNavigation.Register(r, label, enabled);
        if (GameInput.UsingGamepad && UiNavigation.Focused(id)) FocusRing(r);
        return enabled && (UiNavigation.Activate(id) || GUI.Button(r, GUIContent.none, GUIStyle.none));
    }
    public static void End() => UiNavigation.End();

    public static Rect Grow(Rect r, float by)
    {
        return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
    }

    public static Rect Centered(float cx, float cy, float w, float h)
    {
        return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
    }

    // ------------------------------------------------------------ texture generation

    // shape(x, y) returns coverage 0..1 for a point in [-0.5, 0.5] with +y up.
    static Texture2D Make(int size, Func<float, float, float> shape)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                        a += Mathf.Clamp01(shape((x + (sx + 0.5f) / 3f) / size - 0.5f, (y + (sy + 0.5f) / 3f) / size - 0.5f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a / 9f * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static float RoundedBox(float x, float y, float half, float radius)
    {
        radius *= half;
        float dx = Mathf.Max(Mathf.Abs(x) - (half - radius), 0f);
        float dy = Mathf.Max(Mathf.Abs(y) - (half - radius), 0f);
        return Mathf.Sqrt(dx * dx + dy * dy) <= radius ? 1f : 0f;
    }

    static float HeartShape(float x, float y)
    {
        x *= 2.5f;
        y = y * 2.5f + 0.2f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f ? 1f : 0f;
    }
}
