using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.DecompressionGasValve_Patches.DecompressionGasValve_UIPatch;

[HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
internal class DecompressionGasValveUI
{
	private static void Prefix()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[2] { "STRINGS.BUILDINGS.PREFABS.DECOMPRESSIONGASVALVE.NAME", "Decompression Gas Valve" };
		Strings.Add(array);
		string[] array2 = new string[2] { "STRINGS.BUILDINGS.PREFABS.DECOMPRESSIONGASVALVE.DESC", "A mechanical valve capable of reducing the flow of gas from a pressurized pipe to a normal pipe, avoid it to break." };
		Strings.Add(array2);
		string[] array3 = new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS.DECOMPRESSIONGASVALVE.EFFECT",
			DecompressionGasValveConfig.Effect
		};
		Strings.Add(array3);
		ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("HVAC"), "DecompressionGasValve");
	}
}
