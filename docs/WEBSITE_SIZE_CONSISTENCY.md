# Website handoff: building image sizes and connection-sprite geometry

Direction: export → website. This export (OniExtract2024, 2026-10-18) changes how big and where
building art lands on the site, and adds one file the importer
(`app/api/batch/convert-export-2024.ts`) must read. Motivation: the Transit Tube rendered at
about 40% of its size, several icons were cut off at the edge of their render, effects (steam,
sparks, a stray swoosh) set the size of others, and the site carried a hand-made rect for
FairGasWallPump to compensate. The goal on the export side was sizes right by construction, so
the site stops inferring geometry and stops pinning overrides.

The export files are in their usual places; nothing moved, and no existing field was renamed or
reshaped. Field reference in the export repo: `docs/EXPORT_SCHEMA.md`.

---

## 1. What changed in the export

| File | Change |
|---|---|
| `connection_sprite_cells.json` (export root, **new**) | Where the cell sits on each connectable's 16 sprites. **The importer must read this.** §2.1 |
| `building_image_report.json` (export root, **new**) | How each icon was rendered. Diagnostic only, **do not import**. Useful to explain the icon diff (§3). |
| `ui_image/*.png` | 95 icons changed in content (§3). 330 more differ in bytes only, see §5. |
| `building.json` `uiImageRect`, `ui_image_rects.json` | Values changed for 104 buildings and 1 terrain feature; shapes unchanged; every rect still matches its PNG's aspect. |
| `connection_sprites/*/*.png` | No change in content. |

### `connection_sprite_cells.json`

```jsonc
{
  "TravelTube": { "canvas": { "w": 240, "h": 240 }, "cell": { "x": 70.0, "y": 70.0, "w": 100.0, "h": 100.0 } },
  "Wire":       { "canvas": { "w": 106, "h": 106 }, "cell": { "x": 3.0,  "y": 3.0,  "w": 100.0, "h": 100.0 } },
  "Tile":       { "canvas": { "w": 192, "h": 192 }, "cell": { "x": 32.0, "y": 32.0, "w": 128.0, "h": 128.0 } }
}
```

- One entry per `connection_sprites/<prefabId>/` directory (31 today), keyed by the same id.
  All 16 PNGs of a connectable share it.
- Pixels, measured like the PNG: origin top-left, `+y` down. `canvas` is the PNG size. `cell`
  is the building's one cell on that canvas, so `cell.w` is pixels per cell (100 for the 15
  utilities, the atlas resolution for the 16 tiles). Values are real numbers.
- Everything outside `cell` is overhang into the neighbours, in any state, **including
  `15.png`**: a tile's disconnected borders, the 2–4 px outline of a wire or pipe, the transit
  tube's corner fillets (its all-connected state is 2.4 cells wide). That last one is why
  measuring `15.png` gave the tube a scale of ~1.0 instead of 2.4.
- The export checks, every run, that each canvas is its PNGs' size and that `15.png` covers the
  declared cell.

---

## 2. Importer changes

### 2.1 Connection-sprite scale: read it, stop measuring `15.png`

Today `measureConnectionScale` takes `canvas px ÷ 15.png opaque bbox px` and stores
`connectionScale {x, y}`; the renderer draws each sprite centre-anchored at
`100 × connectionScale`. Replace the measurement with the declared geometry:

- `connectionScale.x = canvas.w / cell.w`, `connectionScale.y = canvas.h / cell.h`.
- Anchor at the cell's centre. The cell is centred on the canvas for all 31 entries
  (`cell.x + cell.w/2 == canvas.w/2`, same for y), so the current centre anchoring is right.
  Do not rely on that silently: compute the offset `((canvas.w/2 − (cell.x + cell.w/2)) / cell.w,
  …)` in cells and either apply it or fail the import when it is not ~0.
- **Fallback while both exist:** when the file is absent, or has no entry for a connectable
  that has a directory, measure `15.png` as today and log which connectables used the fallback.
  Remove the fallback once every export you import carries the file.

Expected effect (canvas ÷ bbox measured by the export with alpha ≥ 8/255; your threshold may
move the second decimal):

| Connectables | Measured today | Declared | Visible change |
|---|---|---|---|
| `TravelTube` | ~1.00 | 2.40 | Drawn at full size: the bug this fixes. |
| Wires (`Wire`, `WireRefined`, `HighWattageWire`, …), `LogicWire`, `LogicRibbon`, `SolidConduit` | 1.01–1.03 | 1.04–1.06 | 2–4% larger; the outline now overhangs the neighbour as in game. |
| Gas and liquid conduits (all 6) | 1.02–1.04 | 1.08 | ~5% larger, same reason. |
| All 16 tiles | 1.50 | 1.50 | None. |

Update any snapshot or golden test that pins these scales.

### 2.2 Delete pinned overrides the export now gets right

- **`PINNED_ICONS` → `FairGasWallPump`: delete.** Its rect is now
  `{x: -0.065, y: -0.055, w: 1.12, h: 1.1}` (was 1.625 × 2.0 with a swoosh). Its siblings
  `FairGasWallVent` and `FairLiquidWallVent` changed pose too (§3).
- Review every other pin against the new `uiImageRect`: a pin the export now matches only hides
  the next regression. Keep a pin only for the known limits in §4, and say which one in a
  comment.

### 2.3 Contract document

In `convert-export-2024.md`, contract clause 5 ("`15.png` is our measuring stick") is replaced
by the sidecar. The export repo's copy (`docs/WEBSITE_POSTPROCESSING.md`) already says so and is
flagged as edited ahead of yours. Also update "Connection-sprite scale (measured, not assumed)",
pipeline step 3 and "Rendering model", which describe measuring.

---

## 3. Which icons changed, and why

So the import diff can be read as intended change, not churn. Every changed icon or rect falls
in one of these classes; nothing else changed.

**Effects no longer pick the pose (58 icons, plus 5 in the next class).** Icons used to be posed
in the most active state (`working_loop`), whose art includes steam, smoke, sparks, bubbles,
light cones and musical notes, and the crop and rect grew to include them. The export now
measures how far each state's art reaches, from the anim's own frames, and takes the most
active state whose reach is close to the footprint: usually `on`. Rects shrink to the body;
some images look calmer (the Electrolyzer's tank shows empty, because the water only appears
while working). AdvancedApothecary, AirFilter, AlgaeHabitat, ArtifactAnalysisStation (now
taller: the old pose had its arm lowered), AstronautTrainingCenter, Battery, BatteryMedium,
CO2Scrubber, ClothingFabricator, DLC1CosmicResearchCenter, DevHEPSpawner, DevLifeSupport,
EggCracker, Electrolyzer, EthanolDistillery, ExobaseHeadquarters, FabricatedWoodMaker,
FertilizerMaker, FishDeliveryPoint, FishPickUp, FishTrap, GasFilter, GasPump, Generator,
GravitasCreatureManipulator, HandSanitizer, HighEnergyParticleSpawner, IceCooledFan, Kiln,
LandingBeacon, LiquidCooledFan, LiquidHeater, LiquidReservoir, MicrobeMusher, MilkFeeder,
MineralDeoxidizer, MissileFabricator, NuclearReactor, PeatGenerator, PeterHan_FilteredGasPump,
Phonobox, Polymerizer, PowerTransformer, PowerTransformerSmall, RailGun, Refrigerator,
RustDeoxidizer, Sauna, SolarPanel, SpecialCargoBayCluster, SteamTurbine, StorageLocker,
SublimationStation, TemporalTearOpener, UnderwaterBreathingStation, UnderwaterMilkFeeder,
UnderwaterVentDrill, WoodGasGenerator.

**Art-only renders moved half a cell right (31 rects with the image unchanged, 9 with other
changes too).** Buildings that cannot be spawned outside their context (rocket-restricted
furniture, deprecated and hidden buildings, Gravitas props) are rendered from their art alone,
and every even-width one was drawn half a cell left of its footprint: `uiImageRect.x` was
0.5 too small, so the site drew them half a cell left. Image unchanged, `x` +0.5: BeachChair,
CreatureTrap, FilteredLiquidPump, GasConduitOverflow, GasConduitPreferentialFlow,
GravitasBathroomStall, GravitasContainer, LadderBed, LiquidConduitOverflow,
LiquidConduitPreferentialFlow, LonelyMailBox, LonelyMinionHouse, LuxuryBed, MachineShop,
MassiveHeatSink, MechanicalSurfboard, OrbitalResearchCenter, OxygenMaskStation,
POIDlc2ShowroomDoor, POIFacilityDoor, PropBeachChair, PropGravitasLabWall,
PropGravitasLabWindow, RocketControlStation, RoleStation, Shower, SodaFountain,
WarpConduitReceiver, WarpConduitSender, WashSink, WaterCooler. Shifted and re-posed: AtomicGarden,
FilteredGasPump, Headquarters, HeatCompressor, TeleportalPad. Shifted and meter placeholder
hidden: Bed, FlushToilet, Outhouse, RationBox.

**Meter placeholders hidden on art-only renders (18 icons).** A building's art carries a
`meter_target` symbol where a meter attaches; the game hides it when it builds the meter, which
art-only renders never do. WallToilet showed a bar labelled "meter target". Now hidden; the
rocket modules lose the placeholder bar and some get narrower. AirborneCreatureLure,
BatteryModule, CargoBayCluster, GasCargoBayCluster, KeroseneEngineClusterSmall,
LiquidCargoBayCluster, LiquidFuelTankCluster, OrbitalCargoModule, OxidizerTankCluster,
PioneerModule, SmallOxidizerTank, SolarPanelModule, SolidCargoBaySmall, SteamEngineCluster,
StorageLockerSmart, SugarEngine, WallToilet, WoodStorage.

**No longer cut off (4).** The render canvas had a fixed two-cell margin; it is now sized from
the art. FishFeeder (now reaches 2.18 cells below its footprint), MonumentMiddle (8.02 cells
wide, was cut at 7.62), SteamTurbine2 and VerticalWindTunnel (also re-posed: the steam and wind
are gone).

**Mod kanims with no bounds data (3).** The Wall Pumps mod's kanims carry no frame bounds, so
the effects rule could not see their swoosh until the export measured the frames itself:
FairGasWallPump (rect 1.62 × 2.0 → 1.12 × 1.1), FairGasWallVent, FairLiquidWallVent.

**Tie order (1).** States that score equally are now chosen in name order, not in an order that
moves when the game adds an anim: FloorSwitch shows `on_down`.

**Surroundings (2).** GlassExteriorWall changed colour: a glass panel joins the backwalls next to
it, so its render depended on what stood beside the spot it was rendered at. The export now
renders in a clear spot it finds itself, so this one should not move again.
GeyserGeneric_murky_brine is now 7.96 cells wide instead of 4.21: its own art has a speck about
four cells to the right (`geyser_art`, frame 2), which the old fixed canvas cut off. Faithful to
the game; pin it if it looks wrong on the site.

To see why any icon changed between two exports, diff `building_image_report.json`: its `anim`,
`frame`, `render` (`spawned` or `artOnly`) and `candidates` (the states weighed, with sizes) say
it in one line.

---

## 4. Known limits (deliberate; the export lists them in its validator)

- **SpecialCargoBayCluster** renders only its window strip (art-only, partial art), and the
  three **Staterpillar** buildings render a bare egg: both need a pose override on the export
  side. Keep or add a site pin until then.
- **RadiationLight**'s only active state draws its light cone (2.4 × 3.2 cells on a 1×1).
- **LiquidCooledFan** (deprecated) has no water jug: the jug was its meter placeholder.
- **BatterySmart**'s colour flips between exports (its own charge/logic state art); expect it in
  the diff now and then.

---

## 5. Importer performance notes

- Reading `connection_sprite_cells.json` removes the per-import decode and alpha scan of 31
  `15.png` files; it is a 5.5 KB JSON read.
- 330 icons differ from the previous export in bytes but not in content (within 0.5% outline
  and 4/255 colour; anti-aliased edge pixels move by 1–2 levels). Cause: the render canvas is
  now sized per building, which moves the render camera, and floating-point rounding shifts
  the edges a little. It is tied to the canvas size, which `building_image_report.json` records
  (`canvasPx`): between two runs of the same build, exactly the icons whose canvas changed
  differed in bytes (9 of 13). Your perceptual comparison already keeps these from being
  rewritten; this is the volume to expect in this one import, and far fewer after.
- The 16 tile and 15 utility sprite directories are byte-identical to the previous export
  except for 73 re-encoded but equivalent PNGs; nothing for the content-aware mirror to copy
  beyond that.

---

## 6. Verifying the import

1. Transit Tube placed in a straight run and at a junction: a tube segment fills its cell and
   the junction fillets overhang into the neighbours, at the size of the in-game tube.
2. Wires and pipes in a long run show no gaps and no visible change except a slightly stronger
   outline overlap.
3. FairGasWallPump with its pin deleted: a 1×1 icon, no swoosh above it.
4. SteamTurbine2: no steam cloud below; the intake pipes still reach about 1.3 cells below the
   footprint.
5. A bed or Gravitas prop (any even-width art-only building): centred on its footprint, no
   longer half a cell left.
6. The importer log names no connectable as using the `15.png` fallback.
