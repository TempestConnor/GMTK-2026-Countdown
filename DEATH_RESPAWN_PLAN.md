# Player death and respawn: discussion plan

Last updated: 2026-09-25

## Handoff and working rules

- Purpose: discuss and refine one numbered point per chat without carrying the full conversation.
- Read this document and project AGENTS.md first; inspect only source files relevant to the current point and its dependencies.
- Game code changes are suggestions only. Do not edit scripts, scenes, prefabs, or assets unless the user explicitly changes that instruction. Updating this planning document is separate from implementing game changes.
- All architecture below is PROPOSED, not approved or implemented. The user's four goals are confirmed; design choices remain open.
- At the end of a discussion, update the relevant section with accepted decisions, reasons, unresolved questions, proposed interfaces, and next steps. Keep it concise; replace superseded proposals rather than appending chat transcripts.
- Distinguish design agreed, code proposed, user reports applied, and verified. Never infer implementation from agreement.
- Recheck source before giving exact diffs: the project may change between chats.

## Confirmed goals

1. A placeable checkpoint object for the player.
2. A reusable property/component that kills the player on contact.
3. Ordinary walls kill the player when they clip into them.
4. A tileable wall that blocks movement normally but does not kill an embedded player.

Interpretation to confirm: "clips into" means the player's collider is embedded in solid geometry, especially after plane switching, rather than ordinary touching or wall sliding.

## Discussion index and status

| Point | Topic | Status |
| --- | --- | --- |
| 1 | Player life, death, and respawn reset | Proposed; discuss first |
| 2 | Checkpoint object and spawn data | Proposed |
| 3 | Contact hazards and surface behavior | Proposed |
| 4 | Detecting lethal wall penetration | Proposed |
| 5 | Safe wall behavior and tileable authoring | Proposed |
| 6 | Prefab/palette integration and validation | Proposed |

Suggested sequence: 1 -> 2 -> 3 -> 4 -> 5 -> 6. Point 6's authoring requirements apply while planning each placeable object, not just at the end.

## Project context observed on 2026-09-25

- Unity 6000.5.4f1; Input System 1.19.0; Tilemap Extras 8.0.3; Cinemachine 3.1.7; URP 17.6.0.
- `Assets/Scripts/Player/playerController2.cs` and its Movement, Jump, Dash, PlaneSwap, Preview partial files own player state. `direct.cs` owns ground/wall casts with plane-specific masks.
- `Assets/Scripts/Properties/Banishable.cs` changes plane/layer and invokes a plane-changed event.
- PlaneSwap tracks a group of banished members and a return coroutine. Both automatic and manual return force members to plane A. Other objects can switch into the player even when the player does not switch.
- GravityReversalArea maintains static per-body effect records and per-area overlap sets; respawn cleanup must account for those records, not only reset Rigidbody gravity.
- GoalFlag filters trigger sensors and non-player colliders; useful starting pattern for checkpoint/hazard activation.
- Ground and GroundB in `Assets/Prefabs/Level/LevelTemplate.prefab` have composites serialized with geometry type 0 (Outlines).
- `README.md` is authoritative for terrain/entity authoring. EntityPlaneAutoAssign handles plane assignment and preview materials for painted entity prefabs.
- No death/respawn code, prefabs, or tiles were created in this discussion. Inspection was read-only; no Unity play-mode validation was performed.

## 1. Player life, death, and respawn reset

Proposal: a `PlayerLife` component owns alive/dead state, checkpoint data, idempotent `Kill()`, death delay, and respawn sequencing. Hazards request death through this one entry point.

Controller-specific cleanup belongs in a proposed `playerController2.Life.cs` partial, with explicit methods such as `BeginDeath()` and `ResetForRespawn()`. Names/signatures are not yet agreed.

Reset requirements:

- Gate all player input callbacks and controller updates while dead; canwalk alone is insufficient.
- Stop dash, wall-jump, and banish-return coroutines. Ensure a respawn coroutine is not accidentally stopped along with controller routines.
- Clear velocity, movement input, dash/wall-jump/sliding flags and animation state; restore intended movement permissions.
- Resolve active banished members before clearing their tracking list; decide the world reset policy first.
- Clear targeting/arming/countdown state and preview globals; restore camera visibility.
- Restore player plane and direct's cast masks, then refresh contacts.
- Remove stale gravity-field membership and reconcile fields at the new spawn before movement resumes.
- Validate the spawn against current geometry to avoid an immediate death loop.

Open decisions: death delay/presentation; keep the same player instance or recreate; player-only reset versus resetting boxes/doors/switches; held-input behavior on respawn; simultaneous death/goal priority; fallback for an obstructed checkpoint.

Acceptance checks to perform later: repeated Kill requests produce one death; death during dash/wall-jump/banish/gravity reversal leaves no delayed effects in the next life; initial spawn works before reaching any checkpoint.

## 2. Checkpoint object and spawn data

Proposal: `Checkpoint` trigger prefab with an authored child `RespawnPoint`; player's solid collider registers spawn position and intended plane. Store the authored point rather than the position where the player touched the trigger. Initial player spawn is the fallback.

Suggested first version: session-only checkpoint state, with clear respawn points on plane A. This is not an accepted limitation. Plane-B checkpoints require reconciling the current forced return-to-A behavior.

Open decisions: supported planes; activation visuals; persistence across scene reload/game restart; facing direction; whether checkpoints snapshot puzzle state; handling moved/destroyed checkpoints.

Relevant source: GoalFlag.cs, PlaneSwap partial, Banishable.cs, README.md.

Acceptance checks: last activated checkpoint wins; only the correct player's solid collider activates; plane separation works; spawn is clear and grounded as intended; falls/hazards before first checkpoint use initial spawn.

## 3. Contact hazards and surface behavior

Proposal: reusable `KillOnContact` under `Assets/Scripts/Properties/`, supporting trigger volumes and solid collision hazards. Resolve the player through collider/body ownership and ignore trigger sensors. All paths call PlayerLife.Kill().

Separate proposed `SolidSurface` setting with `EmbeddedPlayerResponse { Kill, ResolveWithoutDeath }`. Keep existing plane collision layers; express death policy through components.

| Object | Blocks movement | Touch response | Embedded response |
| --- | --- | --- | --- |
| Ordinary wall | Yes | Safe | Kill |
| Safe wall | Yes | Safe | Resolve without death (proposed) |
| Contact hazard | Optional | Kill | Kill |

Open decisions: defaults for unmarked solids; whether boxes/doors are lethal when embedded; component ownership for child/composite colliders; behavior if both hazard and safe-surface components exist; enter versus stay/overlap handling for newly enabled hazards.

Acceptance checks: triggers and solid hazards work on both planes; player sensors and boxes do not activate player death; repeated callbacks do not duplicate respawn.

## 4. Detecting lethal wall penetration

Proposal: player-side detector gathers relevant solids on the active plane, excludes triggers/self/ignored collision pairs, and measures penetration using Collider2D.Distance. Reuse query buffers/lists.

Core predicate (not a complete implementation): valid distance result and `distance < -tolerance`. Suggested tuning starting point: 0.05 world units; not validated. Normal touching and tiny solver overlaps must remain safe.

Check after the entire banish/return batch completes and before the next simulation can separate bodies. Include cases where only another object changed plane. Also consider regular physics-step checks for closing doors or other overlap causes. Define transform synchronization and execution order when implementing.

Geometry issue: outline composites have edges but no filled interior; a fully embedded player may not overlap any edge. Proposed solution is polygon geometry for lethal terrain, subject to testing seams and containment. An alternate filled detection representation can be evaluated if movement quality suffers.

Open decisions: exact meaning of clipping; kill immediately versus sustained penetration during ordinary movement; all walls or explicitly marked ones; containment and multiple-collider handling; query capacity/overflow behavior.

Acceptance checks: ordinary landing/wall slide/dash contact is safe; materializing partly or fully inside terrain is lethal; a returned object can kill an embedded player; other-plane walls do not affect the player; tile seams do not cause false deaths.

References:

- https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/colliderdistance2d
- https://docs.unity3d.com/ja/6000.0/ScriptReference/CompositeCollider2D.GeometryType.Outlines.html
- https://docs.unity3d.com/cn/6000.0/ScriptReference/CompositeCollider2D.GeometryType.Polygons.html

## 5. Safe wall behavior and tileable authoring

Confirmed: blocks movement but does not kill for clipping. Recovery behavior is NOT decided.

Proposal: resolve shallow penetration using separation information; use a bounded nearby-position search for complex overlaps. Validate the whole player shape against relevant solids. Fall back to a previously validated safe position without a death sequence when necessary. A safe wall must not suppress death from a simultaneously overlapping lethal wall.

Proposed terrain route: separate SafeWalls and SafeWallsB tilemaps, their own composites and surface policy, retaining plane layers. Safe and lethal tiles should not share one composite when policy is collider-based. Add the safe-wall Tile to TerrainPalette.

Alternative to discuss: a repeatable 1x1 entity prefab if the user means an object painted with EntityBrush. That requires a prefab and EntityPalette entry instead of only a terrain tile.

Open decisions: push out, tolerate embedding, block a plane switch, or relocate; what to do on automatic return; visual identity; ordinary movement/seam behavior; last-safe-position rules; terrain versus entity authoring.

Acceptance checks: blocks normal motion; embedding never invokes death from this surface; player can recover without being trapped; corners/multiple safe walls resolve; lethal neighbors retain their behavior.

## 6. Authoring integration and validation

Read README.md before finalizing any entity proposal. All entity suggestions must include prefab plus palette entry and suggested verification, while respecting the no-game-edits instruction.

- Checkpoint prefab belongs in Assets/Prefabs/Entities/, root at bottom-left of footprint, aligned to the 1x1 grid.
- Add its connected prefab instance to EntityPalette, single row y=3, z=0, one empty column between footprints.
- Observed current rightmost entry: GravityLaunchArea at x=9 with width 4, ending x=13. Next start is (14, 3, 0). Reinspect before use; later chats may add entries.
- Paint with EntityBrush onto Level > Entities; preserve prefab connections and plane/preview conventions.
- If a placeable hazard or safe-wall entity is added, it also requires its own prefab/palette entry. Modifying an existing entity should preserve its existing entry.
- Terrain uses Tile/RuleTile assets and TerrainPalette; separate tilemaps as needed for collider policy.
- Verify prefab setup, palette picking, painting onto Entities, prefab propagation, and both planes. Report anything not actually tested.

## Decision log

- 2026-09-25: User requested a persistent plan to discuss each point in separate chats. The six-point architecture above remains a proposal. No implementation decision has been accepted yet.

## Next chat prompt

Read AGENTS.md and DEATH_RESPAWN_PLAN.md. Let's discuss point 1 only. Treat the architecture as a proposal and inspect relevant code as needed. Suggest game code changes only; do not apply them. Save decisions and open questions back to the plan as we agree on them, distinguishing agreed design from implemented or verified work.
