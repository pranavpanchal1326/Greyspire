using UnityEngine;

// Simulation respects hit-stop/slow motion. Presentation and diagnostics use
// real time; hard pause suspends story/ambient presentation but not menus/music.
public static class GameClocks
{
    public static bool HardPause => Juice.Paused || (Game.I != null && Game.I.StorageBlocked);
    public static float SimulationDelta => HardPause || (Game.I != null && Game.I.State != Game.GameState.Playing) ? 0 : Time.deltaTime;
    public static float WorldDelta => HardPause || (Game.I != null && Game.I.State != Game.GameState.Playing && Game.I.State != Game.GameState.Hub) ? 0 : Time.deltaTime;
    public static float PresentationDelta => HardPause ? 0 : Time.unscaledDeltaTime;
    public static float EffectsDelta => Game.I != null && Game.I.State == Game.GameState.Story ? PresentationDelta : WorldDelta;
    public static void Tick() { _ = SimulationTime; _ = PresentationTime; }
    static float presentation;
    static float simulation;
    static int simulationFrame = -1;
    public static float SimulationTime { get { if (simulationFrame != Time.frameCount) { simulationFrame = Time.frameCount; simulation += SimulationDelta; } return simulation; } }
    static int frame = -1;
    public static float PresentationTime { get { if (frame != Time.frameCount) { frame = Time.frameCount; presentation += PresentationDelta; } return presentation; } }
}
