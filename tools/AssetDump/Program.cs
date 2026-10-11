using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;

// Reads ONI's Unity asset files offline. See docs/OFFLINE_ASSET_EXTRACTION.md.
//
//   AssetDump list       <file|all> [nameRegex] [--type Texture2D,...]
//   AssetDump material   <file|all> <nameRegex>
//   AssetDump textures   <file|all> <nameRegex> <outDir> [--alpha]
//   AssetDump textassets <file|all> <nameRegex> <outDir>
//
// <file> is relative to OxygenNotIncluded_Data, e.g. sharedassets2.assets or
// StreamingAssets/expansion1_bundle; "all" searches every asset file and bundle.
// Options: --data <OxygenNotIncluded_Data dir>  --tpk <classdata.tpk>

const string DefaultData = @"C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data";

var positional = new List<string>();
var options = new Dictionary<string, string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--alpha")
        options["alpha"] = "true";
    else if (args[i].StartsWith("--") && i + 1 < args.Length)
        options[args[i].Substring(2)] = args[++i];
    else
        positional.Add(args[i]);
}
if (positional.Count < 2)
{
    Console.Error.WriteLine("usage: AssetDump list|material|textures|textassets <file|all> [nameRegex] [outDir] [--type T] [--alpha] [--data dir] [--tpk file]");
    return 2;
}

string command = positional[0];
string dataDir = options.GetValueOrDefault("data", DefaultData);
string tpk;
if (options.TryGetValue("tpk", out var explicitTpk))
{
    tpk = explicitTpk;
    if (!File.Exists(tpk))
    {
        Console.Error.WriteLine("--tpk " + tpk + " not found.");
        return 2;
    }
}
else
{
    tpk = Path.Combine(AppContext.BaseDirectory, "classdata.tpk");
    if (!File.Exists(tpk))
        tpk = Path.Combine(Directory.GetCurrentDirectory(), "tools", "AssetDump", "classdata.tpk");
    if (!File.Exists(tpk))
    {
        Console.Error.WriteLine("classdata.tpk not found. Run tools/AssetDump/Get-ClassData.ps1 first, or pass --tpk.");
        return 2;
    }
}
var nameFilter = new Regex(positional.Count > 2 ? positional[2] : ".*");
string outDir = positional.Count > 3 ? positional[3] : null;
var types = options.TryGetValue("type", out var t)
    ? t.Split(',').Select(Enum.Parse<AssetClassID>).ToHashSet()
    : new HashSet<AssetClassID> { AssetClassID.Texture2D, AssetClassID.Material, AssetClassID.Sprite, AssetClassID.TextAsset, AssetClassID.Texture2DArray };

if ((command == "textures" || command == "textassets") && outDir == null)
{
    Console.Error.WriteLine(command + " needs an output directory.");
    return 2;
}
if (outDir != null)
    Directory.CreateDirectory(outDir);

// Output file stems already written this run. Asset names are not unique across files (or even
// within one), so a repeat gets its path id appended instead of overwriting the first.
var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

var am = new AssetsManager();
am.LoadClassPackage(tpk);

foreach (var (file, inst) in ResolveFiles(dataDir, positional[1])
    .SelectMany(f => Open(am, Path.Combine(dataDir, f)).Select(i => (f, i))))
{
    // Shipped ONI builds strip type trees, so field layouts come from the class database.
    am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);

    foreach (var info in inst.file.AssetInfos)
    {
        var type = (AssetClassID)info.TypeId;
        bool wanted = command switch
        {
            "list" => types.Contains(type),
            "material" => type == AssetClassID.Material,
            "textures" => type == AssetClassID.Texture2D,
            "textassets" => type == AssetClassID.TextAsset,
            _ => throw new ArgumentException("unknown command " + command),
        };
        if (!wanted)
            continue;
        AssetTypeValueField field = am.GetBaseField(inst, info);
        string name = field["m_Name"].AsString;
        if (!nameFilter.IsMatch(name))
            continue;

        switch (command)
        {
            case "list":
                string size = type == AssetClassID.Texture2D || type == AssetClassID.Texture2DArray
                    ? $"{field["m_Width"].AsInt}x{field["m_Height"].AsInt}" : "";
                Console.WriteLine($"{file}\t{type}\t{info.PathId}\t{name}\t{size}");
                break;
            case "material":
                PrintMaterial(am, inst, file, name, field);
                break;
            case "textures":
                SaveTexture(inst, name, OutputStem(name, info.PathId, written), field, outDir, options.ContainsKey("alpha"));
                break;
            case "textassets":
                string stem = OutputStem(name, info.PathId, written);
                File.WriteAllBytes(Path.Combine(outDir, stem + ".bytes"), field["m_Script"].AsByteArray);
                Console.WriteLine($"{stem}.bytes");
                break;
        }
    }
}
return 0;

static string OutputStem(string name, long pathId, HashSet<string> written)
{
    string stem = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
    if (!written.Add(stem))
    {
        stem += "__" + pathId;
        written.Add(stem);
    }
    return stem;
}

static IEnumerable<string> ResolveFiles(string dataDir, string spec)
{
    if (spec != "all")
        return new[] { spec };
    var files = Directory.GetFiles(dataDir, "*.assets").Select(Path.GetFileName)
        .Concat(Directory.GetFiles(dataDir, "level*").Select(Path.GetFileName).Where(f => !f.Contains('.')))
        .Concat(Directory.GetFiles(Path.Combine(dataDir, "StreamingAssets"), "*_bundle")
            .Select(f => "StreamingAssets/" + Path.GetFileName(f)));
    return files.OrderBy(f => f);
}

// Plain .assets and level files open directly; the StreamingAssets/*_bundle files (DLC art,
// including every Spaced Out kanim) are compressed Unity bundles that can hold several assets files.
static IEnumerable<AssetsFileInstance> Open(AssetsManager am, string path)
{
    string fileName = Path.GetFileName(path);
    if (fileName.EndsWith(".assets") || fileName.StartsWith("level"))
    {
        yield return am.LoadAssetsFile(path, true);
        yield break;
    }
    BundleFileInstance bundle = am.LoadBundleFile(path, true);
    var entries = bundle.file.BlockAndDirInfo.DirectoryInfos;
    for (int i = 0; i < entries.Count; i++)
        if (bundle.file.IsAssetsFile(i))
            yield return am.LoadAssetsFileFromBundle(bundle, i, false);
}

static void PrintMaterial(AssetsManager am, AssetsFileInstance inst, string file, string name, AssetTypeValueField field)
{
    Console.WriteLine($"{file}  Material '{name}'");
    var props = field["m_SavedProperties"];
    foreach (var te in props["m_TexEnvs.Array"].Children)
    {
        var ptr = te["second.m_Texture"];
        string texName = "(none)";
        if (ptr["m_PathID"].AsLong != 0)
        {
            try { texName = am.GetExtAsset(inst, ptr).baseField["m_Name"].AsString; }
            catch (Exception e) { texName = $"(file {ptr["m_FileID"].AsInt} path {ptr["m_PathID"].AsLong}: {e.Message})"; }
        }
        Console.WriteLine($"  tex   {te["first"].AsString} = {texName}  scale=({te["second.m_Scale.x"].AsFloat}, {te["second.m_Scale.y"].AsFloat})");
    }
    foreach (var f in props["m_Floats.Array"].Children)
        Console.WriteLine($"  float {f["first"].AsString} = {f["second"].AsFloat}");
    foreach (var c in props["m_Colors.Array"].Children)
        Console.WriteLine($"  color {c["first"].AsString} = ({c["second.r"].AsFloat}, {c["second.g"].AsFloat}, {c["second.b"].AsFloat}, {c["second.a"].AsFloat})");
}

// Writes <name>.png with its alpha intact. With --alpha it also writes <name>.onblack.png
// (colour multiplied by alpha) and <name>.alpha.png (alpha as greyscale): ONI's layered
// textures carry their shapes in alpha and leave junk colour where alpha is 0, so the plain
// PNG looks wrong in most viewers.
static void SaveTexture(AssetsFileInstance inst, string name, string safe, AssetTypeValueField field, string outDir, bool alphaViews)
{
    TextureFile tf = TextureFile.ReadTextureFile(field);
    byte[] encoded = tf.FillPictureData(inst);
    if (encoded == null || encoded.Length == 0)
    {
        Console.WriteLine($"{name}: no pixel data");
        return;
    }
    if (!tf.DecodeTextureImage(encoded, Path.Combine(outDir, safe + ".png"), ImageExportType.Png))
    {
        Console.WriteLine($"{name}: cannot decode texture format {tf.m_TextureFormat}");
        return;
    }
    Console.WriteLine($"{safe}.png  {tf.m_Width}x{tf.m_Height}");
    if (!alphaViews)
        return;

    byte[] rgba = tf.DecodeTextureRaw(encoded, useBgra: false);
    int w = tf.m_Width, h = tf.m_Height;
    var onBlack = new byte[w * h * 3];
    var alpha = new byte[w * h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Unity stores rows bottom-up.
            int src = ((h - 1 - y) * w + x) * 4, dst = y * w + x;
            byte a = rgba[src + 3];
            alpha[dst] = a;
            for (int c = 0; c < 3; c++)
                onBlack[dst * 3 + c] = (byte)(rgba[src + c] * a / 255);
        }
    var writer = new StbImageWriteSharp.ImageWriter();
    using (var fs = File.Create(Path.Combine(outDir, safe + ".onblack.png")))
        writer.WritePng(onBlack, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlue, fs);
    using (var fs = File.Create(Path.Combine(outDir, safe + ".alpha.png")))
        writer.WritePng(alpha, w, h, StbImageWriteSharp.ColorComponents.Grey, fs);
}
