# Greyspire: credits

All third-party assets are Creative Commons Zero (CC0, public domain) unless a row says otherwise.

| Asset | Author | Source | Used for |
|---|---|---|---|
| KayKit Adventurers 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0 | No longer shown: the heroes are realistic humans now. Files kept in Resources/Characters |
| KayKit Skeletons 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 | Fallback only, if a monster fails to load. Imps, Puglins and the Gloom King replaced them |
| KayKit Medieval Hexagon Pack 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0 | No longer shown (realistic art direction). Files kept in Resources/Env |
| KayKit Dungeon Remastered 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 | No longer shown. Files kept in Resources/Env |
| KayKit Halloween Bits 1.0 | Kay Lousberg | https://github.com/KayKit-Game-Assets/KayKit-Halloween-Bits-1.0 | No longer shown. Files kept in Resources/Env |
| Universal Base Characters (Standard) | Quaternius | https://quaternius.itch.io/universal-base-characters | Heads, faces and hair of every human: heroes, townspeople, the bandits of the Wilds |
| Modular Character Outfits - Fantasy (Standard) | Quaternius | https://quaternius.itch.io/modular-character-outfits-fantasy | Ranger and peasant outfits: all five heroes, townspeople, the bandits of the Wilds, the Gloom King |
| Universal Animation Library 1 and 2 (Standard) | Quaternius | https://opengameart.org/content/universal-animation-library | Animations for the realistic humans and monsters |
| Bestiary - Dungeon Monsters Kit (Standard) | Quaternius | https://quaternius.itch.io/bestiary-dungeon-monsters-kit | Imp and Puglin enemies (the tower's monsters, and the monster dens of the Wilds). Quaternius Asset License: free to use in games, but the files themselves may not be redistributed as assets |
| Fantasy Props MegaKit (Standard) | Quaternius | https://opengameart.org/content/fantasy-props-megakit | All 94 models: weapons and tools, and the furniture and goods of the town, camps and tower floors |
| Greataxe_Iron, Seax_Steel, Dagger_Steel | Made for Greyspire (Tools/build_weapons.py) | - | The great axe, seax and parrying dagger of the weapon families; original meshes, painted with the Fantasy Props MegaKit trim sheet above |
| Stylized Nature MegaKit (Standard) | Quaternius | https://opengameart.org/content/stylized-nature-megakit | Trees, bushes, rocks, ferns, flowers and mushrooms in town, the arenas and all ten Wilds regions |
| Medieval Village MegaKit (Standard) | Quaternius | https://opengameart.org/users/quaternius | Every building: houses, towers, the well, windmill, church and castle of Brushwick, the Gloom Tower, plus lamp posts, pillars, fences and cobbles (Village.cs, VillageWorks.cs) |
| RPG Audio, Impact Sounds, Interface Sounds | Kenney | https://kenney.nl/assets | Sound effects |
| Music Jingles | Kenney | https://kenney.nl/assets/music-jingles | Level-up, victory and defeat stingers |
| tell_1-3, ambience_meadow_1 | Made for Greyspire (Tools/build_audio.ps1) | - | Enemy wind-up tells: Kenney swing recordings reversed, lowered and normalised; the meadow wind bed is synthesised filtered noise. No generative AI |
| Town Theme RPG | cynicmusic | https://opengameart.org/content/town-theme-rpg | Menu and town music |
| Battle Theme A | cynicmusic | https://opengameart.org/content/battle-theme-a | Tower music |

`Tools/fetch_env.py` re-downloads the environment models and `Tools/fetch_packs.py` the Quaternius packs. Everything else (slimes, Mop, bomber, Lumi,
cactus and crystal props, shaders, code) was made for this project.

## The trailer (videos/greyspire-trailer)

Not part of the game: used only in the trailer film.

| Asset | Author | Source | Licence |
|---|---|---|---|
| "Five Armies" (music) | Kevin MacLeod | https://incompetech.com | Creative Commons: By Attribution 4.0. The credit must stay on the film's closing card |
| Narration | Synthetic voice: Kokoro-82M, voice `bm_george`, through `hyperframes tts` | https://hyperframes.heygen.com | Generated on this machine; credited as a synthetic voice |
| Sound effects (impacts, riser, whooshes, drone) | Made for the film | `videos/greyspire-trailer/tools/sfx.py` | Own work |
| Felix Titling, Palatino Linotype | Microsoft | Installed with Windows / Office | Used to render titles only. The font files are copied into `assets/fonts` for rendering and are git-ignored: do not redistribute them |
