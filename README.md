# Level Design Tooling

## Main menu, player profile, and level progression

### Editing the menu appearance

The menu UI is saved in `MainMenu.unity` and can be edited outside Play mode.
Expand **Main Menu > Menu Canvas > Background > Menu Layout** in the Hierarchy.
Edit the Title, summary labels, and the panels under **Panels** using their
RectTransform, Image, Text, and layout components. Vertical Layout Groups control
spacing; Layout Elements control button heights. The Canvas Scaler controls how
the layout scales with the game window.

Home Panel is visible by default. To preview Level Select Panel or Profile Panel,
disable Home Panel and enable the desired panel in the Inspector. Reset Confirmation
is a separate panel directly under Menu Canvas. Play mode always starts on Home.
The scene's buttons have persistent On Click events targeting the Main Menu
controller, and its serialized fields reference the scene UI. Preserve these
references when replacing elements.

Edit `Assets/Prefabs/UI/MenuButton.prefab` to restyle the shared static buttons;
scene instances keep their own labels and click events. Edit
`Assets/Prefabs/UI/LevelButton.prefab` to style level entries. Only those entries
are instantiated at runtime under **Level Select Panel > Level Scroll View >
Viewport > Level List**. Their label and unlocked state come from the catalog.
The controller updates profile/progress/status text and the Play/Continue label,
but leaves fonts, colors, layout, and other static text to the scene and prefabs.

Open `Assets/Scenes/MainMenu.unity` and press Play to use the menu. It is the
first build scene. Play/Continue loads the first unfinished level; Level Select
shows available, completed, and locked levels. Player Profile lets the player
change their name or reset completion after confirmation. One local profile is
saved as `player-profile.json` in `Application.persistentDataPath`, with a backup
of the previous save. Completion saves immediately; the loader tries the backup
if the main file is invalid. This is a single local profile, not an online account.

Select `Assets/Resources/LevelCatalog.asset` to customize level order. Drag list
entries into the desired order. Use **Add level scene** to select a scene and
generate a stable ID, then edit its display title. Click **Include catalog scenes
in build** after adding levels. The catalog order controls progression independently
of build order. Do not change an existing level's ID after shipping: saves track
IDs, so renaming a title or reordering entries preserves completion. If a scene
moves, update its Scene Path. The build validator rejects duplicate IDs/scenes,
empty titles, and scenes missing from the enabled build list.

The first level is always available. An unfinished level unlocks when all earlier
catalog entries are completed. Previously completed levels remain replayable
after reordering. Only `Level_01` is configured initially; add real level scenes
to expand the campaign.

Paint **GoalFlag** from `EntityPalette` onto the level's **Entities** child to
set its goal. Touching the flag with the player saves completion to the profile
and returns to the menu, where Continue selects the next unfinished level.
Unlocks follow the order in `Assets/Resources/LevelCatalog.asset`; each unfinished
level requires all earlier entries to be completed. The flag needs no scene
reference or UnityEvent wiring. The scene must belong to an unlocked catalog level.

`Level_01` has a **Level Flow** object with `LevelCompletion`. Other win conditions
can still call **LevelCompletion.CompleteLevel**. For each new level, add a
scene-level object with `LevelCompletion` for menu navigation.
Escape returns to the menu without awarding completion. You can
also wire `ReturnToMenu()` to a UI button. This is scene infrastructure and does
not need an entity palette entry.

This project uses Unity's Tilemap system for terrain and the `GameObjectBrush`
(2D Tilemap Extras) for placing enemies, hazards, and other entities — both
painted the same way, from the Tile Palette window.

## Rooms, transitions, and player input locks

A level can contain several room scenes, with one room loaded at a time. The
catalog's **Scene Path** remains its starting room; add its other scene paths to
**Room Scene Paths** on `Assets/Resources/LevelCatalog.asset`, then use **Include
catalog scenes in build**. Goal flags in any of those rooms complete the same
level. Continue starts at the starting room; entrance checkpoints are session-only.

Paint **RoomTransition** from `EntityPalette` at **(16, 3, 0)** using `EntityBrush`
onto **Level > Entities**. Its prefab is
`Assets/Prefabs/Entities/RoomTransition.prefab`. It has a bottom-left grid root,
a default 1x4 trigger, and an **Area Size** field. Its cyan palette visual is
hidden during gameplay; Scene-view gizmos show the trigger and arrival marker.
Transitions work on both player planes and cannot be banished.

1. Give each placed zone a **Zone ID** unique within its room, such as `east`.
2. On **one** of the two doorways, pick the other room under **Destination >
   Scene** and its doorway under **Zone**. Every link is two-way: the other
   doorway links back automatically, and its inspector shows "Linked from …".
   Setting the destination on both sides is allowed if they point at each other.
3. Move each zone's **Arrival** child to a safe player-root position inside its
   room's camera, clear of terrain on both planes. Keep the root unrotated and
   unscaled; resize with Area Size. Make the trigger thick enough to catch dashes.
4. Save the scene. Links live in the generated `Assets/Resources/RoomLinks.asset`,
   refreshed on scene save, on entering Play (open scenes, including unsaved
   edits) and before builds. Never edit it by hand.
5. Include the room scenes in the build and run **Tools > Rooms > Rebuild and
   Validate Links**. It rescans every scene under `Assets/Scenes` and reports
   unlinked or doubly linked doorways, missing destinations, duplicate zone IDs,
   missing arrival markers, player counts and build membership. Builds run the
   same check and fail on errors.

The camera death boundary is always **four world units (four tiles)** beyond
each camera edge: **camera -> transition strip -> death boundary**. There is no
margin setting. Place transitions in that strip or on the camera edge. The
boundary follows the actual camera; a following camera still needs the room's
usual camera confinement. Non-transition edges remain lethal after the margin.

Entry immediately locks inputs, cancels all actions except active banish, and
freezes physics while loading. The destination's player appears at the paired
Arrival marker with zero velocity. Input resumes after setup; overlapping
doorways cannot send the player back until the player clears them. Each room
needs exactly one active Player prefab and its normal camera/terrain setup.

Room travel fades to black for 0.15 seconds before loading, then fades back in
for 0.15 seconds after arrival setup. The destination player stays frozen and
input-locked until the fade finishes. The runtime overlay survives scene loads
and uses unscaled time. Adjust `FadeOutDuration` and `FadeInDuration` in
`Assets/Scripts/Progression/RoomFade.cs` to change the timing. Death respawns
keep their immediate reload behavior.

Banish preserves the player's plane and remaining return timer across travel;
loading time does not consume that timer. Banished objects in the old room are
unloaded and reset with that room. Targeting/arming is cancelled. Death cancels
all current actions and reloads only the current room, restoring the entrance
position with banish reset. Direct
Play or starting a level uses the authored spawn. Re-entering rooms resets their
objects; there is no cross-room puzzle-state persistence.

`Tools/Validation/SetupRooms.cs` can generate `Assets/Scenes/Rooms/ExampleRoom_A.unity`
and `ExampleRoom_B.unity`, a standalone pair where only A declares the link.
The script adds them to the build for testing; they are not campaign catalog levels.
Open A and walk right; open B and walk left. Existing levels are not split
automatically.

**Shared input locking:** acquire an `IDisposable` lease from the player's
`PlayerInputLock.Acquire()` and dispose that same lease when your dialogue or
cutscene ends (also release it if its owner is disabled/destroyed). Multiple
owners can lock at once; only releasing the last lease restores input. The
lock disables the player's InputAction asset centrally and clears held input
state. New actions must use that player's InputAction asset; direct device
polling bypasses it. Input locking alone does not freeze physics or cancel
active abilities.

**Cancellation:** implement `IPlayerAction.Cancel()` and register the action
with `PlayerActionCancellation`. Each existing action's implementation lives
beside its behavior in the controller's partial file; `InitializeActions`
registers them once. Actions caching held/buffered input also implement
`IPlayerInputState.ClearInput()`; the registry clears those on the first input
lock without needing a per-action list in the lock script. New separately attached action components should register
on enable and unregister on disable. `CancelAll()` is the common death cleanup;
room travel excludes only the active banish action. Each action stops its own
coroutines and effects. No per-action list is needed in the death handler.

Validation scripts in `Tools/Validation/`:

- `ValidateRooms.cs`: prefab/palette connections, actual EntityBrush painting,
  cancellation registry, two-way room-link resolution and validation, and catalog membership.
- `RoomPlaySession.Start` / `.Stop`: enter the example room for testing and
  restore the previous play-start scene afterwards without replacing open scenes.
- `ValidateRoomsPlay.cs`: run while playing ExampleRoom_A to exercise stacked
  input locks, cancellation, live room loads, banish transfer, death/respawn,
  reverse trigger physics, and the four-tile death boundary.

## Folder structure

| Path | Contents |
|---|---|
| `Assets/Tiles/` | `Tile`/`RuleTile` assets — the paintable terrain data |
| `Assets/Palettes/` | Tile Palette prefabs and brushes used to paint |
| `Assets/Prefabs/Level/LevelTemplate.prefab` | Reusable `Grid → Ground, Entities` skeleton every level is built from |
| `Assets/Prefabs/Entities/` | Enemy/hazard/pickup prefabs (create this folder as you add them) |
| `Assets/Scenes/LevelXX/` | One folder per level: its starting-room scene plus all of its room scenes |
| `Assets/Scenes/RoomTemplate.unity` | Starting point for new room scenes |

Editing `LevelTemplate.prefab` (e.g. adding a new tilemap layer) updates every
level built from it, since each level's `Level` object is a prefab instance,
not a copy.

## Tile sizing convention

The `Grid` on `LevelTemplate` uses a cell size of **1×1 world units** — this
is the only place "tile size" is defined. It is not baked into individual
Tile assets or sprites.

When real art replaces the placeholder squares, set each sprite's
**Pixels Per Unit** so that `sprite pixel width ÷ PPU = 1`. A 32px sprite at
PPU 32, or a 64px sprite at PPU 64, both fill exactly one cell — so art can
be authored at any resolution as long as PPU is set consistently. To change
the overall tile size later, change the Grid's cell size once; it applies
everywhere.

## Swapping in real art

Lethal terrain (`Tile_Ground`, `Tile_Platform`, `Tile_Hazard`) auto-tiles: each
cell picks its sprite from its neighbors, so one painted tile draws floors and
walls differently without repainting. Exposed top and bottom faces draw as light
floor plates (both are walkable once gravity flips); exposed side faces draw as
smooth blue wall panels; buried cells draw as plain dark plating. `Tile_Hazard`
is the same lethal terrain with a yellow/black striped interior — paint it
anywhere in a lethal region as decoration; it joins its neighbors seamlessly.
The art lives in `Assets/Tiles/Sprites/TerrainFrame.png` and `HazardFrame.png`,
generated atlases like the safe tile frames below. To change the style, edit the
constants in `TerrainFrameGenerator.cs` and run **Tools > Level > Generate
Terrain Frames** (or bump `StyleVersion` in that file, which regenerates on the
next script reload). Don't edit the PNGs by hand; the next regeneration overwrites
them. Tilemaps cache sprites in the scene file, so after regenerating, run
**Tools > Level > Refresh Terrain In All Scenes** to update and save scenes that
weren't open.

Behind everything, the `MainCamera` prefab's **Backdrop** child draws a dim
far-facility-wall pattern (`FacilityBackdrop.cs`). It sits at world Z 20, so the
perspective camera gives it parallax, and it shows on both planes. Restyle it in
`FacilityBackdropGenerator.cs` and run **Tools > Level > Generate Facility
Backdrop**. The per-plane `Background`/`BackgroundB` tilemaps still draw in
front of it for room-specific decor. To use hand-drawn art instead, follow the steps below:

1. **Import it.** Drop the sprite(s) into `Assets/Tiles/Sprites/`. Set
   **Texture Type → Sprite (2D and UI)**, slice sheets with **Sprite Mode →
   Multiple** + the Sprite Editor, and set **Filter Mode → Point** for pixel
   art. Set **Pixels Per Unit** per the convention above.
2. **Repoint the existing Tile assets.** Select `Assets/Tiles/Tile_Ground.asset`
   and `Tile_Platform.asset`, drag the real sprite into each one's **Sprite**
   field, and reset **Color** to white. Tilemaps reference the Tile asset, not
   a baked copy, so anything already painted in a level updates automatically
   — no repainting needed.
3. **New terrain types** (art that doesn't map onto an existing Tile): create
   a new `Tile` (or `RuleTile` for auto-tiling edges/corners) in
   `Assets/Tiles/`, then drag it into `TerrainPalette` in the Tile Palette
   window, same as step 5 under "Painting terrain" below.
4. **Entity art:** swap the sprite on each prefab's `SpriteRenderer` in
   `Assets/Prefabs/Entities/`. Only touch `EntityPalette` if you're adding a
   brand-new entity type, not reskinning an existing one. The sci-fi entity
   sprites (`Assets/Tiles/Sprites/Entity*.png`) are drawn at 16 px per world
   unit to match the safe tiles; Box is 32 px at PPU 32 on its 2x Visual, which
   SafeBox inherits. Door and GravityReversalArea use **Draw Mode → Tiled**
   nine-slice sprites: their scripts set `SpriteRenderer.size` (unit scale) instead
   of stretching the transform, so frames stay crisp at any length/size.
5. **Sanity check:** open a level and confirm painted tiles show the new art
   with no gaps/seams — seams usually mean the PPU or sprite pixel size is
   off from the Grid's 1×1 cell size.

## Player sprite and animations

The player (a robot detective in a trench coat and fedora with a green visor)
is generated pixel art, like the terrain. All frames live in one sliced sheet,
`Assets/Tiles/Sprites/PlayerSheet.png` (32 px per unit, sprites named
`Player_<State>_<frame>`). The clips are in `Assets/Animations/Player/`.
Don't edit these by hand; the next regeneration overwrites them.

- **Art:** `Assets/Scripts/Editor/PlayerSprites/PlayerSpriteArt.cs` defines the
  palette, every pose, and each clip's frame count/fps/looping. Poses are in
  design pixels on a reference 26×51 px body (the 0.8×1.6 hitbox), facing
  right. Bump `StyleVersion` in `PlayerSpriteGenerator.cs` after editing, or run
  **Tools > Player > Generate Player Sprites**.
- **Resizing the hitbox:** change the Player prefab's `CapsuleCollider2D` size
  or offset and save the prefab. The sheet, clips and sprite pivot regenerate
  automatically at the new size, redrawn crisply rather than stretched.
  Proportions follow the hitbox's aspect ratio. Only the prefab's collider is
  read; per-scene overrides of it are ignored.
- **Choosing a clip:** `playerController2.Animation.cs` holds all selection
  rules and writes a `PlayerAnimState` to the Animator's `state` int. The
  generator rebuilds `PlayerAnimatorController`'s base layer as one Any State
  transition per state, so don't add states or transitions there by hand. To
  add a pose, append a value to `PlayerAnimState`, add its clip in
  `PlayerSpriteArt.Clips()`, and pick it in `ChooseAnimState`.
- **States:** idle, walk, rise, fall, wall slide, wall jump, grab (idle, push,
  pull, airborne), aim (thinking: hand on chin) and banish (finger snap held
  close to the chest), each with ground (idle/walk), air and wall-slide
  variants. Aiming shows while the aim button is held. The snap plays for
  `snapPoseDuration` after a banish fires, and is not played on early recall.
  Dash has no pose of its own and shows rise/fall.
- **Flipping:** the sprite flips horizontally to face the move direction (away
  from the wall during a wall jump, toward a grabbed box while grabbing), and
  vertically while gravity is reversed so the player stands upside down. Keep
  the Player root's scale at (1, 1, 1); flipping is done on the SpriteRenderer.

## Creating a new level

1. `File → New Scene → Lit 2D Scene` (gives you a Camera + Global Light 2D
   for free) and save it into a new `Assets/Scenes/LevelXX/` folder (e.g. `Level02/Level_02.unity`).
   Put that level's other rooms in the same folder: the RoomTransition
   Destination dropdown lists rooms from the doorway's own folder
   (choose **Other folder...** to link across levels).
2. Drag `Assets/Prefabs/Level/LevelTemplate.prefab` into the scene.
3. Add the new scene to Build Settings (`File → Build Settings → Add Open
   Scenes`, or `manage_build(action="scenes")` if scripting it).

## Painting terrain

To copy the active room's painted **Ground** cells onto **GroundB**, choose
**Tools > Level > Copy Ground A to Ground B** in Edit mode. Tile assets, cell
colors, transforms, and flags are copied at the same grid coordinates. Existing
GroundB tiles outside the source layout remain untouched. The operation supports
Ctrl+Z and leaves the scene unsaved for review. Editor scripts can also call
`GroundTileCopy.CopyPaintedCells(source, destination)` with two Tilemaps.

1. `Window → 2D → Tile Palette`.
2. Set the palette dropdown to `TerrainPalette` and the brush to the default
   brush.
3. Set the paint target to the level's `Ground` object (select it in the
   Hierarchy, or use the target dropdown in the Tile Palette window).
4. Paint in the Scene view. `Ground` already has a `TilemapCollider2D` +
   `CompositeCollider2D` + static `Rigidbody2D`, so painted tiles are solid
   immediately.
5. To add a new terrain tile type: create a `Tile` asset in `Assets/Tiles/`
   (or a `RuleTile` once auto-tiling matters), then drag it into
   `TerrainPalette` in the Tile Palette window.

### Nonlethal solid terrain

Choose `Tile_SafeWall` in `TerrainPalette` and paint onto
**Level > Ground** (plane A) or **Level > GroundB** (plane B), alongside lethal
tiles. Select a tile asset and toggle **Kills On Penetration** to change its
behavior everywhere it is painted. Ground and Platform are lethal; SafeWall is
nonlethal. Their existing asset references and palette entries are preserved.

Safe tiles draw as one hollow region: an outline matching the SafeBox (color and thickness)
around the region's perimeter, with a plain translucent interior. Adjacent safe
tiles join automatically as you paint or erase, and lethal tiles never join them.
The SafeBox's diagonal stripes and corner brackets set it apart as movable; terrain
has neither. The frames live in `Assets/Tiles/Sprites/SafeTileFrame.png`, a generated
47-sprite atlas. To change the style, edit the constants in
`SafeTileFrameGenerator.cs` and run **Tools > Level > Generate Safe Tile Frames**.
That regenerates the atlas and wires it into every nonlethal `TerrainTile`. Don't
edit the PNG by hand; the next regeneration overwrites it.

Create new tiles with **Create > 2D > Tiles > Terrain Tile**. `TerrainTile` uses
a full rectangular grid-cell collider and identity tile transform, matching this
project's square terrain. Plain Tile and RuleTile assets do not carry this
lethality property; use TerrainTile for lethal terrain.

Ground and GroundB retain their existing physics, rendering, and plane layers.
The player checks penetration depth against only nearby lethal cells, using
**Terrain Penetration Tolerance** (default 0.03 world units) on PlayerPenetrationCheck.
Crossing into a lethal cell beyond that tolerance kills even when also overlapping a
safe tile. Normal contact and shallow overlap stay safe.

Safe terrain uses hollow outline collision: switching planes fully inside a safe
region leaves room to walk and jump against its borders without being pushed out.
Only adjacent safe tiles merge; lethal tiles use separate filled collision, so
walking from a safe interior into a lethal wall stops at its surface without
killing the player. Swapping directly into a lethal wall still kills. The player
must fit inside the safe region; overlapping its border can still cause correction.

`TerrainCollision` bakes a hidden, saved collision-only child for safe cells in
the editor. Keep painting both types onto Ground or GroundB; no extra authored
tilemap or palette entry is needed. Painting and undo schedule an editor rebuild;
scene saving, entering Play mode, and building also bake current collision.
The generated map uses `Assets/Tiles/Tile_SafeCollision.asset` as its invisible
grid tile. Do not paint that internal asset or edit the generated child directly.
Editor scripts needing immediate collision can call `TerrainCollision.Rebuild()`.
The generation methods are excluded from player builds and do not run in Play
mode: gameplay loads the saved colliders without scanning or copying terrain.
Runtime terrain editing is not supported by this baked workflow.
Keep the Player's continuous collision detection enabled for fast movement.
The obsolete SafeGround/SafeGroundB maps have been removed.

## Rule Tiles (optional, for auto-tiling)

`com.unity.2d.tilemap.extras` is already in the project, so Rule Tile is
available with no extra install. **For a jam-sized project, prefer plain
`Tile` assets and hand-placing** — Rule Tiles pay off once you have many
levels and a full edge/corner/inner-corner sprite set to blend, but the
upfront cost of authoring 6-16 neighbor rules per tile type usually isn't
worth it unless your art pack already ships those edge/corner variants or
you're repeatedly hand-placing the same edges and it's slowing you down.

1. **Create the asset.** Right-click `Assets/Tiles/` → **Create → 2D → Tiles
   → Rule Tile**.
2. **Assign a default sprite.** This is the fallback sprite/collider shape
   used before any rule matches.
3. **Add rules.** Click the sprite preview (or **+** under Rules) to add a
   rule: click cells in the 3×3 neighbor grid to cycle **This / Not This /
   Don't Care**, then drag the matching sprite (top edge, corner, inner
   corner, etc.) into that rule's sprite slot. Repeat per edge/corner case.
4. **Set match behavior** (4-way for orthogonal-only neighbor checks, 8-way
   to also check diagonals — needed if you have inner-corner sprites).
5. **Add it to the palette** the same way as any tile: drag it into
   `TerrainPalette` in the Tile Palette window.
6. **Paint and verify** — edges/corners should auto-resolve as you paint
   adjacent tiles. If a rule doesn't fire, check its 3×3 pattern against the
   actual neighbor tiles.

## Adding new entities

1. **Build the prefab.** Create an empty GameObject, add a `SpriteRenderer`
   and a `Collider2D` set to `isTrigger = true`, plus whatever script reacts
   to the player touching it (e.g. `OnTriggerEnter2D` for damage/respawn).
   Tag it if your player logic checks tags.
2. **Save it as a prefab** into `Assets/Prefabs/Entities/`.
3. `Window → 2D → Tile Palette`. Set the palette dropdown to `EntityPalette`
   and the brush dropdown to `EntityBrush` (Prefab Brush) instead of the
   default brush.
4. **Drag the prefab from the Project window into the palette's grid area**,
   the same way you would a tile — it becomes a paintable entry.
5. Set the paint target to the level's `Entities` object (child of `Level`).
6. Paint in the Scene view. Each click instantiates a real GameObject as a
   child of `Entities`, snapped to the grid, with full Ctrl+Z support.

Repeat steps 1–4 for each new entity type — once added to `EntityPalette`,
it's reusable across every level built from `LevelTemplate`.

### Palette layout convention

Entities in `EntityPalette` are laid out in a single row (`y = 3, z = 0`
under the `Layer1` grid transform) so each one aligns to the grid and is
easy to snap-paint:

- Each entity's root transform sits at the **bottom-left corner** of its
  collider/visual footprint, at a whole-number x position.
- Entities are placed left-to-right with exactly **one empty column**
  between the right edge of one entity's footprint and the left edge of
  the next. Footprint width = the entity's `BoxCollider2D` `m_Size.x`
  (e.g. `Box` is 2 tiles wide, `Door` is 1 tile wide, `Switch` is 2 tiles
  wide).
- Current layout: `Box` spans [-6,-4], gap, `Door` spans [-3,-2], gap,
  `Switch` spans [-1,1], gap, `GravityReversalArea` spans [2,6], gap,
  `GoalFlag` spans [7,8], gap, `GravityLaunchArea` spans [9,13].
  `Spike` spans [14,15], after one empty column.
  `RoomTransition` spans [16,17], after one empty column.
  `SafeBox` spans [18,20] (2 wide, root at x=18), after one empty column.
  `TutorialArrow` spans [21,24] (default 3-wide straight arrow), then
  `TutorialKey` spans [25,26] (1-wide keycap), each after one empty column.
  The next entity added should start at x=27,
  and so on — always start at
  `(previous entity's right edge + 1)`.

### Tutorial hints

Tutorials are painted onto the level as decorative entities with no colliders, unaffected
by planes and lighting. Paint them with `EntityBrush` onto **Level > Entities**:
**TutorialArrow** from `(21, 3, 0)` and **TutorialKey** from `(25, 3, 0)`. Both draw
above Background/Ground (Default sorting layer, order 1–2) and below the player.

- **TutorialArrow**: the tail starts at the painted cell's center. **End** is the
  tip offset in cells, and **Arc Height** bows the stroke upward into a jump arc
  (0 = straight). With a placed arrow selected, drag the yellow square to move the
  tip (half-cell snap) and the dot to change the arc (quarter-cell snap).
- **TutorialKey**: a 1-cell-tall keycap that widens in whole cells to fit its label,
  growing right from the painted cell. Its label comes from **Action** (and
  **Composite Part**, e.g. `left`/`right` for Move) in `playerActions.inputactions`,
  so it follows binding changes. **Label Override** shows custom text, and turning
  off **Show Keycap** draws a plain word (e.g. "Grab"). **Label Scale** 0.75 fits
  SPACE in a 2-cell cap.

Color and stroke width are on the prefabs; instances inherit them unless overridden.
Labels use TextMeshPro (`Assets/TextMesh Pro`, essential resources).
`Tools/Validation/SetupTutorialHints.cs` rebuilds the prefabs/palette entries and the
Level01 hints (JumpTutorial, GrabWallJumpTutorial); `ValidateTutorialHints.cs` checks
palette connections, EntityBrush painting, labels, and keycap widths.

### On-screen tutorial prompts

For instructions that are hard to paint into the level (e.g. banish), trigger zones show
text on the player's screen. These zones are **not** on `EntityPalette`; drag
`Assets/Prefabs/Tutorial/TutorialPromptZone.prefab` and `TutorialDismissZone.prefab` into
the room (e.g. under **Level > Entities**). Both have a bottom-left root and an **Area
Size** in cells, fire once per room load when the player enters on either plane, and
draw Scene-view gizmos (yellow = prompt with its text, red = dismiss with lines to its prompts).

- **TutorialPromptZone**: **Text** is shown on entry. **Duration** is real-time seconds
  on screen after fading in; `0` keeps it up until completed or dismissed.
  - **Delay**: real-time seconds between entering and the prompt queuing (e.g. a hint
    that appears only if the player is still stuck).
  - **Complete Action** / **Complete Hold Time**: an action from `playerActions.inputactions`
    (e.g. `Aim`) that completes the prompt once held that long (`0` = a press).
  - **Then Show**: prompts shown when this one completes by its action or duration
    (not when dismissed), for step-by-step instructions.
  - **Replaces**: prompts dismissed when this one is triggered, so it does not wait behind them.
  - **Show On Banish**: also shown when the player's banish catches something (themselves
    included), anywhere in the room (`playerController2.BanishFired`).
  - **Show After Death**: shown when the room reloads because the player died in it
    (`RoomTravel.ArrivedByRespawn`), queued ahead of prompts at the spawn point.
  - **Show On Enter** off: the area is ignored; only the options above, **Then Show** or a
    trigger zone show it.
- **TutorialDismissZone**: entering fades out the listed **Prompts** (or drops them if
  still queued, and stops them showing later). An empty list fades out whichever prompt
  is currently on screen.
- **TutorialTriggerZone** (blue): entering shows the listed **Prompts**, an extra way in
  for a prompt. Each prompt still shows at most once per room load.

Every zone has a **Plane** filter: **Either** (default), **A** or **B**. A filtered zone
also fires if the player swaps onto that plane while already inside it. Gizmos prefix
filtered zones with `[A]`/`[B]`.

Example: Level01_BanishTutorial1 (`Tools/Validation/SetupBanishTutorial1Prompts.cs` places
it, `ValidateBanishTutorial1Play.cs` checks it in Play mode). The Aim prompt chains to the
Banish prompt, the first successful banish shows the Return prompt that replaces both (zone B
just dismisses them), the ledge zone shows "BANISH YOURSELF" after a 30 s delay, and dying shows
"Slamming your face into a wall kills you" on respawn.

Only one prompt shows at a time; a prompt triggered while another is up waits for it.
Text markup:

| Markup | Result |
|---|---|
| `[RIGHT_CLICK]` | Button prompt "RIGHT CLICK" (underscores become spaces) |
| `[@Aim]`, `[@Move/left]` | Button prompt with that action's current key/mouse binding |
| `~text~` | Spooky: tinted, and each letter wiggles |
| `\~`, `\[`, `\\` | Literal character |

Example: `Hold [RIGHT_CLICK] to Aim, Press [LEFT_CLICK] to Banish`.

The display is `Assets/Resources/TutorialPrompts.prefab`, created on demand in the
current room, so prompts and the queue reset on room travel and death (a respawn shows
the room's prompts again). Edit that prefab to restyle: text position/font/size on
**Prompt > Text**, and fade times, the button-prompt rich-text format, spooky colour and
wiggle strength on the root's `TutorialPrompts` component. Timing uses unscaled time, so
the aim slow-motion does not stretch it. Code can also call `TutorialPrompts.Show/Dismiss`.
`Tools/Validation/SetupTutorialPrompts.cs` builds missing prefabs (display and the three zones);
`ValidateTutorialPrompts.cs` checks markup, prefab wiring and that the zones stay off the palette.

### Pushable objects

Add **Pushable** to an object's **Rigidbody2D root** with a solid Collider2D.
Adding the property automatically adds a Rigidbody2D if needed; use **Dynamic**
body type and keep it simulated. The existing **Box** prefab already has this
property and retains its existing EntityPalette entry at **(-6, 3, 0)**.

Face a nearby object and press **Interact (F)** to grab it. Move left/right to
push or pull it, and jump to make both bodies jump. Press F again to release.
The player's **Grab Reach** defaults to 0.35 units from its collider. Solid
obstacles block grabbing; objects on a non-colliding plane cannot be grabbed.
The binding lives in `Assets/Settings/playerActions.inputactions`, Player/Interact.

Pushable locks horizontal movement while ungrabbed, so walking into a box does
not move it. Grabbing unlocks X; releasing stops horizontal velocity and locks X
again. Gravity and vertical movement remain active, including falling after an
airborne release. Leave Y movement available for jumping; freeze rotation for
upright boxes. A temporary physics joint maintains the initial grab offset while
colliders continue to resolve obstacles. Dash, input locks, action cancellation,
disabling either participant, and separation onto non-colliding planes release
the grip. Death and room travel use the existing cancellation registry.

`Tools/Validation/ValidatePushable.cs` checks prefab/input wiring and actual
EntityBrush painting. `ValidatePushablePlay.cs` runs in Play mode using a temporary
physics scene and virtual keyboard to check idle locking, F toggling, movement,
jumping, cancellation, target disabling, and plane separation. It restores input
update settings and removes its temporary scene/devices afterwards.

### Safe Box

Paint **SafeBox** from `EntityPalette` at **(18, 3, 0)** with `EntityBrush` onto
**Level > Entities**. `Assets/Prefabs/Entities/SafeBox.prefab` is a prefab variant
of **Box**: same 2x2 bottom-left footprint, Pushable grabbing, gravity, and
banish/plane behavior. Its visual is two layers under **Visual**: the teal frame
(`Assets/Tiles/Sprites/SafeBoxFrame.png`, border and corner brackets) over a
translucent, diagonally striped **Fill** (`Assets/Tiles/Sprites/SafeBoxFill.png`),
both drawn below the player. `Tools/Validation/SetupSafeBox.cs` regenerates both.
Changes to Box still flow into the variant; edit SafeBox-only values on the variant.

The box is hollow and nonlethal, like safe terrain: a player who phases inside can
walk and jump within it, while its outer boundary blocks entry. It can be grabbed
only from outside. Where a SafeBox touches safe terrain or another SafeBox on the
same plane, the shared boundary opens on both sides. Partial edge contact opens
only the shared span; corner-only contact opens nothing. Boundaries close again when
objects separate, change plane, or are disabled or destroyed. If a boundary closes
through the player, they are moved to the nearest clear pose (never into another
obstacle) instead of being killed. Ordinary Box behavior is unchanged.

Open boundaries also disappear visually in Play mode (`SafeSeams`). Along each open
span the box frame and its brackets clear to reveal the fill, and safe tiles are
redrawn as plain interior, so box and region read as one outline. Inner corners
open too. This works through the plane shaders (`Assets/Shaders/SafeSeams.hlsl`,
included by Sprite-Lit-PlaneAHide and Sprite-Lit-Saturation). Up to 32 seam
rectangles are uploaded as globals, each tagged with its plane, and only renderers
that opt in at runtime (SafeBox frames and safe-terrain tilemaps) react. Edit mode
and the Scene view always show both full borders. Any other sprite shader would
need the same include to take part.

How it works:

- `SafeRegion` (on SafeBox and on each baked safe-terrain map) keeps its original
  collider as **physical support** for non-player bodies, excluding only the
  Player/PlayerB layers. Boxes therefore stay supported however passages open.
- Players collide with baked **edge sections** on a separate child Rigidbody2D
  (kinematic for SafeBox, static for terrain). `SafeBoundarySystem` indexes them in
  4-unit buckets and, each physics step, rebuilds only edges whose region moved,
  changed plane, or was enabled/disabled, plus their opposed, coincident partners.
  Unchanged geometry costs no rebuilds.
- `SafePlayerCollision` filters player movement, wall, and grab queries so support
  colliders never block the player, and resolves boundary closures.
- The terrain bake (`TerrainCollision`) also bakes per-cell safe boundary edges. Their
  hidden carrier, *"Safe terrain collision (generated) player edges (generated)"*,
  sits under the Level root beside Ground, **outside** the terrain composites:
  colliders changing beneath a Manual CompositeCollider2D make Unity regenerate it
  (empty) at runtime. Do not reparent it. Bakes still run only in the editor.

Validation (Unity CLI `run_script`):

- `ValidateSafeBox.cs` (Edit mode): variant, footprint, nonlethal interior, baked
  edges, palette entry/spacing, and actual EntityBrush painting.
- `ValidateSafeBoxPrototype.cs` (Edit mode): boundary geometry for full/partial/corner
  contact, planes, disable/destroy, multiple neighbors, terrain, and timing.
- `PrepareSafeBoxPlay.cs` (Edit mode) bakes a temporary terrain fixture; then run
  `ValidateSafeBoxPlay.cs` in Play mode (gameplay, connections, closures, support,
  profiling), and finally `PrepareSafeBoxPlay.Cleanup` to delete the fixture.
  Physical support must be validated in Play mode: Edit-mode preview scenes
  regenerate tilemap composites whenever any collider changes, which drops
  resting contacts there only.

### Lethal penetration

`KillsOnPenetration` marks a solid object as lethal when the player's movement
collider overlaps its interior by more than **0.03 world units**. Adjust
**Penetration Tolerance** on the component if needed. Surface contact and shallow
physics overlap remain safe. Triggers, disabled colliders, disabled properties,
and layers that do not collide with the player's current plane are ignored.

The Player prefab uses `PlayerPenetrationCheck`, a compatibility subclass of
`DamageablePenetrationCheck` that preserves existing serialized collider settings.
It checks before each physics step. After the complete banish or return volley
changes plane, all active `DamageablePenetrationCheck` components are checked.
Lethal overlap calls `Damageable.Kill()`; the player's On Killed event calls the
existing `PlayerLife.Kill()` death/scene-reload flow. Keep this
explicit check after all group members move, not inside `Banishable.SetPlane`.
Other teleport code should call `CheckNow()` after completing its changes.

The existing Box and Door prefabs carry the property. Their existing palette
entries inherit it without repainting. Tilemaps use each TerrainTile asset's
Kills On Penetration flag instead of this whole-object property.
The authored terrain CompositeCollider2D uses **Polygons** for lethal cells;
`TerrainCollision` builds a separate **Outlines** collider for safe cells.
Filled lethal geometry also detects fully enclosed bodies. Keep this component
on terrain maps and keep their colliders non-trigger. For another
existing entity, add the property to its prefab's collider
object or parent and preserve its existing palette entry. New entity types still
follow the prefab and palette workflow above.

With Unity open in Edit mode, rerun the isolated collision and palette-painting
checks using the installed Pipeline package:

```powershell
unity command --caller plugin --skill unity-cli run_script --file Tools/Validation/ValidatePenetration.cs --format json
unity command --caller plugin --skill unity-cli run_script --file Tools/Validation/ValidateTerrainTiles.cs --format json
```

The check uses a temporary preview scene and closes it afterwards; it does not
save or replace the working scene. Gameplay validation should additionally cover
self-banish into a wall, early recall, and automatic return while inside terrain.

### Killable objects and contact hazards

Add **Damageable** to the object's Rigidbody2D root to make it killable. Connect
its **On Killed** event to the object's death behavior. For a simple disappearing
object, select **Damageable > DestroyObject** in the event; for custom behavior,
connect your own component method. The player prefab already connects this event
to **PlayerLife > Kill**. No player-specific code or inheritance is needed for
other objects.

`Damageable.Kill()` sets **IsDead** before invoking the event and ignores repeated
calls or a disabled Damageable. For pooled objects, call `ResetLife()` explicitly
when spawning them again; disabling and re-enabling does not automatically revive
them. This is instant death, without health points or damage amounts.

For lethal solid/terrain penetration, also add **DamageablePenetrationCheck** and
assign the object's solid body collider. Use a simulated Rigidbody2D. Existing
objects do not become killable until they opt in. `KillsOnPenetration` remains the
hazard marker and keeps its penetration tolerance.

Add **KillsOnContact** on the hazard's collider object to kill Damageable bodies
on 2D trigger or solid collision contact. Enter and stay callbacks are supported;
trigger sensors and objects without Damageable are ignored. Physics layer rules
still apply. At least one participating object needs a Rigidbody2D.

### Door wires

The Door prefab carries `DoorWires`, which draws a dotted wire from each switch in
the door's **Switches** list to the door, in edit mode and in play mode. A wire is
white while its switch is in the state the door's **Condition** asks for (pressed
for AllPressed, released for AllReleased) and red otherwise, so a door is open
exactly when all its wires are white.

Wires take right-angle routes on the tile grid: the cheapest path where a step
through solid terrain (tilemaps with a `TilemapCollider2D` on the switch's plane)
costs less than a step through air, and every bend adds a turn cost. Tune
**Terrain/Air Step Cost**, **Turn Cost** and **Search Margin** on the prefab. Wires
re-route when either end moves or terrain is painted. Each wire takes its switch's
layer, sorting layer and material, so it hides on the other plane like the switch.
Dots have no colliders, are hidden from the Hierarchy and are never saved.

### Spike

Paint **Spike** from **EntityPalette** at **(14, 3, 0)** with **EntityBrush** onto
**Level > Entities**. The prefab is `Assets/Prefabs/Entities/Spike.prefab`, with a
steel triangle sprite (`EntitySpike.png`, 1 x 0.5 units at unit scale) and a
matching triangular trigger. It carries
`KillsOnContact` and has no `Banishable` component. It follows the same editor
plane assignment and preview materials as other static entities.

Select a painted spike and change **Cardinal Orientation > Facing** to **Up**,
**Right**, **Down**, or **Left**. The sprite and hitbox rotate together in exact
90-degree steps around the cell center. Keep the root transform unrotated: its
bottom-left grid anchor stays fixed. Direction is a per-instance prefab override;
changing the prefab's default affects instances without that override. Replace
the sprite on the **Sprite** child and adjust its PolygonCollider2D to match any
new artwork.

Run `Tools/Validation/ValidateSpike.cs` through the Unity CLI `run_script`
command to check orientation, damageable contact handlers, non-player penetration
death, player event wiring, and connected palette painting in an isolated preview
scene. Contact-handler checks invoke callbacks directly; gameplay physics and
scene reload should also be exercised in Play mode.

### Gravity reversal area

Paint `GravityReversalArea` from `EntityPalette` with `EntityBrush` onto the
level's `Entities` child. Its palette entry starts at `(2, 3, 0)` and spans
four columns; `GoalFlag` occupies the following entry at x=7.

Set **Area Size** on `GravityReversalArea` to define its rectangular width and
height in grid units (default 4x4). The trigger and translucent visual (a white
tiled sprite tinted cyan by its SpriteRenderer color) resize together, keeping the root at the bottom-left corner. **Affected Layers**
limits which dynamic Rigidbody2D objects it affects; the physics layer collision
matrix also applies, including the project's plane separation.

Gravity reverses while any solid collider belonging to a body overlaps an area
and restores after leaving all areas or disabling them. Overlapping areas do not
cancel one another. Zero-gravity bodies remain at zero. The player checks for
ground in the gravity direction and jumps away from it, including ceiling jumps;
fall speed and wall slides also follow that direction. Dash input remains in
world directions. This assumes the project's vertical, downward global gravity.

### Gravity launch area

Paint `GravityLaunchArea` from `EntityPalette` at **(9, 3, 0)** onto the
level's `Entities` child using `EntityBrush`. This orange **4x4** prefab variant
inherits the reversal area's trigger, bottom-left pivot, and resizing behavior.
Its **Gravity Multiplier** defaults to **5**, accelerating affected bodies upward
with five times their usual gravity strength. Adjust it on the prefab or a placed
instance; the regular reversal area defaults to 1.

Overlapping fields use the strongest multiplier without stacking. Leaving or
disabling a field restores the remaining field's strength, or normal gravity
after the last field. Velocity is never reset by a field transition, so bodies
keep rising on exit and normal gravity gradually pulls them back down.
The player bypasses its normal fall-speed cap inside boosted fields; normal
jump, dash, wall-slide, and collision behavior still apply. Zero-gravity bodies
remain unaffected.

### Goal flag

`Assets/Prefabs/Entities/GoalFlag.prefab` is a green flag with a **1x2** trigger
footprint and a bottom-left root. Its palette entry is at **(7, 3, 0)**. Select
`EntityPalette`, use `EntityBrush`, pick the flag's bottom-left cell, and paint
onto **Level > Entities**. Placed flags remain prefab instances. Edit the Pole,
Flag, and Base child SpriteRenderers to replace the placeholder visuals.

Only an active player's solid collider activates the goal; boxes and trigger
sensors do not. The flag follows the existing entity plane/layer rules, so the
player must touch it on its plane. Completion is queued once per overlap and
saves before leaving the level. If saving fails or the level is locked or absent
from the catalog, it stays in the level and logs an error; leave the trigger and
touch it again to retry. Add further scenes to the catalog and include them in
the build to expand progression; currently only `Level_01` is configured.
