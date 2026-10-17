using System.Collections.Generic;
using UnityEngine;

// Registers the real IMGUI controls. Input is sampled once per frame and a
// confirmation can activate only one control, even across multiple GUI events.
public static class UiNavigation
{
    public struct Control { public int id; public Rect rect; public string label; public bool enabled, slider; }
    static readonly List<Control> previous = new List<Control>(), building = new List<Control>();
    static string context = "";
    static int sequence, focused = -1, sampled = -1, activated = -1, adjusted = -1;
    static bool confirm, changed;
    static Vector2 direction;
    public static string FocusedLabel { get; private set; } = "";
    public static IReadOnlyList<Control> Controls => previous;
    public static void Reset() { context = ""; previous.Clear(); focused = sampled = activated = -1; FocusedLabel = ""; }
    public static void Begin(string next)
    {
        sequence = 0; building.Clear();
        changed = context != next;
        if (changed) { context = next; previous.Clear(); focused = -1; FocusedLabel = ""; confirm = false; direction = Vector2.zero; }
        if (sampled == Time.frameCount) return;
        sampled = Time.frameCount; GameInput.PollDevice();
        direction = GameInput.MenuDirection;
        confirm = !changed && previous.Count > 0 && GameInput.MenuConfirmPressed;
        if (direction != Vector2.zero) Move(direction);
    }
    static void Move(Vector2 step)
    {
        int current = previous.FindIndex(c => c.id == focused && c.enabled);
        if (current < 0) { var first = previous.Find(c => c.enabled); focused = first.id; return; }
        var origin = previous[current];
        if (origin.slider && Mathf.Abs(step.x) > 0) return;
        Vector2 axis = new Vector2(step.x, -step.y);
        float best = float.PositiveInfinity; int next = focused;
        bool horizontal = Mathf.Abs(step.x) > 0f;
        bool alignedRow = horizontal && previous.Exists(c => c.enabled && c.id != focused &&
            Vector2.Dot(c.rect.center - origin.rect.center, axis) > 1f &&
            Mathf.Abs(c.rect.center.y - origin.rect.center.y) <= Mathf.Max(c.rect.height, origin.rect.height) * 0.5f);
        foreach (var c in previous)
        {
            if (!c.enabled || c.id == focused) continue;
            Vector2 delta = c.rect.center - origin.rect.center;
            float along = Vector2.Dot(delta, axis), cross = Mathf.Abs(delta.x * axis.y - delta.y * axis.x);
            if (along <= 1f) continue;
            if (alignedRow && cross > Mathf.Max(c.rect.height, origin.rect.height) * 0.5f) continue;
            float score = horizontal ? along + cross * 0.3f : along * 1000f + cross;
            if (score < best) { best = score; next = c.id; }
        }
        if (next == focused)
        {
            // At an edge, wrap within the nearest row/column.
            best = float.PositiveInfinity;
            foreach (var c in previous)
            {
                if (!c.enabled || c.id == focused) continue;
                Vector2 delta = c.rect.center - origin.rect.center;
                float along = Vector2.Dot(delta, axis), cross = Mathf.Abs(delta.x * axis.y - delta.y * axis.x);
                if (along >= -1f) continue;
                float score = along + cross * 3f;
                if (score < best) { best = score; next = c.id; }
            }
        }
        if (next != focused) { focused = next; ToonKit.Sfx("click", 0.2f); }
    }
    public static int Register(Rect rect, string label, bool enabled, bool slider = false)
    {
        int id = sequence++;
        building.Add(new Control { id = id, rect = rect, label = label, enabled = enabled, slider = slider });
        if (focused < 0 && enabled) focused = id;
        if (focused == id && !enabled)
        {
            focused = -1;
            foreach (var c in previous) if (c.enabled && c.id != id) { focused = c.id; break; }
        }
        if (focused == id) FocusedLabel = label;
        return id;
    }
    public static void Prefer(string label)
    {
        if (!changed && previous.Count > 0) return;
        int id = building.FindIndex(c => c.label == label && c.enabled);
        if (id >= 0) { focused = building[id].id; FocusedLabel = label; }
    }
    public static bool Focused(int id) => focused == id;
    public static bool Activate(int id)
    {
        if (!confirm || focused != id || activated == Time.frameCount) return false;
        activated = Time.frameCount; confirm = false; return true;
    }
    public static float Adjust(int id)
    {
        if (focused != id || activated == Time.frameCount || adjusted == Time.frameCount || Event.current.type != EventType.Repaint) return 0f;
        adjusted = Time.frameCount; return direction.x * 0.05f;
    }
    public static void End()
    {
        if (Event.current.type != EventType.Repaint) return;
        previous.Clear(); previous.AddRange(building);
        if (!previous.Exists(c => c.enabled && c.id == focused)) focused = -1;
    }
}
