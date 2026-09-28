using High_Pressure_Applications.Components;
using PeterHan.PLib.Options;
using STRINGS;
using TUNING;
using UnityEngine;

namespace High_Pressure_Applications.BuildingConfigs;

public class PressureLiquidPumpConfig : IBuildingConfig
{
	public const string Id = "PressureLiquidPump";

	public const string DisplayName = "High Pressure Liquid Pump";

	public static string Description = "An advanced pump that perform mechanical work to compress and move fluids. More powerful than the standard pump, this one is capable of moving large amounts of liquids, although this is only archived through the " + UI.FormatAsLink("High Pressure Liquid Pipe", "HighPressureLiquidConduit") + ".";

	public static string Effect = "Draws in " + UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID") + " and runs it through " + UI.FormatAsLink("High Pressure Liquid Pipe", "HighPressureLiquidConduit") + ".\n\nMust be submerged in " + UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID") + ".";

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		float[] array = new float[2] { 400f, 200f };
		string[] array2 = new string[2]
		{
			((object)(SimHashes)(-899253461)/*cast due to constrained. prefix*/).ToString(),
			((object)(SimHashes)(-1142341158)/*cast due to constrained. prefix*/).ToString()
		};
		EffectorValues nONE = NOISE_POLLUTION.NONE;
		BuildingDef val = BuildingTemplates.CreateBuildingDef("PressureLiquidPump", 2, 3, "pressure_liquid_pump_kanim", 240, 120f, array, array2, 1600f, (BuildLocationRule)0, PENALTY.TIER1, nONE, 0.2f);
		val.RequiresPowerInput = true;
		val.Overheatable = false;
		val.EnergyConsumptionWhenActive = (float)SingletonOptions<HPA_ModSettings>.Instance.HPLiquid * 240f / 10f;
		val.ExhaustKilowattsWhenActive = 0f;
		val.SelfHeatKilowattsWhenActive = 2f;
		val.OutputConduitType = (ConduitType)2;
		val.Floodable = false;
		val.ViewMode = LiquidConduits.ID;
		val.AudioCategory = "Metal";
		val.PowerInputOffset = new CellOffset(0, 1);
		val.UtilityOutputOffset = new CellOffset(1, 2);
		val.PermittedRotations = (PermittedRotations)1;
		val.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(0, 1));
		GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, "LiquidPump");
		return val;
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		EntityTemplateExtensions.AddOrGet<LogicOperationalController>(go);
		go.GetComponent<KPrefabID>().AddTag(ConstraintTags.IndustrialMachinery, false);
		EntityTemplateExtensions.AddOrGet<LoopingSounds>(go);
		EntityTemplateExtensions.AddOrGet<EnergyConsumer>(go);
		EntityTemplateExtensions.AddOrGet<Pump>(go);
		EntityTemplateExtensions.AddOrGet<Storage>(go).capacityKg = SingletonOptions<HPA_ModSettings>.Instance.HPLiquid;
		ElementConsumer val = EntityTemplateExtensions.AddOrGet<ElementConsumer>(go);
		val.configuration = (Configuration)1;
		val.consumptionRate = SingletonOptions<HPA_ModSettings>.Instance.HPLiquid;
		val.storeOnConsume = true;
		val.showInStatusPanel = false;
		val.consumptionRadius = 8;
		ConduitDispenser val2 = EntityTemplateExtensions.AddOrGet<ConduitDispenser>(go);
		val2.conduitType = (ConduitType)2;
		val2.alwaysDispense = true;
		val2.elementFilter = null;
		EntityTemplateExtensions.AddOrGetDef<Def>(go);
		go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayBehindConduits, false);
	}
}
