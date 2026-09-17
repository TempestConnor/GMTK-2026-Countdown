# Project instructions

## Creating level entities

Before creating or modifying placeable level entities, read `README.md`,
especially "Adding new entities" and "Palette layout convention". The README
is the source of truth for the level-authoring workflow.

Treat requests to create a new placeable entity as including all of the following:

- Create and configure its reusable prefab in `Assets/Prefabs/Entities/`.
- Add it to `EntityPalette` for painting with `EntityBrush` onto the level's
  `Entities` child.
- Follow the README's root/pivot, grid alignment, and palette spacing conventions.
  Inspect the current palette to calculate the next free position; do not assume
  the example layout in the README is still current.
- Preserve prefab connections so prefab changes propagate to placed instances.
- Verify the prefab and palette entry, and verify painting onto the level's
  `Entities` child. Report any verification that could not be completed.

The task is incomplete until both the prefab and palette entry exist. Apply this
workflow regardless of whether objects are created through Unity MCP, editor
scripts, or scene/prefab edits, unless the user explicitly requests an exception.

When modifying an existing entity, update its existing prefab and preserve its
palette entry instead of creating a duplicate entry.

Terrain follows the README's Tile/`TerrainPalette` workflow. These entity rules
apply to placeable level content, not automatically to cameras, UI, runtime-only
helper objects, or other scene infrastructure.
