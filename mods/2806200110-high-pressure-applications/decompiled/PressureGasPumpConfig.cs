using High_Pressure_Applications.Components;
using PeterHan.PLib.Options;
using STRINGS;
using TUNING;
using UnityEngine;

namespace High_Pressure_Applications.BuildingConfigs;

public class PressureGasPumpConfig : IBuildingConfig
{
	public const string Id = "PressureGasPump";

	public const string DisplayName = "High Pressure Gas Pump";

	public static string Description = "An advanced pump that perform mechanical work to compress and move gases. More powerful than the standard pump, this one is capable of moving large amounts of gases, although this is only archived through the " + UI.FormatAsLink("High Pressure Gas Pipe", "HighPressureGasConduit") + ".";

	public static string Effect = "Draws in " + UI.FormatAsLink("Gas", "ELEMENTS_GAS") + " and runs it through " + UI.FormatAsLink("High Pressure Gas Pipe", "HighPressureGasConduit") + ".\n\nMust be submerged in " + UI.FormatAsLink("Gas", "ELEMENTS_GAS") + ".";

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		float[] array = new float[2] { 400f, 200f };
		string[] array2 = new string[2]
		{
			((object)(SimHashes)(-899253461)/*cast due to constrained. prefix*/).ToString(),
			((object)(SimHashes)(-1142341158)/*cast due to constrained. prefix*/).ToString()
		};
		EffectorValues tIER = NOISY.TIER2;
		BuildingDef val = BuildingTemplates.CreateBuildingDef("PressureGasPump", 2, 3, "pressure_gas_pump_kanim", 30, 30f, array, array2, 1600f, (BuildLocationRule)0, PENALTY.TIER1, tIER, 0.2f);
		val.RequiresPowerInput = true;
		val.Overheatable = false;
		val.EnergyConsumptionWhenActive = (float)SingletonOptions<HPA_ModSettings>.Instance.HPGas * 240f;
		val.ExhaustKilowattsWhenActive = 0f;
		val.SelfHeatKilowattsWhenActive = 0f;
		val.OutputConduitType = (ConduitType)1;
		val.Floodable = true;
		val.ViewMode = GasConduits.ID;
		val.AudioCategory = "Metal";
		val.PowerInputOffset = new CellOffset(0, 1);
		val.UtilityOutputOffset = new CellOffset(0, 2);
		val.PermittedRotations = (PermittedRotations)1;
		val.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(0, 1));
		GeneratedBuildings.RegisterWithOverlay(OverlayScreen.GasVentIDs, "GasPump");
		return val;
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		EntityTemplateExtensions.AddOrGet<LogicOperationalController>(go);
		go.GetComponent<KPrefabID>().AddTag(ConstraintTags.IndustrialMachinery, false);
		EntityTemplateExtensions.AddOrGet<LoopingSounds>(go);
		EntityTemplateExtensions.AddOrGet<EnergyConsumer>(go);
		EntityTemplateExtensions.AddOrGet<Pump>(go);
		EntityTemplateExtensions.AddOrGet<Storage>(go).capacityKg = SingletonOptions<HPA_ModSettings>.Instance.HPGas;
		ElementConsumer val = EntityTemplateExtensions.AddOrGet<ElementConsumer>(go);
		val.configuration = (Configuration)2;
		val.consumptionRate = SingletonOptions<HPA_ModSettings>.Instance.HPGas;
		val.storeOnConsume = true;
		val.showInStatusPanel = false;
		val.consumptionRadius = 12;
		ConduitDispenser val2 = EntityTemplateExtensions.AddOrGet<ConduitDispenser>(go);
		val2.conduitType = (ConduitType)1;
		val2.alwaysDispense = true;
		val2.elementFilter = null;
		EntityTemplateExtensions.AddOrGetDef<Def>(go);
		go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayBehindConduits, false);
	}
}
