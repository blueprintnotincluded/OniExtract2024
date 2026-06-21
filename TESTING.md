# Testing Plan

## Approach

Add an `OniExtract2024.Tests` project to the solution using **xUnit**. The test project references the same `$(GameLibsFolder)` HintPaths as the main project and references the main project's output DLL directly. Tests run only on machines with the game installed, which is already a precondition for building.

**Why not isolate from game DLLs:** Every non-trivial class touches `Assembly-CSharp.dll` or `UnityEngine.dll`. Full isolation would require a parallel abstraction layer that adds complexity without payoff for a solo project.

**Why xUnit:** No test-class attributes required, just `[Fact]` on methods. Discovered natively by Visual Studio 17 via `xunit.runner.visualstudio`.

---

## Project Setup ✓ Done

`OniExtract2024.Tests\OniExtract2024.Tests.csproj` is wired into `OniExtract2024.sln`.

Implementation notes:
- SDK-style csproj (not old-style), `net48` — cleaner NuGet/`dotnet test` integration
- NuGet packages: `xunit` 2.9.3, `xunit.runner.visualstudio` 2.8.2, `Microsoft.NET.Test.Sdk` 17.11.1
- `$(GameLibsFolder)` HintPaths defined directly in the test csproj (same path as main project)
- `<ProjectReference>` to main project — MSBuild handles build ordering
- `SmokeTest.cs` holds one empty `[Fact]` so `dotnet test` reports `Passed: 1` instead of "No test is available". Delete it once any real test exists.

**Agent loop command:** `dotnet test OniExtract2024.Tests\OniExtract2024.Tests.csproj` — exit 0 = green.

---

## Small Refactors That Unlock Tests

These are incidental cleanups to existing code, not restructuring — each takes < 5 minutes.

### 1. Extract `BuildExportPath` in `BaseExport`

Currently `GetDatabaseLocation()` calls `Util.RootFolder()` and `DlcManager.IsExpansion1Active()` inline. Extract the pure logic:

```csharp
public static string BuildExportPath(string rootFolder, string dirName, bool isExpansion1Active)
{
    string suffix = isExpansion1Active ? "" : "_base";
    return Path.Combine(rootFolder, "export", dirName + suffix);
}
```

`GetDatabaseLocation()` becomes a one-liner calling `BuildExportPath` with the live game values. The static method is now testable with no game state.

### 2. Leave `SkipUnityObjectConverter` and `SkipUnityObjectContractResolver` as-is

These classes only depend on `Newtonsoft.Json` and `UnityEngine.Object`, both available in the test project via the existing DLL references. No refactor needed.

---

## Test Cases to Write First

These are the deterministic, game-state-free specs described as the starting point.

### `BaseExportTests`

| Test | What it checks |
|---|---|
| `BuildExportPath_WithExpansion1_NoSuffix` | `BuildExportPath("C:/root", "database", true)` → `"C:/root/export/database"` |
| `BuildExportPath_WithoutExpansion1_AddsSuffix` | `BuildExportPath("C:/root", "database", false)` → `"C:/root/export/database_base"` |
| `BuildExportPath_PreservesSubDirName` | `dirName = "recipe"` → path contains `"recipe"` not `"recipe_base"` when expansion active |

### `SkipUnityObjectConverterTests`

| Test | What it checks |
|---|---|
| `CanConvert_UnityObject_ReturnsTrue` | `CanConvert(typeof(UnityEngine.Object))` → `true` |
| `CanConvert_UnityObjectSubclass_ReturnsTrue` | `CanConvert(typeof(UnityEngine.Transform))` → `true` |
| `CanConvert_PlainClass_ReturnsFalse` | `CanConvert(typeof(string))` → `false` |
| `WriteJson_WritesNull` | Serializing a holder class whose property is `null` Unity object writes JSON `null` |

### `ExportFileNameTests`

| Test | What it checks |
|---|---|
| `RecipeExporter_FileName` | `new ExportRecipe().ExportFileName == "recipe"` |
| `FoodExporter_FileName` | `new ExportFood().ExportFileName == "food"` |
| `GeyserExporter_FileName` | `new ExportGeyser().ExportFileName == "geyser"` |
| _(one per exporter)_ | All `ExportFileName` values are lowercase, no spaces, no `.json` suffix |

### `ModelFieldTests` (pure POCOs only)

Target model classes whose constructors take no game types: `BVector2`, `BColor`.

| Test | What it checks |
|---|---|
| `BVector2_SerializesToXY` | Serializes to `{"x":1.0,"y":2.0}` |
| `BColor_SerializesToRGBA` | All four channels present in output |

---

## Out of Scope (for now)

- Testing `Patches.cs` — Harmony transpilers require a live game runtime
- Testing anything that instantiates `ComplexRecipe`, `FoodInfo`, or other game POCOs whose constructors have side effects
- Integration tests that check actual export file output
- CI/CD — tests are local-only by design
