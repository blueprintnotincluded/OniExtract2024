# Export Regressions — Electrical Data Addition

Two regressions were introduced when electrical data was added to the export.

> **STATUS: RESOLVED.** Both fixed in `ExportBuilding.cs` (builds clean, all tests pass).
> Regression 1: `utilities[]` restored (full `BuildUtilityPort` port list incl. cell offsets;
> the new power fields are kept alongside it). Regression 2: `viewMode` now emits the
> game-native overlay string via an `OverlayModes.*.ID` lookup (`null` when there's no special
> overlay). The fixes take effect in the next in-game export after the mod DLL is rebuilt and
> the game is run. See `FRONTEND_INTEGRATION.md` §2/§2b and `EXPORT_SCHEMA.md`.

---

## Regression 1: `utilities[]` replaced — all non-power port offsets lost

### What existed before

Every building had a `utilities` array, one entry per connection port, each with a cell offset:

```json
"utilities": [
  { "type": "GasInput",    "offset": { "x": 0, "y": 0 }, "isSecondary": false },
  { "type": "GasOutput",   "offset": { "x": 1, "y": 2 }, "isSecondary": false },
  { "type": "LogicInput",  "offset": { "x": 0, "y": 1 }, "isSecondary": false },
  { "type": "PowerInput",  "offset": { "x": 0, "y": 0 }, "isSecondary": false }
]
```

Covered all 12 connection types: `PowerInput`, `PowerOutput`, `GasInput`, `GasOutput`,
`LiquidInput`, `LiquidOutput`, `LogicInput`, `LogicOutput`, `SolidInput`, `SolidOutput`,
`LogicReset`, `LogicRibbonInput`, `LogicRibbonOutput`.

### What the new export provides instead

- `powerInputOffset` / `powerOutputOffset` — power ports only, with cell offsets ✓
- `energyConsumer.baseWattageRating` — wattage, no offset
- `conduitConsumer` / `conduitDispenser` — gas/liquid/solid/logic conduit presence, **but no cell offsets**
- `battery.capacity` — storage, no offset

The new electrical fields are useful additions. The problem is that `utilities[]` was removed at
the same time, so the website lost offsets for every non-power port. The `conduitConsumer` /
`conduitDispenser` fields confirm that a port *exists* but don't say which cell it's on.

### Fix

Either restore `utilities[]` alongside the new fields (backward-compatible, zero risk), or add
`inputOffset` / `outputOffset` cell positions to `conduitConsumer` and `conduitDispenser`.
Restoring `utilities[]` is simpler since the data already existed.

---

## Regression 2: `viewMode` is `null` for all non-power buildings

### What existed before

`viewMode` was a Klei `HashedString` hex (e.g. `"0x9DB4F205"` = `"GasConduits"`). The website
decoded those hashes to know which overlay each building belongs to.

### What the new export provides

`viewMode` is now `null` for GasConduit, LiquidConduit, LogicWire, SolidConduit, and all
bridges. Only power buildings correctly emit `"power"`.

The website has a workaround in place (infer overlay from `sceneLayer`), but it is a fragile
fallback — it only works for buildings whose scene layer unambiguously identifies the overlay.

### Fix

Emit the overlay mode name as a lowercase string for all buildings that have one. Expected
values: `"power"`, `"gasconduit"`, `"liquidconduit"`, `"logic"`, `"oxygen"`, `"solidconveyor"`,
`"decor"`, `"light"`, `"temperature"`, `"rooms"`, `"radiation"`. Buildings with no special
overlay should emit `null` or `""`.

---

## Summary

| Field | Before | After | Impact |
|---|---|---|---|
| `utilities[]` | All port types + offsets | **Removed** | Gas/liquid/logic/solid port positions lost |
| `powerInputOffset` | Was inside `utilities[]` | New top-level field | Power-only; redundant with old utilities[] |
| `energyConsumer` | Not present | New ✓ | Wattage now available |
| `battery.capacity` | Not present | New ✓ | Storage capacity now available |
| `viewMode` | Hex hash string | `null` for most | Overlay assignment broken for non-power |

The new electrical fields are good additions. Restoring `utilities[]` and fixing `viewMode` are
the two changes needed.
