# Oxford Runner: Development Log

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
