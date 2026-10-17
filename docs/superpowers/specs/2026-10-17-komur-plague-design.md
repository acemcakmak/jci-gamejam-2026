# Kömür and the Plague Rats: Design and Jam Roadmap

JCI Game Jam 2026 · solo · Unity 6 (6000.0.75f1), URP

**Asset stack:** Cainos *Pixel Art Top Down – Basic* (map, daytime) · *Pet Cats pack* (Kömür) · Feel (game feel) · Epic Toon FX (VFX) · All In 1 Sprite Shader (sprite effects) · DOTween · TextMeshPro

## 1. Intent

A small, polished top-down game. Kömür the black cat protects a medieval town from plague rats on a single day of a plague outbreak. The townsfolk believe black cats bring bad luck, so they fear and attack the one creature that is trying to save them.

**The moment a judge should remember:** a rat bites a villager, the villager visibly falls ill, and Kömür races in to save them just in time.

**Why it scores well:**
- **Kömür is the mechanic, not decoration.** The cat is the only thing that can stop the rats, which is historically true: cats hunt rats.
- **There's a world-building twist.** The town's superstition about black cats is the obstacle the player has to work around.
- **There's an arc.** Each villager you cure stops fearing Kömür, so by the final wave the town is cheering for the cat it once hated. This is also the story for the presentation.

## 2. Goals / non-goals / anti-goals

**Goals**
- A complete, stable WebGL game that plays for 3–5 minutes, with a start screen, the game itself and an end screen.
- A playable game at every stage of development, built up in layers: MVP → Cure → Fear → Polish.
- Clear visual feedback for every state change: bite, sick, cured, dead, scared.

**Non-goals**
- Multiple levels, upgrades, save systems, settings menus beyond a mute toggle, or procedural maps.
- Hand-drawn custom art beyond small recolours and edits. The imported packs and free sprites cover the visuals.
- Night scenes, 2D lights or a day/night cycle. Cainos is a daytime pack, so the game stays in daylight and doesn't need a lighting pass.
- Custom VFX or shaders. Use Epic Toon FX, All In 1 Sprite Shader and Feel presets as they are, tweaking only colours and timings.

**Anti-goals** (failure even if it "works")
- The game crashes or soft-locks in the WebGL build, or the build exceeds 100 MB.
- A judge can't tell within 10 seconds what to do. That means an unclear objective or invisible sickness.
- Kömür reads as a generic avatar that could be swapped for any other character.
- No submission at 13:00 because a late feature broke the build.

## 3. Core design

### Entities
| Entity | Behaviour |
|---|---|
| **Kömür** (player) | WASD 8-way movement. Pounce/attack on Space, a short dash with a hit box. Carries at most **1 medicine**. |
| **Rat** | Spawns at map edges (sewer grates or holes). Picks the nearest healthy villager and moves toward them. On contact, bites: the villager becomes Sick and the rat flees toward the nearest edge. Dies in one pounce. |
| **Villager** | Wanders between waypoints. States: **Healthy → Sick → Dead**, or **Sick → Cured**. |
| **Apothecary** | A fixed building or area. Walking into it picks up medicine, with a short cooldown. |

### Tiers (build in this order; each one is a shippable game)
1. **MVP: Prevent.** Rats target villagers. Kömür pounces to kill them. A bite → villager Sick → when the sickness timer (~25 s) ends → Dead, leaving a gravestone (Cainos has gravestone props). The outbreak comes in ~5 escalating waves, ~4 minutes in total, with a short breather and a "Wave X" banner between waves. The game is lost when *N* villagers are dead, and won when the final wave is cleared. Score = villagers alive.
2. **Cure.** Pick up medicine at the apothecary and touch a sick villager → Cured, shown with a colour pop, sparkle and "Thank you!". This is a triage decision: carry medicine *or* hunt rats.
3. **Fear.** A healthy villager who has never been cured has a fear radius. If Kömür stays inside it for more than ~1 s, a "!" appears. The villager then flees, or throws something that stuns Kömür for 1 s. Sick villagers are too weak to react. Cured villagers become friendly, shown with a heart icon, and never fear Kömür again.
4. **Polish.** Use the packs for juice instead of hand-building it (see the effects map below). Add SFX, a music loop, an intro story card and an end screen showing "X villagers saved". The end screen reflects the arc: lots of villagers saved means the town cheers for Kömür.

### Effects map (lean on the packs)
| Event | Feel (MMF_Player) | Epic Toon FX | All In 1 Sprite Shader |
|---|---|---|---|
| Pounce / rat kill | Hit-stop, small camera shake, squash and stretch on Kömür, sound | Small hit/poof burst | Hit flash on the rat |
| Villager bitten | Shake on the villager, cough sound | Small green/poison puff | Switch to a green hue shift (sick state) |
| Sickness ticking | Pulse scale as the timer runs low | Occasional sick bubbles | Green tint that grows stronger |
| Cured | Floating "Thank you!" text, chime | Heal/sparkle burst | Remove the green tint, short glow/outline |
| Villager dies | Fade out, then spawn a gravestone | Dark smoke | Fade/dissolve |
| Kömür stunned by a villager | Shake, blink | Stars/impact | Hit flash |
| Wave start / end | Banner text, camera punch | — | — |

DOTween stays available for simple UI tweens. Use Feel for anything the player should *feel*.

### Readability rules
- Sick villagers are tinted green, have a skull or cough icon, a radial countdown ring and move slowly.
- An off-screen arrow points to each sick villager at the screen edge. Add this in tier 2 only if time allows, but it's a big readability win.
- The HUD shows the wave counter ("Wave 2/5"), villagers alive/dead, and whether Kömür is carrying medicine.

## 4. Constraints

- **Timeline:** starts Oct 17 13:00, submission Oct 18 13:00, video 14:00, presentation 15:15. All times UTC+3. Mentors are offline 01:00–07:00.
- **Mandatory:** Kömür the black cat, plus the theme announced at 13:00, which is still unknown.
- **Format:** WebGL is strongly recommended. Upload limit is 100 MB.
- **Deliverables:** playable build/link, gameplay video, presentation deck, project info. Tick "present live" on the submission form.
- **Presentation:** 5 minutes, in English, read from a script.

## 5. Decided how-it's-made

- **Engine and stack:** Unity 6 + URP 2D. All the packs listed at the top are already imported.
- **Kömür:** `Pet Cats pack/Sprites/Cat-2`, the black cat. It's side-view, with 8-frame 50×50 strips (400×50) for Idle, Walk, Run, Meow, Sitting, Stretching and more. Flip X to face left. Use Run for movement, Idle when still, and Meow as the pounce or taunt frame. The `Meow-VFX` sprite works as a "meow" pop. The other cats (Cat-1, 3–6) are free for cameos, for example as town cats that come back once the town accepts Kömür.
- **Villagers and rats:** start with **placeholders** so you can work on mechanics first. Use coloured squares/capsules, or tinted Cainos `PF Player` instances for villagers and small dark capsules for rats. Swap in free art later if you find any. Keep the art behind a single `SpriteRenderer` + Animator on each prefab so swapping is a drop-in.
- **Third-party assets aren't in git:** `Assets/Plugins/` and `Assets/ThirdPartyAssets/` are gitignored to comply with the EULAs. A fresh clone won't open cleanly without re-importing them, so keep the original packages downloaded and available in the Package Manager (My Assets). Your own prefabs, scenes and scripts live under `Assets/_Game/` and *reference* pack assets. They never copy pack assets into tracked folders.
- **Input:** the project is on the legacy Input Manager (`activeInputHandler: 0`), and the Cainos `TopDownCharacterController` uses `Input.GetKey`. Keep it that way. Don't switch input systems during the jam.
- **Movement and pathing:** no NavMesh. Build an open town map with a few obstacles. Rats move directly toward their target with simple steering or a short obstacle nudge, and villagers walk between hand-placed waypoints.

## 6. Left to the builder (my calls; change them freely)

- Exact numbers: sickness timer, wave curve, the loss threshold *N*, pounce range. Tune these in play.
- Whether rats die from a single overlap or need a pounce. Default is pounce, because it feels more active.
- The apothecary visual, built from Cainos walls, pots and altar props, with a sign.
- Camera: Cainos `CameraFollow` or a simple lerp follow, with a Pixel Perfect Camera.

## 7. Theme adaptation (13:00–13:30)

Don't throw the concept away. Bend it to fit the theme. When the theme drops, write down 3 ways it could change **one** of these: the map, the rat behaviour, the cure, or the night structure. Then pick the cheapest one. Example mappings:
- **"Light/Dark":** Kömür can hide from villagers in building shadows or bushes. These are sprite-based shadow zones, not real lighting.
- **"Cycles/Loop":** multiple outbreak days, and the town remembers who you saved.
- **"Chaos/Balance":** a town panic meter that rises with every bite.
- **"Small/Big":** in the final wave, the rats swarm into a giant rat king (a scaled-up rat).

Fallback, only if the theme truly can't fit: the "Kömür spreads bad luck" idea, which reuses the same entities.

## 8. Roadmap

### Before the jam (now → 13:00)
Do these in priority order. **Sleep is item 0.** A solo 24-hour jam on no sleep produces broken builds.

1. **Confirm you're in the Telegram group.** The join deadline was Oct 15. Message the organisers if you're not.
2. **Prove WebGL works end to end** (the biggest risk):
   - Install the *WebGL Build Support* module through Unity Hub.
   - Build the Cainos demo scene for WebGL. Note the build time and size.
   - Set *Compression: Gzip* or *Disabled*, plus *Decompression Fallback*, so it runs on itch.io without server configuration.
   - Create an itch.io account and a **draft** HTML5 page, upload the test build and confirm it plays in a browser.
3. **Make sure the project compiles cleanly with every pack imported:**
   - Open the project, clear the Console and fix any red errors. Feel's `FeelDemosHDRP` folder is the usual suspect in a URP project, and deleting it locally is fine.
   - Test-build WebGL with **one** Epic Toon FX prefab and **one** Feel feedback in the scene, and check the build size. Epic Toon FX (~560 MB) and Feel (~430 MB) are big on disk, but only referenced assets end up in the build.
4. **Set up Kömür (asset prep, not gameplay):**
   - Slice the Cat-2 strips (Multiple, Grid 50×50, Point filter, no compression).
   - Set PPU so the cat reads well next to Cainos (32 PPU) characters. Try 32 first.
   - Create Idle/Run/Meow animation clips.
5. **Art you still need (optional before the jam, since placeholders are fine):**
   - Rat and villager sprites. Search itch.io and OpenGameArt for "rat sprite pixel" and "top down villager pixel". **Check the licences.**
   - Small icons: medicine bottle, skull, "!", heart. Epic Toon FX and Feel demos may already have some of these.
6. **Collect audio:** meow, pounce/hiss, rat squeak, cough, cure chime, villager scream, and one music loop (medieval town or village). Check Epic Toon FX's `Sound` folder first. Kenney.nl and freesound (CC0) are good sources.
7. **Get the project ready (no gameplay code):**
   - Commit the current state, including the pending ProjectSettings changes and the `.gitignore` update.
   - Create `Assets/_Game/` (Scenes, Scripts, Prefabs, Art, Audio).
   - Set up a 2D URP renderer and a Pixel Perfect Camera.
   - Add sorting layers to match Cainos.
   - Create empty `Menu` and `Game` scenes and add them to Build Settings.
   - Pick a pixel font and generate a TMP font asset.
   - Skim the Feel quick-start (MMF_Player + adding feedbacks) and the All In 1 Sprite Shader `Start_Here.txt`, so you aren't learning them under pressure.
8. **Check the jam rules on pre-made work:** ask in Telegram whether pre-jam code is allowed. Until you know, write no gameplay code before 13:00 and keep prep to assets and setup.
9. **Presentation prep:** set up a 5–6 slide deck template (title / story / gameplay loop / how Kömür matters / theme / what's next) and test a screen recorder: OBS, or macOS ⌘⇧5.
10. **Sleep 7+ hours.** Watch the 12:30 briefing.

### During the jam (Oct 17 13:00 → Oct 18 13:00)
| Time | Work | Exit criterion |
|---|---|---|
| 13:00–13:30 | Theme mapping (§7). Lock the scope. | One sentence that ties the theme to the game |
| 13:30–17:00 | **MVP (placeholder villagers/rats):** map blockout, Kömür movement and pounce, rat spawn and chase, villager wander, bite → sick → dead, gravestone | You can kill rats and lose villagers |
| 17:00–19:00 | Waves and wave banner, win/lose, basic HUD, restart | A complete loop from start to finish |
| 19:00–19:30 | **First WebGL build, uploaded to the itch.io draft** | It plays in a browser |
| 19:30–22:30 | **Cure tier:** apothecary, carrying medicine, curing, cured visuals | A triage decision exists |
| 22:30–01:00 | **Fear tier:** fear radius, flee/throw, cured villagers become friendly | The irony shows up in play |
| 01:00–01:15 | WebGL build and commit. Write down 3 priorities for the morning. | — |
| 01:15–06:30 | **Sleep.** Mentors are offline anyway. | — |
| 06:30–10:00 | Polish: wire up the effects map (Feel/ETFX/AllIn1), swap in villager/rat art if found, SFX and music, intro card, end screen, tuning, off-screen arrows | It feels good to play |
| **10:00** | **Feature freeze.** Only bug fixes from here. | — |
| 10:00–11:00 | Fix bugs. Make the final WebGL build and upload it. Test in a fresh browser. | Stable build is live |
| 11:00–12:15 | Record a 1–2 minute gameplay video. Fill in the deck. Write the presentation script. | Assets ready |
| 12:15–12:45 | Submit the form (tick "present live"). Upload the video. | Submitted |
| 12:45–15:15 | Buffer. Rehearse the script out loud twice. | Ready to present |

**Rules for yourself:** commit after every working step, build WebGL after every tier, and never start a new tier after 06:30.
