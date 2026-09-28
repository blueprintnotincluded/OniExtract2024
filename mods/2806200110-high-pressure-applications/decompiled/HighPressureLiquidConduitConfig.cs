using System.Collections.Generic;
using High_Pressure_Applications.Components;
using PeterHan.PLib.Options;
using STRINGS;
using TUNING;
using UnityEngine;

namespace High_Pressure_Applications.BuildingConfigs;

public class HighPressureLiquidConduitConfig : IBuildingConfig
{
	public const string Id = "HighPressureLiquidConduit";

	public const string DisplayName = "High Pressure Liquid Pipe";

	public const string Description = "A reinforced liquid pipe capable of handling high pressure flow. Composite nature of the pipe prevents liquid contents from significantly changing temperature in transit.";

	public static string Effect = string.Concat("Carries a maximum of " + (float)SingletonOptions<HPA_ModSettings>.Instance.HPLiquid, "kg of ", UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID"), " with minimal change in ", UI.FormatAsLink("Temperature", "HEAT"), ".\n\nCan be run through wall and floor tile.");

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		string text = "HighPressureLiquidConduit";
		int num = 1;
		int num2 = 1;
		string text2 = "pressure_liquid_pipe_kanim";
		int num3 = 10;
		float num4 = 30f;
		float[] array = new float[2] { 10f, 5f };
		string[] array2 = new string[2]
		{
			((object)(SimHashes)(-899253461)/*cast due to constrained. prefix*/).ToString(),
			"Plastic"
		};
		float num5 = 1600f;
		BuildLocationRule val = (BuildLocationRule)0;
		EffectorValues nONE = NOISE_POLLUTION.NONE;
		BuildingDef val2 = BuildingTemplates.CreateBuildingDef(text, num, num2, text2, num3, num4, array, array2, num5, val, PENALTY.TIER0, nONE, 0.2f);
		val2.Floodable = false;
		val2.Overheatable = false;
		val2.Entombable = false;
		val2.ViewMode = LiquidConduits.ID;
		val2.ThermalConductivity = 1.3f;
		val2.ObjectLayer = (ObjectLayer)16;
		val2.TileLayer = (ObjectLayer)17;
		val2.ReplacementLayer = (ObjectLayer)18;
		val2.AudioCategory = "Metal";
		val2.AudioSize = "small";
		val2.BaseTimeUntilRepair = -1f;
		val2.UtilityInputOffset = new CellOffset(0, 0);
		val2.UtilityOutputOffset = new CellOffset(0, 0);
		val2.SceneLayer = (SceneLayer)5;
		val2.isKAnimTile = true;
		val2.isUtility = true;
		val2.DragBuild = true;
		val2.ReplacementTags = new List<Tag>();
		val2.ReplacementTags.Add(GameTags.Pipes);
		val2.ThermalConductivity = 1E-05f;
		GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, "HighPressureLiquidConduit");
		return val2;
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		GeneratedBuildings.MakeBuildingAlwaysOperational(go);
		BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
		Conduit val = EntityTemplateExtensions.AddOrGet<Conduit>(go);
		val.type = (ConduitType)2;
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		go.GetComponent<Building>().Def.BuildingUnderConstruction.GetComponent<Constructable>().isDiggingRequired = false;
		go.AddComponent<EmptyConduitWorkable>();
		KAnimGraphTileVisualizer val = go.AddComponent<KAnimGraphTileVisualizer>();
		val.connectionSource = (ConnectionSource)1;
		val.isPhysicalBuilding = true;
		go.GetComponent<KPrefabID>().AddTag(GameTags.Pipes, false);
		LiquidConduitConfig.CommonConduitPostConfigureComplete(go);
	}

	public override void DoPostConfigureUnderConstruction(GameObject go)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		KAnimGraphTileVisualizer val = go.AddComponent<KAnimGraphTileVisualizer>();
		val.connectionSource = (ConnectionSource)1;
		val.isPhysicalBuilding = false;
	}
}
