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
reference or UnityEvent wiring. The scene must be an unlocked catalog entry.

`Level_01` has a **Level Flow** object with `LevelCompletion`. Other win conditions
can still call **LevelCompletion.CompleteLevel**. For each new level, add a
scene-level object with `LevelCompletion` for menu navigation.
Escape returns to the menu without awarding completion. You can
also wire `ReturnToMenu()` to a UI button. This is scene infrastructure and does
not need an entity palette entry.

This project uses Unity's Tilemap system for terrain and the `GameObjectBrush`
(2D Tilemap Extras) for placing enemies, hazards, and other entities — both
painted the same way, from the Tile Palette window.

## Folder structure

| Path | Contents |
|---|---|
| `Assets/Tiles/` | `Tile`/`RuleTile` assets — the paintable terrain data |
| `Assets/Palettes/` | Tile Palette prefabs and brushes used to paint |
| `Assets/Prefabs/Level/LevelTemplate.prefab` | Reusable `Grid → Ground, Entities` skeleton every level is built from |
| `Assets/Prefabs/Entities/` | Enemy/hazard/pickup prefabs (create this folder as you add them) |
| `Assets/Scenes/Levels/` | One `.unity` scene per level |

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

Right now `Tile_Ground` and `Tile_Platform` both point at the same
`Assets/Tiles/Sprites/PlaceholderSquare.png` and are only told apart by a
color tint (brown vs. gray) on the Tile asset. When real art arrives:

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
   brand-new entity type, not reskinning an existing one.
5. **Sanity check:** open a level and confirm painted tiles show the new art
   with no gaps/seams — seams usually mean the PPU or sprite pixel size is
   off from the Grid's 1×1 cell size.

## Creating a new level

1. `File → New Scene → Lit 2D Scene` (gives you a Camera + Global Light 2D
   for free) and save it into `Assets/Scenes/Levels/` (e.g. `Level_02.unity`).
2. Drag `Assets/Prefabs/Level/LevelTemplate.prefab` into the scene.
3. Add the new scene to Build Settings (`File → Build Settings → Add Open
   Scenes`, or `manage_build(action="scenes")` if scripting it).

## Painting terrain

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
  The next entity added should start at x=14,
  and so on — always start at
  `(previous entity's right edge + 1)`.

### Gravity reversal area

Paint `GravityReversalArea` from `EntityPalette` with `EntityBrush` onto the
level's `Entities` child. Its palette entry starts at `(2, 3, 0)` and spans
four columns; `GoalFlag` occupies the following entry at x=7.

Set **Area Size** on `GravityReversalArea` to define its rectangular width and
height in grid units (default 4x4). The trigger and translucent cyan visual
resize together, keeping the root at the bottom-left corner. **Affected Layers**
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
