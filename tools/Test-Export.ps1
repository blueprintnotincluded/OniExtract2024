<#
.SYNOPSIS
Checks an export folder: contract invariants, the documented spot checks, and a
field-by-field comparison against a snapshot of an earlier export.

.DESCRIPTION
An export can only be judged by looking at what the game wrote. This script does the looking
that used to be done by hand after every in-game run.

  1. Snapshot.  -Snapshot copies the current export to a baseline folder. Do this BEFORE the
     in-game run you want to judge, because the game overwrites the export in place.

  2. Invariants and spot checks (always run, on -ExportDir alone):
       - the export-contract invariants from CLAUDE.md (utilities[] present with known types,
         viewMode a game overlay name or null, uiImageRect never null, an icon for every
         building, 16 sprites per connectable, every rect matching its PNG's aspect);
       - the spot checks listed in docs/WEBSITE_ROCKET_MODULES.md section 5 and
         docs/AREA_OF_EFFECT.md "Verification checklist".
     A spot check naming a building this export does not contain (no Spaced Out, say) is
     reported as skipped, not failed.

  3. Comparison against the baseline (skipped when there is no baseline):
       - every JSON file, value by value. Arrays of records are matched by their name/id, so
         a building is compared with the same building even if the list order moved. A change
         is reported as a path: building.json /bBuildingDefList[Door]/prioritizable: true -> false
       - every PNG under ui_image, ui_image_facade and connection_sprites. Renders are not
         byte-stable from one run to the next, so a PNG that differs in bytes is decoded and
         compared by size, by outline (which pixels are opaque) and by colour. Small
         differences count as the same image; see -OutlineTolerance / -ColourTolerance.

A behaviour-neutral change should report no JSON differences and no changed images.

The exit code is 0 when everything passed and nothing differs, 1 otherwise. The full list of
differences goes to -ReportPath; the console shows the first -MaxDetail lines of each kind.

PowerShell 5.1 has no usable parser for a 2 MB JSON file, so the work is done in C# against
the game's own Newtonsoft.Json.dll, which is why -GameLibs is needed.

.PARAMETER ExportDir
The export folder the game writes. Default: Documents\Klei\OxygenNotIncluded\export

.PARAMETER BaselineDir
Where the snapshot lives. Default: Documents\Klei\OxygenNotIncluded\export-baseline

.PARAMETER Snapshot
Copy ExportDir to BaselineDir and stop. Refuses to replace an existing baseline without -Force.

.PARAMETER GameLibs
The game's Managed folder, for Newtonsoft.Json.dll.

.PARAMETER OutlineTolerance
Percentage of an image's pixels that may flip between opaque and transparent before the
image counts as changed. Default 0.5.

.PARAMETER ColourTolerance
Mean per-channel difference (0-255) over the opaque pixels before the image counts as
changed. Default 4.

.EXAMPLE
.\tools\Test-Export.ps1 -Snapshot
# ...run the game, export...
.\tools\Test-Export.ps1
#>
[CmdletBinding()]
param(
    [string]$ExportDir = "$env:USERPROFILE\Documents\Klei\OxygenNotIncluded\export",
    [string]$BaselineDir = "$env:USERPROFILE\Documents\Klei\OxygenNotIncluded\export-baseline",
    [switch]$Snapshot,
    [switch]$Force,
    [switch]$SkipImages,
    [string]$GameLibs = 'C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed',
    [double]$OutlineTolerance = 0.5,
    [double]$ColourTolerance = 4,
    [int]$MaxDetail = 40,
    [string]$ReportPath = (Join-Path $env:TEMP 'oniextract-export-report.txt')
)

$ErrorActionPreference = 'Stop'

# What an export consists of. `images\` and building.pre-mod-merge.json, which older runs left
# in the same folder, are not written by the current mod and are left out.
$exportDirs = @('database', 'ui_image', 'ui_image_facade', 'connection_sprites')
$exportFiles = @('ui_image_rects.json', 'pose_overrides.json')
$imageDirs = @('ui_image', 'ui_image_facade', 'connection_sprites')

if (-not (Test-Path (Join-Path $ExportDir 'database\building.json'))) {
    throw "No database\building.json under $ExportDir -- wrong -ExportDir, or the main-menu export has not run."
}

# --- snapshot -------------------------------------------------------------------------
if ($Snapshot) {
    if ((Test-Path $BaselineDir) -and -not $Force) {
        throw "$BaselineDir already exists. Pass -Force to replace it, or -BaselineDir to keep both."
    }
    New-Item -ItemType Directory -Force $BaselineDir | Out-Null
    foreach ($d in $exportDirs) {
        $src = Join-Path $ExportDir $d
        if (-not (Test-Path $src)) { continue }
        # /MIR so a re-snapshot with -Force drops files the export no longer has.
        robocopy $src (Join-Path $BaselineDir $d) /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $d (exit $LASTEXITCODE)" }
    }
    foreach ($f in $exportFiles) {
        $src = Join-Path $ExportDir $f
        if (Test-Path $src) { Copy-Item $src (Join-Path $BaselineDir $f) -Force }
    }
    $count = (Get-ChildItem $BaselineDir -Recurse -File | Measure-Object).Count
    Write-Host "Snapshot of $ExportDir -> $BaselineDir ($count files)."
    exit 0
}

# --- the C# half ----------------------------------------------------------------------
$newtonsoft = Join-Path $GameLibs 'Newtonsoft.Json.dll'
if (-not (Test-Path $newtonsoft)) { throw "Newtonsoft.Json.dll not found in $GameLibs -- pass -GameLibs." }
Add-Type -Path $newtonsoft
Add-Type -AssemblyName System.Drawing

# Written for the C# 5 compiler that Windows PowerShell 5.1 ships: no string interpolation,
# no ?. operator, no out-variable declarations.
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class OniCheck
{
    public string Group;
    public string Name;
    public string Status;   // PASS, FAIL or SKIP
    public string Detail;
}

public static class OniExportCheck
{
    // ---- loading ----

    public static JToken Load(string path)
    {
        using (var sr = new StreamReader(path, Encoding.UTF8))
        using (var jr = new JsonTextReader(sr))
        {
            // Keep strings that look like dates as strings, so they compare as written.
            jr.DateParseHandling = DateParseHandling.None;
            return JToken.Load(jr);
        }
    }

    static string Short(JToken t)
    {
        if (t == null) return "<absent>";
        string s = t.ToString(Formatting.None);
        return s.Length <= 90 ? s : s.Substring(0, 87) + "...";
    }

    // ---- JSON comparison ----

    public static List<string> Diff(JToken a, JToken b)
    {
        var o = new List<string>();
        Walk("", a, b, o);
        return o;
    }

    static void Walk(string path, JToken a, JToken b, List<string> o)
    {
        if (JToken.DeepEquals(a, b)) return;

        if (a.Type == JTokenType.Object && b.Type == JTokenType.Object)
        {
            var oa = (JObject)a;
            var ob = (JObject)b;
            foreach (var p in oa.Properties())
            {
                var q = ob.Property(p.Name);
                if (q == null) o.Add(path + "/" + p.Name + ": removed (was " + Short(p.Value) + ")");
                else Walk(path + "/" + p.Name, p.Value, q.Value, o);
            }
            foreach (var p in ob.Properties())
                if (oa.Property(p.Name) == null)
                    o.Add(path + "/" + p.Name + ": added (" + Short(p.Value) + ")");
            return;
        }

        if (a.Type == JTokenType.Array && b.Type == JTokenType.Array)
        {
            WalkArray(path, (JArray)a, (JArray)b, o);
            return;
        }

        o.Add(path + ": " + Short(a) + " -> " + Short(b));
    }

    // Property names that identify a record within an array, in order of preference.
    static readonly string[] KeyCandidates = { "name", "id", "Id", "prefabId", "Key", "tag" };

    static Dictionary<string, JToken> IndexBy(JArray arr, string key)
    {
        var map = new Dictionary<string, JToken>(StringComparer.Ordinal);
        foreach (var t in arr)
        {
            var obj = t as JObject;
            if (obj == null) return null;
            var v = obj[key] as JValue;
            if (v == null || v.Type != JTokenType.String) return null;
            string k = (string)v;
            if (map.ContainsKey(k)) return null;   // not unique, so not a key
            map[k] = t;
        }
        return map;
    }

    static bool AllPrimitive(JArray arr)
    {
        foreach (var t in arr) if (t is JContainer) return false;
        return true;
    }

    static void WalkArray(string path, JArray a, JArray b, List<string> o)
    {
        // Records: match by identity, so a reordered or lengthened list still compares each
        // record with its own counterpart.
        foreach (string key in KeyCandidates)
        {
            if (a.Count == 0 || b.Count == 0) break;
            var ma = IndexBy(a, key);
            var mb = ma == null ? null : IndexBy(b, key);
            if (ma == null || mb == null) continue;

            foreach (var kv in ma)
            {
                JToken other;
                if (!mb.TryGetValue(kv.Key, out other)) o.Add(path + "[" + kv.Key + "]: removed");
                else Walk(path + "[" + kv.Key + "]", kv.Value, other, o);
            }
            foreach (var kv in mb)
                if (!ma.ContainsKey(kv.Key)) o.Add(path + "[" + kv.Key + "]: added");

            var orderA = ma.Keys.Where(k => mb.ContainsKey(k)).ToList();
            var orderB = mb.Keys.Where(k => ma.ContainsKey(k)).ToList();
            if (!orderA.SequenceEqual(orderB)) o.Add(path + ": same records, different order");
            return;
        }

        // Plain values: tell a reshuffle apart from a real change.
        if (AllPrimitive(a) && AllPrimitive(b))
        {
            var sa = a.Select(t => t.ToString(Formatting.None)).ToList();
            var sb = b.Select(t => t.ToString(Formatting.None)).ToList();
            var onlyA = new List<string>(sa);
            var onlyB = new List<string>();
            foreach (string s in sb) { if (!onlyA.Remove(s)) onlyB.Add(s); }
            if (onlyA.Count == 0 && onlyB.Count == 0)
            {
                o.Add(path + ": same items, different order");
                return;
            }
            string msg = path + ":";
            if (onlyA.Count > 0) msg += " removed [" + Clip(string.Join(", ", onlyA)) + "]";
            if (onlyB.Count > 0) msg += " added [" + Clip(string.Join(", ", onlyB)) + "]";
            o.Add(msg);
            return;
        }

        if (a.Count != b.Count) o.Add(path + ": length " + a.Count + " -> " + b.Count);
        int n = Math.Min(a.Count, b.Count);
        for (int i = 0; i < n; i++) Walk(path + "[" + i + "]", a[i], b[i], o);
    }

    static string Clip(string s) { return s.Length <= 160 ? s : s.Substring(0, 157) + "..."; }

    // ---- image comparison ----

    static int[] Pixels(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var px = new int[bmp.Width * bmp.Height];
            // Stride is exactly 4 * Width for 32bpp, so the rows are contiguous.
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            return px;
        }
        finally { bmp.UnlockBits(data); }
    }

    // "identical"  - same bytes
    // "equivalent" - different bytes, same size, outline and colour within tolerance
    // anything else describes the difference
    public static string ComparePng(string pathA, string pathB, double outlineTolPct, double colourTol)
    {
        byte[] ba = File.ReadAllBytes(pathA);
        byte[] bb = File.ReadAllBytes(pathB);
        if (ba.Length == bb.Length)
        {
            bool same = true;
            for (int i = 0; i < ba.Length; i++) if (ba[i] != bb[i]) { same = false; break; }
            if (same) return "identical";
        }

        using (var msA = new MemoryStream(ba))
        using (var msB = new MemoryStream(bb))
        using (var A = new Bitmap(msA))
        using (var B = new Bitmap(msB))
        {
            if (A.Width != B.Width || A.Height != B.Height)
                return "size " + A.Width + "x" + A.Height + " -> " + B.Width + "x" + B.Height;

            int[] xa = Pixels(A);
            int[] xb = Pixels(B);
            long outline = 0, opaque = 0, colour = 0;
            for (int i = 0; i < xa.Length; i++)
            {
                int pa = xa[i], pb = xb[i];
                bool oa = ((pa >> 24) & 255) > 16;
                bool ob = ((pb >> 24) & 255) > 16;
                if (oa != ob) { outline++; continue; }
                if (!oa) continue;
                opaque++;
                colour += Math.Abs(((pa >> 16) & 255) - ((pb >> 16) & 255))
                        + Math.Abs(((pa >> 8) & 255) - ((pb >> 8) & 255))
                        + Math.Abs((pa & 255) - (pb & 255));
            }
            double outlinePct = xa.Length == 0 ? 0 : 100.0 * outline / xa.Length;
            double meanColour = opaque == 0 ? 0 : colour / (3.0 * opaque);
            if (outlinePct <= outlineTolPct && meanColour <= colourTol) return "equivalent";
            return string.Format("pixels: {0:F2}% of the outline moved, mean colour difference {1:F1}/255",
                outlinePct, meanColour);
        }
    }

    public static bool PngSize(string path, out int w, out int h)
    {
        w = 0; h = 0;
        if (!File.Exists(path)) return false;
        var head = new byte[24];
        using (var fs = File.OpenRead(path)) { if (fs.Read(head, 0, 24) < 24) return false; }
        if (head[1] != (byte)'P' || head[2] != (byte)'N' || head[3] != (byte)'G') return false;
        w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
        h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        return w > 0 && h > 0;
    }

    // ---- invariants and spot checks ----

    static readonly HashSet<string> ConnectionTypes = new HashSet<string> {
        "PowerInput", "PowerOutput", "GasInput", "GasOutput", "LiquidInput", "LiquidOutput",
        "SolidInput", "SolidOutput", "LogicInput", "LogicOutput", "LogicRibbonInput",
        "LogicRibbonOutput", "LogicReset" };

    static readonly HashSet<string> ViewModes = new HashSet<string> {
        "Power", "GasConduit", "LiquidConduit", "SolidConveyor", "Logic", "Oxygen", "Decor",
        "Light", "Temperature", "Rooms", "Radiation", "Disease", "Crop" };

    // Optional per-building keys documented as "omitted when they do not apply": present
    // means a real value, so null (or false, for the flags) is a contract break.
    static readonly string[] OmittedNotNull = {
        "uiImageRect", "mod", "modTitle", "areasOfEffect", "attachPoints", "attachableTo",
        "rocketModulePerformance", "moduleBuildConditions", "valve", "limitValve",
        "userControlledCapacity", "door" };
    static readonly string[] OmittedNotFalse = { "isRocketModule", "userNameable", "prioritizable" };

    static readonly string[] RocketKeys = {
        "isRocketModule", "attachPoints", "attachableTo", "rocketModulePerformance", "moduleBuildConditions" };
    static readonly string[] SettingsKeys = {
        "valve", "limitValve", "userControlledCapacity", "door", "userNameable", "prioritizable" };

    class Run
    {
        public List<OniCheck> Out = new List<OniCheck>();
        public Dictionary<string, JObject> ByName = new Dictionary<string, JObject>(StringComparer.Ordinal);

        public void Add(string group, string name, bool ok, string detail)
        {
            Out.Add(new OniCheck { Group = group, Name = name, Status = ok ? "PASS" : "FAIL", Detail = ok ? "" : detail });
        }

        // Runs a spot check against one named building, or records a skip when this
        // export does not contain it.
        public void On(string group, string building, string what, Func<JObject, string> test)
        {
            JObject b;
            string name = building + ": " + what;
            if (!ByName.TryGetValue(building, out b))
            {
                Out.Add(new OniCheck { Group = group, Name = name, Status = "SKIP", Detail = "not in this export" });
                return;
            }
            string problem = test(b);
            Add(group, name, problem == null, problem);
        }
    }

    static string Sample(List<string> names)
    {
        return names.Count + " (" + string.Join(", ", names.Take(8)) + (names.Count > 8 ? ", ..." : "") + ")";
    }

    static bool Has(JObject b, string key) { return b.Property(key) != null; }

    static bool IsNull(JToken t) { return t == null || t.Type == JTokenType.Null; }

    static string AnyOf(JObject b, string[] keys)
    {
        var present = keys.Where(k => Has(b, k)).ToList();
        return present.Count == 0 ? null : "has " + string.Join(", ", present);
    }

    static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-6; }

    static bool ContainsString(JToken arr, string value)
    {
        var a = arr as JArray;
        return a != null && a.Any(t => t.Type == JTokenType.String && (string)t == value);
    }

    static JObject Area(JObject b, string kind)
    {
        var areas = b["areasOfEffect"] as JArray;
        if (areas == null) return null;
        return areas.OfType<JObject>().FirstOrDefault(a => (string)a["kind"] == kind);
    }

    static string AreaIs(JObject b, string kind, string shape, int cells, double ox, double oy)
    {
        var a = Area(b, kind);
        if (a == null) return "no areasOfEffect entry of kind " + kind;
        var c = a["cells"] as JArray;
        int n = c == null ? 0 : c.Count;
        if ((string)a["shape"] != shape) return "shape is " + Short(a["shape"]) + ", expected " + shape;
        if (n != cells) return n + " cells, expected " + cells;
        if (!Near((double)a["origin"]["x"], ox) || !Near((double)a["origin"]["y"], oy))
            return "origin is " + Short(a["origin"]) + ", expected (" + ox + "," + oy + ")";
        return null;
    }

    static bool RectMatchesPng(JToken rect, string png, out string detail)
    {
        detail = null;
        int w, h;
        if (!PngSize(png, out w, out h)) { detail = "no PNG"; return false; }
        double rw = (double)rect["w"], rh = (double)rect["h"];
        if (rw <= 0 || rh <= 0) { detail = "empty rect"; return false; }
        double pngAspect = (double)w / h;
        double off = Math.Abs(pngAspect - rw / rh) / pngAspect;
        if (off > 0.02) { detail = string.Format("png {0}x{1} vs rect {2}x{3}, {4:P0} off", w, h, rw, rh, off); return false; }
        return true;
    }

    public static List<OniCheck> Check(string exportDir)
    {
        var r = new Run();
        var root = (JObject)Load(Path.Combine(exportDir, "database", "building.json"));
        var list = root["bBuildingDefList"] as JArray;
        r.Add("invariant", "building.json has a bBuildingDefList", list != null && list.Count > 0, "missing or empty");
        if (list == null) return r.Out;

        var duplicates = new List<string>();
        foreach (var t in list.OfType<JObject>())
        {
            string name = (string)t["name"];
            if (name == null || r.ByName.ContainsKey(name)) duplicates.Add(name ?? "<no name>");
            else r.ByName[name] = t;
        }
        r.Add("invariant", "building names are present and unique", duplicates.Count == 0, Sample(duplicates));

        string uiDir = Path.Combine(exportDir, "ui_image");
        var badUtilities = new List<string>();
        var badTypes = new List<string>();
        var badViewMode = new List<string>();
        var nullKeys = new List<string>();
        var falseKeys = new List<string>();
        var noIcon = new List<string>();
        var badRect = new List<string>();
        int withRect = 0, withPorts = 0, modded = 0;

        foreach (var kv in r.ByName)
        {
            JObject b = kv.Value;
            var utilities = b["utilities"] as JArray;
            if (utilities == null) badUtilities.Add(kv.Key);
            else
            {
                if (utilities.Count > 0) withPorts++;
                foreach (var u in utilities)
                {
                    string type = u["type"] == null ? null : u["type"].ToString();
                    if (type == null || !ConnectionTypes.Contains(type) || u["offset"] == null)
                    {
                        badTypes.Add(kv.Key + ":" + (type ?? "<none>"));
                        break;
                    }
                }
            }

            if (!Has(b, "viewMode")) badViewMode.Add(kv.Key + ":<absent>");
            else if (!IsNull(b["viewMode"]) && !ViewModes.Contains((string)b["viewMode"]))
                badViewMode.Add(kv.Key + ":" + (string)b["viewMode"]);

            foreach (string k in OmittedNotNull)
                if (Has(b, k) && IsNull(b[k])) nullKeys.Add(kv.Key + "." + k);
            foreach (string k in OmittedNotFalse)
                if (Has(b, k) && (b[k].Type != JTokenType.Boolean || !(bool)b[k])) falseKeys.Add(kv.Key + "." + k);

            string png = Path.Combine(uiDir, kv.Key + ".png");
            if (!File.Exists(png)) noIcon.Add(kv.Key);

            if (Has(b, "mod")) modded++;
            if (Has(b, "uiImageRect") && !IsNull(b["uiImageRect"]))
            {
                withRect++;
                string why;
                if (File.Exists(png) && !RectMatchesPng(b["uiImageRect"], png, out why)) badRect.Add(kv.Key + " (" + why + ")");
            }
        }

        r.Add("invariant", "every building has a utilities[] array", badUtilities.Count == 0, Sample(badUtilities));
        r.Add("invariant", "every utilities[] entry has an offset and a known ConnectionType name", badTypes.Count == 0, Sample(badTypes));
        r.Add("invariant", "viewMode is a game overlay name or null (never a hash, never absent)", badViewMode.Count == 0, Sample(badViewMode));
        r.Add("invariant", "optional keys are omitted, never null", nullKeys.Count == 0, Sample(nullKeys));
        r.Add("invariant", "optional flags are omitted, never false", falseKeys.Count == 0, Sample(falseKeys));
        r.Add("invariant", "every building has ui_image/<name>.png", noIcon.Count == 0, Sample(noIcon));
        r.Add("invariant", "every uiImageRect matches its PNG's aspect within 2%", badRect.Count == 0, Sample(badRect));

        // The sidecar: buildings must agree with building.json, and every entry (terrain
        // features included) must describe the PNG that is actually on disk.
        string sidecarPath = Path.Combine(exportDir, "ui_image_rects.json");
        if (File.Exists(sidecarPath))
        {
            var sidecar = (JObject)Load(sidecarPath);
            var disagree = new List<string>();
            var sidecarBad = new List<string>();
            foreach (var p in sidecar.Properties())
            {
                JObject b;
                if (r.ByName.TryGetValue(p.Name, out b))
                {
                    if (!Has(b, "uiImageRect") || !JToken.DeepEquals(b["uiImageRect"], p.Value)) disagree.Add(p.Name);
                }
                string why;
                if (!RectMatchesPng(p.Value, Path.Combine(uiDir, p.Name + ".png"), out why)) sidecarBad.Add(p.Name + " (" + why + ")");
            }
            r.Add("invariant", "ui_image_rects.json agrees with building.json for every building in both", disagree.Count == 0, Sample(disagree));
            r.Add("invariant", "every ui_image_rects.json entry matches its PNG's aspect within 2%", sidecarBad.Count == 0, Sample(sidecarBad));
            r.Out.Add(new OniCheck { Group = "info", Name = "ui_image_rects.json entries", Status = "PASS", Detail = sidecar.Count.ToString() });
        }
        else
        {
            r.Out.Add(new OniCheck { Group = "invariant", Name = "ui_image_rects.json", Status = "SKIP", Detail = "absent: the building-image tool has not run on this export" });
        }

        // Connection sprites: a directory is the website's signal that a building is a
        // connectable, and it expects all 16 states inside.
        string spriteRoot = Path.Combine(exportDir, "connection_sprites");
        if (Directory.Exists(spriteRoot))
        {
            var incomplete = new List<string>();
            var notConnectable = new List<string>();
            var dirs = Directory.GetDirectories(spriteRoot);
            foreach (string d in dirs)
            {
                string id = Path.GetFileName(d);
                for (int i = 0; i < 16; i++)
                    if (!File.Exists(Path.Combine(d, i + ".png"))) { incomplete.Add(id); break; }
                JObject b;
                if (!r.ByName.TryGetValue(id, out b)) notConnectable.Add(id + " (no building)");
                else if (!(bool)b["isUtility"] && !(bool)b["isKAnimTile"]) notConnectable.Add(id);
            }
            r.Add("invariant", "every connection_sprites directory has 0.png .. 15.png", incomplete.Count == 0, Sample(incomplete));
            r.Add("invariant", "every connection_sprites directory is a tile or utility building", notConnectable.Count == 0, Sample(notConnectable));
            r.Out.Add(new OniCheck { Group = "info", Name = "connection_sprites directories", Status = "PASS", Detail = dirs.Length.ToString() });
        }
        else
        {
            r.Out.Add(new OniCheck { Group = "invariant", Name = "connection_sprites", Status = "SKIP", Detail = "absent: the connection-sprite tool has not run on this export" });
        }

        r.Out.Add(new OniCheck { Group = "info", Name = "buildings", Status = "PASS", Detail = r.ByName.Count.ToString() });
        r.Out.Add(new OniCheck { Group = "info", Name = "buildings with ports", Status = "PASS", Detail = withPorts.ToString() });
        r.Out.Add(new OniCheck { Group = "info", Name = "buildings with uiImageRect", Status = "PASS", Detail = withRect.ToString() });
        r.Out.Add(new OniCheck { Group = "info", Name = "modded buildings", Status = "PASS", Detail = modded.ToString() });

        // ---- docs/WEBSITE_ROCKET_MODULES.md section 5 ----
        const string R = "rocket/settings";
        var menu = root["rocketModuleMenu"] as JArray;
        if (menu == null || menu.Count == 0)
        {
            r.Out.Add(new OniCheck { Group = R, Name = "rocketModuleMenu", Status = "SKIP", Detail = "empty: no Spaced Out rocket modules in this export" });
        }
        else
        {
            var ids = menu.Select(t => (string)t).ToList();
            bool startOk = ids.Count >= 3 && ids[0] == "CO2Engine" && ids[1] == "SugarEngine" && ids[2] == "SteamEngineCluster";
            bool endOk = ids.Count >= 2 && ids[ids.Count - 2] == "ArtifactCargoBay" && ids[ids.Count - 1] == "ScannerModule";
            r.Add(R, "rocketModuleMenu starts CO2Engine, SugarEngine, SteamEngineCluster and ends ArtifactCargoBay, ScannerModule",
                startOk && endOk, "is " + Clip(string.Join(", ", ids)));
            var wrong = new List<string>();
            foreach (string id in ids)
            {
                JObject b;
                if (!r.ByName.TryGetValue(id, out b)) { wrong.Add(id + " (no entry)"); continue; }
                bool module = Has(b, "isRocketModule") && (bool)b["isRocketModule"];
                bool hidden = Has(b, "showInBuildMenu") && !(bool)b["showInBuildMenu"];
                if (!module || !hidden) wrong.Add(id);
            }
            r.Add(R, "every rocketModuleMenu id is an entry with isRocketModule true and showInBuildMenu false", wrong.Count == 0, Sample(wrong));
        }

        r.On(R, "LaunchPad", "attachPoints is one Rocket point at (0,2), no attachableTo", b =>
        {
            var pts = b["attachPoints"] as JArray;
            if (pts == null || pts.Count != 1) return "attachPoints is " + Short(b["attachPoints"]);
            if (!Near((double)pts[0]["offset"]["x"], 0) || !Near((double)pts[0]["offset"]["y"], 2) || (string)pts[0]["tag"] != "Rocket")
                return "attachPoints is " + Short(pts);
            return Has(b, "attachableTo") ? "has attachableTo " + Short(b["attachableTo"]) : null;
        });
        r.On(R, "KeroseneEngineCluster", "attachableTo Rocket, point at (0,5), enginePower > 0, EngineOnBottom", b =>
        {
            if ((string)b["attachableTo"] != "Rocket") return "attachableTo is " + Short(b["attachableTo"]);
            var pts = b["attachPoints"] as JArray;
            if (pts == null || pts.Count == 0 || !Near((double)pts[0]["offset"]["x"], 0) || !Near((double)pts[0]["offset"]["y"], 5))
                return "attachPoints is " + Short(b["attachPoints"]);
            var perf = b["rocketModulePerformance"];
            if (perf == null || (double)perf["enginePower"] <= 0) return "rocketModulePerformance is " + Short(perf);
            return ContainsString(b["moduleBuildConditions"], "EngineOnBottom") ? null : "moduleBuildConditions is " + Short(b["moduleBuildConditions"]);
        });
        r.On(R, "NoseconeBasic", "attachableTo Rocket, no attachPoints, TopOnly", b =>
        {
            if ((string)b["attachableTo"] != "Rocket") return "attachableTo is " + Short(b["attachableTo"]);
            if (Has(b, "attachPoints")) return "has attachPoints " + Short(b["attachPoints"]);
            return ContainsString(b["moduleBuildConditions"], "TopOnly") ? null : "moduleBuildConditions is " + Short(b["moduleBuildConditions"]);
        });
        r.On(R, "ManualGenerator", "no rocketry keys, showInBuildMenu true", b =>
        {
            string any = AnyOf(b, RocketKeys);
            if (any != null) return any;
            return Has(b, "showInBuildMenu") && (bool)b["showInBuildMenu"] ? null : "showInBuildMenu is " + Short(b["showInBuildMenu"]);
        });
        r.On(R, "LiquidValve", "valve is {conduitType: Liquid, maxFlow: 10}", b =>
        {
            var v = b["valve"];
            return v != null && (string)v["conduitType"] == "Liquid" && Near((double)v["maxFlow"], 10) ? null : "valve is " + Short(v);
        });
        r.On(R, "LiquidLimitValve", "limitValve.maxLimitKg present", b =>
        {
            var v = b["limitValve"];
            return v != null && !IsNull(v["maxLimitKg"]) ? null : "limitValve is " + Short(v);
        });
        r.On(R, "StorageLocker", "capacity range matches storage, source StorageLocker, userNameable, prioritizable", b =>
        {
            var c = b["userControlledCapacity"];
            var s = b["storage"];
            if (c == null || s == null) return "userControlledCapacity " + Short(c) + ", storage " + Short(s);
            if (!Near((double)c["maxCapacity"], (double)s["capacityKg"])) return "maxCapacity " + Short(c["maxCapacity"]) + " vs storage.capacityKg " + Short(s["capacityKg"]);
            if ((string)c["source"] != "StorageLocker") return "source is " + Short(c["source"]);
            if (!Has(b, "userNameable")) return "no userNameable";
            return Has(b, "prioritizable") ? null : "no prioritizable";
        });
        r.On(R, "StorageTile", "userControlledCapacity.source is StorageTile.Def", b =>
        {
            var c = b["userControlledCapacity"];
            return c != null && (string)c["source"] == "StorageTile.Def" ? null : "userControlledCapacity is " + Short(c);
        });
        r.On(R, "Door", "door.doorType Internal, prioritizable", b =>
        {
            var d = b["door"];
            if (d == null || (string)d["doorType"] != "Internal") return "door is " + Short(d);
            return Has(b, "prioritizable") ? null : "no prioritizable";
        });
        r.On(R, "Wire", "no settings keys", b => AnyOf(b, SettingsKeys));
        r.On(R, "Tile", "no settings keys", b => AnyOf(b, SettingsKeys));
        r.On(R, "GasLogicValve", "no valve", b => Has(b, "valve") ? "has valve " + Short(b["valve"]) : null);

        // ---- docs/AREA_OF_EFFECT.md "Verification checklist" ----
        const string A = "area of effect";
        r.On(A, "AirFilter", "13-cell diamond intake", b => AreaIs(b, "elementIntake", "diamond", 13, 0, 0));
        r.On(A, "CeilingLight", "55-cell light cone", b => AreaIs(b, "light", "cone", 55, 0, 0));
        r.On(A, "FloorLamp", "49-cell light circle at (0,1)", b => AreaIs(b, "light", "circle", 49, 0, 1));
        r.On(A, "AutoMiner", "144-cell operating rect", b =>
        {
            var a = Area(b, "operationRange");
            var c = a == null ? null : a["cells"] as JArray;
            return c != null && c.Count == 144 ? null : "operationRange has " + (c == null ? "no" : c.Count.ToString()) + " cells";
        });
        r.On(A, "SteamTurbine2", "5 cells, all at y = -2", b =>
        {
            var a = Area(b, "operationRange");
            var c = a == null ? null : a["cells"] as JArray;
            if (c == null || c.Count != 5) return "operationRange has " + (c == null ? "no" : c.Count.ToString()) + " cells";
            return c.All(cell => (int)cell[1] == -2) ? null : "cells are " + Short(c);
        });
        r.On(A, "ManualGenerator", "no areasOfEffect key", b => Has(b, "areasOfEffect") ? "has areasOfEffect" : null);
        r.On(A, "WaterTrap", "no operationRange", b => Area(b, "operationRange") != null ? "has an operationRange" : null);

        return r.Out;
    }
}
'@

# The game's Newtonsoft is a .NET Standard build, so the compiler also needs the facade.
$refs = @($newtonsoft, 'System.Drawing', 'System.Core')
$netstandard = Join-Path $GameLibs 'netstandard.dll'
if (Test-Path $netstandard) { $refs += $netstandard }
Add-Type -TypeDefinition $source -ReferencedAssemblies $refs

$report = New-Object System.Collections.Generic.List[string]
$problems = 0

function Write-Section([string]$title) {
    Write-Host ''
    Write-Host "== $title" -ForegroundColor Cyan
    $report.Add('')
    $report.Add("== $title")
}

# Prints the first $MaxDetail lines; the report file always gets all of them.
function Write-Lines([string[]]$lines, [string]$indent = '   ') {
    $shown = 0
    foreach ($line in $lines) {
        $report.Add($indent + $line)
        if ($shown -lt $MaxDetail) { Write-Host ($indent + $line) }
        $shown++
    }
    if ($lines.Count -gt $MaxDetail) {
        Write-Host ("$indent... and {0} more (see the report file)" -f ($lines.Count - $MaxDetail)) -ForegroundColor DarkGray
    }
}

# --- 1. invariants and spot checks ----------------------------------------------------
Write-Section "Invariants and spot checks: $ExportDir"
$checks = [OniExportCheck]::Check($ExportDir)
$info = @($checks | Where-Object { $_.Group -eq 'info' })
$real = @($checks | Where-Object { $_.Group -ne 'info' })
Write-Lines @($info | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Detail })
foreach ($group in ($real | ForEach-Object { $_.Group } | Select-Object -Unique)) {
    $inGroup = @($real | Where-Object { $_.Group -eq $group })
    $failed = @($inGroup | Where-Object { $_.Status -eq 'FAIL' })
    $skipped = @($inGroup | Where-Object { $_.Status -eq 'SKIP' })
    $summary = '{0}: {1} passed, {2} failed, {3} skipped' -f $group, ($inGroup.Count - $failed.Count - $skipped.Count), $failed.Count, $skipped.Count
    $report.Add("   $summary")
    if ($failed.Count) { Write-Host "   $summary" -ForegroundColor Red } else { Write-Host "   $summary" -ForegroundColor Green }
    Write-Lines @($failed | ForEach-Object { 'FAIL  {0} -- {1}' -f $_.Name, $_.Detail }) '      '
    Write-Lines @($skipped | ForEach-Object { 'skip  {0} -- {1}' -f $_.Name, $_.Detail }) '      '
    foreach ($c in ($inGroup | Where-Object { $_.Status -eq 'PASS' })) { $report.Add('      pass  ' + $c.Name) }
    $problems += $failed.Count
}

# --- 2. comparison against the baseline -----------------------------------------------
if (-not (Test-Path $BaselineDir)) {
    Write-Section 'Comparison'
    Write-Lines @("No baseline at $BaselineDir -- nothing to compare against.",
        'Run with -Snapshot before the next in-game export to create one.')
} else {
    Write-Section "JSON: $BaselineDir -> $ExportDir"
    $jsonFiles = @()
    foreach ($root in @($BaselineDir, $ExportDir)) {
        $db = Join-Path $root 'database'
        if (Test-Path $db) { $jsonFiles += Get-ChildItem $db -Filter *.json | ForEach-Object { 'database\' + $_.Name } }
        foreach ($f in $exportFiles) { if (Test-Path (Join-Path $root $f)) { $jsonFiles += $f } }
    }
    foreach ($rel in ($jsonFiles | Sort-Object -Unique)) {
        $old = Join-Path $BaselineDir $rel
        $new = Join-Path $ExportDir $rel
        if (-not (Test-Path $old)) { Write-Lines @("$rel -- ADDED (not in the baseline)"); $problems++; continue }
        if (-not (Test-Path $new)) { Write-Lines @("$rel -- REMOVED (not in the export)"); $problems++; continue }
        if ((Get-FileHash $old).Hash -eq (Get-FileHash $new).Hash) { Write-Lines @("$rel -- identical"); continue }
        $diff = [OniExportCheck]::Diff([OniExportCheck]::Load($old), [OniExportCheck]::Load($new))
        if ($diff.Count -eq 0) {
            Write-Lines @("$rel -- same values (the bytes differ: formatting or key order only)")
        } else {
            Write-Host "   $rel -- $($diff.Count) differences" -ForegroundColor Yellow
            $report.Add("   $rel -- $($diff.Count) differences")
            Write-Lines @($diff) '      '
            $problems += $diff.Count
        }
    }

    if (-not $SkipImages) {
        Write-Section "Images: $BaselineDir -> $ExportDir"
        foreach ($dir in $imageDirs) {
            $oldRoot = Join-Path $BaselineDir $dir
            $newRoot = Join-Path $ExportDir $dir
            if (-not (Test-Path $oldRoot) -and -not (Test-Path $newRoot)) { continue }
            $oldSet = @{}
            $newSet = @{}
            if (Test-Path $oldRoot) { Get-ChildItem $oldRoot -Recurse -Filter *.png | ForEach-Object { $oldSet[$_.FullName.Substring($oldRoot.Length + 1)] = $_.FullName } }
            if (Test-Path $newRoot) { Get-ChildItem $newRoot -Recurse -Filter *.png | ForEach-Object { $newSet[$_.FullName.Substring($newRoot.Length + 1)] = $_.FullName } }

            $added = @($newSet.Keys | Where-Object { -not $oldSet.ContainsKey($_) } | Sort-Object)
            $removed = @($oldSet.Keys | Where-Object { -not $newSet.ContainsKey($_) } | Sort-Object)
            $identical = 0
            $equivalent = 0
            $changed = @()
            foreach ($rel in ($oldSet.Keys | Where-Object { $newSet.ContainsKey($_) } | Sort-Object)) {
                $result = [OniExportCheck]::ComparePng($oldSet[$rel], $newSet[$rel], $OutlineTolerance, $ColourTolerance)
                if ($result -eq 'identical') { $identical++ }
                elseif ($result -eq 'equivalent') { $equivalent++ }
                else { $changed += "$rel -- $result" }
            }
            $summary = '{0}: {1} identical, {2} re-rendered but equivalent, {3} changed, {4} added, {5} removed' -f `
                $dir, $identical, $equivalent, $changed.Count, $added.Count, $removed.Count
            $report.Add("   $summary")
            if ($changed.Count + $added.Count + $removed.Count) { Write-Host "   $summary" -ForegroundColor Yellow } else { Write-Host "   $summary" -ForegroundColor Green }
            Write-Lines @($changed | ForEach-Object { "changed  $_" }) '      '
            Write-Lines @($added | ForEach-Object { "added    $_" }) '      '
            Write-Lines @($removed | ForEach-Object { "removed  $_" }) '      '
            $problems += $changed.Count + $added.Count + $removed.Count
        }
    }
}

[IO.File]::WriteAllLines($ReportPath, $report)
Write-Host ''
if ($problems -eq 0 -and (Test-Path $BaselineDir)) {
    Write-Host 'RESULT: clean -- every check passed and nothing differs from the baseline.' -ForegroundColor Green
} elseif ($problems -eq 0) {
    Write-Host 'RESULT: every check passed. Nothing was compared: there is no baseline.' -ForegroundColor Green
} else {
    Write-Host "RESULT: $problems failed checks or differences." -ForegroundColor Yellow
}
Write-Host "Full report: $ReportPath"
exit ([int]($problems -gt 0))
