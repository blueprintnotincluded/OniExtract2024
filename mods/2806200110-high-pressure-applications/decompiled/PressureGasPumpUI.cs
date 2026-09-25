using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.PressureGasPump_Patches.PressureGasPump_UIPatch;

[HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
internal class PressureGasPumpUI
{
	private static void Prefix()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[2] { "STRINGS.BUILDINGS.PREFABS.PRESSUREGASPUMP.NAME", "High Pressure Gas Pump" };
		Strings.Add(array);
		string[] array2 = new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS.PRESSUREGASPUMP.DESC",
			PressureGasPumpConfig.Description
		};
		Strings.Add(array2);
		string[] array3 = new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS.PRESSUREGASPUMP.EFFECT",
			PressureGasPumpConfig.Effect
		};
		Strings.Add(array3);
		ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("HVAC"), "PressureGasPump");
	}
}
