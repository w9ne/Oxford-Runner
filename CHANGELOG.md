# Oxford Runner: Development Log

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
- Arena waves now shoot back with the old wave settings, so later waves are much harder than
  before and need rebalancing.

### Next up

- Three real levels with a difficulty ramp (health carried between scenes)
- Opening / closing UI (main menu, game over / victory) and persistent settings
- Score + persistent high score
- Unlockable spells as level rewards (ideally ones that interact with parry)
