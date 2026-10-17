# Greyspire — phase four: bounded growth

13 October 2026 · primary project `D:/game` · G3 / DA19–22 and DA27

**G3 is complete for the bounded-growth contract below.** Final verification passed **448 checks**, both growth UI captures were inspected, and the Windows build succeeded with **0 errors and 1 existing shader warning**. Later gates remain open.

## Prerequisite closure

Phase three was re-audited before these changes. Its source matched the delivered manifest, but the rerun exposed a long-frame dodge-buffer ordering gap. The repair resolves attack contact before a cancel and evaluates the buffer at the cancel window's opening time. The verified closure passed **58 terrain + 65 contact + 103 save + 28 movement = 254 checks**, followed by a Windows build with **0 errors and 1 warning**. Source, runners, evidence and compiled DLL were backed up to `Backups/phase-three-verified-20261013` before G3 began.

## Implemented contract

| Finding | Result |
|---|---|
| DA19 — caps and stacking | One effective-stat policy applies base → forge → hero → level/stats → bounded boon ranks → final limits. New stat allocations stop at 60; effective level stops at 60. Old records remain intact. |
| DA20 — action/animation time | Offensive clips start at their windup. Gameplay contact, duration and full/upper/legacy playback share a contact plan with a 2× playback budget. Unshowable tempo becomes bounded impact, up to 1.5×. Landing remains the plunge's physical contact. |
| DA21 — complete summaries | A multi-level summary includes all points, all crossed ranks and all skills. Notices queue rather than replace each other; reading time stops under pause, dialogue, cards, fades and town panels. Daily reward presentation follows a successful combined kill commit. |
| DA22 — style feedback | Core kill XP depends on encounter strength and Wilds night risk. Style pays carried bonus Sparks, up to the enemy's base coin value, instead of doubling permanent XP. Post-defeat callbacks cannot reopen a settled purse. |
| DA27 — repeated daily farming | Twenty additional daily points per campaign, then currency rewards continue. Dawn, rest, reload and repeated same-day callbacks cannot reset the budget or pay twice. |

### Limits and retained value

Level 60 grants the final three level points. Surplus and later XP convert to banked Sparks at **20 XP : 1 Spark**, keeping the exact remainder. Awarding the largest representable XP amount terminates without level arithmetic overflow. An overflowing wallet rejects the whole transaction and retains the last committed records.

Damage is unchanged through 50, then approaches 100 smoothly: `50 + (raw - 50) / (1 + (raw - 50) / 50)`. Effective tempo approaches 180% and movement approaches 108%, keeping sprint within the gait playback budget. Critical probability stops at 75%; overflow increases the critical multiplier above its base 2×. Other final bounds are 30 hearts, 2× reach, dash cooldown at least 0.2 seconds, 20× burst gain, 5× pickup currency and 15 m magnet range. Boon ranks bound thorns/lifesteal/rescue charges; Second Wind charges refill on the next expedition, not on stat refresh.

The initial extreme-build export gave a 1.3–2.1 second active damage budget against the strongest King. The smooth damage limit addresses that result while preserving early damage and positive marginal Strength value. The final export is linked below; these are idealized damage estimates, not observed encounter lengths or a claim of player-tested enjoyment.

Fresh point supply is **177 from levels + 20 from dailies + 10 from five rescue receipts = 207**, against 240 allocation slots. A specialized 177-point build does not require daily or rescue farming. The Status screen shows effective values, limits, critical multiplier, daily budget and retained legacy values. Town offers a free full respec and optional **1 point : 25 banked Sparks** conversion, labeled permanent. Currency funds the existing finite forge/hero/companion unlocks and paid choices/rerolls.

### Save compatibility

The save schema, campaign fence, reward IDs and earlier receipts remain unchanged. `dailyGrowth` is a new campaign-scoped counter. Historical daily point payments cannot be reconstructed from old saves; they remain earned, and the new budget allows at most twenty further daily points. Legacy level, allocations, XP, points, boons and currency are not erased or rewritten merely by calculating effective stats. Respec refunds the full stored allocation, including ranks above the new cap. If the exact refund exceeds the stored integer range, it rejects before erasing anything.

Every new allocation, respec, conversion, daily completion and XP award owns a nested-safe transaction. A kill still commits XP, mission progress, daily entitlement and style currency together. Progress and skills survive reload; the visual notice queue itself is transient and Status remains the durable reference.

## Balance artifacts and reproduction

`Assets/Editor/GrowthTest.cs` exports the same effective-stat calculations and actual controller contact plans used in play. It reads imported animation playback, instantiates enemy health at six floor snapshots across all Gloom tiers, and projects six complete tower climbs using the actual deterministic `FloorWave` counts. The model includes guaranteed elite Guardians, BigSlime children and the King's fixed 600 XP; optional missions/Wilds/dailies, random elites and boss summons are explicitly excluded from the pace projection.

- [Balance explanation and results](../Reports/growth-balance.md)
- [Encounter envelopes](../Reports/growth-balance.csv)
- [Effective loadouts](../Reports/growth-stats.csv)
- [Contact timing and playback](../Reports/growth-actions.csv)
- [Permanent XP pace comparison](../Reports/growth-pace.csv)

`Tools/balance_tower.py` is the earlier approximate difficulty study. Its old style-scaled formulas are historical; the runtime export above is the authority for G3.

Run from PowerShell with the Unity editor closed:

```powershell
& Tools/growthtest.ps1 -Visual
& Tools/contacttest.ps1
& Tools/savetest.ps1
& Tools/feeltest.ps1
& Tools/terraintest.ps1 -Build
```

Each runner isolates journey files and restores the exact Greyspire editor preference key. The visual growth run captures the actual Status and aggregated summary UI while gameplay updates are frozen. Failed attempts remain separate diagnostic evidence and are not counted as passing checks.

## Remaining scope

This phase closes the bounded-growth implementation and its repeatable technical proof once the checks below pass. Human progression/encounter playtests, artistic review of authored weapon contact poses, controller/comfort coverage, and target-hardware profiling remain separate. Full stable objective identities, chief matching, quest transitions and consequences belong to G4 / the next phase. No later gate is started by this delivery.

## Verification

| Check | Final evidence |
|---|---|
| Growth / save-failure / animation / model / UI | **194 checks passed**, `Logs/growth.log`; no unexpected runtime or rendering errors |
| Contact and mutual-kill settlement | **65 checks passed**, `Logs/contact.log` |
| Save and reward safety | **103 checks passed**, `Logs/save-safety.log` |
| Movement, input, camera and targeting | **28 checks passed**, `Logs/feel.log` |
| Shared terrain, navigation and elevated rescue | **58 checks passed**, `Logs/terrain.log` |
| Windows package | **Succeeded, 382 MB, 0 errors, 1 warning**, `Logs/terrain-build.log` |

The one build warning is the existing D3D11 loop/gradient warning in `Assets/Resources/Post.shader:32`. `PlayerStats` is now serializable; the previous serialization warning is gone. The current compiled DLL is `Builds/Greyspire/Greyspire_Data/Managed/Assembly-CSharp.dll`, rebuilt at 22:44 IST on 13 October; SHA256 `2D3B5EE0AFE2A366582B6655AD2E09FD5D97CC87905872C54776EE8286F7BB3B`.

The final model has **35 effective loadouts, 130 action plans, 3,780 encounter rows and 180 pace rows**. Extreme fresh builds have 1.01–1.23 second three-hit combos, a 6.51–7.25 second expected active light-damage budget against the 5,890.5 HP Gloom-5 King, and can survive four or five 3-heart heavy hits before defeat. The six-climb projection ends at level 40 at the old D-style/current XP rate versus level 50 under the old continuously-SSS upper bound. Optional encounters and rewards remain outside that projection.

Status capture shows retained legacy level/allocations, effective limits, every skill and town respec/conversion without clipping. Summary capture shows all 177 points, every rank crossing, every skill and the XP conversion rule together. Notice timers hold while these messages are obscured.

The movement rerun exposed a fixture assumption: the old 20-HP targeting Slime died from the now 21-damage throw before the intended target-switch step. The fixture keeps its candidates alive until its explicit death test; the subsequent run passed all 28 checks. Growth-fixture initialization, manual animation evaluation and disabled-target registration were also corrected before the final clean run. Failed logs are retained separately and do not count as passing evidence.

The validated source, editor tests, runners, delivery reports, runtime model, evidence logs/captures and compiled DLL are retained under **`Backups/phase-four-complete-20261013`**, with a SHA256 manifest. The executable remains `Builds/Greyspire/Greyspire.exe`. Human balance playtesting remains the scope limit described above; phase five/G4 has not begun.
