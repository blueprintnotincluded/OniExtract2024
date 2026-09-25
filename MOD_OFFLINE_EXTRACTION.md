# Extracting Mod Buildables Without Launching the Game

Research findings (2026-07-18) on whether mod art and building data can be extracted
from installed mod files directly, instead of teaching the Extract mod about each mod
and loading them all into the game (some are mutually incompatible).

**Conclusion: yes, fully offline extraction is viable.** Art ships as loose kanim
files; properties are decompilable C# in the mod DLL. No game launch, no mod
compatibility concerns, no Extract-mod changes needed.

> **Status update (2026-07-19): implemented.** The property pipeline is built and all 5
> buildable mods (15 buildings) are extracted to `mods/mod_database.json`. The operational
> playbook — per-mod notes, refresh/merge scripts, schema — is **[mods/README.md](mods/README.md)**.
> This file remains the research background (kanim format, decompile findings).

---

## Where mod files live

Installed Steam mods: `C:\Users\sinep\Documents\Klei\OxygenNotIncluded\mods\Steam\<workshop-id>\`

Typical layout (Airlock Door, id 2094698134):

```
2094698134\
  AirlockDoor.dll          ← building defs, components, Harmony patches
  mod.yaml / mod_info.yaml ← title, supported game version
  translations\*.po
  anim\assets\<kanim-name>\
    <name>.png             ← texture atlas (e.g. 1024x1024)
    <name>_build.bytes     ← sprite/symbol definitions ("BILD" format)
    <name>_anim.bytes      ← animation data ("ANIM" format)
```

## Survey of installed mods (July 2026)

All 28 installed mods were scanned: **every mod with custom art ships loose kanim
triplets; none use Unity asset bundles.** Mods with custom buildable art:

| Workshop ID | Mod | Kanims |
|---|---|---|
| 2094698134 | Airlock Door | airlock_door, airlock_door_insulated |
| 1866754178 | Drains | drain, solidDrain |
| 1840755803 | Buildable Natural Tile | 2 |
| 2806200110 | High Pressure Applications | 8 (pressure pumps/pipes/bridges/valves) |
| 2856555858 | Duplicant Stat Selector | 3 |
| 3544371748 | Splitters MK II | 3 |
| 3172734005 | Dupery Fixed | 1 |

Mods with 0 kanims either add no buildables or reuse base-game art (e.g. True Tiles).

> **Correction (2026-07-19): kanim count is NOT a reliable buildables signal.** A full
> decompile of every current mod DLL, grepping for `IBuildingConfig` implementations,
> shows exactly **5 mods add buildables**: Airlock Door (2), Drains (1), Buildable
> Natural Tile (1), High Pressure Applications (8), Splitters MK II (3) — 15 buildings.
> Duplicant Stat Selector and Dupery Fixed ship kanims but define **no** buildings
> (cosmetic/UI art). Use the `IBuildingConfig` scan (see mods/README.md) when vetting
> new mods.

---

## Art: the kanim triplet is fully parseable offline

### `_build.bytes` — "BILD" format (verified by parsing)

Binary layout (little-endian; Klei strings are `int32 length + UTF-8 bytes`):

```
char[4] magic        "BILD"
int32   version      (10 in current mods)
int32   numSymbols
int32   numFrames    (total across symbols)
kstring buildName    (e.g. "airlock_door")
per symbol:
  int32 hash         (symbol name, via trailing hash table)
  int32 path         (only when version > 9)
  int32 color
  int32 flags
  int32 frameCount
  per frame:
    int32 sourceFrameNum, duration, buildImageIdx
    float pivotX, pivotY, pivotW, pivotH
    float u1, v1, u2, v2          ← atlas UV rect
trailing hash table:
  int32 numHashes
  per entry: int32 hash, kstring name
```

Atlas pixel rect from UVs — **UVs are top-left origin, same as PNG pixels** (pixel-verified
2026-07-19 by cropping `ui` symbols; an earlier draft of this doc wrongly claimed bottom-left
origin and flipped with `(1 - v2)`, which produced misaligned crops):

```
pxX = u1 * atlasW
pxY = v1 * atlasH
pxW = (u2 - u1) * atlasW
pxH = (v2 - v1) * atlasH
```

**2x-scale quirk:** kanim art is authored at double resolution. The frame's
`pivotW/pivotH` "size" is 2x the atlas rect (e.g. `door_fg` reports 704x472 but
occupies 352x236 atlas pixels). The atlas rect is the real pixel data.

Verified end-to-end on `airlock_door_build.bytes`: 15 symbols decoded with names
(`door_fg`, `door_trim`, `doorclamp`, `lock`, `meter`, `place`, `ui`, ...), pivots,
and exact atlas rects. Working proof-of-concept parser: a PowerShell script that
dumps the full symbol table (see "Tooling" below).

Useful symbols for the website: the **`ui` symbol is the build-menu icon**; `place`
is the placement silhouette; `fx`/`meter`/`light` symbols can usually be skipped.

### `_anim.bytes` — "ANIM" format

Contains the animation banks: which symbol frames each animation frame uses and
their affine transforms. Needed to composite the **assembled default pose** (e.g.
`closed`, `off`) rather than individual sprite parts — this is what the in-game
snapshotter gets for free from `KBatchedAnimController`.

### Tooling

- **[kanimal-SE](https://github.com/skairunner/kanimal-SE)** — open-source C# CLI
  that converts kanim triplets → Spriter project / PNGs entirely offline. Since it's
  C#, its reader classes can be vendored/referenced directly rather than reimplementing.
- Proof-of-concept BILD parser written during this research:
  [`tools/Parse-KanimBuild.ps1`](tools/Parse-KanimBuild.ps1) — parses header,
  symbols, frames, hash table, and computes atlas pixel rects. Easy to port to C#
  if not using kanimal-SE.

  ```powershell
  .\tools\Parse-KanimBuild.ps1 -BuildFile <path>\airlock_door_build.bytes `
                               -PngFile <path>\airlock_door.png
  ```
  - PowerShell 5.1 gotcha hit while writing it: `-shl` on a `[byte]` overflows
    silently (`[byte]4 -shl 8` → 0). Cast to `[int]` before shifting.

---

## Properties: decompile the mod DLL with ilspycmd

`ilspycmd` (already installed as a dotnet global tool — see GAME_INTERNALS.md) reads
building definitions straight out of mod DLLs. Both authoring styles decompile cleanly:

### PLib style (PeterHan mods, e.g. Airlock Door)

`AirlockDoorConfig.CreateBuilding()` is a single `PBuilding` object initializer that
literally lists everything:

```csharp
new PBuilding("PAirlockDoor", ...) {
    Width = 3, Height = 2, HP = 30,
    Animation = "airlock_door_kanim",
    Category = "Base", SubCategory = "doors",
    ConstructionTime = 60f,
    Decor = PENALTY.TIER1,
    Ingredients = { new BuildIngredient("RefinedMetal", 4) },
    Placement = (BuildLocationRule)6,
    PowerInput = new PowerRequirement(120f, new CellOffset(0, 0)),
    LogicIO = { Port.InputPort(...) },
    Tech = "ImprovedGasPiping",
    ...
}
```

Plus `CreateBuildingDef()` overrides: `IsFoundation`, `ThermalConductivity`, etc.

### Vanilla style (e.g. Drains)

```csharp
BuildingTemplates.CreateBuildingDef("Drain", 1, 1, "drain_kanim", 100, 30f,
    MASS, MATERIALS.ALL_METALS, 1600f, (BuildLocationRule)6,
    PENALTY.TIER0, NOISE_POLLUTION.NONE, 0.2f);
// width, height, kanim, HP, construction time, mass, materials,
// melting point, placement, decor, noise, thermal conductivity
```

Followed by field assignments: `OutputConduitType`, `UtilityOutputOffset`,
`IsFoundation`, `PermittedRotations`, ...

### Caveats (the bespoke-per-mod part)

1. **TUNING constant references** — `MATERIALS.ALL_METALS`,
   `CONSTRUCTION_MASS_KG.TIER2`, `PENALTY.TIER1` etc. are static arrays in the
   game's `Assembly-CSharp.dll`. Build a one-time lookup table by decompiling the
   game DLL the same way (path in GAME_INTERNALS.md).
2. **Component-phase properties** — things added in `ConfigureBuildingTemplate` /
   `DoPostConfigureComplete` (conduit consumers, storage, access control, ...) are
   readable in decompiled source but have no uniform schema; interpret per mod.
3. **Config-dependent defs** — e.g. Drains picks `drain_kanim` vs `solidDrain_kanim`
   from a mod option at runtime. Pick the default (or both) explicitly.
4. **Enum casts** — decompiled output shows raw casts like `(BuildLocationRule)6`,
   `(ConduitType)2` (= Liquid); resolve names against the game assembly's enums.

### Commands

```powershell
# list classes (find the *Config types)
ilspycmd "<mod>.dll" -l c | Select-String Config

# decompile one building config
ilspycmd "<mod>.dll" -t "Namespace.SomeBuildingConfig"
```

---

## Recommended pipeline

Keep the Extract mod as-is for base-game export. For mods, build a small offline
pipeline instead of loading them into the game:

1. **Art (generic, one-time code):** kanimal-SE (or vendored reader classes) over
   each mod's `anim\assets\*` → sprite parts + assembled default-pose PNG + `ui` icon.
2. **Properties (bespoke per mod):** `ilspycmd` decompile → transcribe/extract the
   `BuildingDef` values into the export JSON schema (EXPORT_SCHEMA.md), resolving
   TUNING constants via a lookup table generated once from the game assembly.

This avoids mod-compatibility conflicts entirely — no two mods ever need to be
loaded together, and the game never has to run.
