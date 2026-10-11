# Website handoff: rocket-interior layouts and backdrop art

Direction: game files → website, **once**. This is not an export and nothing in `export/`
changes. The layouts and art below were taken straight from the game install, packed into a zip
and carried across by hand. They change only if Klei redraws the rocket interior, so the site can
keep them as static assets. Motivation: rocket blueprints are built inside a habitat module's
interior, and showing them in that interior (fixed walls, ports and window, with the game's star
and moon backdrop) makes rocket blueprints easier to read and share.

How the files were extracted, and how to do it again: [OFFLINE_ASSET_EXTRACTION.md](OFFLINE_ASSET_EXTRACTION.md).
Game-side facts: [GAME_INTERNALS.md → Rocket interiors](GAME_INTERNALS.md#rocket-interiors).

---

## 1. What is in the zip

| Path | What |
|---|---|
| `layouts/habitat_small.yaml` | Interior of `HabitatModuleSmall` (the nose cone), verbatim from the game. |
| `layouts/habitat_medium.yaml` | Interior of `HabitatModuleMedium`, verbatim from the game. |
| `backdrop/moon_landed.png` | The moon shown while the rocket is on the ground. 1024×512, transparent. |
| `backdrop/moon_orbit.png` | A full moon from the game's `orbit` backdrop. When the game shows it is not established (section 3). |
| `backdrop/stars_landed/` | The star field while landed: `space_day_00..02` and `space_00..02`. |
| `backdrop/stars_in_flight/` | The star field while in flight: `space_deepspace_*`, plus `traveling_stars.png`. |
| `reference/rocket-small.png`, `reference/rocket-large.png` | In-game screenshots of both interiors, landed. Match against these. |

## 2. Layouts

Both files are Klei's `TemplateContainer` YAML. Locations are cell offsets from the template's
centre, with **y up** as in the game. A missing `location_x` or `location_y` means 0.

- `cells`: the element in every cell. `Steel` = wall, `Diamond` = window, `Vacuum` = the room.
- `buildings`: what stands on them, each with `id`, location and `element`.
- `otherEntities`: the door.

```
habitat_small (8×8)                     habitat_medium (12×11)
  4      ##loli##                         6          ########
  3    #### . .####                       5  ####lo############li####
  2    [] . . . .[]                       4  ## . . . . . . . . . .##
  1  #### . . . .####                     3  [] . . . . . . . . . .[]
  0  [] . . . . . .[]                     2  [] . . . . . . . . . .[]
 -1  ## . . . . . .##                     1  [] . . . . . . . . . .[]
 -2  ## . .CS . . .##                     0  ## . . . . . . . . . .##
 -3  ####go####gi####                    -1  ## . . . . . . . . . .##
x:   -3-2-1 0 1 2 3 4                    -2  ## . . . . . . . . . .##
                                         -3  ## . . . .CS . . . . .##
                                         -4  ####go############gi####
                                         x:  -5-4-3-2-1 0 1 2 3 4 5 6
```

| Glyph | Prefab id | Size | Material | In the export as |
|---|---|---|---|---|
| `##` | `RocketWallTile` | 1×1 | Steel | `building.json`; also `connection_sprites/` (it joins to its neighbours) |
| `[]` | `RocketEnvelopeWindowTile` | 1×1 | Diamond | `building.json`; also `connection_sprites/` |
| `go` / `gi` | `RocketInteriorGasOutputPort` / `RocketInteriorGasInputPort` | 1×1 | Steel | `building.json` |
| `lo` / `li` | `RocketInteriorLiquidOutputPort` / `RocketInteriorLiquidInputPort` | 1×1 | Steel | `building.json` |
| `CS` | `RocketControlStation` | 2×2 | Cuprite | `building.json`. The template gives its origin cell; it covers that cell, the one to its right, and the row above. |
| — | `ClustercraftInteriorDoor` | | | `entities.json`, not `building.json`. It sits on the wall cell at small (-3,-2) / medium (-5,-3), and its art hangs outside the left wall. |

Every piece already has its art in the regular export (`ui_image/`, and `connection_sprites/`
for the two tiles). The zip adds no building art.

Ports, not power: the four ports are fixed. Power plugs (`RocketInteriorPowerPlug`) are not in
either template; players place their own.

**Intact and altered.** Players cannot remove these pieces without cheats, but some blueprints
are made with cheats or built outside a rocket. A suggestion:

- **Intact:** draw the template as fixed, uneditable scenery under the blueprint.
- **Altered:** draw the same template, but let the blueprint's own cells win. Any template piece
  the blueprint overwrites or leaves out shows what the player changed.

Aligning the template with a blueprint's coordinates is the website's call. This handoff does
not know how the site places a rocket blueprint's origin.

## 3. Backdrop

Back to front: star field, moon, the interior itself, then a black vignette.

### Star field

The game draws one full-screen layer with its `Klei/Stars` shader, which blends several
textures. While landed it uses the `stars_landed` set:

| File | Role |
|---|---|
| `space_day_00.png` (2048²) | Base: the blue nebula in the screenshots. Opaque. |
| `space_day_01.png` | Wispy smoke overlay. **The shape is in the alpha channel.** |
| `space_day_02.png` | Star-speck overlay. **The shape is in the alpha channel.** |
| `space_00..02.png` | The same three roles in darker purple, probably the night variant. The screenshots show the day set. |

Most image viewers show the overlay files as orange and purple blobs, because their colour is
arbitrary wherever alpha is 0. Composite them over the base with normal alpha blending and they
look right.

Shader settings, as starting points for tuning: texture scales 12×25 / 10×20 / 12×25 for
`_Tex0..2` (1×1 for the day set), parallax 0.01 and 0.02 for the two moving layers. How the shader
combines day and night was not decompiled. Tune by eye against the screenshots.

### Moon

`moon_landed.png` is a single image. It is not mirrored, tiled or animated (one symbol, the
same in all five frames). The art covers roughly the left 750×494 px of the 1024×512 texture;
the rest is transparent. Only the curved horizon is a real edge: the art stops in straight
cuts at the left, right and bottom, which in-game sit under the vignette, so the site needs a
vignette (or a crop) that hides them too. The game places the moon at 50% of the interior's
width and 35% of its height and does not move it, so any parallax on the site is a design
choice.

### Vignette

Not found as a game asset. The screenshots show an oval black fade whose size differs between the
two modules. A CSS radial gradient sized to each module is the expected approach.

## 4. What is not verified

- Only the **landed** view was checked in-game, for both modules (the screenshots). The
  in-flight star set comes from game code and was not looked at in-game. Whether the moon shows
  in flight is unknown.
- When `moon_orbit.png` is shown is not established; no game code was found that picks it.
- Day/night switching of the star field was inferred from the material's texture names, not
  observed.
- The moon's on-screen size in cells was not derived; match it against the screenshots.
