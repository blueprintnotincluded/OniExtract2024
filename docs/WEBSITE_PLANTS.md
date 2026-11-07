# Website handoff: wild plants rendered like geysers

For blueprintnotincluded/blueprintnotincluded, which places `Plant`-tagged entities as
terrain annotations (its PR #314). Written 2026-11-06 against game build U59-744825 and confirmed by the in-game runs of
2026-11-07; it moves to `docs/archive/` once the importer reads the fields below.

## What the export now ships

For every `Plant`-tagged prefab except the four branch prefabs (`ForestTreeBranch`,
`SpaceTreeBranch`, `SeaTreeBranch`, `VineBranch`, which the site already ignores):

1. **`ui_image/<prefabId>.png`** is a render of the spawned, fully grown plant at 200 px/cell,
   tight-cropped, alpha preserved, by the same path as the geysers. It replaces the build-menu
   icon. The sprite sync picks it up as a changed PNG.
2. **`ui_image_rects.json` has an entry** `<prefabId> → {x, y, w, h}` in cells, relative to the
   bottom-left of the footprint below, +y up, exactly as for geysers. The mod checks
   `w:h` against the PNG's pixel aspect at the end of every run.
3. **The footprint is on the entity in `database/entities.json`**, as three new fields present
   only on plants: `width` and `height` (integers, cells) and `footprintOffset` (`{x, y}`,
   integers): the box of the game's own `OccupyArea.OccupiedCellsOffsets`, with
   `footprintOffset` the box's bottom-left cell relative to the plant's own cell.

Everything else in the export is unchanged by the mod: buildings, geysers, connection
sprites and the other database files. Renders are not byte-stable between runs, so the
building PNGs re-render visually identical, as before.

## What the site has to read

`width` and `height` equal `round(kBoxCollider2D)` for every plant except the two trees
below, so the collider rounding keeps working everywhere else. Two things need the new fields:

- **Ceiling plants hang below their cell.** Five plants carry the `Hanging` tag and the game
  gives them `GenerateHangingOffsets`: Pincha Pepperplant (`SpiceVine`, 1×3), Dasha Saltvine
  (`SaltPlant`, 1×2), Lura Plant (`FlyTrapPlant`, 1×2), Seakomb (`KelpPlant`, 1×2) and Dew
  Dripper (`DewDripperPlant`, 1×2). Their `footprintOffset` is `(0, 1-height)`: the plant's
  cell is the **top** of its box. Anchoring the box's bottom-left at the plant's cell draws it
  one or two cells too high; anchor it at `cell + footprintOffset` instead. For every other
  plant `footprintOffset` is `(-floor((width-1)/2), 0)`, the building convention the site
  already uses, so reading it everywhere costs nothing.
- **The Arbor Tree and Bonbon Tree are drawn whole.** `ForestTree` and `SpaceTree` are
  rendered as the trunk plus a branch at every offset the game fixes for them, and their box
  takes the branches in: `width: 3, height: 3, footprintOffset: {x: -1, y: 0}` where the
  collider says 1×2. The trunk cell is the bottom-centre of the box, as for a 3-wide building.
  A live tree carries at most five of the Arbor Tree's seven offsets (all five of the Bonbon
  Tree's), chosen at random, so the icon is the full canopy rather than any one tree.

## What was not done

- **Tower Kelp (`SeaTree`) and Ovagro Node (`VineMother`)** are rendered as their trunk
  alone (1×2). Their branches grow along whatever terrain is there, with no fixed offsets, so
  there is no canonical whole tree to draw.
- The branch prefabs keep the build-menu icon and have no rect, as before.

## Report from the in-game runs (2026-11-07)

| | |
|---|---|
| Plants rendered | 50 of 50 (the 54 `Plant`-tagged prefabs less the four branches); every rect passed the aspect check |
| Posed in something other than `idle_full` | `idle` for the Bluff Briar family (12 one-cell decor plants), `idle_open` for the Saturn Critter Trap, `idle_loop` for the Flue Coral, `grown` for the Bonbon Tree trunk, `basic_idle_full` for the Spindly Grubfruit Plant, `super_idle_full` for the Grubfruit Plant, `idle_full_closed` for the Clam (its own crop anim) |
| Footprints that differ from the collider rounding | `ForestTree`, `SpaceTree` (3×3 for 1×2) |
| Whole-tree render | Arbor Tree (7 branches) and Bonbon Tree (5 bare branches: fill frame 0, flower meters hidden) yes; Tower Kelp and Ovagro Node no |
| Determinism | the 48 plants rendered in both runs produced identical rects; renders are compared by size and outline, as for buildings |
| Everything else | byte-identical to the previous export: buildings, facades, connection sprites, the other database files |
