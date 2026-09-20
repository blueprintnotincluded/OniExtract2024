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
            // A stored uiImageRect means the in-game pass already rendered this prefab at
            // 200 px/cell and the rect was measured against THAT crop. Def.GetUISprite returns
            // something else entirely — the kanim's authored "ui" build symbol, an atlas
            // sub-rect with no footprint-relative placement — so overwriting the render with it
            // leaves the rect describing an image that is no longer on disk. That is how 302 of
            // 342 building rects came to disagree with their PNG's aspect ratio. Keep the render;
            // uiSpriteInfo is still recorded below either way.
            bool hasMeasuredRender = OniExtract2024.building.UiImageRectStore.TryGet(
                prefab.PrefabTag.Name, out _);
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
        if (!properName.Equals("")) return properName;
        var instance = Assets.GetPrefab(prefab.PrefabTag);
        return instance == null ? prefab.PrefabTag.Name : UI.StripLinkFormatting(instance.GetProperName());
    }
}
