# Greyspire — phase seven: reproducible production

15 October 2026 · `D:/game` · G6 / DA31–33, DA36, DA38–40

**G6 closed for the recorded technical contract: implementation, acceptance, build and checkpoint verification passed.** No later phase has begun.

**Fresh closure re-audit, 15 October:** all **948 checks** passed again, both rescue branches and navigation completed, the Windows rebuild retained zero errors/warnings and the same runtime DLL, and a new **609.7068-second** production soak passed. Initial checkpoint verification matched **589 archived / 588 live files**; all **430 runtime inputs** remained unchanged after the reruns. [Closure report and phase-eight entry contract](GREYSPIRE_PHASE_SEVEN_CLOSURE_AUDIT.md) / [machine-readable decision](../Reports/phase-seven-closure-acceptance.json). Measurements below describe the original sign-off, retained in `Backups/phase-seven-complete-20261015`; the closure report records the fresh measurements. Phase eight is now cleared to begin.

The prerequisite was verified before edits: 486 phase-six checkpoint hashes matched, live source/tool/report/package/audio files matched, and the delivered DLL retained SHA256 `42836D233B6B10F405CE88260A0CAD25DA175F50657601C70CB24EF29E9D7FA0`. Fresh feedback and save runs passed **178 + 103 checks**. The verified baseline and fresh evidence are retained in `Backups/phase-six-verified-20261014`.

This gate preserves the thirty existing floors, theme/layout assignment, Guardians, rescues and story references. It introduces explicit floor definitions and independent random streams; generated-resource ownership, authored-light preservation and measured dash reuse; named simulation/world/presentation clocks; complete dense camera queries; authoritative opt-in local telemetry; and reproducible replay/transition/stress evidence.

It does not create six new authored strata, a branched floor, mastery trees or a new faction arc. Those are later content gates. Seed replay covers the declared random/content contract, not cross-platform bit-identical physics. Local stress measurements will identify the machine/scenario and will not be presented as target-hardware certification.

| Finding | Implementation and review source |
|---|---|
| DA31 | [DashGhostPool](../Assets/Scripts/DashGhostPool.cs) reuses at most 192 level-owned pieces and baked meshes. Renderer discovery is cached for the current model. Rent resets pose, mesh, age, tint and scale; overload omits cosmetic pieces. [GhostFade](../Assets/Scripts/GhostFade.cs) returns pieces to the pool and retains legacy cleanup on destruction. |
| DA32 | [Game startup](../Assets/Scripts/Game.cs) parents its generated sun to the game and preserves authored lights. [GeneratedResources](../Assets/Scripts/GeneratedResources.cs) owns fresh native assets and the new meshes created by runtime static batching. Imported/shared assets remain shared. |
| DA33 | [GameClocks](../Assets/Scripts/GameClocks.cs) names simulation, world, presentation and effect clocks. [CharAnim](../Assets/Scripts/CharAnim.cs) evaluates humanoid graphs manually and uses the same actor clock for clip commitment. [StoryScene](../Assets/Scripts/StoryScene.cs) holds the director and controller during pause, suppresses false stall recovery, and restores original animator mode/speed on close. |
| DA36 | [ProductionRandom](../Assets/Scripts/ProductionRandom.cs) versions portable streams, persists the campaign root and transactional loot state, and separates combat, spawn, per-enemy AI/appearance/drop, audio, narrative, cosmetic and pickup motion domains. Actor/split IDs and seed formatting are invariant under number culture. |
| DA38 | [ThirdPersonCamera.SafeBoom](../Assets/Scripts/ThirdPersonCamera.cs) falls back to the complete cast results when its 48-hit buffer saturates, then selects the closest meaningful solid. |
| DA39 | [PlaytestLog](../Assets/Scripts/PlaytestLog.cs) receives authoritative damage results, committed rewards, defeats, despawns, actions, inputs, screen/floor transitions and successful journal mutations. Frame polling no longer infers kills or damage. |
| DA40 | [FloorCatalog](../Assets/Scripts/FloorCatalog.cs) defines thirty stable floor IDs, theme/layout mapping, six grouping IDs, intro/heart/Guardian/boss flags and layout seeds. Floor 30 retains the actual boss-arena seed 1002. [Export](../Reports/floor-definitions.csv). |

Simulation uses scaled time during Playing; world movement also runs in Hub. Presentation uses unscaled time and stops under hard pause. Story effects use presentation time; ordinary world effects use world time. Menus and DSP music remain responsive during pause. The compatibility Legacy Animation backend uses Unity's scaled animation time and is explicitly held when its actor is suspended. Closing a panel retains the input fence; hard pause clears pending combat input while retaining the pose. Town movement, ready enemy actions, zero-delay callbacks, projectiles, pickups, camera, character graphs, Timelines and dialogue-controller pause are exercised directly.

Generated ownership includes sky/ridge/grass/flower/star meshes, fresh world materials, crystal/potion/slime/windmill materials, story ring materials, navigation overlays, post materials, baked dash poses, and runtime static-batch output. The transition test originally exposed **103 additional combined meshes per cycle** that the first ownership counter missed. Comparing loaded mesh identities identified Unity's batching output; adopting only newly produced meshes fixed the accumulation. Application-lifetime material/texture/mesh caches and the two shared particle emitters remain distinct from level-owned resources. No blanket pooling of gameplay entities is claimed.

The root seed and `seedPolicy=1` extend the schema-2 journal without changing earned records. Loot draws and the exact offered choices share the outer reward transaction; an interrupted write restores random state as well as rewards. Unsupported future stream policies reject loading before runtime writes and preserve the committed bytes. Rebuilding a declared scene resets its named gameplay streams; split children derive identities from their parent. Fixed layout/content streams preserve the current terrain contract, with pack/camp/chest draws isolated from scenery consumption. Adding a sound or visual draw cannot advance combat, AI, spawn or drop streams. Full input/physics recording, cross-engine replay, and bit-identical physics across platforms are outside this contract.

Telemetry is enabled only by `-playtest <name>` or an explicit test call. Local CSV rows carry session/sequence identity, build/content/save/seed versions, root seed, campaign, expedition, scope, source/target, action, result, actual damage and real/simulation time. The queue flushes in batches of at most 64 and at boundaries/one-second intervals, with an 8 MiB file limit and disclosed failure/drop status. A failed optional log cannot block gameplay saving. There is no network transmission. A sudden process termination can lose the final unflushed diagnostic batch; reward persistence remains owned by JourneyStore. Session counters come from accepted events, including lethal damage and revival; silent removals and split children do not invent rewards. The action/probe stream is diagnostic evidence, not a recording sufficient to reconstruct arbitrary physics.

The source fixtures are [ProductionTest](../Assets/Editor/ProductionTest.cs), [TerrainTest](../Assets/Editor/TerrainTest.cs) and [FeelTest](../Assets/Editor/FeelTest.cs). Terrain's fixed-iteration controller cases now use a fixed 60 Hz engine clock and isolated seed. The buffered-landing fixture refreshes collision flags after teleport, processes key release separately, and observes descent followed by the upward response instead of relying on one sample near the jump apex. The gameplay grounding and input-buffer requirements remain in place.

The final production run passed **201 checks**, including a saved world-day rollover. [Independent export validation](../Reports/production-evidence-check.json) passed **1,769 unique events**, including 1,200 stress probes, 512 escaped Unicode batch probes, two committed kill rewards, three silent despawns and one defeat. Revival and lethal contact each retain their actual damage. The run ended with zero dropped diagnostic events and a healthy isolated journey. Runtime log.

| Recorded local measurement | Result |
|---|---|
| Sustained town dash/particle/audio load | 610.2056 seconds; 47,007 rendered editor frames |
| Frame time, median / p95 / p99 | 12.7430 / 15.7550 / 18.7518 ms |
| Peak recorded GC allocation in one frame | 600,467 bytes; this is not a zero-allocation claim |
| Dash reuse, median / p95 over 50 calls | 1.0392 / 1.5132 ms, versus historical 1.5874 / 2.7456 ms |
| Dash lifecycle | 192 created slots/baked meshes in overload fixture; zero active after cleanup; 14 slots during soak |
| Warm town native counts: owned / meshes / materials | 61 / 311 / 610 |
| Warm floor native counts: owned / meshes / materials | 35 / 281 / 614 |
| Warm wilds native counts: owned / meshes / materials | 250 / 386 / 724 |
| Transition coverage | Six town → floor → wilds cycles, 18 transitions; identical native counts after warm-up |

Sources: [stress frames](../Reports/production-stress.csv), [dash baseline](../Reports/dash-baseline.csv), [dash reuse](../Reports/dash-reuse.csv), [transition counts](../Reports/production-transitions.csv), [seed replay](../Reports/seed-replay.csv), and [run manifest](../Reports/production-manifest.txt). The editor rendered at 1536×672, quality 2, 4× MSAA, on Windows 11 with Ryzen 7 250, RTX 5060 Laptop GPU and 15,674 MB reported RAM. The frame percentiles cover this town fixture; the floor transition fixture measures construction/cleanup, not a full combat campaign. Native inventories are retained per scope/cycle. The fresh town camera capture was visually inspected.

Final fresh regression evidence is **201 production + 103 save/reward + 65 contact + 29 movement/input + 58 terrain + 194 growth + 120 objective + 178 feedback/controller = 948 passing checks**. Both rescue Timeline branches, skip handling, and tower route/follow/hunt completed separately. The reduced-effects, map and town camera captures were inspected. [Machine-readable acceptance](../Reports/phase-seven-acceptance.json) and sequential runner log record the final results. Earlier one-minute samples and failed/interrupted logs remain for traceability and are not the final gate evidence.

The final Windows build log reports **382 MB, 0 errors and 0 warnings**. The delivered `Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll` is **494,592 bytes**, SHA256 **`FE242FF14283C410C5D913FA48A98C46FE6FAAA328E71E1514ACF89C3C71AF03`**. This is the compiled runtime after the final clock, identity and ownership fixes. Cold builds earlier in the work emitted the existing `Post.shader` gradient-loop warning; the final build emitted none.

The checkpoint manifest covers **589 files**, with **zero hash mismatches** and **588 matching live source/evidence files** (the extra file is its checkpoint readme). Verification includes the delivered DLL. The checkpoint contains the runtime/editor source, runners, reports, project/package settings, audio/story/shader resources, boot scene, final logs/captures and isolated production journey proof. It is a source/evidence checkpoint used with the repository's existing assets; the complete player remains under `Builds/Greyspire`. Final checkpoint verification.

Reproduce from `D:/game` with Unity closed:

```powershell
pwsh -NoProfile -File Tools/productiontest.ps1 -Visual -StressSeconds 600
pwsh -NoProfile -File Tools/productionverify.ps1 -Build
python Tools/check-production.py
```

Each runner isolates journey files under `Logs/test-saves` and restores the exact Greyspire editor registry key. The historical [dash baseline](../Reports/dash-baseline.csv) was captured before pooling; rerunning the baseline tool against optimized source measures the new implementation and cannot recreate the historical result. The verified phase-six source is retained separately.

Human listening/playability, a complete fresh campaign, hours-long endurance, physical power-loss testing, multiple controller models and target-hardware performance certification remain separate acceptance work. Native-count stability, seeded fixtures and editor frame samples support only their recorded scenarios. Phase eight has not begun.
