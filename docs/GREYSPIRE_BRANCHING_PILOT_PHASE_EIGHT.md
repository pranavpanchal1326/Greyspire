# Greyspire — phase eight: branching pilot

16 October 2026 · `D:/game` · G7 · W01/W03/P01/N01

**Phase eight / G7 is accepted.** All 1,050 technical checks passed against the unchanged candidate, and on 16 October the user reported “I played it: ready for phase 9” in response to the build-specific playtest question. [Technical acceptance](../Reports/phase-eight-acceptance.json) and [the attributed human assessment](../Reports/phase-eight-human-playtest.json) record the gate decision. Phase nine may now begin from this verified baseline.

Before edits, the verified phase-seven checkpoint matched **601 archived files and 600 live comparisons**, with zero mismatches. The original phase-seven runtime and evidence remain preserved in `Backups/phase-seven-verified-20261015`. This work changes the playable content version to `legacy30-pilot-v2` and the build version to `greyspire-g7-v1`; it retains the thirty floor identities, theme/layout assignments, existing rescues and earned progression.

## Playable content

**Floor 3 — Surveyor's Crossing** now has a combat road east, a raised survey route west, and a gated cross-pass through the central ruin. Both routes reach the original north stair objective. The optional route contains Iven the courier and the existing floor-3 chest. Its one-time identity and 12 carried Sparks are preserved; previously opened chests stay opened. Rhea's floor-4 rescue remains in place.

The ramps rise 3.2 metres over 12 metres. Collision and navigation use the same authored geometry. The cross-pass latch is accessible from the west, saves atomically, and rebakes navigation when opened. It shortens the measured medium-body navigation route from about **63.1 m to 20.4 m**. Both complete approaches were traversed using real keyboard movement with baseline player stats, and navigation was checked for small, medium and large bodies. The overview capture disables fog for inspection; ordinary gameplay keeps the existing fog policy.

The **Field desk**, southeast of the central town well, opens mastery previews, practice and a liberated-floor revisit. A revisit preserves earned tower progress and routes both exits home. It provides recovery for skipped optional content without opening a progression shortcut.

## Mastery

One exclusive path has two fixed ranks. Stable branch IDs are `mobility`, `duelist` and `breaker`; ranks 1 and 2 are permanent definition identities. Floor 1 clearance earns one technique point and floor 3 earns the second. Technique points are separate from stat points, currency, boons and daily growth. Town respec costs zero and restores the technique budget while retaining story and stat records.

| Path | First node | Second node | Tradeoff |
|---|---|---|---|
| Mobility | A perfect dodge arms one accepted light contact for 1.2 seconds at +30% damage | That counter also staggers for 0.6 seconds | Actual heavy-attack duration is 20% longer |
| Duelist | Open-target riposte changes from 2.2x to 2.7x damage | Riposte grants 12 extra Colour charge when that skill is available | Throw recharge changes from 1.4 to 1.75 seconds |
| Breaker | Heavy attacks charged at least 60% gain 20% damage | Those impacts gain 25% radius | Actual heavy-attack duration is 20% longer |

Animation contact is preserved when heavy duration changes. The tests exercise accepted contacts, actual cooldown/duration values and the expanded impact radius. They establish mechanical differences; human build viability is not inferred from damage arithmetic.

The practice arena uses the existing move set and a signalled partner. Its actors cannot die, produce drops, award XP/Sparks/daily progress or clear floors. Incoming partner strikes are nonlethal even with one maximum heart. The partner gives a gold warning before a strike; the player can guard late, dodge and counter, or charge a heavy. A persistent readout shows the last accepted hit. Returning to town releases the practice actors. Tests require actual partner contacts, rather than treating the absence of defeat as sufficient proof.

## Event and consequence

The event is eligible only on floor 3 after floor 1 has been cleared. A separate `pilot-event` seed domain chooses one of two courier-pack variants and persists the exact selection. It does not advance combat, AI, spawn, loot or cosmetic streams. The event completes at most once per campaign; there is no adjacent-floor repeat. Ineligible visits omit the optional event and retain both routes and the mandatory objective.

| Decision | Immediate committed result | Later town callback |
|---|---|---|
| Send the pack home | The cross-pass opens | After floor 3 is cleared, the field clerk pays 20 banked Sparks once and records the delivery |
| Keep the field kit | 24 banked Sparks | The clerk records Iven's missing delivery; no second payment. The west latch still opens the cross-pass manually |

The preview states both payments and their timing before selection. Choice, shortcut and reward mutate in one journey transaction. A started but unanswered offer recovers in town after interruption. An unseen optional selection does not force a dialogue on return. The later callback also commits its receipt and payment together. Repeated callbacks, re-entry and old-campaign tokens cannot pay twice or resolve another journey.

Records are additive to schema 2 with `pilotVersion=1`. Existing journeys gain no unearned mastery ranks or event payments. Unsupported future pilot policies and unrecognized branch/event identities reject loading before runtime writes; original committed bytes are retained. The prior source/evidence checkpoint should be used for a deliberate downgrade; an old executable does not implement the new pilot mechanics.

## Evidence and remaining acceptance

The pilot acceptance runner is [PilotTest](../Assets/Editor/PilotTest.cs), invoked through [the isolated runner](../Tools/pilottest.ps1). The final pilot run passed **102 checks**, including interruption/reload, both courier branches, controller selection, reduced-effects settings, real baseline traversal, all navigation profiles, opened shortcut reconstruction, practice contacts, one-heart safety, town callbacks, repeat construction and retained chest receipts. Native owned counts remained identical after warm-up during repeated pilot/town construction. The first traversal found a chest on the walking line; it was moved off the route. Earlier logs are retained for traceability.

Fresh captures: floor overview, mastery previews, courier response, later callback, and practice. They were visually reviewed. The pilot uses authored prototype geometry with existing assets; it does not claim final chapter art or a full chapter upgrade.

The final candidate passed [check-pilot.py](../Tools/check-pilot.py): **948 prior assertions plus 102 pilot assertions = 1,050**, with all **440** runtime/editor/resource/scene/package/settings input hashes unchanged. Both rescue branches including skip, tower navigation and independent production exports passed. The sustained fixture ran **618.7878 seconds / 36,991 frames**, with median **16.6700 ms**, p95 **17.1635 ms** and p99 **18.0468 ms**; eighteen transitions retained stable ownership and 1,200 stress probes completed without drops. These are local editor measurements, not target-hardware certification. All automated journeys use retained isolated directories and restore the exact editor preference key.

The delivered Windows build is **382 MB**, with **0 errors and 0 warnings on the final unchanged rebuild**. The earlier cold compile succeeded with one existing `Hidden/ToonArena/Post` gradient-in-loop warning at `Assets/Resources/Post.shader(32)` on d3d11; its [record](../Reports/phase-eight-cold-build.json) and log are retained. The rebuild does not claim a shader repair or a warning-free cold compile. `Assembly-CSharp.dll` is **514,560 bytes**, SHA256 **5709509DEAA0D45FDCB3157B245828ADC6C232358CDE581B033FC87219AB048B**.

Both synthetic presets also passed isolated standalone startup and clean-exit smoke checks: A and B. Hidden player windows had no capturable backbuffer; the five images above are editor captures. The recovery checkpoint location and manifest are recorded in checkpoint metadata, with independent hash verification.

The [human playtest kit](../Reports/phase-eight-playtest-kit.json) contains two synthetic journeys immediately before floor 3, with no mastery or courier choice already granted. Its launchers verify the runtime hash against the technical acceptance report. The kit changes its own journey files; normal audio/camera preferences remain shared. It avoids modifying a real player campaign or claiming its preset progress was earned.

Master-plan section 40 requires: “A human playtest when its success depends on comprehension, enjoyment, or a meaningful choice.” The requested review covered both approaches, the three early mastery paths and the courier tradeoff and later response. The user supplied that assessment on 16 October; the automated fixtures do not supply it. Wider campaign, physical-controller coverage, hours-long endurance, physical power-loss tests and target-hardware certification remain separate work.

Reproduction, with Unity closed:

```powershell
python Tools/check-pilot.py --start
pwsh -NoProfile -File Tools/pilottest.ps1 -Visual
pwsh -NoProfile -File Tools/productiontest.ps1 -Visual -StressSeconds 600
pwsh -NoProfile -File Tools/productionverify.ps1 -Build
python Tools/check-pilot.py
```

The final checker reports technical acceptance separately from human acceptance. The recorded user response matches this runtime and permits phase nine. Run this historical candidate checker before changing its runtime inputs; later phase-nine changes need their own verification and must not rewrite the phase-eight checkpoint.
