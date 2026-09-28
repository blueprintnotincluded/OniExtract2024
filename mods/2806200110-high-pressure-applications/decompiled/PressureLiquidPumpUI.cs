using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.PressureLiquidPump_Patches.PressureLiquidPump_UIPatch;

[HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
internal class PressureLiquidPumpUI
{
	private static void Prefix()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[2] { "STRINGS.BUILDINGS.PREFABS.PRESSURELIQUIDPUMP.NAME", "High Pressure Liquid Pump" };
		Strings.Add(array);
		string[] array2 = new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS.PRESSURELIQUIDPUMP.DESC",
			PressureLiquidPumpConfig.Description
		};
		Strings.Add(array2);
		string[] array3 = new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS.PRESSURELIQUIDPUMP.EFFECT",
			PressureLiquidPumpConfig.Effect
		};
		Strings.Add(array3);
		ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("Plumbing"), "PressureLiquidPump");
	}
}
