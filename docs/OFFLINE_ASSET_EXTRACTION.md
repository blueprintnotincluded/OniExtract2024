# Offline asset extraction

How to pull art and data straight out of the game's Unity asset files without running the
game, using [`tools/AssetDump`](../tools/AssetDump). The mod reads what the running game has
loaded; this reads what is on disk. Use it for static assets that the export never reaches
(backdrops, materials, shader inputs, DLC kanims) and for one-off pulls that do not belong in
the export.

First used for the rocket-interior backdrop; that worked example closes this page, and its
website handoff is [WEBSITE_ROCKET_INTERIORS.md](WEBSITE_ROCKET_INTERIORS.md).

---

## Setup

Requires the .NET 8 SDK or newer and the GitHub CLI (`gh`), signed in. The tool targets
net8.0 but rolls forward to the newest installed runtime, so a .NET 8 runtime is not needed.

```powershell
.\tools\AssetDump\Get-ClassData.ps1      # once, and again after the game moves to a new Unity version
dotnet build tools\AssetDump
```

`Get-ClassData.ps1` downloads `classdata.tpk` (gitignored) beside the tool. Shipped ONI builds
**strip the type trees** from their asset files, so nothing can read an object's fields
without a database of Unity's class layouts. AssetRipper publishes that database, but only as a
CI artifact of [AssetRipper/Tpk](https://github.com/AssetRipper/Tpk): its releases have no files.
The script takes the newest successful `Type Tree Tpk` run on master.

The game was Unity `6000.3.5f2` when this was written. Every command reads the version from the
file, so nothing needs changing when the game upgrades, except refreshing the `.tpk` if it
predates the new version.

## Commands

```powershell
$dump = 'dotnet run --no-build --project tools\AssetDump --'
```

| Command | What it does |
|---|---|
| `list <file\|all> [regex] [--type T,...]` | One tab-separated line per asset: file, class, path id, name, size. Defaults to textures, sprites, materials, text assets and texture arrays. |
| `material <file\|all> <regex>` | Each matching material's texture slots (resolved to texture names), floats and colours. Shader inputs such as parallax factors and tiling scales live here. |
| `textures <file\|all> <regex> <outDir> [--alpha]` | Decodes matching `Texture2D`s to PNG with alpha intact. `--alpha` also writes `.onblack.png` and `.alpha.png`; see the gotchas. |
| `textassets <file\|all> <regex> <outDir>` | Writes matching `TextAsset`s as `.bytes`. Kanim `_build` and `_anim` files are text assets. |

`<file>` is relative to `OxygenNotIncluded_Data`, for example `sharedassets2.assets` or
`StreamingAssets/expansion1_bundle`. `all` searches every `.assets` file, every `level*` scene
and every `StreamingAssets/*_bundle`; start with `list all <regex>` when you do not know where
something lives. Asset names are not unique (`sharedassets0.assets` alone has four
`outline_0` textures), so when a name repeats within one run the later files get its path id
appended, as in `outline_0__1261.png`, rather than overwriting the first. `--data <dir>` points at a different install (default: Steam's), and
`--tpk <file>` at a different class database.

```powershell
iex "$dump list all 'starmap_landed'"
iex "$dump material sharedassets2.assets '^Stars_Surface$'"
iex "$dump textures StreamingAssets/expansion1_bundle '^starmap_landed_surface_0$' out --alpha"
iex "$dump textassets StreamingAssets/expansion1_bundle '^starmap_landed_surface_(build|anim)$' out"
.\tools\Parse-KanimBuild.ps1 out\starmap_landed_surface_build.bytes
```

## Where things live

| File | Holds |
|---|---|
| `sharedassets0.assets` | Base-game kanim textures (`<anim>_0`) and their `_build` / `_anim` text assets, tile atlases (`tiles_*`), element textures, UI sprite atlases. |
| `sharedassets2.assets` | Background and effect materials and their textures: `Stars_*`, `space_*`, gas, liquid, `bgarray*`. |
| `resources.assets` | Fonts and a few UI textures. Little of use. |
| `StreamingAssets/<dlc>_bundle` | **All DLC art**: `expansion1_bundle` is Spaced Out, then `dlc2_bundle`... A search of the `.assets` files alone never finds any DLC kanim. |
| `StreamingAssets/hires_<dlc>_bundle` | No textures or text assets: `hires_expansion1_bundle` is one `GameObject` and one `MonoBehaviour`. What reads it is not established. |

A kanim named `foo_kanim` in game code appears on disk as the texture `foo_0` (more atlas pages
are `foo_1`, ...) and the text assets `foo_build` and `foo_anim`.

## Gotchas

- **The PNG writer is an undeclared dependency.** `AssetsTools.NET.Texture` writes PNGs
  through `StbImageWriteSharp` but does not list it, so the first `textures` call throws
  `FileNotFoundException`. The project references it explicitly.
- **Do not reach for ImageSharp 3 or later.** It needs a licence key for non-open-source use and
  warns on every build. The tool needs no image library beyond the one above.
- **Layered textures look broken in a normal viewer.** ONI's background layers carry their
  shapes in alpha and leave arbitrary colour where alpha is 0, so the plain PNG shows orange and
  purple blobs. `--alpha` writes the colour multiplied onto black and the alpha as greyscale.
- **A picture on screen need not be a texture you can find by name.** Two things draw without
  any `Renderer` in the scene: `TerrainBG` draws the sky and world backdrop with
  `Graphics.DrawMesh`, and kanims draw through `KAnimBatchManager`. Neither shows up in a search
  for scene objects, and a kanim's art shows up only under its own name.

## When offline search is not enough

If you cannot tell *which* asset draws something, ask the running game, then come back offline
to extract it. A throwaway pause-screen button that logs the following answered it for the
rocket interior in one run:

- `TerrainBG`'s `Material` fields, with each texture slot (`Material.GetTexturePropertyNames()`);
- every enabled `Camera` (depth, culling mask, target);
- every enabled `Renderer` overlapping the main camera's view;
- every `KBatchedAnimController` in the active world with its `AnimFiles`. This is the line
  that found the moon.

Do not use `UnityEngine.GeometryUtility` in mod code for the view test. It drags in a
netstandard 2.1 reference and fails the build with `CS1705`. Compare `Rect`s in XY instead;
every game camera is orthographic.

## Worked example: rocket interiors

| Piece | Source | Found by |
|---|---|---|
| Layouts | `StreamingAssets/dlc/expansion1/templates/interiors/habitat_{small,medium}.yaml`: plain template YAML, no extraction needed | searching `StreamingAssets` for `interior` |
| Star field | `TerrainBG.starsMaterial_surface` → material `Stars_Surface` in `sharedassets2.assets` → `space_day_00..02` and `space_00..02` | decompiling `TerrainBG`, then `material` |
| Moon | Entity `OrbitalBG` with kanim `starmap_landed_surface_kanim` (`OrbitalTypeCategories.landed`) → `starmap_landed_surface_0` in `StreamingAssets/expansion1_bundle` | the in-game probe, then `list all` |
| Black vignette | Not found as an asset | the website draws it |

The in-game half lives in [GAME_INTERNALS.md → Rocket interiors](GAME_INTERNALS.md#rocket-interiors).
