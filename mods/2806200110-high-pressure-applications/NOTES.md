# High Pressure Applications (2806200110) — extraction notes

**Author style:** vanilla configs (`BuildingTemplates.CreateBuildingDef`), PLib **options only**
(`HPA_ModSettings`, ships a separate `PLib.dll`). Registration is scattered: pipes/bridges in
`HarmonyPatches`, pumps/valves each in their own `*TechMod` / `*UI` patch class (very long
nested namespaces — note the Windows long-path issue below).

**Buildings (8):** `PressureGasPump`, `PressureLiquidPump`, `HighPressureGasConduit`,
`HighPressureLiquidConduit`, `HighPressureGasConduitBridge`, `HighPressureLiquidConduitBridge`,
`DecompressionGasValve`, `DecompressionLiquidValve`.

## Option-dependent values (defaults committed)

`HPA_ModSettings` (PLib, `[RestartRequired]`): `HPGas` default **10** (range 2–10),
`HPLiquid` default **40** (range 11–100). These feed:

| Building | Formula | Committed value |
|---|---|---|
| PressureGasPump wattage | `HPGas * 240` | 2400 W |
| PressureGasPump pump rate / storage | `HPGas` | 10 kg/s / 10 kg |
| PressureLiquidPump wattage | `HPLiquid * 240 / 10` | 960 W |
| PressureLiquidPump pump rate / storage | `HPLiquid` | 40 kg/s / 40 kg |
| Gas pipe capacity (Effect text) | `HPGas` | 10 kg |
| Liquid pipe capacity (Effect text) | `HPLiquid` | 40 kg |

## Registration map (which class registers what)

| Building | Strings + plan screen | Tech |
|---|---|---|
| Both pipes + both bridges | `HarmonyPatches.HighPressure_GeneratedBuildings_LoadGeneratedBuildings` (gas→HVAC, liquid→Plumbing) | `HarmonyPatches.HighPressure_Db_Initialize` (gas→HVAC, liquid→LiquidTemperature) |
| PressureGasPump | `PressureGasPumpUI` (HVAC) | `PressureGasPumpTechMod` (ValveMiniaturization) |
| PressureLiquidPump | `PressureLiquidPumpUI` (Plumbing) | `PressureLiquidPumpTechMod` (ValveMiniaturization) |
| DecompressionGasValve | `DecompressionGasValveUI` (HVAC) | `DecompressionGasValveTechMod` (HVAC) |
| DecompressionLiquidValve | `DecompressionLiquidValveUI` (Plumbing) | `DecompressionLiquidValveTechMod` (LiquidTemperature) |

All plan-screen adds use `ModUtil.AddBuildingToPlanScreen(category, id)` with no subcategory
→ "uncategorized".

## Quirks

- **Pipes are drop-in replacements on the normal conduit networks.** The high-pressure
  capacity is enforced by Harmony transpilers on `ConduitFlow` (`AddElement`, `UpdateConduit`,
  `IsConduitFull`) via the `Pressurized` component — not by the building defs. Vanilla pipes
  still carry vanilla capacity; HPA cells carry `HPGas`/`HPLiquid`.
- `HighPressureLiquidConduitConfig` sets `ThermalConductivity = 1.3f` then overwrites with
  `1E-05f` — the final value wins.
- Pump logic port and power port share the same cell `(0,1)`.
- Valves are `Reservoir`-style: `ConduitConsumer` (forceAlwaysSatisfied) + `Storage`
  (10 kg gas / 40 kg liquid) + `ConduitDispenser`.
- `GeneratedBuildings.RegisterWithOverlay(GasVentIDs, "GasPump")` in PressureGasPumpConfig
  registers the **vanilla** pump ID — a mod bug, harmless for extraction.
- **Windows long-path issue:** the decompiled per-building patch classes live in absurdly long
  namespace directories. Use `ilspycmd -t <FullTypeName>` to stdout (what the refresh script
  does) instead of whole-project decompile, or prefix paths with `\\?\`.

## How to update after a mod/game update

1. `tools\Refresh-ModSources.ps1` → diff the 8 config classes + `HPA_ModSettings` (+ patch classes).
2. Re-check the wattage/capacity formulas against `HPA_ModSettings` defaults.
3. Update `buildings.json`, run `tools\Build-ModDatabase.ps1`.
