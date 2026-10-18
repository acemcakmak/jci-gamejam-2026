# Kömür and the Plague Rats

<!-- TRAILER: edit this file on github.com, drag your trailer .mp4 onto this line, and GitHub replaces it with an embedded video player. -->

### The town says black cats bring bad luck.
### But the plague rats are coming, and only Kömür can stop them.

**Kömür and the Plague Rats** is a short top-down action game about a black cat saving a town that fears him.
Made solo in 24 hours for the **JCI Game Jam Europe 2026** with Unity 6.

**▶ [Play it in your browser on itch.io](https://acemcakmak.itch.io/komur-and-the-plague-rats)**

---

## How it plays

| | |
|---|---|
| **Pounce the rats** | Waves of plague rats crawl out of their holes to bite the villagers. Pounce on them with **Space**. |
| **Mind the superstition** | Villagers fear black cats. If they see you, they turn red with anger and throw stones. |
| **Bring the cure** | Bitten villagers turn green and start running out of time. Grab the glowing potion from the apothecary and carry it to them, and they’ll love you for it. |
| **Don’t be late** | If a villager dies, the whole town panics, and even your friends stop loving you. |
| **Beat the Plague Slime** | In the final wave it seizes the apothecary and hurls plague stones. Defeat it to free the medicine. |

## Three endings

When the plague is over, the whole town gathers around Kömür:

| | |
|---|---|
| **The village loves you** | Most survivors were cured by the cat. |
| **An unknown hero** | The town is saved, but it still blames the black cat. |
| **The plague won** | Only the spirits of the villagers remain. |

## Controls

| Key | Action |
|---|---|
| **WASD / Arrow keys** | Move |
| **Space** | Pounce |
| **P** | Pause (Esc may exit fullscreen in the browser) |
| **?** on the title screen | Quick how-to-play |

---

## About this repository

This repository holds the game’s own code and content. The third-party art, audio and tool packs are **not included**, in line with their licenses, so the project opens with missing sprites and sounds until those packs are imported into `Assets/ThirdPartyAssets/` and `Assets/Plugins/`.

| Path | What’s inside |
|---|---|
| `Assets/_Game/Scripts/` | All gameplay code: Kömür, villagers, rats and the boss, multi-floor pathfinding over the stairs, waves, endings, HUD |
| `Assets/_Game/Scripts/Editor/` | One-click editor tools that build the scene, prefabs and HUD, plus buttons to summon the boss or jump to any ending |
| `Assets/_Game/Shaders/` | Custom sprite shader: see-through silhouettes (stencil), outlines, hit flash and dissolve |
| `Assets/_Game/Scenes/`, `Prefabs/`, `Art/Materials/`, `Data/`, `UI/` | The game scene, prefabs, materials and the HUD layout data |
| `docs/`, `tools/` | Design notes and the map tooling used during the jam |

Built with Unity 6000.0 (URP 2D), exported to WebGL.

## Credits

Code and design by **Arda Cem Çakmak** (Scheme Games).

Assets used: Cainos Pixel Art Top Down – Basic · Minifolks Villagers 2 · Pet Cats Pack · Monsters Creatures Fantasy 2 · 48 Free Magic Potions · Sprout Lands UI Pack · [Complete UI Essential Pack](https://crusenho.itch.io) by Crusenho ([CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)) · Controllers and Keyboard · Epic Toon FX · All In 1 Sprite Shader · Feel · PixelBattleText · Cozy Tunes · Cozy FX
