using BuildableNaturalTile;
using STRINGS;
using TUNING;
using UnityEngine;

public class NaturalTileConfig : IBuildingConfig
{
	public const string ID = "NaturalTile";

	public const string DisplayName = "Natural Tile";

	public const string Description = "Fill that hole you dug out back in with any solid element.";

	public static string Effect = "Fills a block in the world with " + UI.FormatAsLink("Solids", "ELEMENTS_SOLID") + ".";

	public static readonly int BlockTileConnectorID = Hash.SDBMLower("natural_tile_block");

	public override BuildingDef CreateBuildingDef()
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		string text = "NaturalTile";
		int num = 1;
		int num2 = 1;
		string text2 = "natural_tile_kanim";
		int num3 = 100;
		float buildSpeed = BuildableNaturalTilePatches.Settings.BuildSpeed;
		float[] array = new float[1] { BuildableNaturalTilePatches.Settings.BuildMass };
		string[] array2 = new string[1] { "Solid" };
		float num4 = 1600f;
		BuildLocationRule val = (BuildLocationRule)0;
		EffectorValues nONE = NOISE_POLLUTION.NONE;
		return BuildingTemplates.CreateBuildingDef(text, num, num2, text2, num3, buildSpeed, array, array2, num4, val, BONUS.TIER0, nONE, 0.2f);
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		GeneratedBuildings.MakeBuildingAlwaysOperational(go);
		BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		GeneratedBuildings.RemoveLoopingSounds(go);
		go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles, false);
	}

	public override void DoPostConfigureUnderConstruction(GameObject go)
	{
		((IBuildingConfig)this).DoPostConfigureUnderConstruction(go);
	}
}
