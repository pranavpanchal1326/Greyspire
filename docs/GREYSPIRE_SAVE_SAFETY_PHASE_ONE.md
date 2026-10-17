# Greyspire — phase one: save and reward safety

Started 12 October 2026; validation continued 13 October 2026. Primary project: `D:\game`, Unity 6000.5.4f1. This implements the first “protect the journey” work package from the master plan.

## Delivery contract

Earned progress, reward entitlement, and payment belong to the saved journey. Dialogue, cards, and animations present that state. Interrupting presentation must not cancel an earned entitlement or repeat its payment.

| Package | Owner discipline | Delivered artifact | Acceptance / recovery |
|---|---|---|---|
| Atomic journey snapshots | Gameplay persistence | `JourneyStore.cs`, migrated `Save.cs` | An interrupted write keeps the previous complete snapshot; a corrupt primary recovers a checked backup; two invalid copies stop loading and preserve files |
| Guardian entitlements | Progression | `SaveRewards.cs`, card flow in `Game.cs` | Clearing the floor saves the exact offered IDs and price; resume in town; selected boon and payment receipt commit together |
| Commission payment | Quests / progression | Stable mission IDs in `GameGuild.cs`, `Save.PayMission` | Sparks, XP, levels, points, completion, and receipt commit before thanks; replaying thanks cannot pay again |
| Rescue recovery | Narrative / gameplay | `GameSaveSafety.cs`, `GameStory.cs`, tower gate recovery route | Save rescue reward and pending choice together; resolve choice before its branch; unfinished presentation resumes in town; missed rescues remain accessible |
| Expedition settlement | Gameplay / economy | Saved carried purse and settlement in `SaveRewards.cs` | Death applies the existing penalty immediately; quit and repeated results cannot bank again; interrupted living expeditions return to town with their saved purse |
| New-game isolation | Gameplay persistence | Complete journey namespace reset | New campaign identity and empty journey; settings remain; no leftover rescue choices or reward records |
| Evidence and migration rules | Engineering / QA | `SaveSafetyTest.cs`, `Tools/savetest.ps1`, this report | Repeatable fault injection and runtime checks; retained test saves/logs; editor preferences restored |

Dependencies are the existing floor definitions, commission data, boon pool, rescue writing, and Unity's local filesystem. No new art, sound, Timeline, or external service is required. Existing local work was preserved; pre-edit sources are retained in `Backups/save-safety-20261012`.

## Save format and failure behavior

Journey schema 1 is a JSON payload with a campaign ID, explicitly owned key/value entries, and a SHA-256 checksum. `journey.json.tmp` is written and flushed to disk before an atomic replacement of `journey.json`. An abandoned temporary file is never treated as a committed save. A separately flushed campaign marker is written before a campaign reset swaps the primary. A valid primary remains authoritative and can repair an interrupted marker update; fallback backups must match that marker. Missing primary and backup files with an existing marker fail closed instead of importing stale registry state. Transaction failures restore the in-memory state and retain the committed file.

Atomic replacement rotates the previous complete snapshot into `journey.json.bak`. Ordinary reward/pickup transactions use one explicit disk barrier for the primary. Explicit checkpoints, migration, and new-game resets refresh and flush both copies. Backup recovery can restore the preceding complete event snapshot; it never combines a payment from one snapshot with a receipt from another. This is local crash recovery, not a guarantee against hardware failure affecting both copies.

A recovered damaged primary is retained as `journey.json.damaged`, and the menu tells the player that backup recovery occurred. Unsupported schema versions stop loading instead of silently falling back to an older schema. Unrecoverable loading or write failure displays a storage error and stops progression; it never creates an empty journey over the damaged files.

Default locations are under `Application.persistentDataPath/journey/editor` for the editor and `Application.persistentDataPath/journey/player` for the built game. Settings stay in their existing PlayerPrefs store. Every command-line editor method receives an isolated save location automatically; the safety runner also passes an explicit directory under `Logs/test-saves`. The player's actual journey files are not used by the tests.

Ordinary carried Sparks are saved when collected. Tower/Wilds chest rewards include the chest-open state and purse change in one transaction. Region discovery includes its flag, currency, and XP together. Forge purchases, hero purchases, stat spending, daily payouts, and Gloom-cycle reset use the same snapshot ownership. One enemy-kill event commits its XP, commission progress, and daily progress together.

## Reward and settlement rules

- A Guardian receipt is scoped by campaign identity, Gloom tier, and floor. An offered reward retains its original boon IDs and reroll price. A reroll commits its cost and replacement choices together. Resumed town offers use the banked purse for rerolls, since the interrupted expedition's carried purse has already been banked.
- When every boon is at its cap, the Guardian pays **50 banked Sparks**, once. The receipt is consumed in the same transaction.
- Commission storage uses permanent IDs such as `first-steps` and `wolves-road`. UI indices are resolved from the current definitions. Reordering commissions does not reinterpret active or paid work.
- Rescue rewards grant two stat points once. The choice is either unresolved or explicitly saved; loading never invents an answer. Town follow-up can replay the selected branch without repeating the points.
- The tower gate offers a return to an unfinished rescue on an already liberated floor. This is a safe recovery visit, keeps the active climb's floor unchanged, and sends the player home afterward. Previously opened chests remain opened.
- Tower defeat retains `ceil(carried / 2)`; Wilds defeat retains `floor(carried / 2)`, preserving the existing rounding. Settlement occurs before the defeat timer. A normal return or quit retains the full saved purse. Tower run/win counts and Gloom unlock commit once with settlement; Wilds retain their existing run-count policy.
- Ending choice and victory settlement commit before the ending dialogue, so interrupting that presentation cannot lose the win or its unlock.

## Legacy migration policy

The first load with no journey snapshot imports the existing registry journey. The old registry data is retained for recovery, but the new snapshot becomes authoritative. A later new game writes an empty new campaign; it cannot reimport the previous campaign on ordinary reload.

The frozen 19-entry mission migration table is in `SaveRewards.cs`. Old active and completed indices map to permanent mission IDs. Owned boons, stats, perks, heroes, floor/chest masks, world clock, discovered regions, met names, and rescue choices are copied.

Old saves have no durable association between a particular Guardian floor and a chosen boon, and no receipt proving whether a completed commission's delayed XP ran. Migration preserves historical completion and ownership. It **does not automatically reissue ambiguous old boons or XP**, because that could duplicate legitimate rewards. An old rescued character with no saved answer receives a pending town conversation without additional points. Future payments are covered by the new receipts.

## Content identity and expansion plan

| Family | Schema 1 contract | Required before reordering/expansion |
|---|---|---|
| Commissions | Stable IDs, independent of display order; frozen old-index migration | Add new unique IDs; never reuse old IDs; retain removed completion records |
| Guardian boons | Existing explicit numeric IDs are permanent | Never reuse an ID; migrate or supply a stated replacement for a removed pending choice |
| Guardian floors | Existing 30-floor identity and Gloom tiers 0–5 | Migrate floor receipts and pending offers before changing chapter/floor identity |
| Regions / Wilds chests | Existing ordering is frozen; at most 16 regions, two chest bits each | Schema 2: explicit region IDs; map old discovery/chest bits and commission region references using a frozen table |
| Rescues | Existing five rescue indices/floor bindings are frozen | Schema 2: explicit rescue IDs; migrate rescued bits, choices, pending presentation, and recovery destinations |
| Echoes / endings | Existing bit assignments are frozen and bounded | Explicit IDs and frozen migration before repurposing or exceeding 32 flags |
| Heroes / forge / stats | Existing indexed families retain their current identities | Add permanent definition IDs and a frozen mapping before changing their ordering |

Startup rejects duplicate commission IDs or an exceeded region/rescue/Echo bit-set capacity. Tower chest index 31 is distinct; index 32 is rejected instead of wrapping to index 0. Stable region/rescue identity migration remains a prerequisite for content reordering; this pass does not claim those arrays can already be freely reordered.

## Verification record

The final safety run passed **103 checks**, including after the disk-flush optimization. A compile failure during the added content-capacity guard was corrected before this successful run; the final test and build logs contain no compile errors. It covers registry import; commission reordering; new-game isolation and settings; failed writes and orphan temporaries; corruption recovery and future-schema refusal; fault injection between each commission/Guardian/rescue state mutation; complete XP and points; duplicate claims; reroll cost/offer recovery; exhausted-pool fallback; forge purchase atomicity; bit boundaries; living/dead/winning expedition settlement; and runtime card/dialogue/rescue/chest/death flows.

The first timing sample found 18.02 ms median / 22.33 ms p95 across 100 small flushed transactions. This exposed redundant disk barriers and motivated the final optimization. The final sample measured **14.43 ms median / 17.66 ms p95 / 19.39 ms maximum**. One enemy kill now uses one commit even when XP, a commission, and the daily objective all advance. This improves write cost and avoids multiple independent commits for one kill; synchronous saves can still consume a meaningful part of a frame and need target-hardware profiling.

Commands:

```powershell
.\Tools\savetest.ps1
.\Tools\savetest.ps1 -Visual -Build
.\Tools\feeltest.ps1
```

The existing movement/camera/input suite also passed **28 checks** (`Logs/feel.log`). Visual QA inspected `Logs/save-safety/rescue-recovery-gate.png`; the recovery button fits the existing gate controls. The Timeline rescue regression completed both branches, including skipping the opening cutscene. Its final state was `rescued=3`, Rhea choice `2` (tell), Dorn choice `1` (go home), and normal gameplay restored (`Logs/save-safety-story.log`).

The final **Windows build succeeded: 382 MB, 0 errors, 1 existing `PlayerStats` serialization warning**. Output: `Builds/Greyspire/Greyspire.exe`; build evidence: `Logs/save-safety-build.log`.

**Phase-one core safeguards are implemented and validated.** Full region/rescue/hero content-ID migration remains planned before content reordering or expansion; the schema-1 ordering rules above still apply.

Logs: `Logs/save-safety.log`, `Logs/save-safety-build.log`, and the corresponding gameplay regression logs. The safety runner refuses to run against an already open Unity editor, snapshots the exact Greyspire editor preference registry key, restores it in `finally`, and retains isolated test snapshots for inspection.

Performance is measured on this development machine, not certified for target hardware. The per-event save is synchronous; sustained worst-case pickup/kill loads remain a performance review scenario. Tests simulate interruption at the mutation and atomic-replacement boundaries; they are not a physical power-loss test. Broad campaign playtesting, controller choice navigation, full content-ID migration, and the later combat/world upgrade gates remain separate work.


## Closure audit requested before phase two

The original 95-check sign-off did not cover two additional failure windows. They were found during the user-requested closure audit and fixed before phase two:

- **Failed reset backup refresh:** a new-game primary could commit while the backup still held the old campaign. If the new primary was then damaged, the previous implementation could recover old progress. The campaign marker now rejects that backup; the valid new primary still recovers normally. No stale registry import occurs when committed snapshot files are missing.
- **Defeat-delay interactions and healing:** floor/Wilds interactions and healing could reopen activity after the purse had settled. An explicit defeat-pending state now blocks roaming, interaction, pickups, and healing until a proper return/revive boundary. Posthumous XP remains saved without canceling defeat.

Eight additional checks cover these windows. The closure safety run passed **103 checks** and the rebuilt Windows game compiled successfully with **0 errors**. The closure revision also passed all **28 movement checks**, both Timeline rescue branches (`rescued=3`, choices `2` and `1`), and the Windows build (**382 MB, 0 errors, 1 existing warning**). **G0 is closed for the existing content layout; phase two may proceed.** Content-ID migration remains a stated prerequisite for content expansion, not an unfinished requirement for the existing unchanged content layout or the combat-contact phase.
