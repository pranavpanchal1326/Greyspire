# Greyspire — phase five: objective and consequence safety

14 October 2026 · primary project `D:/game` · G4 / DA03–06, DA24–25

**G4 is closed for the current objective and consequence contract:** 568 assertions passed, both rescue Timeline branches passed, the town consequence capture was inspected, and the Windows build succeeded with zero errors and zero warnings. Phase six has not begun.

## Phase-four prerequisite

The previous checkpoint was verified before phase five began: **232 files matched their SHA256 manifest**, the compiled DLL matched the delivered hash, and the recorded **448 passing checks / zero-error build** were intact. Fresh growth and save reruns passed **194 + 103 checks**. The source and updated closure evidence were saved under `Backups/phase-four-verified-20261013` before G4 edits.

## Objective policy

| Finding | Implemented behavior |
|---|---|
| DA24 — regional kill eligibility | An enemy carries its immutable home-region ID. Chasing it across a border changes neither its commission eligibility nor its split children's origin. Standing inside a commission region cannot make an outsider qualify. |
| DA25 — named target | Named commissions match a separate target ID, correct home region, bandit classification and Brute kind. A different chief cannot substitute, even with the same display title. The actual camp spawner binds the requested chief exactly once. |
| DA06 — stable identity | Regions, their two treasure slots, rescues, heroes, mission references and selection are stored by permanent IDs. Definition indexes remain presentation boundaries. Reordering/insertion cannot reinterpret old ownership; names and appearance travel with definitions. |
| DA03 — interrupted choice | Rescue grant, pending state and unique campaign token commit together. Choice and its consequence commit together. Return completion requires a resolved choice and matching token; repeated or old-campaign callbacks cannot alter a new rescue. |
| DA04 — return access | The previous cleared-floor recovery route remains available and is covered by the save/terrain/rescue regressions. It does not advance the active climb or reissue rewards. |

Kills and recovered items carry quest, attempt, expedition and event identity. An abandoned/reaccepted quest gets a new attempt. Event deduplication persists across reload; ready commissions stop accepting extra events. Accepted-event slots are bounded by the commission's goal and reused on a new attempt, rather than accumulating an unlimited event history.

Commission stages are **Available → Active → Ready → Paid**, derived from durable identity/progress and the paid receipt. Acceptance validates rank, content and the single-active-commission rule. Turn-in requires real readiness and the correct town client. Payment commits currency, XP, receipt and a fulfilled consequence before thanks dialogue. Earlier floor clears and discoveries remain valid for climb/exploration commissions. “Every Road” follows the current authored region count; previously paid receipts stay paid.

An unavailable saved commission stays in storage and blocks accidental replacement/payment. The board explains its absence and lets the player explicitly abandon it. Abandoning clears current progress and establishes a new attempt on reacceptance. Core rescue removal and loss of the free default hero are rejected by authoring validation until an explicit migration policy exists.

The tracker/acceptance notice explains the home-region rule and named-chief requirement. Ordinary enemies retain a runtime reward-commit flag and birth campaign: repeating an already committed kill callback cannot grant XP, style currency or daily progress; an old campaign's callback cannot reward a new journey. A failed save leaves the commit flag unset for a valid retry. Normal new encounter instances can still award ordinary repeatable combat rewards.

## Durable discovery, treasure and branch consequences

Discovery flags, carried currency and discovery XP commit together. The initial gate arrival preserves its existing no-bonus policy, but its discovery is now committed immediately. Wilds treasure uses a stable region/slot receipt and expedition fence; a failed write leaves both chest and purse untouched, and a stale or settled expedition cannot reopen the purse. Recovery strongboxes capture the exact quest/attempt/item entitlement before interaction; an old box cannot complete another job or a reaccepted attempt.

A rescue choice records either `safe-return` or `guardian-intel`; successful departure separately records `home`. Town conversation reads that ledger by the rescued person's ID. The safe-return branch acknowledges being sent straight home; the information branch recalls that person's Guardian advice. Both branches retain the same previously earned two points. This phase adds no faction simulation or numerical reward for replaying dialogue.

Split Slimes inherit their parent's birth tier as well as objective context. Their health/XP tier cannot change merely because the parent was chased into a different region. Each child remains a distinct kill event.

## Save compatibility and migration

The JSON envelope advances from **schema 1 to schema 2**. Existing schema-1 files and frozen registry saves are supported. Schema-2 files retain the same campaign fence, checksum, atomic replacement and backup recovery. Phase-four and older builds reject schema 2 instead of misreading its new identity records.

Before replacing a schema-1 JSON primary, the store preserves its exact bytes beside it as `journey.json.v1.<snapshot-digest>.pre-objectives`. This archive is independent of the rotating backup; retries reuse the same snapshot archive and a different old snapshot receives a different name. It is retained for manual recovery and is never automatically substituted for the upgraded journey. To return to an older build, close the game and preserve the entire current journey directory first, then restore the matching schema-1 archive with that build and its original campaign marker. That restores pre-upgrade progress; progress earned in schema 2 requires the upgraded build. Source checkpoints do not contain the player's personal saves. Registry-only imports retain their original registry records.

Migration reads frozen original region, rescue and hero index tables. It preserves levels, allocations, XP, points, wallets, existing receipts, pending choices, the campaign ID, raw legacy fields, unknown reserved bits and unrelated extension records. Known fields are translated to stable ID keys; unknown legacy bits are not silently assigned to newly inserted content. Historical paid/choice/pending evidence is interpreted conservatively to avoid a second rescue grant.

Migration commits once and refreshes the backup only after primary commit. Interruption leaves both original schema-1 snapshots available; reload retries the migration. Unsupported future envelope or objective-data revisions fail without falling back to an older interpretation. New game clears the complete namespace and all consequences while preserving settings.

The indexed Echo/ending/tower-floor families remain formally bounded under their existing contracts. Their broader authored-content redesign belongs to later production work. Region/chest/rescue bit masks now act as current-index facades over ID records; content must still respect the facade bounds until those UI interfaces are replaced. Additional rescued characters also require authored town placement beyond the current five sockets; this phase validates the five existing characters.

## Artifacts and reproduction

- [Identity manifest](../Reports/objective-identities.csv): commissions, regions, rescues and heroes with source-owned IDs.
- Consequence dialogue capture: rendered conversation offered by the actual rescued town NPC.
- `Assets/Editor/ObjectiveTest.cs`: migration, reordering/insertion, interruption, lifecycle fences, real Wilds spawning, lethal boundary kill, strongboxes and town callback proof.
- `Tools/objectivetest.ps1 -Visual`: dedicated journey directory and exact editor-preference restoration.

Run with the Unity editor closed:

```powershell
& Tools/objectivetest.ps1 -Visual
& Tools/savetest.ps1
& Tools/growthtest.ps1 -Visual
& Tools/contacttest.ps1
& Tools/feeltest.ps1
& Tools/terraintest.ps1
& Tools/scenariotest.ps1 -Scenario Story
& Tools/savetest.ps1 -Build
```

The save regression fixtures now use the current schema for the future-version rejection case, genuine floor readiness before commission payment, and stable rescue mutation keys. The growth failure fixture uses a new enemy instance for its new campaign rather than replaying an already committed one. Those changes preserve the original safety assertions under the stronger contracts. Failed attempts are retained separately and never counted as passing evidence.

## Verification and limits

| Evidence | Result |
|---|---|
| Objective migration, atomic transactions, real spawns/interactions and consequences | **120 checks** — `Logs/objective.log` |
| Growth and action contracts | **194 checks** — `Logs/growth.log` |
| Save and reward interruption/reload safety | **103 checks** — `Logs/save-safety.log` |
| Combat contact | **65 checks** — `Logs/contact.log` |
| Movement, camera and input | **28 checks** — `Logs/feel.log` |
| Terrain/navigation course | **58 checks** — `Logs/terrain.log` |
| Total assertions | **568 passed** |
| Rescue Timeline scenarios | **Both branches completed, including skip** — `Logs/terrain-story.log` |
| Real rescued town conversation | **Capture inspected; persisted Guardian advice is readable** — `Logs/objective-consequence.png` |
| Windows build | **Succeeded, 382 MB, 0 errors, 0 warnings** — `Logs/save-safety-build.log` |

The objective and save suites were rerun after the final migration archive/schema-revision changes, followed by the build. The earlier full regressions exercise the final gameplay changes; the later changes affect migration and acceptance wording only. Test runners use isolated journey directories and restore the exact Greyspire editor-preference registry key. The retained migration fixture is `Logs/test-saves/objective-20261014-092803`; its archive is test data, not a personal journey.

Compiled runtime DLL SHA256: `2B86CF08F57FBA290ED6E70042F0EAE0A495A1F194C5F220EDD1B3C9868B347D` (`Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll`). The source, editor fixtures, runners, reports, evidence, migration proof and compiled DLL are retained in `Backups/phase-five-complete-20261014` with a SHA256 manifest.

This gate covers the current objectives and observable rescue branches. A full campaign playtest, new faction arcs, controller/comfort review, asset/audio completion, physical power-loss testing and target-hardware profiling remain later work. G5 / phase six is the next gate and has not begun.
