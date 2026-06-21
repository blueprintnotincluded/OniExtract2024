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
- `[assembly: InternalsVisibleTo("OniExtract2024.Tests")]` added to `Properties\AssemblyInfo.cs` — lets tests reach `internal` classes (`SkipUnityObjectConverter`, `SkipUnityObjectContractResolver`)
- `netstandard` reference added to test csproj pointing at `$(GameLibsFolder)\netstandard.dll` — Unity 6 DLLs reference `netstandard 2.1.0.0` which the .NET Framework 4.8 SDK facade does not provide; the game ships its own `netstandard.dll` that does
- `SmokeTest.cs` deleted; replaced by real tests

**Agent loop command:** `dotnet test OniExtract2024.Tests\OniExtract2024.Tests.csproj` — exit 0 = green.

---

## Small Refactors That Unlock Tests

These are incidental cleanups to existing code, not restructuring — each takes < 5 minutes.

### 1. Extract `BuildExportPath` in `BaseExport` ✓ Done

`BuildExportPath(string rootFolder, string dirName, bool isExpansion1Active)` extracted as a public static method. `GetDatabaseLocation()` is now a one-liner delegating to it.

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

### `ExportFileNameTests` — blocked, moved to Out of Scope

`new ExportRecipe()` implicitly calls the `BaseExport()` base constructor, which calls `DlcManager.GetActiveDLCIds()` and `BuildWatermark.GetBuildText()` — game-state calls that crash without a live runtime. These tests require either game context or a targeted refactor (e.g. a protected `BaseExport(bool forTesting)` constructor that skips DLC init).

### `ModelFieldTests` (pure POCOs only)

Target model classes whose constructors take no game types: `BVector2`, `BColor`.

| Test | What it checks |
|---|---|
| `BVector2_SerializesToXY` | Serializes to `{"x":1.0,"y":2.0}` |
| `BColor_SerializesToRGBA` | All four channels present in output |

---

## Out of Scope (for now)

- `ExportFileNameTests` — `BaseExport()` constructor calls game state; tests can't run outside the game runtime without a targeted constructor refactor
- Testing `Patches.cs` — Harmony transpilers require a live game runtime
- Testing anything that instantiates `ComplexRecipe`, `FoodInfo`, or other game POCOs whose constructors have side effects
- Integration tests that check actual export file output
- CI/CD — tests are local-only by design
