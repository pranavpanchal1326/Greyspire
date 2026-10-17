# Greyspire — terrain and world-coordinate safety, phase three

Date: 13 October 2026. Scope: G2 / DA13–18 in the upgrade masterplan.

G2 began only after the phase-two closure audit repaired frame-boundary contact gaps and passed 63 contact, 103 save, and 28 movement checks plus a zero-error Windows build. That G1 baseline is retained in `Backups/phase-two-verified-20261013`.

## Implementation contract

`WorldSupport.cs` defines world-space feet, bounded suppor t queries with collider identity and normal, body clearance, supported movement, and interaction sight/range. A failed query returns failure; it never substitutes a global floor. Query buffers have complete-query fallback when saturated. Non-controller travel uses small collision-checked steps and caps extreme frame displacement to preserve that bound.

| Participant | Result |
|---|---|
| Player | Last safe support is recorded above or below the original floor; fall recovery is relative to that support. Existing controller collision, jump, ceiling, input, and camera behavior is retained. |
| Enemies | Removed the y=0.1 correction. Compatible navigation guides approaches; controller collision and local separation handle crowds. Summons require supported body clearance. Boss leaps move the controller through their real three-dimensional trajectory and can be interrupted by walls/ceilings. |
| Loot | Swept world collision replaces the y=0.5 bounce plane. Drops land on bridges/stairs/terrain and recover beside the player after timing out, rather than disappearing. Ordinary attraction requires clear cover; clear-wave attraction remains explicitly magical. |
| Companion | Complete routes or collision-checked supported fallback. Explicit recovery selects a validated socket behind the player. Assistance requires an already engaged, visible, eligible enemy; idle camps and covered enemies are not attacked. |
| Residents | Assigned home/post remain owned destinations. Complete routes or checked fallback replace unchecked transform travel. Closed barriers stop travel and opening them allows it to resume. Strollers also check support and collision. |
| Rescue | Stage origin preserves the actor's local ground. Departure checks collision and support; the existing saved choice and Timeline completion flow remains intact. |
| Interaction | Range, relevant height, supported feet/spot, and clear world sight are required. Explicit target ownership lets a chest's own collider be ignored while retaining the separate wall in front of it. |
| Contact/targeting | Existing G1 height/cover policies remain; aim range uses three-dimensional distance. Bridge undersides block upward projectiles and lock visibility. |
| Ground markers | Local support projection clips unsupported mesh portions and preserves the bounded ground-height band, rather than painting a second floor. |

## Navigation ownership and readiness

Navigation collects the active root's collider hierarchy and derives bounds from its geometry, including stacked heights and a translated root. It excludes world participants and restores every temporarily disabled collider to its exact prior state in `finally`, including on injected failure.

Three conservative profiles cover radius/height up to **0.35/1.0 m**, **0.6/2.2 m**, and **1.25/4.1 m**. Controller dimensions include actor scale. Oversized bodies require authored compatibility instead of borrowing an incompatible mesh. Valid runtime agent IDs can be negative; only `-1` means unavailable.

Town and tower use staged asynchronous builds. Source collection restores colliders before worker builds run. `Ready` remains false until all profiles are registered. Disabling an old field cancels outstanding builds, removes its native data, and clears readiness. Missing or failed navigation uses checked fallback movement. The synchronous entry point is retained for deterministic course/diagnostic checks.

Destination sampling is anchored to local support, rejects another level, checks body clearance, and requires a complete route. Actual movement still checks physical support and collision: a complete NavMesh path is insufficient evidence by itself. Native agents supply navigation state while owners retain movement authority. Enemy controllers and navigation share the same 50-degree slope limit and 0.3 m climb/step setting.

`BakeMs` records elapsed preparation/build time; triangulation/area diagnostics aggregate the three profiles and are not unique floor area. The course's asynchronous build took about 158 ms in the initial passing run on this machine. This is evidence of readiness and staging, not a target-hardware frame-time certification.

## Reusable terrain proof

Create the course with **Greyspire → Create G2 Terrain Course**, or run:

```powershell
.\Tools\terraintest.ps1
.\Tools\contacttest.ps1
.\Tools\savetest.ps1
.\Tools\feeltest.ps1
.\Tools\scenariotest.ps1 -Scenario Story
.\Tools\scenariotest.ps1 -Scenario Nav
```

`TerrainCourse.cs` contains uphill/downhill travel, convex/concave corners, a narrow door, stair landing, overhead beam, bridge over a lower path, closing barrier, and an unsupported ledge. Its lower floor is at world y=-20 and bridge at y=-12. This deliberately exposes hidden absolute-height assumptions. The camera evidence is `Logs/terrain-course.png`.

`TerrainTest.cs` exercises real controllers/actors/physics, compatible and incompatible body routes, forced bake failure, pending/cancelled staged builds, actual pet fallback/recovery, crowd separation, and a live enemy chase. The elevated rescue runs the real Timeline, saves its choice, walks its departure, and restores gameplay.

The final course passes **58 assertions**. Tests use isolated journey directories and restore the exact Greyspire editor preference key; existing player journey files are preserved.

## Completion evidence

**G2 is implemented and validated for this support/navigation contract.** The combined run passed **252 assertions: 58 terrain + 63 contact + 103 save/reward + 28 movement/camera/input**. The terrain run includes real elevated enemy crowd separation and chase, actual no-nav companion wall/recovery behavior, and the elevated rescue's saved choice/departure/completion. The closure review aligned controller/navigation slope settings and verified that a broad slime standing on another capsule separates and lands on its local bridge support. Final asynchronous course navigation completed in **135 ms elapsed** in `Logs/terrain.log`.

The separate Timeline regression completed both branches, including skip, with `rescued=3`, choices `2` and `1`, and gameplay restored (`Logs/terrain-story.log`). The existing tower navigation scenario completed route arrival, heel/follow, and hunt/strike with agents on their compatible NavMesh (`Logs/terrain-nav.log`). Its staged floor bake completed in 1021 ms elapsed; it proceeded asynchronously while collision remained active.

The **Windows build succeeded: 382 MB, 0 errors, 1 existing `PlayerStats` serialization warning** (`Logs/terrain-build.log`). Output: `Builds/Greyspire/Greyspire.exe`; its compiled game DLL was rebuilt after the closure review on 13 October. Source, runners, delivery reports, compiled DLL, evidence logs, course capture, and SHA256 manifest are retained in `Backups/phase-three-complete-20261013`.

## Scope and authoring constraints

### Closure audit before phase four

Source checksums matched the delivered baseline. The rerun then exposed a real long-frame input ordering gap: the dodge buffer could expire before processing a cancel window that had opened during that frame. The controller now resolves contact before the cancel and checks the input's validity at the window's opening time. Two deterministic checks verify accepted and already-expired input. The closure passed **58 terrain + 65 contact + 103 save + 28 movement = 254 checks**, plus a **0-error Windows build**. The verified source/evidence baseline is retained in `Backups/phase-three-verified-20261013`; G3 starts only after this closure.

The current town/tower/Wilds retain their existing layouts and authored sockets; this pass establishes the support/navigation contract and its terrain proof. New chapter routes still need authored objective/door/branch graph validation and any additional agent profiles they require. Moving-platform attachment, arbitrary rotating/deforming navigation geometry, full content-ID migration, progression balance, new animation assets, controller choice focus, and target-hardware profiling remain separate work. No save schema or reward identity is changed here.

The course exposed native path initialization/agent-ID integration issues, an unintended small ramp joint gap, clearance that was unnecessarily lifted under a low beam, and fixture errors involving resume-frame pause and disabled enemy registry membership. These were corrected before the passing course run; failed attempts are not passing evidence.
