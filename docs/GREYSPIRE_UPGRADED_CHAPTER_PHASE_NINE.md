# Greyspire — phase nine: upgraded first chapter

16 October 2026 · `D:/game` · G8

**Phase nine has begun; its full chapter gate is open.** Phase eight was closed first: 1,050 checks, the 618.8-second sustained fixture, a successful Windows build, the user's reported pilot playtest, and a recovery checkpoint with 581 verified files / 580 live comparisons. [Phase-eight acceptance](../Reports/phase-eight-acceptance.json), [human assessment](../Reports/phase-eight-human-playtest.json), and checkpoint verification preserve that decision. The checkpoint is `Backups/phase-eight-complete-20261016-090327`.

## Chapter contract and work order

Master-plan sections 17, 18 and 39 require town/training and floors 1–5, a Guardian payoff, the full P1 backlog, and a fresh-player review. The accepted floor-3 pilot is a prerequisite. It is not proof that every first-chapter requirement is complete.

| Package | Concrete artifact and acceptance | Status in this phase |
|---|---|---|
| W01/W02 — floor-2 route | Upper bridge, lower crossing, reconnecting persistent service gate; real baseline traversal and all navigation profiles | Technical package accepted: 33 new checks plus all 1,050 prior checks |
| First-player learning path | Town/training through awakening, routes, mastery, rescue and Guardian with readable direction and recovery | Stage-by-stage transcript from the running game; in-tower objectives, town NEXT line, compass directions; 27 checks; fresh-player confirmation is Q01 |
| A01/A02 — directional motion and placement | Reviewed backward/lateral/turn/start/stop assets and measured feet/hands | Directional set authored and measured (ledger), hip yaw with closed-loop torso twist, run stop, foot and pelvis placement, two-handed axe guard; 43 checks; run start intentionally omitted; hands-on review pending |
| C01/C02/C03 — combat chapter | Three weapon families, coordinated mixed encounters and Guardian mechanic/phase/retry/payoff | C01 technical package and presentation pass verified (113 weapon checks, all 1,083 prior checks green); C02 coordinated roles (53 checks) and C03 Gatekeeper (62 checks) verified; hands-on reviews pending |
| N01/N02 — consequence and companion | Floor-4 branch, later visible response, tested follow/hold/assist/solo | Both branches have immediate and later effects; Mop takes four tested orders (34 checks); hands-on review pending |
| V01/S01 — presentation | First-zone visual/material guide and coherent pass, readable mixed-fight cues | Environment pass and mixed-fight audio verified (36 checks, paired captures); human presentation and listening review pending |
| U01/U02/U03 — information and access | Contextual HUD/journal, actual rebinding and complete controller/onboarding/comfort journey | Rebinding (13 actions, keyboard and controller), hold/toggle guard and sprint, disclosed timing assist, fight-focus HUD, journal tab; 73 checks; hands-on and fresh-player review pending |
| Q01 — external playtest | Fresh-player observations across the playable first chapter and prioritized fixes | Kit, session recording and findings report ready and proven on a synthetic session; observations and the fixes from them await real testers |

Work proceeds in playable packages. Completing a route package does not close G8 or start expansion / phase ten.

## First package: floor 2, The Millrace

The upper approach climbs two 20-metre ramps to a bridge 5.4 metres above the lower crossing. Its ten-metre deck is collision-backed and has parapets. The lower trail passes beneath the deck with 4.9 metres of vertical clearance, connects both banks through a service gate, and offers the existing healing spring. A route board explains both approaches. Lower-lane packs retain the existing encounter budget; all monsters must still be defeated before the north stairs open.

The west winch opens a campaign-scoped permanent gate receipt. The receipt commits before collision and navigation change. Interrupted writes retain the previous state, repeated callbacks have no reward, and callbacks from an old campaign cannot mutate a new journey. `ChapterProgress` validates its additive `chapterVersion=1` policy while retaining save schema 2 and the thirty existing floor identities. Unsupported future policy is rejected before runtime writes. Restore the phase-eight checkpoint deliberately to downgrade; older code does not implement this geometry or chapter gate policy.

Both existing floor-2 chest IDs, slots 0 and 1, keep their **10 carried Sparks each** and their one-time receipts. One is beside the upper bridge, the other beside the lower bank. Existing opened receipts remain authoritative; a rebuild cannot refill either chest. The spring still restores two hearts once per visit. No service-gate interaction grants XP, mastery, currency or a floor clear.

Runtime sources: [geometry and interactions](../Assets/Scripts/GameChapter.cs), [persistent receipt](../Assets/Scripts/ChapterProgress.cs), [floor dispatch](../Assets/Scripts/GameFloors.cs), [load validation](../Assets/Scripts/JourneyStore.cs) and [HUD](../Assets/Scripts/GameUI.cs). Version tags are `greyspire-g8-w01-v1` / `legacy30-chapter-v3`; random-stream policy and existing floor seeds remain unchanged.

## Second package: C01, three weapon families

Before this package, a hero's weapon changed only stat multipliers. Bram's axe and Nix's blade used the sword's combo timings, reach and arc, and they could block, parry and shield-bash without a shield. The [weapon family definitions](../Assets/Scripts/WeaponFamily.cs) now hold every number a light swing uses: tempo, contact fraction, damage, reach, hit arc, lunge, push, stagger, guard break, thrust lane, dodge-cancel window, guard style, parry window, flank bonus, and feedback (swing pitch, cut colour, hitstop, shake). The hit test, the drawn cut and the cancel window all read the same step, so they cannot disagree.

| Family (hero) | Spacing | Commitment | Guard | Feel |
|---|---|---|---|---|
| Sword and shield (Pip, Cedric) | 2.5 m reach, 169° arc, 3.2 m spin finisher | Dodge cancel at 55% | Held guard, 0.25 s parry, shield bash | Unchanged from before C01 |
| Great axe (Bram) | 2.6 m reach, 220° arc: holds off a ring of five | Slower blows (0.42 s vs 0.34 s); dodge cancel only at 72% | Haft: 0.16 s parry, gives ground, no bash; third blow breaks guards | Lower pitch, longest hitstop and shake, camera kick on the guard-breaker |
| Light blade (Nix) | 2.1 m reach, 110° arc; finisher is a 3.1 m thrust lane | Quicker blows (0.28 s); can feint out at 32%, before contact, forfeiting the hit | No held guard: a 0.35 s deflection with a 0.2 s parry, one per press, no bash | ×1.6 "Weak point" damage from outside the enemy's front; higher pitch, lightest hitstop |

The sword-and-shield tables are the exact pre-C01 values, so Pip and Cedric play as before (only the drawn cut widens from 150° to the 169° it already hit with). Sol keeps her original tables as "bare hands and focus"; she is outside C01's three families. Bram's tune is now ×1.25 damage and +1 heart, with tempo and reach moved into the family. Nix keeps her crit, speed, dash and heart traits, with damage ×0.85 and no attack-speed multiplier. Front-on combo DPS at level 12 is sword 36.5, axe 43.7 and blade 37.7; the blade's flank bonus adds to that. The hero card text describes each family's trade-off. Runtime sources: [attack and strike](../Assets/Scripts/PlayerController.cs), [cancel and bash gating](../Assets/Scripts/PlayerFeel.cs), [guard styles](../Assets/Scripts/PlayerMoves.cs) and [heroes](../Assets/Scripts/Heroes.cs). Saves are unaffected: hero IDs, ownership and the selected hero are unchanged.

[WeaponFamilyTest](../Assets/Editor/WeaponFamilyTest.cs) and [its isolated runner](../Tools/weaponfamilytest.ps1) drive the real Strike, StartAttack/TickAttack, buffered-dodge, guard and contact code. They passed **85 checks**: exact reach edges, arcs at 50°/75°/105°, crowd coverage (axe 5, sword 3, blade 3 of a ring of five), spin versus thrust finishers, guard break only on the axe's third blow, flank damage (14.5 versus 8.4 front) and frontal guards bypassed from behind, tempo, every family's cancel window with no late hit after a cancel, parry windows, bash gating, held versus deflect guards, distinct feedback, clip-fit budgets, and the DPS band. Per-step data is in [weapon-families.csv](../Reports/weapon-families.csv). The guard-hold rule was extracted into `TickGuard(dt, held)` because the batch-mode editor does not deliver queued key state to `GameInput`. The test exercises it with the button held, and the release path through the real input read. Overhead diagnostic captures (`Logs/weapon-*.png`) are synchronous and T-posed. They prove that the cut is drawn, not how it animates.

Regression against the C01 code, run on an isolated mirror of the project (`D:/gs-mirror`; the user had the editor open on `D:/game`): save 103, contact 65, feel 29, terrain 58, growth 194, objective 120, feedback 178, pilot 102, chapter route 33 and production 201 checks passed (the production run included the 600-second soak and its day-boundary check), and the story (both rescue branches and skip) and navigation scenarios completed. The first mirror pass failed five suites on an editor-search `ArgumentOutOfRangeException` from `UnityEditor.Search.SearchDatabase`, caused by a mirror missing `UserSettings/Search.index`. After that file was copied, all five passed. The mirror's Windows build succeeded: 382 MB, 0 errors, 0 warnings. GrowthTest now computes combo DPS from each hero's family steps instead of the removed static table. The earlier 446-input fingerprint no longer applies, because combat sources changed. A fresh fingerprint and checkpoint belong to the package's acceptance run.

### C01 presentation pass: making the families visible

The user's review of the first C01 build was that the weapons were "lacking". Animated captures from the new [WeaponShowcase](../Assets/Editor/WeaponShowcase.cs) ([runner](../Tools/weaponshowcase.ps1)) confirmed why. Bram swung a small one-handed hatchet with the sword's moves; Nix was Pip without a shield, with the same sword and the same three animations. The rules differed, but nothing on screen showed it. Two measurements then found faults that affected every hero:

- **Hits landed before the blade.** A frame-by-frame probe of each attack clip found the frame where the weapon's striking point moves fastest ([weapon-clip-probe.csv](../Reports/weapon-clip-probe.csv)). Pip's first blow registered at 0.204 s into a clip whose cut peaks at 0.338 s.
- **Weapons cut flat-first.** Averaged around contact, the sword's edge met its target at a dot product of 0.03 (flat-on) on the second blow and −0.11 on the third. A symmetric sword hid this; an axe or single-edged blade made it obvious.

What changed:

| Area | Change |
|---|---|
| Props | [Tools/build_weapons.py](../Tools/build_weapons.py) builds three new props in Blender, in the kit's conventions (grip at origin, length +Z, edge +X) and UV-mapped onto the kit's own props trim sheet, so they share its paint and normal map: a 1.29 m bearded **Greataxe_Iron**, a 0.71 m **Seax_Steel**, and a 0.41 m **Dagger_Steel** carried in Nix's left hand. Source: `Blender/Weapons.blend`. |
| Clips | Bram: diagonal sweep (`Sword_Attack`), downward chop (`TreeChopping_Loop`), full spin (`Sword_Regular_C`), so his spin finisher now plays a spin. Nix: quick backhand hook (`Melee_Hook`), forehand cut, lunge. The axe's chop is narrower (minDot 0.2), lands early in a long recovery, and staggers for 0.5 s. |
| Timing | `HeroDef.comboContact` times each hit to its clip's measured peak, for all heroes. Pip's first blow now lands with the blade (its duration grows from 0.34 s to 0.352 s; every other sword number is unchanged). |
| Edge | `HeroDef.comboTwist` turns the weapon about its length during each wind-up, at 1,100°/s, back to rest after the blow. Measured over the 120 ms around contact ([weapon-edge-alignment.csv](../Reports/weapon-edge-alignment.csv)), every slashing blow of all three heroes now meets its target edge-first at a dot product of 1.00, identical across repeated runs. |
| Carry | A weapon longer than 1 m is slung across the back, head behind the right shoulder. A second blade hangs at the right hip. |
| Feel | Per-family trails (axe 0.8 m wide and lingering, blade 0.28 m and brief), and layered sound from existing cues: the axe adds a low rush of air and a thud under each hit, and the blade a bright whip. The sword's sound is unchanged. |

WeaponFamilyTest grew to **113 checks** to cover all of this: held props, prop proportions, measured contact inside each clip, the wrist twist reaching each blow before contact and settling afterwards, back and hip carry, and sound and trail identity. Against the final code, on the isolated mirror, every earlier suite passed again: save 103, contact 65, feel 29, terrain 58, growth 194, objective 120, feedback 178, pilot 102, chapter route 33 and production 201 checks (including the 600-second soak), plus story and navigation. The Windows build succeeded at 382 MB with 0 errors. Its single warning is the pre-existing `Post.shader` loop-unroll warning, which the phase-eight report already disclosed for cold shader compiles; this pass changed no shaders. Animated review frames from the gameplay-side and over-the-shoulder views, plus stowed views, are in `Logs/weapons/`.

**Not yet accepted for C01:** your hands-on review of the feel from the gameplay camera, and A01 directional clips. Bram's two-handed weapon is still swung by one-handed clips with the left hand free, because the library has no two-handed set and no hand IK. Hit-arc and reach numbers are tuned by measurement, not by playtest. C02 (coordinated enemy roles) and C03 (the floor-5 Guardian) build on this package next.

## Third package: C02, coordinated enemy roles

Before this package, only the sword duelists shared an attack cap. Slimes, throwers, bombers and brutes each attacked on their own timer, so a mixed pack could strike in the same instant, from behind the camera, with no one waiting their turn and no reason to prioritise any enemy. The [encounter director](../Assets/Scripts/EncounterDirector.cs) now handles every damaging commitment except the Guardian's own move set, which C03 redesigns:

- **Budget:** at most 2–4 weight at once, growing with the tower as the duelists' cap did. A brute's slam and a bomber's blast weigh 2; other attacks weigh 1.
- **Spacing:** 0.3 s minimum between attack starts, so blows do not land together.
- **Out of sight:** at most one commitment at a time from where the camera cannot see. It also winds up 0.25 s longer, snarls from its own direction (a Threat-priority spatial cue), and its edge marker swells and pulses with a "!".
- **Fair turns:** an attacker kept waiting 0.75 s longer than the asker goes first, so slow, heavy roles are not starved by quick ones that keep refilling the slots.

A lit fuse always burns, but it occupies the budget. Training partners and the Guardian are outside the director.

| Role (who) | While waiting | When it commits | Counterplay |
|---|---|---|---|
| Duelist (imps, sword bandits) | Circles the hero, watching | Closes, winds up, strikes, steps out | Read the wind-up, punish the recovery |
| Shield carrier (shield bandits) | Holds the lane between the hero and the nearest thrower or caller | Ordinary strikes; three frontal light blows blocked within 5 s bring a telegraphed 0.5 s counter-bash (6 s recovery) | Heavy blow (breaks the guard, never feeds the counter), flank, displace |
| Skirmisher (slimes) | Works round to the flank at about 3 m | Crouch, then pounce | Turn and intercept |
| Artillery (throwers) | Keeps its distance and strafes | Needs a clear line of sight and its turn; the shot in flight holds the turn for 0.5 s | Cover, close the distance |
| Caller (a waking pack of three or more promotes its first thrower, by stable spawn identity) | As artillery | Every 9 s a 1.2 s signal, gold ring and "CALLING" label; on completion, allies within 10 m are rallied for 5 s (×1.2 speed, ×1.5 attack recovery) | Hit it during the signal: "Signal broken", nobody rallied, longer wait before retrying |
| Heavy (brutes) | Holds the ground about 4 m out, a step outside its reach | Telegraphed slam (weight 2) | Dodge direction and commitment |
| Sapper (bombers) | – | Fuse always lights | Kill it first or get clear |

Runtime sources: [roles, caller and shield carrier](../Assets/Scripts/EnemyRoles.cs), [behaviours](../Assets/Scripts/Enemy.cs), [duelists](../Assets/Scripts/EnemyDuel.cs), [HUD cues](../Assets/Scripts/GameUI.cs). The adaptation is bounded and telegraphed: no input reading. Saves are unaffected.

[EncounterRoleTest](../Assets/Editor/EncounterRoleTest.cs) ([runner](../Tools/encounterroletest.ps1)) passed **53 checks**. The synchronous half covers:
- role assignment for every kind and the weights;
- budget, spacing, release and forced commitments;
- the unseen limit and the longer unseen wind-up (0.65 s against 0.4 s);
- an uncoordinated control;
- caller selection, rally radius, expiry and interruption;
- the shield carrier's three-block counter, its cooldown, window and heavy-blow exemption, and its screening post.

The second half runs the same 30-second fight of eight (two imps, a shield bandit, two slimes, two throwers, a brute) through the enemies' own Update, against a hero who never moves, twice: coordinated and uncoordinated ([summary](../Reports/encounter-summary.csv), [frame log](../Reports/encounter-fights.csv)).

| 30 s mixed fight (final verification run) | Coordinated | Uncoordinated |
|---|---|---|
| Peak simultaneous commitment (weight) | 2 (the budget) | 9 |
| Commitments from out of sight at once | 1 | – |
| Hits landing within 0.1 s of each other | 3 | 21 |
| Worst single second | 3 hits (bound: 2 per budget slot) | 7 |
| Fighters who never got a turn | 0 | 0 |
| Hits on a motionless hero | 38 | 97 |
| Waiting enemies' average movement | 1.50 m/s | 1.20 m/s |

Frame timing varies from run to run, so the exact counts do too. Across the four runs of the final code, the coordinated fight always held the budget, never had more than one unseen attacker, and never starved a role. Simultaneous hits ranged 1–3 coordinated against 19–27 uncoordinated, and the worst second 3–4 hits against 6–8.

Review frames at 6/12/18/24 s, from the gameplay view and from above, are in `Logs/encounter-*.png`.

Final verification on the isolated mirror, with the final code: encounter 53, weapon 113, save 103, contact 65, feel 29, terrain 58, growth 194, objective 120, feedback 178, pilot 102, chapter route 33 and production 201 checks passed (including the 600-second soak), plus story and navigation. The Windows build succeeded at 382 MB with 0 errors and 0 warnings.

Two failures in an earlier batch on the same code are disclosed:
- **Terrain chase:** an imp was 3 m short of its 5 m target when the 14-second wall-clock limit expired. It passed in two immediate reruns and in the final batch. That scenario's imp follows an unchanged duelist path with attacks disabled, so this reads as load-timing variance.
- **Soak day boundary:** the ten-minute soak did not cross the world-day boundary. ProductionTest now logs the world hour at the soak's start and end. The two later runs started at 08:45 and ended at 02:13 and 03:03 the next day, margins of about 55 s and 75 s of world time. The soak runs in town with no enemies, so the director cannot affect it, but the check remains timing-sensitive on battery.

**Not yet accepted for C02:** a hands-on playtest of mixed fights on floors 1–5 and in the Wilds. Budget and timing values are measured, not tuned with players. The Guardian's coordination belongs to C03.

## Fourth package: C03, the first Guardian

Before this package, floor 5's Guardian, Grol the Gatekeeper, was an elite brute with a name: the same slam and AI as every brute, at the foot of the stairs. Floor 4's rescue offered "guardian intel" that only changed one line Rhea said in town. Master plan sections 7 and 17 ask for a distinct mechanic, an arena, phases, a retry path, a meaningful boon and a town aftermath.

**The arena** ([GameGuardian.cs](../Assets/Scripts/GameGuardian.cs)). An iron-railed ring 11 m in radius stands before the stairs, built from the village kit's fences, stone pillars and torches. Its paved floor marks it as a place apart, and floor 5's props, packs, chests and spring are kept out of it. It has a south gate the hero enters by and a north gate to the stairs, both sealed in red, and four stone pillars inside. Grol keeps the gate shut, and takes no damage, while any other monster on the floor lives. When the floor goes quiet, the south gate opens; walking in shuts it behind the hero and wakes him.

**His move language** ([EnemyGuardian.cs](../Assets/Scripts/EnemyGuardian.cs)): three moves, each telegraphed for at least 0.8 s.

| Move | Telegraph | Counter |
|---|---|---|
| Hammerfall | 0.8 s red disc in front of him | Step aside |
| Gate Sweep | 1.0 s red ring 4.8 m all round him | Get out, or dodge through as it lands |
| Bar Rush (phase 2 on) | 1.0 s red lane; he settles his aim, then cannot turn | Put a pillar behind you and step aside: the pillar shatters and he is stunned and open for 2.5 s. Into the bars he is stunned only 1 s. A hero left in the lane takes one heavy blow. |

At 60% and 30% health he roars for 1.2 s: no damage gets through, and the phase changes. Phase 2 adds the charge and points out the pillars. Phase 3 shortens his recoveries, adds shockwaves to the slam, and turns a charge that meets the bars straight into a second telegraphed charge, so only stone stops him. Four pillars are a finite resource. Grol is 30% larger than a floor brute and sits outside the encounter director as the Guardian role.

**Floor 4 pays off.** Choosing "guardian intel" when freeing Rhea now gives the charge's weakness ("once it charges, it cannot turn. Put stone at your back"). At the seal, her warning appears and every pillar is marked from the start. Without the intel, the hint only comes with phase 2. After Grol falls, Rhea acknowledges that her warning helped.

**Retry.** When floor 5's packs fall with Grol standing, a campaign-scoped checkpoint receipt commits. It is in [ChapterProgress](../Assets/Scripts/ChapterProgress.cs), uses the same transaction, interruption and old-campaign rules as the floor-2 gate, and records progress, never a reward. A hero who falls to Grol sees "RETRY THE GATEKEEPER". The retry rebuilds floor 5 with only Grol and starts at his open gate: the packs stay beaten and pay nothing again, and the fall still costs half the carried Sparks. Grol's fall spends the checkpoint. Without it, floor 5 starts at its foot as before.

**Boon.** **Gatebreaker** is appended as boon id 14 (ids are the save format; 0–13 are untouched): heavy blows stagger even brutes and Guardians open, and ripostes deal 30% more. It is never rolled. Grol's offer always leads with it while the hero lacks it, and a paid reroll keeps it on offer.

**Aftermath.** Floor 5's clear now narrates the Gatekeeper's fall and his twisted gate. In Brushwick, his bars hang by the tower door between two torches, and Gate Sergeant Hale, who has watched that door for forty years, has something to say. He notices Gatebreaker if the hero took it, and Rhea's part if she gave the intel.

[GuardianTest](../Assets/Editor/GuardianTest.cs) ([runner](../Tools/guardiantest.ps1)) passed **63 checks** against the real floor-5 build:
- **Receipts:** interruption, reload, replay, old campaign, and unsupported values rejected with their bytes retained.
- **Boon:** id append, never rolled (40 rolls), Grol's offer, reroll and ownership.
- **Arena:** shut gate, no monsters inside, dormant immunity, opening only when the floor is quiet, the seal, and the intel and no-intel openings.
- **Moves:** each one stepped deterministically through his real behaviour, including nothing landing during wind-ups; hammer and sweep hitting inside their telegraphs and missing outside; no charge in phase 1; the immune phase roar; a charge into a pillar (shattered, stunned and open, 22 damage from a 10-point riposte, the hero behind it untouched); into the bars (1 s stun); through the hero (one heavy blow); and phase 3's chained charge.
- **After the fight:** defeat, gates, clear, offer and lines; the retry path; Gatebreaker against a brute; the town aftermath (the bars present in the rebuilt Brushwick) and Rhea's later lines; and no logged errors.

Review frames: `Logs/guardian-arena-over.png`, `Logs/guardian-arena-gate.png`, `Logs/guardian-rush.png`, `Logs/guardian-pillar.png`, `Logs/guardian-town-trophy.png`.

The first full batch against this package found two regressions; both are fixed:
- **Growth:** `GrowthPolicy.Build` indexed the boon array by id, and the growth test's older 14-entry loadouts had no slot for id 14. `Build` now treats a missing entry as untaken, which is correct for any record from before a boon existed. GrowthTest's loadouts also set every boon id explicitly.
- **Controller chapter:** the controller-chapter journey (inside the feedback suite) shortcuts each floor by damaging every monster. Grol's dormant immunity is deliberate, so on floor 5 the shortcut now goes through the real arena flow (the quiet floor opens the gate; walking in seals it and wakes him) before the same shortcut applies.

Final verification on the isolated mirror, with the final code: Guardian 62 (63 with the later town capture), encounter 53, weapon 113, save 103, contact 65, feel 29, terrain 58, growth 194, objective 120, feedback 178, pilot 102, chapter route 33 and production 201 checks passed (the soak crossed the world day with about 2 h to spare), plus story and navigation. The Windows build succeeded at 382 MB with 0 errors and 0 warnings.

**Not yet accepted for C03:** a hands-on fight against Grol, both with and without Rhea's intel, and tuning of his health and stun windows from that play.

## Fifth package: N01/N02, the floor-4 branch and the companion

**N01, the branch.** Every freed Painter is asked to GO HOME or WHAT DID YOU SEE? Before this package, "What did you see" had a later effect (the Grol fight, from C03) but no immediate one, and "Go home" had no gameplay effect at all. Both now have both ([GameBranch.cs](../Assets/Scripts/GameBranch.cs)):

| Choice | At once (in the tower) | Later (Rhea, floor 4) |
|---|---|---|
| Go home | She presses her last draught on the hero: +2 hearts, never above full | Back in Brushwick, House Vane sends for the hero; Lady Vane thanks them and pays 30 banked Sparks, once, through a durable receipt (`consequence:rescue:rhea:vane-thanks-paid`). Rhea's own town line remembers. |
| What did you see? | The talking carries: an ambush of two imps and a thrower comes into view, 6–11 m ahead | Grol's fight opens with her warning and the pillars marked (C03). Lady Vane is cool with the hero and pays nothing; she says so once. Rhea's lines remember. |

The thanks is a story payment in Sparks (like a chest), never XP or a stat. An interrupted payment leaves it due, and a reload cannot pay it twice. The interrupted-rescue path that finishes a rescue in town plays only the later effects: an ambush cannot be staged in Brushwick.

**N02, the companion.** Mop the slime now takes orders ([Companion.cs](../Assets/Scripts/Companion.cs)). They are given with **H** or the **left stick click** (a new control action appended after all existing ones), announced with a description, shown on the HUD beside the purse, and kept in the journey (`companion:order`; an unknown value falls back to Follow without rewriting the save).

| Order | Behaviour |
|---|---|
| Follow (default, as before) | At heel; goes for the nearest threat within 11 m of the hero |
| Assist | Goes only for the hero's locked target, or whatever the hero struck in the last 4 s; with neither, waits at heel. His bump (every 1.6 s) makes ordinary enemies flinch 0.3 s, opening them for a riposte; brutes and Guardians shrug it off |
| Hold | Guards the spot where he was ordered, never more than 4 m from it, and bites only what comes within 3.5 m. On a new floor he holds where he stands |
| Solo | Keeps about 4 m behind the hero and never fights |

Testing found a long-standing bug in Mop's movement fallback (used whenever no navigation field is ready). It closed exactly to his stopping distance, landed a hair outside it, and crept forward forever without biting. He now strikes within a 0.2 m reach tolerance.

[BranchCompanionTest](../Assets/Editor/BranchCompanionTest.cs) ([runner](../Tools/branchcompaniontest.ps1)) passed **34 checks**:
- **Branch effects:** the draught, its cap, and its absence on the other branch; the ambush's make-up, distance and readiness.
- **The thanks:** due only on Go home; interruption; payment; no repeat after reload; no level or floor granted.
- **Lady Vane in conversation:** the real greeting pays after her lines and then opens her board, while the cool greeting plays once and pays nothing. Rhea's town lines differ by branch.
- **Orders:** cycling, persistence, the unknown-value fallback, and the control and its descriptions.
- **Scenes through Mop's real Update:** Follow takes the nearest threat; Assist takes the locked target over a nearer enemy and makes it flinch open, waits with no target, and takes the last-struck enemy without a lock; Hold keeps its post with the hero 14 m away and bites what comes near; Solo never fights and keeps back; a new floor re-anchors Hold.

The branch effects are verified by calling the aftermath code directly. In the live rescue cutscene, the story scenario still completes both branches and the skip; it does not log the ambush, so its in-scene spawn is not separately evidenced.

Final verification on the isolated mirror: branch 34, Guardian 63, encounter 53, weapon 113, save 103, contact 65, feel 29, terrain 58, growth 194, objective 120, feedback 178, pilot 102, chapter route 33 and production 201 checks passed (the soak crossed the world day with about 3 h to spare), plus story and navigation. The Windows build succeeded at 382 MB with 0 errors and 0 warnings.

**Not yet accepted for N01/N02:** a hands-on play of both floor-4 answers through to Brushwick, and of Mop's orders in a real fight with the controller.

## Sixth package: V01/S01, the first-zone environment pass and mixed-fight audio

**V01.** [The art guide](GREYSPIRE_FIRST_ZONE_ART_GUIDE.md) named the route prototypes' bare slabs, the near-black Millrace underpass, the featureless survey-ruin boxes, the unframed rescue, and services that existed only as floating labels. [GameDress.cs](../Assets/Scripts/GameDress.cs) dresses them in the village kit's own stone, paving and timber, **without moving a single proven surface**. Every existing collider stays as it was. Only what is drawn changes: tiled-UV meshes with tangents, so the kit's normal maps light them, owned by the floor. Details sit clear of the walking lines.

| Place | Change |
|---|---|
| Floor 2, Millrace | Paved deck and ramps with stone sides; brick parapets and sluice piers; a planked service gate; four stone piers carrying the deck corners, clear of the 6 m crossing lane; four shadowless lanterns on the sluice piers that light the underpass (the guide asks for fill before shadow-casting lights); a lantern at the route board; a chain coil and barrel at the winch; worn stone where the ramps meet the meadow |
| Floor 3, survey ruin | Both wings in kit stone, crowned with 72 non-solid merlons, with fallen stone at their feet; a paved overlook and ramps; a planked cross-pass gate; a lean-to and lantern as the courier's rest point, clear of the courier and the chest |
| Floor 4 (every rescue floor) | Stepping stones from the trail to the drained Painter, a broken ring of standing rocks that opens toward the trail and the story camera, and a pale light pool |
| Brushwick | The Field desk (mastery, practice, courier reports) and the Guild's commission desk become real furniture: tables with ledgers, scrolls and candles, banners, and a lamp post |

[EnvironmentShowcase](../Assets/Editor/EnvironmentShowcase.cs) ([runner](../Tools/environmentshowcase.ps1)) captures fixed cameras on floors 1–5 and in town, with the same seed, the same 10:30 lighting, fog on and reduced effects: `Logs/env/baseline` before the pass, `Logs/env/candidate` after, paired in `Logs/env/v01_before_after.png` by [Tools/env_pairs.py](../Tools/env_pairs.py). Floor 1 and the floor-5 arena were reviewed and left as they were. Detail props use fixed orientations, so the floors' seeded layouts (chests included) are unchanged.

The route test caught a real leak during this pass. An opened shortcut gate is already inactive when it is dressed, and Unity never sends OnDestroy to an object that was never active, so its mesh outlived the floor: one mesh per rebuild. Such meshes are now owned by the floor, and the environment test rebuilds with both shortcuts open to prove ownership stays exact.

**S01.** The audio already reserved four threat voices that lesser sounds cannot evict. What was missing:

- **Contact tells.** Most enemy wind-ups were silent. Every commitment now sounds a "tell": a Threat-priority, spatial cue from where the blow is coming, pitched by size (slime 1.3 down to Grol 0.55). The takes are Kenney swing recordings reversed, lowered and loudness-normalised by [Tools/build_audio.ps1](../Tools/build_audio.ps1).
- **Movement.** Brutes, and Grol louder and lower, are heard walking in.
- **Ambience.** A 24 s seamless wind bed is synthesised from filtered noise (no generative AI). It plays under the score on the floors, softer in town, ducks to 55% in a fight and further under a threat, and follows the quiet mix ([Ambience.cs](../Assets/Scripts/Ambience.cs)).
- **Mix.** While a threat is sounding, lesser world sounds play at 70%, so the warning carries.

The audit now covers 34 cues plus the bed. Credits record both derivations.

[EnvironmentAudioTest](../Assets/Editor/EnvironmentAudioTest.cs) ([runner](../Tools/environmentaudiotest.ps1)) passed **36 checks**:
- **Proven geometry:** the deck, ramps, parapets, piers and gates keep their colliders and dimensions while being drawn in kit surfaces; the corner piers stay clear of the lane; the underpass lanterns are shadowless and above head height.
- **Ruin and rescue floors:** the ruin keeps its bulk, with non-solid battlements; the rest point blocks neither the chest nor the courier; the statue ring opens toward the trail.
- **Ownership:** generated meshes stay exactly bounded across rebuilds with shortcuts open (floor 2 45/45, floor 3 110/110).
- **Audio:** the tell's catalogue entry, the bed and its ducking rules; under 4 s of a mixed-fight barrage, every tell got a voice (17 of 17); contact steps back to 70% under a threat.
- **A real fight:** every blow that landed was heard winding up first, the brute was heard walking in, and the bed sat ducked.

Final verification on the isolated mirror (on mains power), run by the new [Tools/verify-phase-nine.ps1](../Tools/verify-phase-nine.ps1): environment/audio 36, branch 34, Guardian 63, encounter 53, weapon 113, save 103, contact 65, feel 29, terrain 58, growth 194, feedback 179, pilot 102, chapter route 33 and production 201 checks passed (the soak crossed the world day with about 2 h to spare, median frame 12.3 ms), plus story and navigation. Objective passed 120 on an immediate rerun: in the batch, its only failing check was a screenshot file missing while the laptop display slept. The Windows build succeeded at 382 MB with 0 errors. Its one warning is the pre-existing `Post.shader` cold-compile warning; no shader changed.

An earlier batch run on battery failed the feel gait, terrain chase and production keyboard-movement checks. All passed on mains, which matches the frame-timing sensitivity already recorded for these suites.

**Not yet accepted for V01/S01:** a human chapter review of the dressed floors and town, and a listening review of the tells, footfalls and bed in a real fight.

## Seventh package: U01–U03, information, controls and access

**U02, rebinding.** [Bindings.cs](../Assets/Scripts/Bindings.cs) holds the player's own controls: 13 actions, each with a keyboard key and a controller button. [GameInput](../Assets/Scripts/GameInput.cs) reads them, and so does [ControlGuide](../Assets/Scripts/ControlGuide.cs), so every prompt, the help list, the HUD's slot keys and the tutorial name the real binding.
- With default bindings, all 19 prompts read exactly as before, on keyboard and on controller.
- Some controls stay fixed, so no binding can lock the player out: movement, look, the menus, pause, target switching (Z / V and the d-pad's sides), dialogue, the mouse buttons, and Ctrl and I.
- Choosing a cell on the new **Controls** settings page listens for the next key or button. Esc or Start cancels it, and a silent cell gives up after 6 s.
- A key that is already in use swaps with the old binding, so nothing is ever left unbound. The page says what moved.
- The press that is captured never also confirms a menu button, goes back or pauses. A controller pulled mid-capture cancels cleanly.
- Bindings are stored in PlayerPrefs. A stored table that doubles a key, or holds a reserved or unknown one, falls back to the defaults on load.

**U03, access and onboarding.** A new **Access** page offers four options:
- **Guard:** hold or toggle. With toggle, one press raises the guard and the next lowers it. A dodge or a spent deflect drops it, so Nix's blade never sticks up.
- **Sprint:** hold or toggle. With toggle, a dash in a fight carries into a sprint until the hero stops. In town, one press runs and another walks.
- **Timing assist:** widens the parry window by 60% and holds a pressed move for 0.35 s instead of 0.2 s. Nothing else changes: not damage, not rewards, not enemies. It is disclosed on the page, by an ASSIST tag beside health and in the journal.
- **Fight focus HUD.**

The page also has a button that replays the control tips. The first climb's tips now describe whichever hold or toggle is active, and end with a pointer to Controls and Access. The pause screen's move list follows the toggles too.

**U01, contextual HUD and journal.**
- **Fight focus.** While a fight is on, the commission, daily and clock panels fade to 20% and return gradually afterwards. Health, enemy markers, the tells' unseen-attacker warning and prompts never fade. It is on by default and can be turned off.
- **Journal.** A new pause tab, [GameJournal.cs](../Assets/Scripts/GameJournal.cs), reads only the records the game already keeps, so it cannot disagree with the world. It shows:
  - the current objective;
  - the commission and its progress, with the number fulfilled;
  - the courier's outcome, with the supplies and whether it was reported;
  - each rescue's choice and both of its effects, including Lady Vane's thanks, due or paid;
  - the floor-5 checkpoint and Rhea's intel, or the fallen Gatekeeper;
  - Mop's standing order and the key that changes it;
  - whether timing assist is on.

[AccessControlsTest](../Assets/Editor/AccessControlsTest.cs) ([runner](../Tools/accesscontrolstest.ps1)) passed **73 checks**, using real keyboard and controller state events through the real settings page and the hero's own combat input:
- **Table:** defaults; identical prompts; refusal of reserved keys and buttons; swaps; persistence; recovery from doubled, reserved and unknown stored values; reset.
- **Capture:** the Controls tab reached by controller focus; capture and cancel; a reserved key refused with its reason; the captured press consumed (including binding the back button); a controller pulled mid-capture.
- **Hero:** hold and toggle guard on the shield and on Nix's deflect, with the dodge release; toggle sprint in town and in a fight.
- **Timing assist:** a block raised 0.3 s early is a plain block without the assist and a parry with it, while 0.45 s stays a block; the hero's buffer follows the setting.
- **Tips, focus and journal:** the onboarding tips; the focus fade's level, gradual return and off switch; the journal across a young, mid and late journey.

Captures are in `Logs/access/`: Controls before and after rebinding, Access, the tip with the ASSIST tag, fight focus and the journal.

Full verification on the isolated mirror (on mains power, display kept awake) ran [Tools/verify-phase-nine.ps1](../Tools/verify-phase-nine.ps1), which now includes this suite. These suites passed:

| Suite | Checks |
|---|---|
| Access | 73 |
| Environment and audio | 36 |
| Branch | 34 |
| Guardian | 63 |
| Encounter | 53 |
| Weapon | 113 |
| Save | 103 |
| Contact | 65 |
| Feel | 29 |
| Growth | 194 |
| Objective | 120 |
| Feedback and controller | 179 |
| Pilot | 102 |
| Chapter route | 33 |
| Production, including the soak | 201 |

Story and navigation also passed. The terrain suite failed once in the batch, on one check: a slime stacked on another enemy's capsule ended at y −11 instead of returning to its own ground. This package does not touch enemy physics, and the suite passed all 58 checks when rerun alone at once. The failure is recorded as an intermittent enemy-separation case to watch, not hidden. The Windows build succeeded at 383 MB with 0 errors. Its one warning is the known `Post.shader` cold-compile warning.

**Not yet accepted for U01–U03:** a hands-on controller and keyboard review, and a fresh player's onboarding pass with assist and comfort settings, as Q01 requires.

## Eighth package: A01/A02, directional movement, foot and hand placement

**A01, the directional gait set.** The humanoid library has one forward walk, one run and a sprint, nothing else. The hero used to face a locked target and slide sideways on a forward run, and backed off on the walk played in reverse. [GaitAuthoring](../Assets/Editor/GaitAuthoring.cs) ([runner](../Tools/author_gaits.ps1)) authors the missing clips from the library's own muscle curves. That is keyframe processing on the real body rig, with no generative model. Every clip is measured: its pace on the ground, how far a planted foot strays over a support (drift), how close the feet come, and whether the ball of the foot reaches the true floor. The results are written to the [gait ledger](../Assets/Resources/Humans/gait_ledger.json) that the master plan asks for (section 28).

| Clip | How it is made | Pace (m/s) | Drift | Feet apart | Floor gap |
|---|---|---|---|---|---|
| Walk / run / sprint (library) | as shipped | 0.97 / 6.35 / 8.06 | 11 / 5 / 11 cm | 21–26 cm | 0 to -1 cm |
| Backstep, back run | cycles played in reverse, arm swing 70% | 0.97 / 6.41 | 12 / 7 cm | 21–23 cm | 0 cm |
| Easy jog, and its reverse | walk and run cycles on one clock from their left plants, mixed half and half | 3.15 / 3.14 | 9 cm | 21 cm | -3 cm |
| Side step right / left | walk timing and lift; each foot solved by two-bone IK onto a straight sideways path on its own side, the body lowered until the supporting foot reaches the floor, then corrected against playback | 0.57 | 7 / 8 cm | 16 / 19 cm | 0 cm |
| Turn step | walk stepped on the spot | 0 | 12 cm | 21 cm | -3 cm |
| Run stop | the front foot held by IK, sliding back only by the hero's braking distance (0.44 m over 0.14 s), while the trailing leg steps in | — | 0.4 cm after the brake | 35 cm | -1 cm |

Every authored loop is rotated so its left foot plants at phase zero, and the runtime keeps the whole set in step by the measured plants.

At runtime, [CharAnim](../Assets/Scripts/CharAnim.cs) blends forward, back, left and right by the angle of travel:
- **Paces:** the ledger's measured paces replace the old guesses (jog 3.3 m/s, against the 6.35 m/s the clip actually covers).
- **Gait choice:** along each direction it picks the one gait whose playback rate would sit nearest 1, with hysteresis, and fades between them. A pose mix of two strides is not a linear stride, so it never holds a permanent mix.
- **Foot IK:** humanoid foot IK is off for gaits. Mixing a clip that has recorded goals with one that has none dragged the feet 9 cm off the ground.

[PlayerController](../Assets/Scripts/PlayerController.cs) adds three things:
- **Hip yaw:** moving sideways past a walk while locked on (or guarding), the hips turn toward the travel, up to 70°, or 50° running backward. The chest turns back by the same amount, so the hero still faces the target.
- **Turning on the spot:** steps round with the turn step.
- **The stop:** letting go of a run plays the authored stop, starting with the brake so their timings match. A fresh input cuts it short.

The locked-on backstep is now capped at what the authored backstep can carry with its feet planted: its pace times the top playback rate, about 1.5 m/s. It was 1.9 m/s against a reversed walk, and the feel suite's range (1 to 2.05 m/s) still holds.

**A02, placement.** [LimbPlacement](../Assets/Scripts/LimbPlacement.cs) runs after the animation is evaluated:
- **Feet:** a foot the clip has planted (low and still in the world) is put with the ball of the foot on the ground under it. A lifted foot keeps its lift above its own ground.
  - The pelvis answers only to the ground's shape, so level ground leaves the stance as made.
  - The sole tilts to the surface (capped at 30°), and the leg is solved again so the tilt does not lift the toe.
  - The root's height over level ground is learned there, because a capsule on a slope rides higher over the ground straight beneath it.
- **Torso:** the counter-twist is spread over four spine joints, then closed on the shoulder line, keeping the clip's own torso movement.
- **The great axe:** the library has no two-handed guard. Bram's haft guard used the shield block, so his empty left forearm came up while the axe hung from his right hand. The guard is now posed: the right hand brings the haft diagonally across the chest and the left hand closes on it, riding the grippable length of the haft. The same stance holds while he is squared up to a threat. His swings stay the clips' own one-handed swings, and the off hand is not dragged after the haft, because it cannot reach it through them.

[AnimationTest](../Assets/Editor/AnimationTest.cs) ([runner](../Tools/animationtest.ps1)) passed **43 checks** with the real hero and real input. Each scenario ran with the directional set off (the old behaviour) and on. Slip is the median world speed of a planted ball of the foot.

| Scenario | Before | After |
|---|---|---|
| Locked-on side step right, 4.9 m/s | slip 6.88 m/s (140% of speed) | 1.75 m/s (36%); hips 70°, shoulder line within 0.03° of its aim |
| Locked-on side step left, 4.9 m/s | 6.87 m/s (140%) | 1.91 m/s (39%) |
| Guard shuffle sideways, 2.2 m/s | 2.71 m/s (123%) | 1.04 m/s (47%) |
| Backstep, 1.5 m/s | 0.37 m/s | 0.36 m/s: the same kinematics (the authored clip is the reversed walk); the gain is the honest speed cap |
| Forward run, 6.8 m/s | 0.44 m/s | 0.37 m/s |
| Run stop | — | median slip 0.13 m/s through the stop; a new input cuts it short |
| Slope 18°, sole error left / right | 12.2 / 4.2 cm | 0 / 0 cm |
| Stair step 18 cm, sole error | 21.3 / 2.5 cm | 0.7 / 0 cm |
| Great axe, haft guard and squared-up advance | left hand 64 cm off the haft | on the haft (within 3 cm) in 100% of frames |

Also checked: turning on the spot steps round and lets go when still, and on flat ground the standing soles sit on the ground with the pelvis still. The evidence sheets are before and after and the gait set as the hero plays it (from [Tools/anim_sheet.py](../Tools/anim_sheet.py)), and the raw numbers are in `Logs/anim/results.csv`.

**Not solved, and said plainly:**
- **Running side-steps still slip.** The side step reaches 0.57 m/s at its natural rate. Faster sideways movement is carried by hip yaw plus the side step, which cuts slip by about three quarters, not to zero. A dedicated side-run clip would need new motion capture or hand-keyed animation.
- **The great axe's swings are one-handed.** They come from the library's sword clips.
- **The run start is not authored.** The hero reaches speed in 0.2 s, which the forward lean already carries, so a start clip was left out on purpose.

## The first-player learning path

[LearningPathTest](../Assets/Editor/LearningPathTest.cs) ([runner](../Tools/learningpathtest.ps1)) walks a fresh journey from Brushwick to the floor-5 Guardian with the real game loop. At each stage it reads what a newcomer is told (the objective, the town's next step, the tip, the journal) and writes it all down with captures, in Logs/learning/path.md. The transcript showed three real gaps, now fixed:

1. **On the first climb the objective still said "Enter the Gloom Tower".** Inside the tower the objective is now the floor's own task: "Floor 1: clear its monsters to open the stairs", the Guardian by name on a Guardian floor, then "the stairs are open, to the north".
2. **Back in town nothing said what the town could do.** A green **NEXT** line under the objective, also in the journal ([GameGuidance](../Assets/Scripts/GameGuidance.cs)), names the one most useful thing:
   - spend stat points, with the key;
   - learn at the Field desk;
   - report the courier;
   - accept Lady Vane's thanks;
   - take a commission (not on the very first visit, which is for the tower alone).

   Each suggestion comes from the save, so it goes as soon as it is done, and none grants anything.
3. **Directions were missing or hard-coded.** Every place named now comes with a compass direction read from where it actually stands in town: the Field desk is south-east of the square, the Guild and Lady Vane north-west. The journal's empty-commission line no longer says the Field desk posts work.

27 checks passed. That includes a synthetic session recorded in the playtest format (see Q01).

## Q01: the fresh-player playtest

A real fresh player's observations cannot be produced by the project's own tests, and none are claimed here. What this package delivers is a playtest that can run at once, and turns into findings without hand tallying:

- **Session recording.** The game's existing local recorder (`-playtest <name>`, now with `-playtest-dir`) logs new events alongside floors, defeats and screens: every change of objective and next step, journal opens, town services used, tips reached, access and binding changes, the courier and rescue choices, Guardian engagements, the checkpoint, and the win. Nothing is sent anywhere.
- **The kit** ([Tools/make_playtest_kit.ps1](../Tools/make_playtest_kit.ps1)). It writes a folder with:
  - `Play.cmd`, which runs only the verified build and gives each tester a fresh journey under their own name;
  - the facilitator's script (what to watch at each stage, what not to say, when to stop);
  - the observation form, copied per tester;
  - `Report.cmd`.
- **The findings report** ([Tools/playtest_report.py](../Tools/playtest_report.py)). It writes Q01-findings.md: the chapter funnel, each session's timeline (floor times, defeats, Guardian attempts, choices, helps used), how long newcomers stood on one instruction, and ranked fix candidates with their evidence. A finding marked "observe" needs the facilitator's notes to say why before anything is changed. Synthetic sessions are labelled and excluded from the findings.

The pipeline was proven on a synthetic session recorded by the learning-path test, and its report says so in bold at the top.

The kit is `Playtests/phase-nine-chapter-20261017-214454/` ([record](../Reports/phase-nine-chapter-playtest-kit.json)). It launches only the build recorded in [the chapter acceptance](../Reports/phase-nine-chapter-acceptance.json) (runtime SHA256 9AC42E41…CACF). A smoke run of that build with the recorder on wrote its session file.

**Verification for A01/A02, the learning path and Q01.** On the isolated mirror (partly on battery), [Tools/verify-phase-nine.ps1](../Tools/verify-phase-nine.ps1), now running all 21 suites, passed:

| Suite | Checks |
|---|---|
| Learning path | 27 |
| Animation | 43 |
| Access | 73 |
| Environment and audio | 36 |
| Branch | 34 |
| Guardian | 63 |
| Encounter | 53 |
| Weapon | 113 |
| Save | 103 |
| Contact | 65 |
| Feel | 29 |
| Growth | 194 |
| Objective | 120 |
| Feedback and controller | 179 |
| Pilot | 102 |
| Chapter route | 33 |
| Production, including the soak | 201 |

Story and navigation also passed. Terrain failed in the batch on its stacked-slime check, for the second batch running. It was not a fall: the slime sat 4 cm short of its ground after a fixed 100 manual updates, which all share one frame's delta time. The check now steps until the slime settles, with a cap. A rerun then failed the 14 s live chase on battery, which is already known to be timing-sensitive, and the next rerun passed all 58.

The final Windows build is 383 MB with 0 errors and 0 warnings. The `Bindings` code no longer uses the obsolete `Key.IMESelected`, and the old `Post.shader` warning did not reproduce on this build.

**Q01 status: ready, not done.** The chapter's fresh-player acceptance, and the prioritized fixes that come from it, wait for real testers.

## Verification

[ChapterRouteTest](../Assets/Editor/ChapterRouteTest.cs) and [its isolated runner](../Tools/chapterroutetest.ps1) exercise physical keyboard traversal above and below the bridge, controller winch input, all three navigation profiles, the actual shorter gate path, stacked support, interrupted gate/chest writes, stale-campaign callbacks, retained chest receipts, the original spring, stair blocking and repeated floor/town ownership. Visual evidence includes a diagnostic overview and gameplay views from both levels. The overview disables fog only for inspection; ordinary gameplay retains it.

All test journeys are isolated from player campaigns and the runner restores the exact Greyspire editor preference key. The final candidate passed **1,083 checks**: 33 route, 102 pilot, 201 production, 103 save, 65 contact, 29 movement, 58 terrain, 194 growth, 120 objective and 178 feedback/controller assertions. Both rescue branches including skip and the tower route/follow/hunt scenarios also completed. All **446 candidate input hashes** remained unchanged. [Machine-readable acceptance](../Reports/phase-nine-route-acceptance.json) records the result separately from the open full chapter gate.

Independent parsing confirmed **1,769 unique telemetry events**, **18 stable transitions**, **1,200 stress probes**, zero dropped events and all thirty preserved floor definitions. The successful sustained fixture ran **617.8460 seconds / 36,950 frames** with median **16.6662 ms**, p95 **17.1395 ms** and p99 **18.0757 ms**. Peak native mesh count was **386**, matching the previous accepted fixture's peak; repeated Millrace/pilot/town reconstruction retained identical warmed mesh/material/owned-asset counts. These are local editor measurements, not target-hardware certification.

The delivered Windows build succeeded at **382 MB, 0 errors, 0 warnings**. Its **521,216-byte** runtime DLL has SHA256 **5E678AE866EE118321C057F4A6A50D32C42F43D5B5B9E787467027E944323651**. The [standalone startup check](../Reports/phase-nine-route-startup.json) loaded an isolated copy of the synthetic preset and exited cleanly with exact player preferences restored. Hidden-window capture was unavailable; the images below were captured in the editor.

Reviewed visuals: overview, upper gameplay view and lower gameplay view. The lower crossing remains below the deck under actual keyboard movement; all three navigation profiles complete both routes. The service gate reduces the measured connection from **63.2541 m to 16 m**, and its receipt applies before a rebuilt navigation field becomes ready. Opened chests and the two-heart spring passed interruption/rebuild/use checks without granting a floor clear.

The verified source/evidence checkpoint location and manifest are recorded in checkpoint metadata, with independent archive/live verification. The predecessor phase-eight archive remains unchanged. No fresh-player chapter review or full P1 completion is claimed by this package.

The first final production attempt was interrupted by Windows Modern Standby. Its trace contains a 355.1-second gap and only 638 of the required 1,200 stress probes before the scenario deadline. The runner restored editor preferences; that attempt is not accepted as a completed soak. [Power-event evidence](../Reports/phase-nine-standby-events.json), the failed production log and its runner output are retained. The retry uses the unchanged candidate and the original test duration and thresholds.

To reproduce the complete candidate verification after Unity has imported the project and closed:

```powershell
pwsh -NoProfile -File Tools/verify-chapter-route.ps1
```

The [route-package checker](../Tools/check-chapter-route.py) fingerprints source inputs before the run, rejects later changes and stale/incomplete evidence, and keeps `phase_nine_complete=false` and `phase_ten_allowed=false`. Its preview launcher was checked while acceptance was incomplete and correctly refused to launch. The [isolated preview kit](../Reports/phase-nine-route-playtest-kit.json) is for this floor-2 package; its preset is not a campaign achievement or a complete chapter review.
