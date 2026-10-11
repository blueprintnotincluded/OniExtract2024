using OniExtract2024;
using OniExtract2024.utils;
using Database;
using System;
using System.Collections.Generic;
using System.IO;
using PeterHan.PLib.Options;
using UnityEngine;
using System.Linq;
using STRINGS;

public class ExportUISprite : BaseExport
{
    public override string ExportFileName { get; set; } = "uiSpriteInfo";
    public Dictionary<string, BUISprite> uiSpriteInfos = new Dictionary<string, BUISprite>();
    public Dictionary<string, BUISprite> uiFacadeInfos = new Dictionary<string, BUISprite>();

    public ExportUISprite()
    {
    }

    // Icon file names (without .png) that belong to the game-data run in progress: every
    // prefab whose icon the run wrote, or kept because the file holds a measured render, plus
    // the port indicators. Anything else in ui_image/ was left by an earlier run for a prefab
    // that is not loaded now, and PruneStaleIcons removes it at the end of the run. Compared
    // ignoring case because the file system does.
    private static readonly HashSet<string> s_currentIcons =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static void BeginIconRun() => s_currentIcons.Clear();

    public static void NoteCurrentIcon(string fileName) => s_currentIcons.Add(fileName);

    // Deletes every PNG in ui_image/ that no step of this run accounted for. Returns how many
    // went. Only meaningful after both the building and the UI-sprite steps have run, since
    // between them they note every current icon; the caller checks that.
    public static int PruneStaleIcons()
    {
        string dir = Path.Combine(Util.RootFolder(), "export", "ui_image");
        if (!Directory.Exists(dir)) return 0;
        List<string> stale = ExportPruning.Stale(Directory.GetFiles(dir, "*.png"), s_currentIcons);
        int removed = 0;
        foreach (string path in stale)
        {
            try
            {
                File.Delete(path);
                removed++;
                Debug.Log("OniExtract: removed stale icon " + Path.GetFileName(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: could not remove stale icon " + path + ": " + e.Message);
            }
        }
        return removed;
    }

    public void AddUISpriteInfo(KPrefabID prefabID, Tuple<Sprite, Color> tupleUISprite, string properName)
    {
        this.uiSpriteInfos[prefabID.PrefabTag.Name] = new BUISprite(prefabID.name, tupleUISprite.first, tupleUISprite.second) {
            name = properName
        };
    }

    public void AddFacadeInfos(string id, Sprite sprite, string properName)
    {
        this.uiFacadeInfos[id] = new BUISprite(id, sprite) {
            name = properName
        };
    }

    public void ExportAllUISprite()
    {
        string ExportIconDir = Path.Combine(Util.RootFolder(), "export", "ui_image");
        foreach (var prefab in Assets.Prefabs)
        {
            if (prefab == null || prefab.PrefabTag == null)
            {
                continue;
            }
            if (prefab.PrefabTag.Name.StartsWith("Compost") || prefab.PrefabTag.Name.EndsWith("Preview") || prefab.PrefabTag.Name.EndsWith("UnderConstruction") || prefab.PrefabTag.Name.EndsWith("_Preview") || prefab.PrefabTag.Name.EndsWith("_preview"))
            {
                if (prefab.PrefabTag.Name != "Compost")
                {
                    continue;
                }
            }
            var formattedName = GetFormatedUIImageFileName(prefab);
            // Noted whether or not a sprite is found below: a building with no atlas icon can
            // still have a hi-res render under this name, and that render is current.
            NoteCurrentIcon(formattedName);
            // A stored uiImageRect means the in-game pass already rendered this prefab at
            // 200 px/cell and the rect was measured against THAT crop. Def.GetUISprite returns
            // something else entirely — the kanim's authored "ui" build symbol, an atlas
            // sub-rect with no footprint-relative placement — so overwriting the render with it
            // leaves the rect describing an image that is no longer on disk. That is how 302 of
            // 342 building rects came to disagree with their PNG's aspect ratio. Keep the render;
            // uiSpriteInfo is still recorded below either way.
            //
            // The rect is keyed by prefab tag, but the icon's filename follows the
            // SaveUIFileName option, so the two only coincide in ID mode. Don't trust the key
            // as a proxy: confirm the file actually sitting at formattedName IS the measured
            // render by checking its aspect against the rect. If the option changed since the
            // sweep, the render lives under the other name and this path holds a stale icon or
            // nothing at all — in which case the write must go ahead.
            bool hasMeasuredRender =
                OniExtract2024.building.UiImageRectStore.TryGet(prefab.PrefabTag.Name, out var storedRect)
                && OniExtract2024.building.ExportBuildingImages.PngMatchesRect(
                    Path.Combine(ExportIconDir, formattedName + ".png"), storedRect);
            Element element = ElementLoader.GetElement(prefab.PrefabTag);
            if (element != null)
            {
                var tupleUISprite = Def.GetUISprite(element);
                if (!hasMeasuredRender)
                    AnimTool.WriteUISpriteToFile(tupleUISprite.first, ExportIconDir, formattedName, tupleUISprite.second);
                this.AddUISpriteInfo(prefab, tupleUISprite, GetProperName(prefab));
            }
            else
            {
                Tuple<Sprite, Color> tupleUISprite = null;
                try
                {
                    tupleUISprite = Def.GetUISprite(prefab.PrefabTag);
                }
                catch (Exception)
                {
                    //Debug.LogError("OniExtract: read " + prefab.PrefabTag.Name + " Failed.");
                }
                if (tupleUISprite != null)
                {
                    Sprite UISprite = tupleUISprite.first;
                    if (UISprite != null && UISprite != Assets.GetSprite("unknown"))
                    {
                        if (!hasMeasuredRender)
                            AnimTool.WriteUISpriteToFile(UISprite, ExportIconDir, formattedName);
                        this.AddUISpriteInfo(prefab, tupleUISprite, GetProperName(prefab));
                    }
                }
            }
        }
        string[] facadeSetIds = new string[] {
            Db.Get().Permits.BuildingFacades.Id,
            Db.Get().Permits.EquippableFacades.Id,
            Db.Get().Permits.ArtableStages.Id,
            Db.Get().Permits.StickerBombs.Id,
            Db.Get().Permits.ClothingItems.Id,
            Db.Get().Permits.BalloonArtistFacades.Id
        };
        foreach (string facadeSetId in facadeSetIds)
        {
            if (!Db.Get().Permits.Permits.Keys.Contains(facadeSetId))
            {
                continue;
            }
            string ExportFacadeDir = Path.Combine(Util.RootFolder(), "export", "ui_image_facade", facadeSetId);
            foreach (PermitResource permitResource in Db.Get().Permits.Permits[facadeSetId])
            {
                if (!(permitResource.Id == "Default"))
                {
                    Sprite UISprite = permitResource.GetPermitPresentationInfo().sprite;
                    
                    if (UISprite != null && UISprite != Assets.GetSprite("unknown"))
                    {
                        AnimTool.WriteUISpriteToFile(UISprite, ExportFacadeDir, GetFacadeUIImageFileName(permitResource));
                        this.AddFacadeInfos(permitResource.Id, UISprite, UI.StripLinkFormatting(permitResource.Name));
                    }
                }
            }
        }
        string ExportFacadeDir2 = Path.Combine(Util.RootFolder(), "export", "ui_image_facade", Db.Get().Permits.MonumentParts.Id);
        foreach (MonumentPartResource monumentPart in Db.GetMonumentParts().resources)
        {
            if (!(monumentPart.Id == "Default"))
            {
                Sprite UISprite = Def.GetUISpriteFromMultiObjectAnim(monumentPart.AnimFile, monumentPart.State, false, monumentPart.SymbolName);
                if (UISprite != null && UISprite != Assets.GetSprite("unknown"))
                {
                    AnimTool.WriteUISpriteToFile(UISprite, ExportFacadeDir2, monumentPart.Id);
                    this.AddFacadeInfos(monumentPart.Id, UISprite, UI.StripLinkFormatting(monumentPart.Name));
                }
            }
        }
    }

    private string GetFacadeUIImageFileName(PermitResource permitResource)
    {
        return SingletonOptions<ModOptions>.Instance.SaveUIFileName == ModOptions.SaveNameMod.ID
            ? permitResource.Id
            : UI.StripLinkFormatting(permitResource.Name);
    }

    public static string GetFormatedUIImageFileName(KPrefabID prefab)
    {
        if (SingletonOptions<ModOptions>.Instance.SaveUIFileName == ModOptions.SaveNameMod.ID)
            return prefab.PrefabTag.Name;
        return GetProperName(prefab);
    }

    public static string GetProperName(KPrefabID prefab)
    {
        var properName = TagManager.GetProperName(prefab.PrefabID(), true);
        // Game.OnSpawn (TagManager.FillMissingProperNames) fills every tag registered without
        // a name with its STRINGS.MISC.TAGS entry, which is usually "MISSING.STRINGS..." and
        // otherwise a tag description ("Beds" for the Cot). Treat a filled-in name as no name,
        // so the export run from a loaded colony falls back to the prefab's own name, as it
        // did when it ran at boot.
        if (!properName.Equals("") && properName != TagDescriptions.GetDescription(prefab.PrefabID()))
            return properName;
        var instance = Assets.GetPrefab(prefab.PrefabTag);
        return instance == null ? prefab.PrefabTag.Name : UI.StripLinkFormatting(instance.GetProperName());
    }
}
