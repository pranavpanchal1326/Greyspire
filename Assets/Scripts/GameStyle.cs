using System.Collections.Generic;
using UnityEngine;

// How a fight feels. A style rank that climbs from D to SSS while the hero fights
// well (varied moves, crits, parries, kills) and falls when he is hit or stands
// about; the higher it is, the more carried Sparks every kill is worth. And the things
// that sell a blow: a word in the air for a parry or a riposte, an ink-and-paper
// impact frame, speed lines, and a cut-in band for the big moments.
public partial class Game
{
    static readonly string[] StyleLetters = { "D", "C", "B", "A", "S", "SS", "SSS" };
    static readonly string[] StyleWords = { "Sketchy", "Clean", "Bold", "Vivid", "Brilliant", "Dazzling", "MASTERPIECE" };
    static readonly float[] StyleAt = { 0f, 60f, 140f, 240f, 360f, 500f, 660f };
    static readonly float[] StyleBonus = { 1f, 1.1f, 1.2f, 1.35f, 1.5f, 1.75f, 2f };
    static readonly Color[] StyleColors =
    {
        new Color(0.75f, 0.78f, 0.86f), new Color(0.55f, 0.85f, 1f), UI.Green, UI.Gold, new Color(1f, 0.6f, 0.2f), UI.Red, UI.Purple,
    };
    const float StyleMax = 800f;

    float style, styleIdle, stylePunch;
    int styleRank, lastStyleTag = -1, sameTag;

    // Up to this fraction of the enemy's base coins is awarded as a style bonus.
    public float StyleMult => StyleBonus[styleRank];

    class WordFx
    {
        public Vector3 pos;
        public string text;
        public Color color;
        public float age, tilt;
        public int size;
    }

    readonly List<WordFx> words = new List<WordFx>();
    float cutT;
    string cutText = "";
    Color cutColor = Color.white;

    void ResetStyle()
    {
        style = 0f;
        styleRank = 0;
        lastStyleTag = -1;
        sameTag = 0;
    }

    // A blow landed. tag says which move it came from: mixing moves up is what earns style.
    public void StyleHit(int tag, bool crit, bool air, bool riposte)
    {
        sameTag = tag == lastStyleTag ? sameTag + 1 : 0;
        lastStyleTag = tag;
        // The same move over and over is worth less each time.
        float gain = Mathf.Max(3f, 14f - sameTag * 3f);
        if (crit) gain += 6f;
        if (air) gain += 8f;
        if (riposte) gain += 30f;
        AddStyle(gain);
    }

    float StyleAfterGain(float amount)
    {
        if (State != GameState.Playing || awakening) return StyleMult;
        float next = Mathf.Min(StyleMax, style + amount); int rank = 0;
        for (int i = 0; i < StyleAt.Length; i++) if (next >= StyleAt[i]) rank = i;
        return StyleBonus[rank];
    }

    public void AddStyle(float amount)
    {
        if (State != GameState.Playing || awakening) return;
        style = Mathf.Min(StyleMax, style + amount);
        styleIdle = 0f;
        int rank = 0;
        for (int i = 0; i < StyleAt.Length; i++) if (style >= StyleAt[i]) rank = i;
        if (rank > styleRank)
        {
            stylePunch = 1f;
            ToonKit.Sfx("combo", 0.8f, 0.9f + rank * 0.08f);
            if (rank >= 4) Lines(0.6f);
        }
        styleRank = rank;
    }

    // Getting hit costs a whole rank.
    void StyleHurt()
    {
        styleRank = Mathf.Max(0, styleRank - 1);
        style = StyleAt[styleRank];
        lastStyleTag = -1;
    }

    void TickStyle(float udt)
    {
        // The screen moments ease back out on real time, so they still play through hit-stop.
        post.impact = Mathf.MoveTowards(post.impact, 0f, udt * 9f);
        post.lines = Mathf.MoveTowards(post.lines, 0f, udt * 2.6f);
        post.aberration = Mathf.MoveTowards(post.aberration, 0f, udt * 5f);
        stylePunch = Mathf.MoveTowards(stylePunch, 0f, udt * 4f);
        cutT -= udt;
        for (int i = words.Count - 1; i >= 0; i--)
        {
            words[i].age += udt;
            if (words[i].age > 0.9f) words.RemoveAt(i);
        }

        if (State != GameState.Playing || style <= 0f) return;
        styleIdle += GameClocks.SimulationDelta;
        if (styleIdle < 2.5f) return;
        style = Mathf.Max(0f, style - (20f + styleRank * 8f) * GameClocks.SimulationDelta);
        while (styleRank > 0 && style < StyleAt[styleRank]) styleRank--;
    }

    // ------------------------------------------------------------ screen moments

    // An ink-and-paper frame, with the colours pulled apart round it.
    // Kept rare: a frame like this every other hit would stop meaning anything, so lesser blows
    // inside the same half second only pull the colours apart.
    public void Impact(float strength)
    {
        post.aberration = Mathf.Max(post.aberration, strength);
        if (strength < 0.95f && Time.unscaledTime - lastImpact < 0.5f) return;
        lastImpact = Time.unscaledTime;
        post.impact = Mathf.Max(post.impact, Mathf.Clamp01(strength));
    }

    float lastImpact = -9f;

    public void Lines(float strength)
    {
        post.lines = Mathf.Max(post.lines, Mathf.Clamp01(strength));
    }

    // A band across the screen with a name on it: a skill unleashed, a rank earned.
    public void CutIn(string text, Color color)
    {
        cutText = text;
        cutColor = color;
        cutT = 1.1f;
    }

    // A lettered sound effect hanging in the air where something happened.
    public void Word(Vector3 pos, string text, Color color, int size = 40)
    {
        if (words.Count > 10) words.RemoveAt(0);
        words.Add(new WordFx { pos = pos, text = text, color = color, size = size, tilt = ProductionRandom.Cosmetic.Range(-14f, 14f) });
    }

    // ------------------------------------------------------------ drawing

    void DrawWords()
    {
        if (!UI.Repaint) return;
        foreach (WordFx f in words)
        {
            if (!ToGui(f.pos, out Vector2 p)) continue;
            float k = f.age / 0.9f;
            float pop = 1f + Mathf.Max(0f, 0.12f - f.age) * 6f;
            Color c = f.color;
            c.a = Mathf.Clamp01((1f - k) * 3.5f);
            Matrix4x4 keep = GUI.matrix;
            // The pivot is in screen pixels; the rest of the UI is drawn on the scaled canvas.
            GUIUtility.RotateAroundPivot(f.tilt * 0.3f, new Vector2(p.x, p.y) * UI.Scale);
            UI.Text(UI.Centered(p.x, p.y - k * 34f, 520, 90), f.text, Mathf.RoundToInt(f.size * pop), c);
            GUI.matrix = keep;
        }
    }

    // The style rank: a big letter, its name, and how close the next one is. Top right, under the clock.
    void DrawStyle(float w, float y)
    {
        if (style <= 0f && stylePunch <= 0f) return;
        Color c = StyleColors[styleRank];
        float next = styleRank + 1 < StyleAt.Length ? StyleAt[styleRank + 1] : StyleMax;
        float frac = Mathf.InverseLerp(StyleAt[styleRank], next, style);
        float a = Mathf.Clamp01(style / 20f + stylePunch);
        c.a = a;

        var r = new Rect(w - 236, y, 210, 74);
        UI.Panel(r, new Color(0.045f, 0.047f, 0.058f, 0.55f * a), true);
        int size = Mathf.RoundToInt((styleRank >= 5 ? 44 : 54) * (1f + stylePunch * 0.45f));
        UI.Text(new Rect(r.x + 2, r.y - 4, 96, 72), StyleLetters[styleRank], size, c);
        UI.Text(new Rect(r.x + 98, r.y + 8, 108, 22), StyleWords[styleRank], 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleLeft);
        UI.Text(new Rect(r.x + 98, r.y + 28, 124, 20), "Sparks +" + Mathf.RoundToInt((StyleMult - 1f) * 100f) + "%", 12, c, TextAnchor.MiddleLeft, false);
        UI.Bar(new Rect(r.x + 98, r.y + 52, 100, 9), frac, c, new Color(UI.Ink.r, UI.Ink.g, UI.Ink.b, a));
        if (combo > 1) UI.Text(new Rect(r.x, r.yMax + 2, r.width, 22), combo + " hits", 15, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleRight);
    }

    // The cut-in: a slanted black band sweeps across the screen carrying a name.
    void DrawCutIn(float w, float h)
    {
        if (cutT <= 0f || !UI.Repaint) return;
        float age = 1.1f - cutT;
        float inK = Mathf.Clamp01(age / 0.12f), outK = Mathf.Clamp01(cutT / 0.25f);
        float a = Mathf.Min(inK, outK);
        float y = h * 0.42f, bandH = 96f * inK;

        Matrix4x4 keep = GUI.matrix;
        GUIUtility.RotateAroundPivot(-4f, new Vector2(w * 0.5f, y) * UI.Scale);
        UI.Fill(new Rect(-80f, y - bandH * 0.5f, w + 160f, bandH), new Color(0.021f, 0.022f, 0.026f, 0.9f * a));
        UI.Fill(new Rect(-80f, y - bandH * 0.5f - 5f, w + 160f, 5f), new Color(cutColor.r, cutColor.g, cutColor.b, a));
        UI.Fill(new Rect(-80f, y + bandH * 0.5f, w + 160f, 5f), new Color(cutColor.r, cutColor.g, cutColor.b, a));
        // The name slides in fast, then drifts.
        float slide = (1f - Mathf.Pow(inK, 0.5f)) * -260f + age * 46f;
        UI.Text(new Rect(slide, y - 46f, w, 92f), cutText, 70, new Color(cutColor.r, cutColor.g, cutColor.b, a));
        GUI.matrix = keep;
    }
}
