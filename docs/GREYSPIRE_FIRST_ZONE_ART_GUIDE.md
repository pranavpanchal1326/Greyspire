# Greyspire — first-zone art and readability guide

16 October 2026 · phase nine / V01 · direction document, environment pass pending

The first chapter keeps the established painted meadow, human village and visible return of Colour. Brushwick should feel worked and inhabited; the tower should feel like pieces of that place held apart by the Gloom. Preserve the existing hero silhouette, painter tools, grass palette, warm sun and restrained interface. The route prototype proves collision and navigation; its plain bridge slabs are not the finished environment standard.

## Palette and material roles

The source values below are authoring colours, not sampled display pixels; lighting and colour-space conversion affect the final image.

| Role | Existing source value | Use |
|---|---|---|
| Meadow base / patch | `GameLevel.cs`: `(0.36, 0.55, 0.27)` / `(0.29, 0.46, 0.23)` | Grass and low foliage, with larger quiet patches beside combat lanes |
| Warm daylight / ambient | `GameLevel.cs`: `(1, 0.96, 0.86)` / `(0.6, 0.66, 0.76)` | Sun-facing warmth, readable cool shadow |
| Ruin stone | `GamePilot.cs`: `(0.42, 0.48, 0.46)` | Structural piers, foundations and retained survey ruins |
| Millrace deck | `GameChapter.cs`: `(0.57, 0.64, 0.59)` | Light, worn upper surface that separates feet and the route from surrounding grass |
| Worked wood | `GameChapter.cs`: `(0.43, 0.31, 0.20)` | Route boards, winch and replaceable gate pieces |
| Ink / paper | `UI.cs`: `(0.06, 0.065, 0.08)` / `(0.93, 0.91, 0.86)` | Existing panel and text hierarchy |
| Old gold / steel blue | `UI.cs`: `(0.88, 0.73, 0.42)` / `(0.46, 0.64, 0.8)` | Important interactions and System feedback, supported by words and shape |

Use the existing nature, village and props kits as the scale/material family. Keep source records in `CREDITS.md` when assets change. This guide introduces no new external assets or fonts. Do not mix unrelated realistic surface detail into the current stylized silhouettes.

## Town and floor identities

| Segment | Anchor | Environment work still required |
|---|---|---|
| Town / practice | Well, Field desk, clear practice partner and return marker | A recognizable desk/work area, resident sightlines and visible service identity |
| Floor 1 | First threat and Colour Heart | A framed arrival, restrained clutter and a clear northbound destination |
| Floor 2 | Millrace bridge and underpass | Replace bare slab presentation with fitted stone/wood surfaces, piers and modest edge wear; retain the proved ramp/deck dimensions |
| Floor 3 | Survey ruin and raised overlook | Give each ruin wing construction detail and a distinct courier rest point; retain its route and chest clearance |
| Floor 4 | Drained Painter | Frame the rescue silhouette and its approach, with room for the existing story camera and departure |
| Floor 5 | Guardian silhouette and arena | Arena language must support the eventual authored Guardian mechanic, retry route and aftermath |

Give signs, winches and interactable objects visible world shapes. A floating label supports an object; it should not be the only evidence that a service exists. Lower and upper routes need different silhouettes, surface treatment and landmarks. Colour alone cannot identify a safe path or distinguish a threat.

## Protect the verified playable space

The Millrace rises 5.4 m over 20-metre ramps, has a 10-metre bridge deck, and leaves 4.9 m beneath the deck. Its service-gate opening is 6 m wide along the crossing. Preserve those supported surfaces and clearances when replacing prototype renderers. Keep decoration off the walking line and chest interaction positions. New colliders require physical traversal and all three navigation profiles to pass again.

The Colour Heart now sits beside the lower approach at `(27, 0, -14)` because its original centre collider blocked the underpass. Do not restore it to the crossing while dressing the level. Both original floor-2 chests keep their receipt slots and reward amounts. Decoration, visual changes and route repairs must not reset a journey or a reward.

Review the actual following-camera views at upper-ramp joins, beside the parapets, beneath the bridge, at both chests and at the winch. The hero must remain identifiable, the next exit must be readable, and the view must recover after an obstruction. Brighten underpass materials or ambient contribution before adding extra shadow-casting lights. Keep ordinary fog and the reduced-effects preset enabled during this review; the fog-free overview is diagnostic only.

## Animation and character acceptance targets

The directional motion and placement packages remain separate work. Review backsteps, both lateral directions, turns, starts and stops at the existing legal movement extremes. Use purpose-made clips where needed; a reversed forward walk is not final A01 acceptance.

For A02, propose and measure a stance-foot drift target of at most **0.05 m** over a **0.25 s** planted interval, and hand-to-grip deviation of at most **0.03 m** in the reviewed weapon contact poses. These are production targets, not measurements already achieved. Capture front, side and gameplay-camera views. Check heavy commitment, each supported weapon, dodge/guard transitions, pause, death, scene skip and reload ownership before accepting the package.

## Evidence required for the environment pass

Keep paired baseline/candidate gameplay captures with the same camera, seed, lighting phase and comfort settings. Record any intentional density change and its readability purpose. Measure frame timing and warmed generated/native ownership after dressing; stay within the prior accepted bounds or document and fix the increase before acceptance. Imported/shared assets remain shared, and generated meshes/materials need explicit lifetime ownership.

The current reviewed Millrace captures show two remaining presentation issues: broad untreated slab surfaces and a very dark underpass. The exit, player and interaction prompt are readable in the retained views, but V01 is not accepted until the authored environment pass and human chapter review meet this guide.
