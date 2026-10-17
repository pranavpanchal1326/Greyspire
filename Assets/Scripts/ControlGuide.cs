public enum ControlAction { Move, Look, Jump, Attack, Dash, Block, Heavy, Throw, Burst, Wave, Interact, Lock, Cycle, Status, Map, Pause, Advance, Skip, Companion }

// The help, tutorial and contextual prompts share the same bindings.
public static class ControlGuide
{
    static readonly string[] Keyboard = { "WASD", "Mouse", "Space", "Left click / J", "Right click / Shift", "C / Ctrl", "R / middle click", "G", "Q", "F", "E", "T", "Z / V", "Tab / I", "M", "Esc / P", "Click / Space / Enter", "X", "H" };
    static readonly string[] Pad = { "Left stick", "Right stick", "A / South", "X / West", "B / East", "LT", "RT", "D-pad up", "RB", "LB", "Y / North", "Right stick click", "D-pad left / right", "D-pad down", "View / Select", "Menu / Start", "A / South", "Y / North", "Left stick click" };
    // Rebindable actions print the player's binding; the mouse buttons and Ctrl / I stay as fixed extras.
    public static string Key(ControlAction action)
    {
        if (!Bindings.Rebindable(action)) return (GameInput.UsingGamepad ? Pad : Keyboard)[(int)action];
        if (GameInput.UsingGamepad) return Bindings.PadLabel(action);
        string key = Bindings.KeyLabel(action);
        switch (action)
        {
            case ControlAction.Attack: return "Left click / " + key;
            case ControlAction.Dash: return "Right click / " + key;
            case ControlAction.Block: return key + " / Ctrl";
            case ControlAction.Heavy: return key + " / middle click";
            case ControlAction.Status: return key + " / I";
            default: return key;
        }
    }
    public static string ShortKey(ControlAction action)
    {
        if (!GameInput.UsingGamepad) return action == ControlAction.Dash ? "RMB" : Key(action);
        switch (action) { case ControlAction.Dash: case ControlAction.Throw: case ControlAction.Wave: case ControlAction.Burst: case ControlAction.Status: case ControlAction.Companion: return Bindings.PadShortLabel(action); default: return Key(action); }
    }
    public static string Dialogue => Key(ControlAction.Skip) + " skip all     " + Key(ControlAction.Advance) + " advance";
    public static string Navigation => GameInput.UsingGamepad ? "D-pad / stick: focus    A / South: choose    B / East: back" : "Arrows: focus    Enter: choose    Esc: back";
    public static string Tutorial(int step)
    {
        switch (step)
        {
            case 0: return Key(ControlAction.Move) + " move. " + Key(ControlAction.Look) + " look.";
            case 2: return Key(ControlAction.Attack) + " attack; hold to charge. Attack in air to plunge.";
            case 3: return Key(ControlAction.Dash) + (Save.SprintToggle ? " dash; it carries into a sprint. " : " dash; hold to sprint. ") + Key(ControlAction.Jump) + " twice: double jump.";
            case 4: return Key(ControlAction.Block) + (Save.GuardToggle ? " raises or lowers the guard" : " block") + "; time it for a parry. Attack while blocking to bash.";
            case 8: return "Settings > Controls rebinds any key or button. Settings > Access: hold or toggle guard and sprint, timing assist.";
            case 5: return Key(ControlAction.Lock) + " lock. " + Key(ControlAction.Cycle) + " switch target. " + Key(ControlAction.Throw) + " throw. " + Key(ControlAction.Heavy) + " heavy.";
            case 7: return Key(ControlAction.Status) + " Status: 3 points per level through 60. Free respec in town.";
            default: return null;
        }
    }
}
