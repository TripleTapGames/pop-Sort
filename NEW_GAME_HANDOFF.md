# New game handoff: balls, funnel, conveyor, and trays

Prepared from the Pop Sort project on 2026-09-21.

## Prompt to use in the new game chat

> I am creating a new Unity game. Use this document as the reference for the shared ball mechanics: balls fall with 2D physics, pass through a funnel, enter a conveyor belt with limited slots, and collect into matching trays at the bottom. Preserve the useful behavior and fixes described here. The new game's upper playfield, release rules, theme, art, and level design can differ. Inspect the new project before implementing. If the original source is available, adapt the listed components; otherwise implement from this specification. Build a playable prototype of the full ball-to-tray flow first. Do not assume that the original game's tutorials, analytics, or monetization are requirements for the new game.

## Scope and source availability

The user has confirmed similar ball mechanics and physics, a conveyor belt, and bottom tray collection. The new game's release mechanic and other rules are still to be defined.

This file is a self-contained design and implementation reference, not a Unity package. It does not contain the source code, sprites, prefabs, or animations. File paths below are relative to the original project. The original local checkout is `/Users/pavanreddy/pop-Sort`; another chat or machine may not have access to it. If source access is unavailable, use the behavioral specification below and build replacement assets.

The source project uses Unity **2022.3.62f3**, Unity Splines **2.8.4**, TextMeshPro **3.0.7**, and URP **14.0.12**. These are the inspected project's versions, not mandatory versions for a new project. Match APIs and rendering assets to the new project's installed packages. The spline implementation also uses Unity Mathematics. Input currently uses Unity's legacy `Input` API.

## Gameplay flow

1. Release colored balls from the upper playfield. In the original game, tapping a holder releases its balls in a short staggered burst. Only an exposed holder can be tapped.
2. Balls fall and collide with each other and the funnel walls using 2D physics.
3. The funnel manager detects balls near the outlet and guides one ball at a time toward it.
4. When a free conveyor slot passes close enough, the ball attaches and smoothly seats into that slot.
5. Seated balls circulate until they pass a matching active tray's pickup area.
6. A matching tray reserves a position immediately and animates the ball into it. A full tray finishes its landing and completion sequence, then the next tray moves forward.
7. The level wins when all tray columns finish. The original loss rule is a full belt with no ball matching an active tray for a configured delay.

The conveyor has a fixed number of occupied or free slots. Waiting funnel balls are not belt occupants. Trays can collect while other balls are still falling or waiting in the funnel.

## Ball ownership and physics states

Keep movement ownership explicit so physics and scripted motion never compete.

| State | Movement and collision behavior |
| --- | --- |
| `InGrid` | Waiting for release; kinematic body with trigger collider in the original implementation. Holders may represent balls that have not been spawned yet. |
| `Falling` | Dynamic Rigidbody2D, gravity enabled, solid CircleCollider2D. |
| `FunnelWaiting` | Normally dynamic and solid, so waiting balls form a physical pile. The ball selected for extraction becomes kinematic with a trigger collider while being guided through the outlet. |
| `Queued` | Attached to a conveyor slot; Rigidbody2D simulation disabled. Includes the brief seating animation, so this state alone does not mean fully seated. |
| `TrayLanding` | Reserved by a tray; physics disabled and collider disabled. Scripted landing follows the tray's moving target. |
| `InTray` | Landed, physics disabled; released back to the pool when its tray is cleared. |

Use an object pool. On reuse, reset velocity, angular velocity, collider state, parent, scale, rotation, sorting, callbacks, and animation state. Track all active balls centrally, including airborne balls, so retry can release everything. Do not modify the pool's active collection while enumerating it.

Count balls still scheduled for release separately. Tapping the last holder does not mean every ball has spawned or reached the conveyor.

## Funnel behavior and jam prevention

The funnel combines physical waiting with scripted extraction:

- Scan the pool's active balls for `Falling` balls whose centers are inside the outlet capture radius.
- Register each captured ball once as `FunnelWaiting`.
- Select the waiting ball closest to the outlet. Keep other waiting balls dynamic.
- Guide the selected ball toward the exit with a kinematic trigger body so it cannot wedge against the lip or block the pile.
- Attach it only when belt capacity is available and an unoccupied slot is within the catch distance.
- Keep slot occupancy consistent when attaching, collecting, and clearing the level.

A previous scene used a capture radius of **0.3**, which could miss a stable pile above the outlet. It was changed to **1.2**. Capture now scans active pooled balls instead of a physics query limited to 32 collider results. Preserve both changes when reusing the source. The radius must be retuned if the new game uses a different world scale or funnel geometry; too small can miss a jam, too large can start extraction visibly early.

The physical funnel is authored in `Assets/Game/Scenes/GameScene.unity` under `Game/Funnel`, including `FunnelExitPt` and wall objects. Copying scripts alone does not recreate its geometry.

## Conveyor behavior

`SplineConveyorBelt2D` distributes slot transforms around a closed spline. Balls become children of slots and move with them. Its speed is measured in **loops per second**, not world units per second.

`BeltQueueManager` owns occupied slot indices and each ball's seating progress. It attaches without snapping, then interpolates local position to zero. Only seated balls can be collected. `OnBallSeated` fires when seating completes, before possible collection that frame.

Keep `BeltItemCount` separate from the count including funnel/pending balls. Capacity and fullness visuals should use actual belt occupancy.

## Tray collection

Each tray column has an ordered sequence, with only the front tray accepting balls. A ball must match its color, pass through that column's pickup band, and find remaining capacity. A transitioning column temporarily rejects intake.

Reserve capacity at acceptance time, before the landing animation. This prevents two incoming balls from claiming the same position. Exclude accepted `TrayLanding` balls from the belt's remaining inventory.

Landing uses a short eased movement with horizontal drift and impact squash. Resolve the target position each frame because the tray can move. After the last landing, play completion feedback, clear pooled balls, and slide the next tray forward.

Pickup areas are rectangular bands around authored transforms. Place them on the belt path. The current manager supports up to three pickup columns; extend its pickup mapping explicitly if the new game needs more.

## Reference tuning values

These are current serialized values or explicitly identified script defaults. Treat them as starting points, not universal physics constants.

| Setting | Reference value | Source |
| --- | --- | --- |
| Physics2D gravity | `(0, -9.81)` | Project settings |
| Fixed timestep | `0.02 s` | Project settings |
| Physics velocity / position iterations | `8 / 3` | Project settings |
| Ball prefab local scale | `(0.8, 0.8, 0.8)` | Ball prefab |
| Circle collider local radius | `0.18181819` | Ball prefab; world radius also depends on scale |
| Rigidbody mass / linear drag / angular drag | `10 / 0 / 0.05` | Ball prefab |
| Rigidbody interpolation / collision detection | Interpolate / Continuous | Ball prefab |
| Falling gravity scale | `1.5` | Ball component on prefab |
| Physics material friction / bounciness | `0.4 / 0.5` | `BallPhysics.physicsMaterial2D` |
| Script bounce retention / minimum bounce speed | `0.699 / 0.35` | Ball prefab; script's retention default is `0.22` |
| Release velocity / release interval | `1.5 / 0.12 s` | Game scene |
| Funnel capture radius / extraction speed | `1.2 / 6` | Game scene; world units and world units/s |
| Exit tolerance / slot catch distance | `0.2 / 0.5` | Game scene |
| Belt seating duration | `0.12 s` | Game scene |
| Tray pickup half-width / half-height | `0.5 / 0.05` | Game scene, all three columns |
| Tray landing / impact duration | `0.12 / 0.12 s` | Game scene |
| Tray horizontal drift / impact squash | `0.12 / 0.12` | Game scene |
| Tray completion hold / forward slide | `0.45 / 0.35 s` | TrayColumn script defaults |
| Full belt with no match: failure delay | `1 s` | Game scene |
| Fast finish timescale | `2` | Game scene |

Ball collision code also applies a reflected velocity on qualifying impacts while `Falling`. Copying only the physics material will not reproduce the existing bounce feel. Keep the prefab's material and script settings together when comparing behavior.

Belt capacity, belt speed, tray capacities, colors, and upper playfield contents come from `LevelData`. Avoid treating one level's values as global rules.

## Fast finish rule to preserve

Fast forward collection only when all of these are true:

1. The level is playing and all holders have been released (or the new game's equivalent: no further player releases remain).
2. No scheduled balls remain unspawned.
3. No ball remains in the upper playfield, falling, or waiting in the funnel.
4. Every queued ball has finished seating; pending and funnel queues are empty.
5. At least one belt ball can enter a currently active tray. If trays are transitioning, wait and check again.
6. Queued counts by color satisfy the entire remaining tray sequence, with no missing or surplus balls. Already accepted tray balls are excluded.

The reference performs a read-only count simulation through each column's remaining tray requirements. This assumes a circulating belt with reachable pickup areas; it is not a simulation of physics or pickup geometry. Do not accelerate merely because the last holder was tapped. Restore normal timescale on retry, level change, win, loss, and cleanup. Account for tutorial pauses separately.

## Source map

All paths are relative to the original project root.

| Responsibility | Source |
| --- | --- |
| Ball states, physics, bounce, visual reset | `Assets/Game/Scripts/Core/Ball/Ball.cs` |
| Pool and active ball tracking | `Assets/Game/Scripts/Core/Pooling/BallPool.cs` |
| Funnel capture, belt occupancy, seating, pickup and overflow | `Assets/Game/Scripts/Core/Belt/BeltQueueManager.cs` |
| Belt collision intake and fullness glow | `Assets/Game/Scripts/Core/Belt/ConveyorBelt.cs` |
| Spline slots and belt movement | `Assets/Game/Scripts/Core/ConveyorBelt/SplineConveyorBelt2D.cs` |
| Tray generation, pickup areas and remaining-count check | `Assets/Game/Scripts/Core/Tray/TrayManager.cs` |
| Ordered tray sequence and transitions | `Assets/Game/Scripts/Core/Tray/TrayColumn.cs` |
| Reservation, ball landing and tray animation events | `Assets/Game/Scripts/Core/Tray/TraySlot.cs` |
| Tray completion effect | `Assets/Game/Scripts/Core/Tray/TrayCompletionVfx.cs` |
| Original release rules, bursts and delayed spawn tracking | `Assets/Game/Scripts/Core/Grid/GridManager.cs` |
| Original holder visuals and release feedback | `Assets/Game/Scripts/Core/Grid/BallHolder.cs` |
| Original mouse/touch input | `Assets/Game/Scripts/Core/Input/TapInputManager.cs` |
| Level schema, art references and tray generation | `Assets/Game/Scripts/Data/LevelData.cs` |
| Loading, win/loss, retry and fast finish | `Assets/Game/Scripts/GameManager.cs` |
| Referenced gameplay sounds | `Assets/Game/Scripts/Core/Audio/SfxManager.cs` |

Most scripts use namespace `PopSort`; the spline class uses `PaperSort.Game`. Preserve or consistently update these references when migrating.

Important assets: `Assets/Game/Prefabs/Gameplay/BallPrefab.prefab`, `BallPhysics.physicsMaterial2D`, `BallHolder.prefab`, `TraySlotPrefab.prefab`, and `TrayCompletionVfx.prefab`; plus `Assets/Game/Prefabs/Static/ConveyorBelt.prefab` and the reference scene `Assets/Game/Scenes/GameScene.unity`.

## Migration and implementation order

1. Inspect the new project's Unity version, packages, camera, world scale, and input system. Establish the upper playfield release rule with the user.
2. Create one ball prefab and a test funnel. Match Rigidbody2D, collider, material, and falling settings before tuning visuals.
3. Add a closed spline belt, visible slots, occupancy tracking, and smooth seating. Stress it with simultaneous releases.
4. Add one tray column, color filtering, capacity reservation, and landing. Then add multiple columns and tray transitions.
5. Introduce a level definition with matching ball totals and tray requirements per color. Adapt `LevelData` if retaining the original grid schema makes sense; otherwise replace the grid-specific data boundary.
6. Wire level lifecycle, pool reset, overflow and the final-ball fast finish gate. Add art, sound and optional tutorials afterward.

For source reuse, export the chosen scripts, prefabs, reference scene and required dependencies as a `.unitypackage`, or copy assets together with their `.meta` files. Audit the dependency selection: `GameManager` currently references tutorial/UI classes and `TripleTapSDK`, so importing it directly also brings those compile dependencies. Adapt those integrations for the new project rather than assuming they exist. Preserve referenced sprites, materials/shaders, animator controllers, clips and animation-event receiver methods if copying visual prefabs. Install matching package dependencies separately. Do not copy `Library`, `Temp`, or generated build folders.

Optional original-game systems include FTUE, saved progression, and analytics. Original analytics sends GA Start/Complete/Fail with `level_N`, plus milestones at 5/10/15/20 to GA, Facebook and Firebase. Those are original-game conventions, not confirmed requirements for the new game. Use the new game's own service configuration and signing setup if these integrations are added.

## Regression checks and known limits

- Release a dense pile: balls should drain through the funnel whenever belt space becomes available. Retest the widened capture area against the new geometry.
- Verify that each slot has at most one ball and each ball is collected only once.
- A ball still seating must not enter a tray. A ball already reserved by a tray must not consume belt capacity afterward.
- Confirm collection from every column at normal speed and fast finish speed. Narrow pickup bands can be skipped between frames at high speeds; widen or use swept checks if that occurs.
- Test a full belt with no usable color and a full belt during tray transitions. The reference overflow check uses current tray acceptance; temporary transitions must not be mistaken for a permanent deadlock in the new game.
- Fast finish must remain off during delayed spawning, falling, funnel waiting and seating. It should start only after the final arrival and a successful remaining-count check.
- Retry with balls falling, queued and landing. No old ball, occupied slot, coroutine, tutorial restriction or accelerated timescale should survive.
- Repeatedly reload the scene and leave Play Mode. Unity destroyed objects need `!= null` checks before access; C# `?.` does not detect Unity's destroyed native objects. The FTUE cleanup was fixed for this issue.
- Test the complete loop on device at different frame rates. Source compile checks in the prior work passed, but the funnel and timing changes were not verified through a recorded Unity Play Mode or device run. Validate the new implementation rather than treating this reference as a runtime test certificate.

Keep future new-game rules and tuning decisions in the new project's own handoff document as they are confirmed.
