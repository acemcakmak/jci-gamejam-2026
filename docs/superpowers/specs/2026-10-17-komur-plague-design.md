# Kömür and the Plague Rats: Design and Jam Roadmap

JCI Game Jam 2026 · solo · Unity 6 (6000.0.75f1), URP, Cainos *Pixel Art Top Down – Basic*

## 1. Intent

A small, polished top-down game. Kömür the black cat protects a medieval town from plague rats on a single night. The townsfolk believe black cats bring bad luck, so they fear and attack the one creature that is trying to save them.

**The moment a judge should remember:** a rat bites a villager, the villager visibly falls ill, and Kömür races in to save them just in time.

**Why it scores well:**
- **Kömür is the mechanic, not decoration.** The cat is the only thing that can stop the rats, which is historically true: cats hunt rats.
- **There's a world-building twist.** The town's superstition about black cats is the obstacle the player has to work around.
- **There's an arc.** Each villager you cure stops fearing Kömür, so by dawn the town is cheering for the cat it once hated. This is also the story for the presentation.

## 2. Goals / non-goals / anti-goals

**Goals**
- A complete, stable WebGL game that plays for 3–5 minutes, with a start screen, the game itself and an end screen.
- A playable game at every stage of development, built up in layers: MVP → Cure → Fear → Polish.
- Clear visual feedback for every state change: bite, sick, cured, dead, scared.

**Non-goals**
- Multiple levels, upgrades, save systems, settings menus beyond a mute toggle, or procedural maps.
- Hand-drawn custom art beyond small recolours and edits. The Cainos pack and free sprites cover the visuals.

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
1. **MVP: Prevent.** Rats target villagers. Kömür pounces to kill them. A bite → villager Sick → when the sickness timer (~25 s) ends → Dead, leaving a gravestone (Cainos has gravestone props). Waves escalate over a ~4-minute night. The game is lost when *N* villagers are dead, and won when you survive until dawn. Score = villagers alive.
2. **Cure.** Pick up medicine at the apothecary and touch a sick villager → Cured, shown with a colour pop, sparkle and "Thank you!". This is a triage decision: carry medicine *or* hunt rats.
3. **Fear.** A healthy villager who has never been cured has a fear radius. If Kömür stays inside it for more than ~1 s, a "!" appears. The villager then flees, or throws something that stuns Kömür for 1 s. Sick villagers are too weak to react. Cured villagers become friendly, shown with a heart icon, and never fear Kömür again.
4. **Polish.** Juice with DOTween (squash and stretch on pounce, screen shake, hit-stop), SFX, a music loop, an intro story card and an end screen showing "X villagers saved". The end screen reflects the arc: lots of villagers saved means the town cheers for Kömür.

### Readability rules
- Sick villagers are tinted green, have a skull or cough icon, a radial countdown ring and move slowly.
- An off-screen arrow points to each sick villager at the screen edge. Add this in tier 2 only if time allows, but it's a big readability win.
- The HUD shows a night progress bar (moon → sun), villagers alive/dead, and whether Kömür is carrying medicine.

## 4. Constraints

- **Timeline:** starts Oct 17 13:00, submission Oct 18 13:00, video 14:00, presentation 15:15. All times UTC+3. Mentors are offline 01:00–07:00.
- **Mandatory:** Kömür the black cat, plus the theme announced at 13:00, which is still unknown.
- **Format:** WebGL is strongly recommended. Upload limit is 100 MB.
- **Deliverables:** playable build/link, gameplay video, presentation deck, project info. Tick "present live" on the submission form.
- **Presentation:** 5 minutes, in English, read from a script.

## 5. Decided how-it's-made

- **Engine and stack:** Unity 6 + URP 2D, Cainos for tiles and props, DOTween and TextMeshPro, which are already imported.
- **Villagers:** reuse the Cainos `PF Player` prefab and its animations, with colour variants via `SpriteRenderer.color` or palette swaps. No new human art.
- **Input:** the project is on the legacy Input Manager (`activeInputHandler: 0`), and the Cainos `TopDownCharacterController` uses `Input.GetKey`. Keep it that way. Don't switch input systems during the jam.
- **Movement and pathing:** no NavMesh. Build an open town map with a few obstacles. Rats move directly toward their target with simple steering or a short obstacle nudge, and villagers walk between hand-placed waypoints.

## 6. Left to the builder (my calls; change them freely)

- Exact numbers: sickness timer, wave curve, the loss threshold *N*, pounce range. Tune these in play.
- Whether rats die from a single overlap or need a pounce. Default is pounce, because it feels more active.
- The apothecary visual, built from Cainos walls, pots and altar props, with a sign.
- Camera: Cainos `CameraFollow` or a simple lerp follow, with a Pixel Perfect Camera.

## 7. Theme adaptation (13:00–13:30)

Don't throw the concept away. Bend it to fit the theme. When the theme drops, write down 3 ways it could change **one** of these: the map, the rat behaviour, the cure, or the night structure. Then pick the cheapest one. Example mappings:
- **"Light/Dark":** Kömür is invisible to villagers in the shadows, while lanterns reveal the cat.
- **"Cycles/Loop":** multiple nights, and the town remembers who you saved.
- **"Chaos/Balance":** a town panic meter that rises with every bite.
- **"Small/Big":** rats swarm into a giant rat king at dawn.

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
3. **Collect art** (Cainos has no cat, rat or villager variety):
   - A black cat sprite sheet, top-down or 3/4 view, ~32 px, with idle/walk and ideally attack frames.
   - A rat sprite.
   - Small icons: medicine bottle, skull, "!", heart.
   - Search itch.io for free assets ("cat sprite 32x32", "rat sprite pixel") and OpenGameArt. **Check the licences**, and that the pixel scale matches Cainos (32 PPU).
4. **Collect audio:** meow, pounce/hiss, rat squeak, cough, cure chime, villager scream, and one night music loop. Kenney.nl and freesound (CC0) are good sources.
5. **Get the project ready (no gameplay code):**
   - Commit the current state, including the pending ProjectSettings changes.
   - Set up a 2D URP renderer and a Pixel Perfect Camera.
   - Add sorting layers to match Cainos.
   - Create empty `Menu` and `Game` scenes and add them to Build Settings.
   - Pick a pixel font and generate a TMP font asset.
   - Import the sprites and slice them.
6. **Check the jam rules on pre-made work:** ask in Telegram whether pre-jam code is allowed. Until you know, write no gameplay code before 13:00 and keep prep to assets and setup.
7. **Presentation prep:** set up a 5–6 slide deck template (title / story / gameplay loop / how Kömür matters / theme / what's next) and test a screen recorder: OBS, or macOS ⌘⇧5.
8. **Sleep 7+ hours.** Watch the 12:30 briefing.

### During the jam (Oct 17 13:00 → Oct 18 13:00)
| Time | Work | Exit criterion |
|---|---|---|
| 13:00–13:30 | Theme mapping (§7). Lock the scope. | One sentence that ties the theme to the game |
| 13:30–17:00 | **MVP:** map blockout, Kömür movement and pounce, rat spawn and chase, villager wander, bite → sick → dead, gravestone | You can kill rats and lose villagers |
| 17:00–19:00 | Waves, night timer, win/lose, basic HUD, restart | A complete loop from start to finish |
| 19:00–19:30 | **First WebGL build, uploaded to the itch.io draft** | It plays in a browser |
| 19:30–22:30 | **Cure tier:** apothecary, carrying medicine, curing, cured visuals | A triage decision exists |
| 22:30–01:00 | **Fear tier:** fear radius, flee/throw, cured villagers become friendly | The irony shows up in play |
| 01:00–01:15 | WebGL build and commit. Write down 3 priorities for the morning. | — |
| 01:15–06:30 | **Sleep.** Mentors are offline anyway. | — |
| 06:30–10:00 | Polish: juice, SFX and music, intro card, end screen, tuning, off-screen arrows | It feels good to play |
| **10:00** | **Feature freeze.** Only bug fixes from here. | — |
| 10:00–11:00 | Fix bugs. Make the final WebGL build and upload it. Test in a fresh browser. | Stable build is live |
| 11:00–12:15 | Record a 1–2 minute gameplay video. Fill in the deck. Write the presentation script. | Assets ready |
| 12:15–12:45 | Submit the form (tick "present live"). Upload the video. | Submitted |
| 12:45–15:15 | Buffer. Rehearse the script out loud twice. | Ready to present |

**Rules for yourself:** commit after every working step, build WebGL after every tier, and never start a new tier after 06:30.
