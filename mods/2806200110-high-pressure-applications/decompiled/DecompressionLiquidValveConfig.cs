using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace High_Pressure_Applications.BuildingConfigs;

public class DecompressionLiquidValveConfig : IBuildingConfig
{
	public const string Id = "DecompressionLiquidValve";

	public const string DisplayName = "Decompression Liquid Valve";

	public const string Description = "A mechanical valve capable of reducing the flow of liquid from a pressurized pipe to a normal pipe, avoid it to break.";

	public static string Effect;

	public static readonly List<StoredItemModifier> ValveStoredItemModifiers;

	static DecompressionLiquidValveConfig()
	{
		Effect = "Allows " + UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID") + " to be transfered from " + UI.FormatAsLink("High Pressure Liquid Pipe", "HighPressureLiquidConduit") + " to normal " + UI.FormatAsLink("Pipes", "LIQUIDCONDUIT") + ".";
		ValveStoredItemModifiers = new List<StoredItemModifier>
		{
			(StoredItemModifier)1,
			(StoredItemModifier)2,
			(StoredItemModifier)0
		};
	}

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		float[] array = new float[2] { 50f, 20f };
		string[] array2 = new string[2]
		{
			((object)(SimHashes)(-899253461)/*cast due to constrained. prefix*/).ToString(),
			((object)(SimHashes)(-1142341158)/*cast due to constrained. prefix*/).ToString()
		};
		BuildingDef val = BuildingTemplates.CreateBuildingDef("DecompressionLiquidValve", 2, 1, "deco_liquid_valve_kanim", 100, 50f, array, array2, 800f, (BuildLocationRule)0, PENALTY.TIER1, NOISY.TIER0, 0.2f);
		val.Floodable = false;
		val.Overheatable = false;
		val.ViewMode = LiquidConduits.ID;
		val.AudioCategory = "HollowMetal";
		val.InputConduitType = (ConduitType)2;
		val.OutputConduitType = (ConduitType)2;
		val.UtilityInputOffset = new CellOffset(0, 0);
		val.UtilityOutputOffset = new CellOffset(1, 0);
		val.PermittedRotations = (PermittedRotations)2;
		GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, "DecompressionLiquidValve");
		return val;
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		EntityTemplateExtensions.AddOrGet<Reservoir>(go);
		Storage val = BuildingTemplates.CreateDefaultStorage(go, false);
		val.showDescriptor = false;
		val.storageFilters = STORAGEFILTERS.LIQUIDS;
		val.capacityKg = 40f;
		val.SetDefaultStoredItemModifiers(ValveStoredItemModifiers);
		val.showCapacityStatusItem = false;
		val.showCapacityAsMainStatus = false;
		ConduitConsumer val2 = EntityTemplateExtensions.AddOrGet<ConduitConsumer>(go);
		val2.conduitType = (ConduitType)2;
		val2.ignoreMinMassCheck = true;
		val2.forceAlwaysSatisfied = true;
		val2.alwaysConsume = true;
		val2.capacityKG = val.capacityKg;
		ConduitDispenser val3 = EntityTemplateExtensions.AddOrGet<ConduitDispenser>(go);
		val3.conduitType = (ConduitType)2;
		val3.elementFilter = null;
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		EntityTemplateExtensions.AddOrGetDef<Def>(go);
		go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayBehindConduits, false);
	}
}
