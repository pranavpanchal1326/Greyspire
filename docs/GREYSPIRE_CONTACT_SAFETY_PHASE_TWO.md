# Greyspire — phase two: trustworthy combat contact

13 October 2026. Primary project: `D:\game`, Unity 6000.5.4f1.

## Entry gate

Phase two started only after the user-requested phase-one closure audit. That audit found and fixed defeat-delay interaction/healing and older-campaign backup recovery. The closure revision passed **103 safety checks**, **28 movement/camera/input checks**, both Timeline rescue branches, and a **382 MB Windows build with 0 errors and 1 existing warning**.

The exact source/evidence baseline, including SHA-256 records, is retained in `Backups/phase-one-complete-20261013`. The save contract and content expansion constraints are in the phase-one report. No content arrays are reordered or new saved identities introduced by this combat pass.

## Implemented contract

| Package | Source | Behavior and acceptance |
|---|---|---|
| Explicit damage request/result | `CombatContact.cs`, `Actor.cs`, `Enemy.cs`, `PlayerController.cs` | Requests carry amount, source/position, push, category, heavy/critical, stagger, and launch; results distinguish miss, immunity, block, parry, damage, kill, defeat, and revival, with actual damage and effect flags |
| Complete projectile segment | `Projectile.cs`, `CombatContact.cs` | Swept radius 0.25 m; ordered actor capsule entries and solid-world contacts; nearest accepted contact wins; initial overlaps and saturated buffers are handled |
| Moving actor contact | `Actor.cs`, `CombatContact.cs`, `PlayerController.cs`, `NavFollower.cs` | Relative translation sweep uses previous/current actor poses; explicit teleports/warps reset pose history; newly spawned actors work without waiting for a physics query registration step |
| Physical cover and height | `CombatContact.cs`, player/enemy moves, companion, shockwave | Vertical capsule overlap and solid cover are checked; flattened horizontal distance alone cannot authorize contact |
| Damage-based rewards/feedback | `PlayerController.cs`, `PlayerMoves.cs`, `Projectile.cs` | Blocks/immunity do not increment landed damage, combo, or damage-driven Burst/style feedback; status effects require accepted damage |
| Hurt/override cancellation | `PlayerController.cs`, `PlayerMoves.cs` | Accepted hurt cancels pending offensive contact and queued intent; Brush Wave/Burst consume old offence before dealing their own effect |
| Defeated actor eligibility | `Enemy.cs`, targeting, companion, projectile | A retained defeated King remains available for the ending scene but cannot be damaged, locked, or selected as an active threat |
| Mutual-kill settlement | `Game.cs` | Ordinary/Guardian final-enemy completion survives either event order; entitlement persists alongside one defeat settlement and an intact results callback |

The existing bool `TakeDamage` entry point remains available and returns whether health damage was applied. It delegates to the new result contract. Critical/heavy flags no longer live as mutable fields on the victim, so another damage source cannot inherit an unrelated attack's flags.

## Collision and action policies

- **Projectiles:** solve the entire segment against authoritative actor capsules and the nearest swept solid collider. Ignore allies, dead/defeated actors, companions, and pickups as appropriate; ignored actors cannot conceal a wall. World geometry wins an equal-distance tie. Block/parry consumes a projectile; immunity/dodge permits it to continue. A spent projectile cannot deliver a second hit. Travel is clipped to remaining lifetime.
- **Physics-query saturation:** ordinary casts reuse buffers; full buffers use complete-query fallback. Actor capsule candidates are ordered separately, so character-controller query registration is not required for actor contact.
- **Melee:** preserve the existing authored contact moment and analytical range/cone. A frame crossing both contact and recovery emits the contact once. This pass does not claim to reconstruct the animated blade's full continuous mesh path. Dash/slide retain per-action hit sets.
- **Physical attacks:** require a reachable vertical band and clear solid-world path. This applies to player strikes/slams, enemy melee/slams/explosions, companion contact, thorns, and ground shockwaves. Ground shockwaves only affect their ground-height band; lower or upper floors are not flattened into it.
- **Brush Wave:** magic that pierces actors while respecting solid cover and a bounded vertical band.
- **Colour Burst:** deliberately penetrates cover, preserving its magic area identity, with a bounded vertical band. This exception is explicit; it does not change ordinary physical contact rules.
- **Hurt:** no offensive hyperarmor is introduced. An accepted damaging contact clears the current wind-up/move, queued light follow-up, lunge, and buffered intent. Immunity, block, and parry retain their own behavior. Heavy/light category is independent of the floor's extra health damage.
- **Second Wind:** reports revival, consumes a charge, and avoids defeat settlement. Actual applied damage reports the health lost before revival, not an invalid negative after-health difference.
- **Mutual final-boss kill:** the fall remains a fall, with permanent floor/XP progress retained and the results callback preserved. Ending/win requires surviving the available rematch. It never awards both a win and a fall through competing callbacks.

Actor translation is sampled across frame poses and treated linearly for collision. Capsule dimensions and orientation come from the current controller. Arbitrary rotating/scaling body deformation is not continuous mesh collision. Player and navigation teleports reset this history to prevent contacts along a teleport path.

## Verification

`Assets/Editor/ContactTest.cs` and `Tools/contacttest.ps1` exercise real game actors, controllers, physics obstacles, damage handling, projectile advancement, and saved settlement. Test saves are isolated; the exact editor preference key is restored by the runner.

**Final results: 56 contact checks passed; all 103 save/reward checks still passed; all 28 movement/camera/input checks still passed.** The Timeline rescue regression completed both branches, including skip, with `rescued=3`, choices `2` and `1`, and gameplay restored (`Logs/contact-story.log`). The navigation regression completed route arrival, heel/follow, and hunt/strike with agents on the NavMesh (`Logs/contact-nav.log`).

The **Windows build succeeded: 382 MB, 0 errors, 1 existing `PlayerStats` serialization warning**. Output: `Builds/Greyspire/Greyspire.exe`; packaging evidence: `Logs/contact-build.log`.

**G1 is implemented and validated for the current combat system and the policies below.** This is a source/runtime gate, not a full campaign playtest or performance certification. Phase one remained passing throughout the final phase-two regression run.

Coverage includes:

- Actual damage, invalid damage rejection, light block, parry, immunity, guard break, stagger/launch, capped lethal damage, and Second Wind.
- High-speed projectile crossing frame endpoints; simulated 30/120 fps agreement; repeated update after consumption; nearest-target ordering; wall-first contact; ignored ally before wall; block/parry consumption; immune player followed by a wall; initial overlap; elevated miss/aimed hit; saturated-query fallback; moving target crossing; teleport history reset.
- Same-level melee; blocked hit reward rejection; both-sided cover; covered slam without launch; Brush Wave cover; explicit Burst penetration; upper-floor rejection; hurt cancellation; skill/Burst override; per-dash duplicate suppression; large-step contact timing.
- Both mutual Guardian event orders, reload with pending reward and one settlement, defeated King eligibility/targeting, and final-boss mutual-kill results liveness.

During development, the tests exposed a character-controller query-registration gap and a guard fixture aimed at the defender's back. The actor sweep was made independent of physics registration; the guard test now correctly faces the incoming shot. A deprecated Unity instance-ID call was removed before the successful run. These failed development attempts are not included as passing evidence.

```powershell
.\Tools\contacttest.ps1 -Build
.\Tools\savetest.ps1
.\Tools\feeltest.ps1
```

Evidence logs are `Logs/contact.log`, `Logs/contact-build.log`, `Logs/save-safety.log`, and `Logs/feel.log`. The existing rescue and navigation regressions are also run with isolated saves. G2 terrain/support/navigation ownership, growth balancing, full content-ID migration, new animation assets, controller choice focus, RNG separation, and production profiling remain later gates.


## Completion evidence

### Closure audit before phase three — 13 October 2026

The closure audit found and repaired two frame-boundary gaps: shockwaves previously checked only the final radius (and retired before resolving the last segment), and an expiring projectile used the actor's full-frame motion after its own lifetime ended. Ring crossings now include relative player translation and the final radius; projectile relative sweeps clip actor motion to the remaining active fraction and reject already expired shots.

The revised gate passed **63 contact checks, 103 save checks, and 28 movement checks (194 assertions)**. The Windows build again succeeded with **0 errors and 1 existing serialization warning**. The seven added checks cover 30/120 fps and a two-second shockwave step, cover, a moving player crossing, and projectile expiry. Closure source/evidence is retained under `Backups/phase-two-verified-20261013`. This is the verified G1 baseline for starting G2.

The combined final run passed **187 automated assertions** (56 contact + 103 save + 28 movement), plus the rescue and navigation scenario completions. Final source/evidence and a checksum manifest are retained in `Backups/phase-two-complete-20261013`. The existing working tree changes and all phase-one baseline files remain available for review or recovery.
