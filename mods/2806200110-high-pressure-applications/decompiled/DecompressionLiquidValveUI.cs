using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.DecompressionLiquidValve_Patches.DecompressionLiquidValve_UIPatch;

[HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
internal class DecompressionLiquidValveUI
{
	private static void Prefix()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[2] { "STRINGS.BUILDINGS.PREFABS.DECOMPRESSIONLIQUIDVALVE.NAME", "Decompression Liquid Valve" };
		Strings.Add(array);
		string[] array2 = new string[2] { "STRINGS.BUILDINGS.PREFABS.DECOMPRESSIONLIQUIDVALVE.DESC", "A mechanical valve capable of reducing the flow of liquid from a pressurized pipe to a normal pipe, avoid it to break." };
		Strings.Add(array2);
		string[] array3 = new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS.DECOMPRESSIONLIQUIDVALVE.EFFECT",
			DecompressionLiquidValveConfig.Effect
		};
		Strings.Add(array3);
		ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("Plumbing"), "DecompressionLiquidValve");
	}
}
