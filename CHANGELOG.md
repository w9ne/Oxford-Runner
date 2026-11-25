# Oxford Runner: Development Log

## 2026-11-24 (rune walls, 1 HP casters, firing squad)

- Every wall-runnable surface in Levels 1-3 gets tutorial rune strips (glow bands + diamonds): `LevelBuilder.RuneTrim`,
  run by `Finish` on build, patched in place into Level1/Level2. Faces pressed against a wall are skipped.
  Level 3 uses a dim copy (`RuneGlow_Dim`, glow 0.5 vs 1.5) so the dark library is not glaring.
- Floating mages glide (SmoothDamp, speed 5) and only pick spots with a clear path. Area 1 wander 4 to 7 m.
- All mages in Level 3 (balcony too) and all garrison casters: 1 HP. Garrison fire interval 3.4 to 2 s.
- Level 2 area 3: lookout casters removed; firing squads of 4 on a walk atop each link wall either side of the
  centre barricade (8 casters, ~3.9 m apart).
- Removed the unused `EnemyKind` enum/field (compiler warning).

## 2026-11-24 (audit fixes)

- Parry hit-stop now freezes time dead for 0.08 s (ULTRAKILL style). `PlayerParry.freezeFrame` off = old slow-mo.
  New `HitStopStarted` event = hook for the parry SFX.
- Pausing during the hit-stop no longer unpauses the game. Jump/dash pressed while paused no longer fire on resume.
- Targeting and blast buffers 64/32 to 1024: missiles home properly in Level 3 (~700 colliders in 50 m).
- "Build Level 1/2" asks before overwriting the hand-edited scenes.
- Level 2 plants: prefab copies with GPU instancing instead of merged meshes. Level2.unity 64 MB to 10 MB.
  Old Scenery_Trees folded into the dressing Forest.
- FROZEN/STUNNED label moved from per-enemy OnGUI to a TextMesh on the health bar.
- StyleMeter: best score read once, score text rebuilt only on change.
- Removed: wave-by-wave spawning (never used), lightning legacy fields, `Enemy.Kind`/`FireInterval`, `Level2Trees`.
- First EditMode test: `Editor/Tests/GameFlowTests` (campaign order). Passes.

## 2026-11-24 (later): Level 3 cleanup, ways up, moving mages, door torches, unloading; project cleanup

### Level 3
- Less clutter, still messy:
  - Floor books cut by more than half on both floors (ground 900 to 380, hall 700 to 280).
  - Rubble 160 to 70, scorch marks 60 to 35.
  - Fewer stacks and tables in the Upper Hall.
- The grand staircase landing is 2 m tall (was 3 m), so a double jump gets you onto it.
- New ways up to the balcony:
  - A wall-jump shaft on each side: two bookcases 3.5 m apart out to the balcony edge.
  - Broken-bookcase steps on each side, a double jump apart.
  - The railing has gaps at each of these.
- Floating mages drift around (`Hover.wander`) and have 1 HP. The Upper Hall now has 6 (was 2), plus 2 balcony mages.
- Two giant braziers stand either side of the metal door (new `Ignite` component). They are unlit until the hall
  is cleared, then burst into low-poly flame and sparks with a bright light flash.
- Unloading (new `unloadOnEnter` on LevelManager areas):
  - Entering the Upper Hall shuts the hatch, then destroys the whole ground floor.
  - Walking through the metal door swings it shut behind you (new `GateDoor.Close`), then destroys the Upper Hall.
  - Each unload is followed by `Resources.UnloadUnusedAssets`. Area 3 has its own checkpoint.
- Fixed the L3_Flame material losing its glow (emission keyword wasn't saved).

### Cleanup
- Removed missing-script components from the Blue Olive candle prefabs (the "Point Light script missing" warnings).
- Deleted the old MovementTest sandbox scene, `GameManager` (its old wave spawner) and `ArenaBuilder`, along with every
  reference to them.
  - Removed it from the build list, which was also out of order: a build would have booted into the Tutorial. MainMenu is first again.
- Deleted unused Graybox materials and their dead accessors, and the leftover Assets/Screenshots folder.

## 2026-11-24: Level 3 area 2 (Upper Hall), Level 2 dressing, Level 1 wall fix

### Level 3: Upper Hall (`Scripts/Editor/LevelBuilder.Library2.cs`, scene rebuilt)
- Two storeys: the hall floor (y 13) and a U-shaped balcony (y 19) along both side walls and the back.
  A grand staircase leads up the middle: a central flight to a landing, then a flight to each side balcony.
  Hidden ramps along the step noses carry the player.
- When you step off the spiral stairs (z > 70), the area starts. Two hatch halves slide shut over the
  stair hole, the staircase hides, and a checkpoint is set at the top of the stairs.
  - New per-area `closeOnEnter` and `checkpoint` fields on LevelManager.
  - New `SlideShut` component.
- The hall is wrecked:
  - The front of the hall (z < 50) has caved in.
  - Bookcases are toppled, leaning or broken. Two fallen bookcases are walkable ramps up to the balcony.
  - A chandelier has crashed down, and tables are overturned.
  - The balcony railing is broken in places.
  - There is rubble, soot, a torn runner and books on both floors (merged into a few meshes).
  - Torches flicker and dust hangs in the air.
- Enemies: 2 floating mages, 2 balcony mages, and waves of 6 + 7 spawning on both floors.
- A giant riveted metal door in the back wall at balcony level. It opens when the hall is cleared.
  Behind it is a short corridor with the exit portal: area 3 ("Sealed Door"), where the boss room will go.
- `MergeByMaterial` now uses only LOD0 of props that have LODs.

### Level 2 (patched in place, `Scripts/Editor/LevelBuilder.Castle.cs`)
- 42 wall torches: curtain walls, both faces of the gate walls, the start wall and the castle wall.
- Wooden doors with stone frames at the foot of every round tower, a great door in the keep, and two posterns by the start.
- Grass clumps and flower patches across the open ground, thicker along the wall bases. Merged; about 590k verts.
- 170 more trees outside the walls (big ones close in, so they show over the walls).
- Removed two leftover screenshot cameras from the scene.

### Level 1
- Two Hills wall-run corridors were outside or on the fence line. They have been moved inside (scene and builder).

## 2026-11-23 (late night): New hero, Greenland dressing, Hovl VFX

### Player (`Oxford Runner → Setup Player Body`, `Scripts/Player/PlayerBody.cs`)
- Body is now Standout7 "LOFRE Player 01" (low poly), scaled 1.3x so the shoulders sit under the
  camera. Its URP material is switched to Standard. Head, hair, scarf and shoulder guards only cast
  shadows so they never block the view.
- Right hand is held out palm-down; the open spellbook floats above it, bobbing and swaying.
- Left hand moves per spell (new `CastGesture` on each Spell): Fireball thrust, Frost Lance sweep,
  Chain Lightning high crackling point, Arcane Missiles quick circles, Arcane Nova lift-and-slam.

### Level 1
- A few trees (SimpleNaturePack + RPGPP) and bushes/flowers around island edges.
- Rolling hills on a valley floor far below the islands, a ring of mountains on the horizon, clouds.
  Fog thinned (0.0018) so the mountains read. Exit portal uses the Hovl blue portal effect.
- Tower step pillars removed from the builder (user call). Level 2 was hand-edited by the user and
  saved as is (not rebuilt).

### VFX (`Oxford Runner → Setup Spell VFX`, also run by Setup Spells / Setup Combat Assets)
- Hovl Magic effects: sparks trail each bolt (core glow kept for parry reads), lightning aura on enemy
  bolts, new hits (explosion, snow, electro, star, pink sparks), Laser AOE for the nova, a blue magic
  shield while the parry barrier is up and a holy hit on a successful parry.

## 2026-11-23 (night): Wall-run momentum

- Wall-running keeps your entry speed (deceleration 0, was 3), lasts up to 4 s (was 3) and slides
  down slower (wall gravity 2.5, was 4). Grabbing a wall still refreshes the double jump and air dash.
- New player model, Level 1 trees/mountains and new spell VFX are waiting on the asset packs being
  imported into the project (they aren't in Assets/ yet).

## 2026-11-23 (evening): Parry feel, tower access, slide sections

### Parry
- The barrier is now a real collision sphere (1.1 m around the chest). Enemy bolts that touch it while
  it's up bounce off out in front, instead of only when they reach your body. Feels earlier, easier to see.
- The purple "PARRY" box draws after the bolts move (LateUpdate) and sits slightly ahead of the bolt,
  so it no longer trails. It appears from 40 m (was 30).
- Enemy bolts fly faster: 20 m/s (was 16).

### Levels (rebuild with the Oxford Runner menus)
- Towers are slimmer (2.5 m shaft, 4 m deck), the parapets are gone so nothing blocks shots on top,
  and a spiral of wall-runnable step pillars (about 2 m apart, double-jump height) climbs to each deck.
  Level 2 watchtowers got the same treatment.
- Slide sections: low tunnels (1.3 m roof, glowing edges) at the area transitions in Level 1, a tunnel
  at the Level 2 start, and both Level 2 gates are now low slots you slide under. Low beams to slide
  under are scattered through every area.
- More wall-running: wall-run corridors in Level 1, a second wall across pit 2, and wall-run slabs
  linking round towers together in Level 2.

## 2026-11-23 (later): Levels 1 & 2, campaign flow, fantasy font

### Campaign levels (`Scripts/Editor/LevelBuilder.cs`, `Scripts/Core/LevelManager.cs`)
Untextured flat-colour levels, each split into **areas**. Walking into an area starts its waves
(spawning from that area's pads); casters already standing in towers count toward the area too.
Clear every area and the **exit portal** opens (with a light beacon so you can find it).
- **Level 1 - Greenland** (`Oxford Runner → Build Level 1`): four grass islands over a void,
  joined by three pits you cross by wall-running (twin walls, one long wall with a rest pillar,
  staggered walls). Six square stone towers each house one caster. Rocks, terraces and wall-run
  slabs for cover and movement. 3 areas, 6 tower casters, 23 wave enemies.
- **Level 2 - Castle Grounds** (`Oxford Runner → Build Level 2`): three walled courtyards outside a
  castle, joined by gates flanked by big round towers. Lots of cylindrical towers to wall-run
  around, watchtowers with casters, barricade lines enemies hide behind. 3 areas, 6 casters, 34 enemies.
- Level 3 (library) and the final boss come later.
- Enemies no longer walk off ledges and die if knocked into a pit.

### Flow
- Play: Tutorial → Level 1 → Level 2 → main menu (until Level 3 exists). Health carries between levels.
- Top-of-screen level banner, objective and enemies-left counter. Debug: **K** kills everything alive.

### UI
- All UI now uses **Cinzel** (fantasy serif, SIL Open Font License, `Assets/Resources/Fonts`).
- Removed the menu tagline.

---

## 2026-11-23: Parry rework, 5 spells, enemy health bars, menus

### Parry reworked: pure collision
- **Right click** raises a magic barrier for **0.3 s** (translucent shell in front of you, visual only).
- An enemy bolt that **actually hits the player** (its collision with the player capsule) while the
  barrier is up is reflected toward the crosshair at **2x speed** (3x damage). No proximity or early catch.
- Bolt hit radius 0.3 → 0.2 so it matches the visible core (it hits when it looks like it hits).
- Removed the enemy's purple charge orb: it read as a timing cue and didn't line up. The bolt is the cue.
- Barrier blocks splash damage while up. White flash + hit-stop + "PARRY!" pop on success.
- HUD: purple square box with "PARRY" tracks the nearest incoming bolt (cyan while the barrier is up).
- Miss = 2 s cooldown; success = none. Tested at ~120 fps: 12/12 bolts parried pressing ~0.1 s before
  contact, including two bolts caught by one barrier.

### Main menu, pause menu, settings
- New `Scenes/MainMenu.unity` (first in the build) via `Oxford Runner → Build Main Menu`:
  **PLAY** (tutorial, then the sandbox arena until the real stages exist), **TUTORIAL** (tutorial only,
  then back to the menu), **SETTINGS** (mouse sensitivity, saved in PlayerPrefs), **EXIT**.
- **Esc** in a level opens the pause menu: Resume / Settings / Main Menu (time stops).
- `Core/GameFlow.cs`: scene routing + `GameSettings`; `UI/MenuUI.cs`: code-built uGUI helpers.

### Five spells (`Scripts/Spells/`, `Oxford Runner → Setup Spells`)
| Key | Spell | What it does |
|---|---|---|
| 1 | Fireball | Straight shot, small blast (15). |
| 2 | Frost Lance | Very fast, pierces up to 3 enemies (12 each), slows them 50% for 2.5 s (they tint blue). |
| 3 | Chain Lightning | Instant hitscan (16), arcs to 3 more enemies within 9 m (−25% per jump), needs line of sight. |
| 4 | Arcane Missiles | 3 homing missiles (8 each) fanned out; they curve onto the nearest enemy in front. |
| 5 | Arcane Nova | Blast around you: 20 damage + knockback in 7 m, erases enemy bolts in range. 6 s cooldown. |
- `ProjectileSpell` gained multishot, pierce, homing, slow and knockback options; `DamageInfo`
  carries slow/knockback; new `ChainLightningSpell` and `NovaSpell` spell types.
- Bottom-centre spell hotbar with cooldown overlays; mouse wheel cycles spells.
- All 5 are unlocked on the Player prefab for testing (`SpellCaster.unlockedCount`).

### Enemy health bars (`Scripts/UI/WorldHealthBar.cs`)
- Camera-facing bar above every enemy and training dummy, with a draining "recent damage" trail.
- Common enemies have a base of **50 HP**; arena waves now ramp 50 → 95.

### Player model
- No textured human mage exists in the current assets (only the Kevin Iglesias purple mannequin
  and its mage hat), so the player model is unchanged for now.

---

## 2026-11-22: Player body, test arena, tutorial, combat & parry

A big day: the player went from a floating camera to a full-body spellcaster, the old obstacle
course was replaced with a purpose-built movement arena, a guided tutorial level was added, and
enemies now shoot parryable bolts at you.

---

### Controls (current)

| Input | Action |
|---|---|
| `W A S D` / mouse | Move / look |
| `Space` | Jump, double jump, wall jump, slide jump (while sliding) |
| `Left Shift` | Dash |
| `Left Ctrl` (hold) | Slide |
| `A` / `D` toward a blue wall (airborne) | Wall run |
| `Left Click` (hold) | Cast the selected spell (Fireball) |
| `Right Click` | **Parry** (0.3 s window, 2 s cooldown on a miss) |
| `1`–`9` | Select unlocked spell |

---

### 1. Full-body first-person player (`Scripts/Player/PlayerBody.cs`)

Mirror's Edge-style "body awareness": you can see your own body, hands and spellbook.

- **Player model**: Kevin Iglesias humanoid with an 8-way run/idle blend tree driven by the
  player's actual velocity. Playback speed scales with movement speed and slows in the air.
- **Open spellbook in the right hand** (Blue Olive Studio *Magic Book-Opened*): the right hand is
  pinned to the camera with humanoid IK so the book always sits in the lower-right of the view,
  with sway when turning and bob while running.
- **Left hand casts**: the left palm thrusts out whenever a spell is cast (and when parrying);
  spells spawn just in front of it.
- **Head/neck hidden**: the neck bone is collapsed so the camera never sees inside the face.
- **Slide pose**: the body leans back from the hips with the legs stretched forward, and the left
  hand trails by the hip instead of swinging into view.
- **Wall-run lean**: the body tilts with the camera.
- **View-aware camera clearance solver**: each frame, upper-body bones that are *on screen* and
  too close to the camera push the body backward (with per-bone radii and a dead zone to avoid
  jitter). This removes clipping when looking down, running, landing and sliding without moving
  the body when it isn't visible.
- **Editor tools**: `Oxford Runner → Setup Player Body` builds the animator controller and rigs
  the player; `Oxford Runner → Rebuild Player Animator` regenerates just the controller.

### 2. Movement test arena (`Scripts/Editor/ArenaBuilder.cs`)

`Oxford Runner → Rebuild Test Arena` replaces the old obstacle course with four zones, one per
quadrant. The arena is still 100 × 100 and the enemy doors and wave spawner are unchanged; the
lanes from each door to the centre are kept clear.

| Quadrant | Zone |
|---|---|
| NW | **Wall-run gauntlet**: 5 alternating zig-zag panels + a 30 m parallel corridor for chained wall jumps |
| NE | **Climb tower**: a 14 m circular "chimney" you spiral up by wall-jumping (hold `D`, keep jumping), rest ledges, summit deck, all inside a circular run ring |
| SW | **Slide zone**: a 24 m low tunnel, five low gates, and a 15 m slide-jump gap |
| SE | **Box yard**: 28 procedurally placed crates (seeded rejection sampling, no overlaps, guaranteed walkways) + wall-run slabs |

### 3. Tutorial level (`Scenes/Tutorial.unity`, `Scripts/Tutorial/`)

`Oxford Runner → Build Tutorial Scene` generates a linear rooftop course over a void, one skill
per station. Each gap/obstacle is sized so you *must* use the skill being taught.

| # | Station | Challenge |
|---|---|---|
| 1 | Move | Walk and look |
| 2 | Jump | 4 m gap |
| 3 | Double Jump | 2.2 m ledge |
| 4 | Dash | 10 m gap |
| 5 | Slide | Low tunnel (can't be jumped over) |
| 6 | Slide Jump | 15 m gap |
| 7 | Wall Run | 14 m gap along a wall |
| 8 | Wall Jump | Parallel walls, climb 3 m |
| 9 | Cast | Destroy 5 floating targets to open a gate |
| 10 | Parry | Reflect a sentinel's bolt back into it (only parried bolts hurt it) |
| 11 | Ready | Portal into the arena |

- **Prompt card** (top centre) shows step number, skill name, the exact keys, and live progress
  (e.g. targets destroyed). It resizes to fit its text and animates in.
- **Checkpoints** at every station (green pads). Falling into the void respawns you at the last
  one with a "Back to checkpoint" message.
- **Timer + persistent best time** (`PlayerPrefs`), shown with a results card ("NEW BEST!",
  respawn count) before the arena loads.
- Build settings: `Tutorial` (first) → `MovementTest` (arena).

### 4. Combat: enemy spells, player health, parry

**Enemies shoot** (`Scripts/Enemy.cs`, `Spells/Enemy_Bolt.asset`)
- Slow, readable magenta bolts (16 m/s, 10 damage) with a glowing core and trail.
- Every shot is telegraphed: the enemy plants its feet, a **charge orb grows at its hand**, and
  its face pulses faster as the shot approaches.
- Fires only with line of sight; first shots are staggered so waves don't fire in unison.
- Optional `onlyReflectedDamage` flag (used by the tutorial sentinel).

**Player health** (`Scripts/Player/PlayerHealth.cs`)
- 100 HP with brief invulnerability after a hit. On death: respawn at the last checkpoint with
  full health, or restart the scene if there is none.

**Parry, no blocking** (`Scripts/Player/PlayerParry.cs`)
- `Right Click` opens a **0.3 s** parry window. A window that catches nothing starts a
  **2 s cooldown**; a successful parry has no cooldown.
- Any enemy bolt that touches you, or comes within 2.5 m of the camera, during the window is
  **reflected toward the crosshair** from your left hand: 2× speed, 3× damage, turns cyan, and
  can't be parried again. One window can catch several bolts.
- **"Parry now" cue**: purple corner brackets close in on the crosshair while an incoming bolt is
  inside the parryable range, and meet at impact. The cue appears at the earliest moment a press
  will succeed, so pressing any time it's visible catches the shot.
- Feel: 0.07 s hit-stop, "PARRY!" pop, cyan flash, +10 HP heal per parry (all tunable).

**HUD** (`Scripts/UI/PlayerHUD.cs`)
- Health bar with a trailing damage bar, parry meter under the crosshair
  (white = ready, cyan = window open, grey countdown = cooldown), red hit flash, parry cue.

**Factions & damage** (`Scripts/Combat/IDamageable.cs`)
- `IDamageable` + `DamageInfo` + `Faction`: projectiles only damage the opposing side, and
  targets know whether a hit came from a parried projectile.
- Projectiles copy their stats at launch so they can be modified per-shot (reflection) and keep
  a registry of live projectiles for the parry scan.

`Oxford Runner → Setup Combat Assets` creates the bolt prefab/spell, arms the Enemy prefab, and
adds health + parry + HUD to the Player prefab.

### 5. Shared systems & tooling

- `Assets/Prefabs/Player.prefab`: one player shared by every scene.
- `Scripts/Core/Checkpoints.cs`, `KillVolume.cs`, `SceneExit.cs`: reusable respawn / level-exit
  pieces for future levels.
- `Scripts/Core/Aim.cs`: crosshair aiming shared by casting and parry reflection.
- `FirstPersonController.Teleport()`, plus `ForceSlideHeld` / `ForceMoveInput` hooks for testing
  and scripted sequences.
- `Scripts/Editor/Graybox.cs`: shared helpers for the level builders. Every generated level is
  deterministic and can be rebuilt from its menu item.

---

### Known issues

- A `NullReferenceException` from `UnityEditor.Graphs.Edge` can appear in the console. It's an
  editor bug from the Animator window, not game code; closing the Animator window clears it.
- Enemies walk straight at the player and can snag on tower walls and crates.
- Tutorial wall-run / wall-jump gap sizes were derived from the controller's numbers and need a
  real playtest; tweak them in `TutorialBuilder.cs` and rebuild.
- Arena waves were retuned for 50 HP enemies but still need a real balance pass with the new spells.

### Next up

- Three real levels with a difficulty ramp (health carried between scenes)
- Opening / closing UI (main menu, game over / victory) and persistent settings
- Score + persistent high score
- Unlockable spells as level rewards (ideally ones that interact with parry)

## 2026-11-23 (late night 2): Spell tuning
- Fireball blast radius 2 → 4 m, explosion effect doubled in size.
- Arcane Nova effect is now a quick burst (Hovl Explosion + pink sparks at 1.8x speed, gone in ~1.5 s) instead of Laser AOE.
- Parry shield effect moved down to the centre of the view (new `barrierVfxOffset` on PlayerParry, default (0, -0.6, 0.9)).

## 2026-11-23 (late night 3): Heavy nova, area-by-area progression
- Spells can have a wind-up: new `castTime` / `castMoveMultiplier` on Spell, `BeginCast` hook. SpellCaster runs the
  wind-up (gesture at start, spell fires at the end, no other casts meanwhile) and exposes `MoveMultiplier`, which
  FirstPersonController uses to slow the player (also in the air) until the spell goes off.
- Arcane Nova: 0.3 s cast, player at 25% speed while casting, 45 damage, knockback 30 (more lift). Uses the Hovl
  "Red energy explosion"; it starts charging on the player at cast start, sped up by the new `ParticleTimeShift` so its
  burst lands right when the damage hits. The left-hand slam now lands at ~0.3 s too.
- Enemies stagger longer after big knockbacks, so they fly further (tested: thrown ~24 m).
- LevelManager runs areas in order: only the current area can start, later areas' garrisons sleep, and clearing an
  area sinks its `gates` (new field per encounter) and wakes the next area. Portal opens after the last area.
  HUD shows "Area n/N: name" and that area's enemy count.
- LevelBuilder: `AreaGate` (tall purple translucent barrier, `AreaGate.mat`). Level 1 gates at the far edge of the
  meadow and hills; Level 2 gates fill both castle wall gateways. Level 1 rebuilt; Level 2 gates added straight into
  the hand-edited scene (not rebuilt).

## 2026-11-23 (late night 4): Spell pass, ramps, cubbies, all-at-once spawns
- Fireball hit: Explosion + red/yellow sparks + smoke burst (3.5 s).
- Frost Lance: freezes 1.5 s instead of slowing. Frozen enemies (`Enemy.Freeze`, `DamageInfo.freezeDuration`) can't
  move/turn/cast (Rigidbody FreezeAll, wind-up cancelled), take 2x damage, turn icy, and show "FROZEN" overhead.
- Arcane Missiles: 5 missiles, 14 dmg, 60° fan, 120° homing cone, turn rate ramps up with flight time (no orbiting).
- Chain Lightning -> Lightning Strike (same asset/class): thick blue bolt from the sky onto the aimed spot, aim-assist
  snaps to enemies within 4 m, 28 dmg AOE (4.5 m) + small knockback, blue Electro hit x2 + blue sparks, 0.8 s cooldown.
- LevelManager `spawnAllAtOnce` (default on): an area spawns every wave's enemies at once; max 3 per spawn point,
  spread around it.
- Level 1: every tower has a 32° ramp up to its deck; 8 cubbies (3-sided roofed shelters). Level 2: 9 cubbies added
  to the hand-edited scene (builder updated to match).

## 2026-11-24: Missile lock-on, enemy types, wall-aware AI
- `MissileLockOn` (on Player, added by Setup Spells): with a `lockOn` spell selected (Arcane Missiles), enemies in view
  get red [ ] brackets; holding the crosshair on one (10° cone, 0.35 s) closes the bracket to green + "LOCKED ON".
  Locked missiles (`SpellProjectile.ForceTarget`) arc over cover, ignore everything but the target and live 8 s.
  Missiles: 5 x 14 dmg, 70° fan, speed 30.
- Enemy rewritten with `EnemyKind` (Caster / Skeleton / Mage / Commander), per-prefab health/speed/interval scales,
  ranged keep-distance band + strafing, melee (wind-up >= 0.5 s, sword raise/chop, reach + front-cone check),
  Commander dash (5 s cooldown, dodges incoming player spells sideways, dashes out if crowded).
  All kinds: wall/ledge avoidance (sphere-probe ahead, scan +-30..150°, sticky side, stuck detection), ranged kinds
  reposition after 1 s without line of sight. `Enemy.All` registry, `VisualBounds`.
- New menu "Oxford Runner/Setup Enemy Types" (EnemySetup.cs): Enemy_Skeleton (bone, Sword_01, 1.2x HP, 1.35x speed,
  15 dmg), Enemy_Mage (robe + Kevin Iglesias hat, 0.6x HP, 12-20 m, new weak Enemy_MageBolt 6 dmg),
  Enemy_Commander (1.2x size, crimson/gold + cape, 2.5x HP, 1.5x speed, 0.55x fire interval, 16-26 m, dash).
- LevelManager mixes types per area: 1 commander (4+ enemies) or 2 (9+), rest alternate skeleton/mage starting with
  skeleton. Level 1 rebuilt; Level 2 scene got the type prefabs set (not rebuilt). Tower garrisons stay Casters.

## 2026-11-24 (2): Tuning, stun, parry aim, projectile fix
- Projectile fix: on impact the Visual's local-space particles (the glowing ball) were left frozen at the hit point for
  1 s; now cleared, only world-space trail particles fade. Projectile centre now ends at the sphere-cast contact
  (was snapped to the surface point).
- Lightning stuns 0.8 s (`DamageInfo.stunDuration`, `Enemy.IsStunned`): stops in place, no attacks, "STUNNED" overhead.
- Parry reflects at the visible enemy closest to the crosshair (within 15°, `reflectAssistAngle`), else at the aim point.
- Skeleton HP 0.8x / speed 1.6x, Mage HP 0.4x, Commander scale 1.6 + Hovl "Debuff" aura.
- Fix: Setup Enemy Types deleted + recopied the type prefabs (new GUIDs), breaking the levels' references so only
  Casters spawned. Now saves over the existing prefabs (GUIDs kept). Level 1/2 references re-set. Lightning stun 1 s.

## 2026-11-24 (3): Parry homing, spell switch readability, lower hand
- Parried bolts lock onto the crosshair enemy (25° assist) via `ForceTarget` and home in (720°/s, ramping), so they
  track moving targets.
- Hotbar ~1.5x bigger; switching spells flashes the spell name (in its colour) under the crosshair, fading in ~0.8 s.
  (No page-turn effect in the book pack, so no page animation.)
- Book hand lowered (handOffset y -0.3) and book hover 0.14 (still floating).

## 2026-11-24 (4): Spell books, textured Level 1, gate doors
- Spell books (Setup Spell Books / SpellBookSetup.cs): one GreyratsLab book per slot (Fire, Ice, Air, Necromancy,
  Earth). On spell change the new book pops up from the palm (spin + ease-out-back scale) with a coloured spark burst
  (BookSwap_* one-shots). Pack materials converted URP -> Standard using their saved _BaseColor. Old open book hidden.
- Hotbar bigger again (235x105); switch name under crosshair smaller and quicker.
- Arcane Nova damage 58.5 (1.3x). Skeleton HP 0.5x. Tower garrison casters 20 HP (L1 builder + L2 scene).
- Level textures: generated seamless grass / dirt / rock / bricks / wood / roof-tile textures (LevelTextures.cs) drawn
  with new world-space `OxfordRunner/Triplanar` shader; applied by name to every L1/L2 level material.
- Level 1: wooden fences 2.5 m inside every island edge (gaps at pit crossings) with invisible 30 m walls, flowers /
  bushes / small trees planted behind the fences, 60 trees in the valley below.
- Level 2: slide gates replaced with giant two-leaf wooden gate doors (`GateDoor`, swing open when the area unlocks);
  the purple area barriers are now invisible (still stop wall-running over the walls). Patched into the hand-edited
  scene; builder matches.
- Spell books flipped 180° about their cover normal (`spellBookSpin`). Book size no longer drifts on rapid switching
  (each book's scale captured once). Hotbar key numbers no longer clipped.
- Cubbies get a cosmetic bubbling cauldron (LowPolyDungeons Pot_01 + Particles_Pot); L1 rebuilt, L2 scene patched.
- Fireball projectile now carries Kevin Iglesias' Human_Spell_Fireball_Large (looping) instead of Hovl red sparks.
- Cauldrons removed (builder + both scenes). Lightning Strike 24 dmg (was 28). Frost freeze 2 s, frozen enemies take
  2.5x damage. User hand-edited Level 1 (removed a box): patch it in place, don't rebuild.

## 2026-11-24 (5): Level 2 ramparts, palisades, trees
- New builder pieces: `Rampart` (6 m walkable wall, 4 m wide walk, wall-runnable front face, merlons on the front edge
  with gaps where ramps land, 1 m back rail, optional arch), `RampartTower` (r 3.5 watchtower + ramp from the walk up to
  its deck, one garrison caster), `Palisade` (3.5 m wall-runnable wooden wall). `Ramp` takes an optional `bottom` height.
- Area 1: rampart across z 55 (x -32.5..32.5) with an 8 m arch over the ground route, garrison towers at (+-36, 55),
  ground ramps at x -14 / +10, two wall-run speed lanes at x +-20. Old watchtowers removed; spawns 1/2 moved to z 46/47.
- Area 2: side ramparts at x +-47 (z 73.5..120.5), towers at both ends (4 garrisons, crossfire), ground ramps at z 100.
  Old watchtowers removed; edge barricades moved in to x +-38, spawns 4/5 moved to (-40,92) / (40,106).
- Area 3: 5 palisades in the open gaps of the keep approach.
- Big trees (64) and a grass apron outside the curtain walls, background only.
- Patched into the hand-edited scene via `Level2*` builder methods; builder matches.

## 2026-11-24 (6): Parry fixes, spawn slide tunnels removed
- Parry [ ] cue now sits exactly on the bolt (the 0.05 s lead put it 1 m ahead) and PlayerHUD runs after the camera
  (`DefaultExecutionOrder(100)`), so it no longer lags when you turn.
- Timing read: brackets close in as the bolt approaches and turn cyan once it will reach the barrier within the 0.3 s
  window (press now). "PARRY" text kept.
- Barrier catch radius 1.1 -> 1.8 (script default + Player prefab); shield visual stays at 0.9 m.
- PlayerParry reads the click before projectiles move (`DefaultExecutionOrder(-50)`).
- Removed the slide tunnel next to the spawn in Level 1 (A_Start) and Level 2 (A_OuterField): builder and scenes.
  Rule from now on: no slide tunnel near the spawn in any map.

## 2026-11-24 (7): Level 3 "The Library" (area 1)
- New menu "Oxford Runner/Build Level 3 (Library)" (`LevelBuilder.Library.cs`, LevelBuilder is now partial). Enclosed
  hall x -32..32, z 0..120: ground floor (0..12), ceiling slab with a 10 m hole over the rotunda (z 64), upper floor (13..28).
- Area 1 "Ground Floor": spawn inside the shut entrance doors. Middle: red runner, 12 reading tables (benches, candles,
  books), candle stands, rotunda with candle ring + lecterns. Sides: 7 m bookcase walls at x +-12.5 (slide-under shelves
  in the gaps at z 36 / 92), 10 bookcase stacks per side (6 / 8 m), outer aisle along the floor-to-ceiling wall bookcases
  (fallen bookcases to slide under at z 46 / 98), rolling ladders. Every bookcase is wall-runnable.
- 4 floating mages (Enemy_Mage, kinematic, 30 HP, `Hover` bob, Hovl magic circle) over the stacks; waves 5 + 6.
- Lighting: no sun; 6 chandeliers (procedural iron ring + Blue Olive candles, one pixel light each), wall torches
  (LowPolyDungeons Light_08 + flickering light) on stone pilasters, warm ambient + dust fog.
- New generated full-colour "Books" texture (book spines on shelves, 2 m tile, world-aligned) for any `*bookshelf*` material.
- Area 2 "Upper Floor" placeholder: clearing area 1 lowers the spiral staircase (`DescendOnOpen`, new LevelManager gate
  type) 13 m into the rotunda. The steps are visual; a hidden ramp of tilted colliders carries the player. Upstairs is an
  empty lit hall with the exit portal (z 108) for now.
- GameFlow campaign now Tutorial -> L1 -> L2 -> L3. `LightFlicker` for torches/chandeliers.
- Level 3: ~900 scattered books (45 piles + loose) on the ground floor, merged by `MergeByMaterial` into 16 static
  meshes (per material per 30 m band, no shadows). Meshes live in the scene file (Level3.unity ~18 MB).
- Style rank + score (`UI/StyleMeter.cs`, added by LevelManager): ranks D Decent, C Cast Master, B Battle Mage, A 2nd Rate Mage,
  S Sorcerer, SS Slinging Sorcerer, SSS Supreme Spellslinging Sovereign. Points for hit, kill, combo (hit on frozen/stunned),
  airshot, parry, parry kill; hit taken knocks the meter down, death resets it, 5 s idle drains it. Score (x rank multiplier)
  is per level (every level starts at D / 0); best per level saved (`BestScore_<scene>`) and shown on the main menu. Last 3 events shown under the rank (stacks x2..).
- Style meter moved to the top-left.
- Tutorial rebuilt as an enclosed dungeon (`LevelBuilder.Dungeon.cs`: stone hall, dark pit, skylights with a steep sun +
  faint light shafts, torches). Wall-runnable walls = teal wood with glowing rune bands + diamond glyphs (`RunePanel`);
  plain stone isn't runnable. Slide-jump runway 6 m -> 16 m. New spell school after the fireball range: Frost Lance,
  Lightning Strike, Arcane Missiles, Arcane Nova rooms, each behind its own gate (harmless practice skeletons /
  floating targets). Course now ends at z 281.
- Tutorial lighting: skylights and light shafts removed. Solid ceiling with warm glowing panels between beams every 6 m,
  shadowless straight-down sun + bright flat ambient, so the whole hall is evenly lit. Torches kept for warmth.
- Tutorial checkpoint pads: flush with the floor and no collider (they snagged movement).
- Spells now cast in SpellCaster.LateUpdate (execution order 50), after the controller applies this frame's mouse look;
  before, shots used last frame's view and drifted off the crosshair while turning/moving. Wind-up spells too.
- Parry: reflected bolts explode with the fireball blast (`PlayerParry.reflectedImpactVfx` = Fireball_Hit), blast radius >= 2.5 m.
- LevelManager: `commandersPerArea`, `skeletonShare`. Level 2: 1 commander per area, 70% skeletons, waves 5+6 / 5+6+6 / 6+7,
  4 per spawn point. Garrison enemies (all levels) are 1.35x bigger with a Hovl Buff aura, an orange light, an additive
  halo and a tall beam (`GarrisonGlow.mat`) so the last ones are easy to find. Builder sets this via `SetGarrisonAura`.

## 2026-11-24 — audit fixes
- GateDoor swings to absolute poses (closed rotation saved in Awake, StopAllCoroutines): Close() mid-open no longer over-rotates (Level 3 metal door).
- Homing projectiles with no target re-search at most every 0.25 s (was every frame per missile).
- StyleMeter saves the best score only if the level was cleared (LevelManager.LevelCleared).
- DescendOnOpen merged into SlideShut (Open() = same one-way slide); Level 3 SpiralStaircase patched in place, builder updated.
- Reuse: MissileLockOn uses Targeting.Clear; Lightning aim uses Aim.PointUnderCrosshair (new origin/dir overload);
  PlayerHUD + TutorialManager build UI via MenuUI (Canvas/Rect/Label).
- Removed dead code: PlayerParry.HitStopStarted, SpellCaster.UnlockNext, PlayerHUD.OnHealed.
- Perf: SpellCaster hotbar GUIStyles cached; HUD health text only rebuilt on change.
- Tests: ParrySphereTests (SpellProjectile.HitsSphere now public). EditMode 4/4 pass.

## 2026-11-24 — Level 3 upper hall fixes
- Hall waves spawn only in front of the grand staircase (floor z 58/84) and on its landing; mages spawn on the balconies
  (`Encounter.mageSpawnPoints`, filled first, overflow goes to the main points). Spawns behind the staircase removed:
  skeletons walked straight into it and got stuck underneath.
- Grand staircase side flights are solid down to the hall floor (no crawl space underneath). Removed the floating-step path.
- Hall garrison (floating mages) wakes when you walk in at the top, not when area 1 clears (`Encounter.garrisonWakesOnEnter`),
  so nothing shoots down the stair hole while you climb. Waves already started at the top.
- Spiral staircase: ramp is 4 short pieces per step sized to the outer edge (no gaps/ledges), plus an invisible
  outer railing from the 3rd step up.
- FirstPersonController: walking up a ramp counts as grounded (unless you just jumped). Before, any upward speed meant
  "in the air": weak air control + extra gravity, which made stairs feel like sliding out. Scripted climb: 347/379 air
  frames before, 0/374 after.
- Level 3 rebuilt from the builder (hierarchy diff: only the staircase, spawn points and ramp changed).
- Debug K (editor/dev builds) kills only the live area's awake enemies; before, it also deleted later areas' sleeping garrisons (Level 3's floating mages).
- Level 3 Upper Hall waves 6+7 -> 10+12 (22 at once: 10 skeletons, 10 mages, still 2 commanders), plus the 8 floating mages. Spawn capacity: 15 main + 9 balcony slots. Level 3 rebuilt.

## 2026-11-25 — camera judder, Level 1 spawns, level results screen
- Camera judder while strafing + turning fixed (FirstPersonController): the controller wrote `transform.rotation` every
  frame, which reset Rigidbody interpolation, so the camera only moved on physics ticks (worst sideways with A/D).
  Now the camera's world rotation is set per frame from yaw/pitch, the body turns only via `rb.MoveRotation`, and
  movement / wall checks use the yaw directly (`Forward`/`Right`). Test at 4 Hz physics: per-frame camera speed
  median 0 (24 m/s jumps) before -> 8.0 m/s every frame after.
- Level 1 spawn points moved >= 2.5 m inside the fences (Meadow 3, Hills 4, Gate 2), patched in Level1.unity and the
  builder. LevelManager no longer spreads stacked spawns through walls/fences (linecast, tries 6 directions).
- Level results screen (`UI/LevelResults.cs`): entering a campaign portal freezes time and shows time, style score
  counting up and a bar filling D -> rank earned (letter pops per rank), NEW BEST, then click/Space continues.
  SSS = 8k / 15k / 20k (Level 1/2/3); ranks start at 0/20/35/50/65/80/100 % of that. Pause menu stays shut meanwhile.
- Tests: LevelResultsTests (rank thresholds). EditMode 5/5.

## 2026-11-25 — pre-alpha perf + movement check
- Rendering: player camera now uses deferred shading (`Core/DeferredFog.cs` on Player.prefab's camera). Forward
  rendering drew every mesh again per point light (58 torches/candles in Level 3): ~10,140 draw calls, Camera.Render
  34 ms with almost no enemies. Deferred: ~1,000 draw calls, 5 ms. 30-enemy Upper Hall fight: whole frame 6.7 ms avg,
  8.1 ms worst, no spikes. Built-in deferred skips fog on opaque objects, so `Resources/DeferredFog.shader` re-applies
  the scene's exponential fog from depth (image matches forward within ~0.6% brightness). No MSAA in deferred.
- Mouse look is applied at the end of FirstPersonController.Update (was LateUpdate), before animation IK, so the
  hands/book no longer trail the view by a frame. Judder test (4 Hz physics): 0 frozen camera frames of 411.
- Fewer per-frame strings: LevelManager HUD text and StyleMeter feed lines only rebuilt when they change.
- Movement smoke test: jump peak 1.31 m, dash 5 m, slide on/off, W/D directions correct.
