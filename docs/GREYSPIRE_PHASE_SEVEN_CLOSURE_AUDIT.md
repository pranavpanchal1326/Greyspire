# Greyspire — closure audit through phase seven

15 October 2026 · Primary Unity project: `D:/game` · G0–G6

**PASS for the recorded technical contracts. G6 is closed and phase eight / G7 may begin.** This decision follows fresh execution and source verification. Phase-eight gameplay has not been implemented by this audit.

The initial checkpoint check verified **589 archived files and 588 corresponding live files**, including the delivered runtime DLL, with zero mismatches. After the reruns, **all 430 runtime/editor/resource/scene/package/project-setting inputs still matched** the original checkpoint. Runtime and editor file inventories matched too. No gameplay repair was required during this closure audit.

| Phase | Technical contract | Fresh passing checks |
|---|---|---:|
| 1 / G0 | Save, reward, interruption and recovery safety | 103 |
| 2 / G1 | Combat contact, immunity, cover and mutual kills | 65 |
| 3 / G2 | Terrain support, participants and navigation | 58 |
| 4 / G3 | Bounded growth and attack/animation timing | 194 |
| 5 / G4 | Objective identity, migration and consequences | 120 |
| 6 / G5 | Feedback, controller, comfort and chapter fixtures | 178 |
| 7 / G6 | Replay, clocks, lifecycle, telemetry and stress | 201 |
| Shared regression | Movement, input buffering, targeting and camera | 29 |
| **Total** | | **948** |

Both rescue Timeline branches and skip handling completed separately, as did the tower route/follow/hunt scenario. The fresh terrain, reduced-effects, map and town-camera captures were visually inspected. The scenarios exercise scripted fixtures; this is not a human campaign or controller-hardware sign-off.

The fresh sustained-load run lasted **609.7068 seconds**, with **49,765 rendered editor frames** and **1,200 stress probes**. It passed the saved world-day rollover and bounded native-resource checks. Across **18 transitions**, warm native counts repeated exactly: town `61 / 311 / 610`, floor `35 / 281 / 614`, wilds `250 / 386 / 724` (owned assets / meshes / materials). Independent export validation confirmed **1,769 unique events**, correct CSV escaping, two committed kill rewards, three silent despawns, one defeat and **zero dropped diagnostic events**.

Local frame time was **13.1334 ms median / 17.2044 ms p95 / 20.6537 ms p99**; peak recorded GC allocation was **557,655 bytes in one frame**. The soak used 28 dash slots within the 192-slot cap and retained the 12-voice world-audio capacity. These measurements describe this editor fixture and machine, not target-hardware certification or zero-allocation gameplay.

The fresh Windows build succeeded at **382 MB, zero errors and zero warnings**. Its runtime DLL is unchanged: **494,592 bytes**, SHA256 `FE242FF14283C410C5D913FA48A98C46FE6FAAA328E71E1514ACF89C3C71AF03`.

The built executable also completed startup and a timed clean exit using an isolated journey directory. Unity reported `Failed to capture screen shot.` during the hidden-window smoke attempts; a standalone player screenshot is **unavailable**, and no standalone visual sign-off is claimed. The smoke runner reports this separately from startup health. Visual acceptance here uses the fresh editor captures. Exact editor and player registry keys were restored after their respective checks; real journey files were not used for test writes.

Evidence:

- [Machine-readable closure decision](../Reports/phase-seven-closure-acceptance.json), including freshness checks and the original checkpoint comparison.
- Fresh sequential regression/build runner.
- Fresh production runner, runtime checks, [stress measurements](../Reports/production-stress.csv) and [independent exports](../Reports/production-evidence-check.json).
- Windows build and standalone startup runner.
- Verified closure checkpoint. The original `phase-seven-complete-20261015` checkpoint remains the historical sign-off; the verified checkpoint retains the fresh evidence and audit tools.

The new [closure checker](../Tools/check-phase-seven-closure.py) rejects stale logs, incomplete runners, changed runtime inputs, a shortened soak, failed export evidence or a failed build. It was exercised while the reruns were incomplete and correctly refused to clear phase eight. Its final decision passed only after all required fresh checks completed.

## Phase-eight entry contract

The next gate is **G7 — branching pilot**, as defined in master-plan section 39:

1. One playable branched floor with a readable main route, meaningful optional route and reconnecting shortcut, reachable with baseline movement.
2. Small mastery choices with preview, stable saved identities, training and a bounded town respec; demonstrate three distinct early builds.
3. One seeded event with persisted selection, eligibility, cooldown and fallback, plus an explicit immediate consequence and a later callback.
4. Prove the routes, mastery, event and callback together, including both choices, interruption/reload, controller and reduced-effects coverage, then rerun the prior gates and build.

Retain the existing thirty-floor journey and earned progression. The broader chapter upgrade is **phase nine / G8**, after the pilot succeeds. A full fresh-player campaign, human listening/playability/comfort review, hours-long endurance, physical power-loss testing, multiple physical controllers and target-hardware performance certification remain open acceptance work; this audit does not silently relabel those as complete.
